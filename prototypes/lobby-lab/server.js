/**
 * lobby-lab server (AI-039) — locally runnable adapter around the pinned
 * alpha lobby worker contract.
 *
 * - Imports upstream/worker.js VERBATIM (see upstream/PROVENANCE.md).
 * - Adapts Node http requests to the worker's fetch(request, env) signature.
 * - In-memory KV with TTL + controllable clock (see kv.js).
 * - Serves the browser UI from public/.
 * - Binds loopback (127.0.0.1) by default; override with HOST/PORT env.
 * - Local-only boundary (AI-045): non-loopback bind hosts are rejected
 *   before listening; per request, the Host header must be loopback and
 *   browser mutations must carry a loopback Origin/Referer.
 *
 * LOCAL DEVELOPMENT ONLY. Caller-supplied UUIDs are NOT authentication:
 * anyone holding a hostUuid can manage that lobby. Never expose this
 * server beyond loopback without a real identity layer (see README.md).
 */

import http from "node:http";
import { readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import worker from "./upstream/worker.js";
import { createKv } from "./kv.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const PUBLIC = path.join(here, "public");

const MIME = {
    ".html": "text/html; charset=utf-8",
    ".js": "text/javascript; charset=utf-8",
    ".css": "text/css; charset=utf-8",
    ".json": "application/json; charset=utf-8",
    ".md": "text/markdown; charset=utf-8",
};

const API_PREFIXES = ["/lobbies", "/queue", "/pair", "/report", "/leaderboard", "/rating"];

/**
 * Loopback-only policy (AI-045). The lab server must never be reachable
 * beyond this machine:
 * - start() rejects non-loopback bind hosts BEFORE listening (covers the
 *   HOST env var, which the main block passes straight into start()).
 * - every request's Host header must be loopback (defeats DNS rebinding,
 *   where evil.example resolves to 127.0.0.1).
 * - state-changing requests from a browser must carry a loopback Origin
 *   (or Referer); non-browser clients send neither and are unaffected.
 *
 * This is a local-development boundary, NOT production authentication:
 * caller-supplied UUIDs still own lobbies (see README.md).
 */
export function isLoopbackHost(host) {
    if (!host || typeof host !== "string") return false;
    const h = host.trim().toLowerCase();
    if (h === "localhost" || h === "::1" || h === "[::1]") return true;
    const v4 = h.match(/^127\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})$/);
    if (v4) return v4.slice(1).every((n) => Number(n) <= 255);
    return false;
}

/** Hostname part of the request's Host header (port stripped). Null if absent. */
function hostHeaderHostname(req) {
    const host = req.headers.host;
    if (!host || typeof host !== "string") return null;
    const bracketed = host.match(/^\[([^\]]+)\](?::\d+)?$/);
    if (bracketed) return bracketed[1];
    return host.split(":")[0];
}

/**
 * Hostname of the request's Origin (falling back to Referer). Null when
 * neither is present — i.e. a non-browser client (Node LobbyClient, demo,
 * curl). Returns "" when the value is present but unparseable, which
 * fail-closed counts as non-loopback.
 */
function requestOriginHostname(req) {
    const origin = req.headers.origin || req.headers.referer;
    if (!origin) return null;
    try {
        return new URL(origin).hostname;
    } catch {
        return "";
    }
}

function readBody(req, maxBytes) {
    return new Promise((resolve, reject) => {
        const chunks = [];
        let size = 0;
        let done = false;
        const fail = (err) => {
            if (done) return;
            done = true;
            req.removeAllListeners("data");
            req.removeAllListeners("end");
            reject(err);
        };
        req.on("data", (c) => {
            if (done) return;
            size += c.length;
            if (size > maxBytes) {
                fail(Object.assign(new Error("request body too large"), { statusCode: 413 }));
                return;
            }
            chunks.push(c);
        });
        req.on("end", () => {
            if (!done) {
                done = true;
                resolve(Buffer.concat(chunks));
            }
        });
        req.on("error", fail);
        req.on("aborted", () => fail(Object.assign(new Error("request aborted by client"), { statusCode: 400 })));
    });
}

function isApiPath(pathname) {
    return API_PREFIXES.some((p) => pathname === p || pathname.startsWith(p + "/"));
}

async function serveStatic(req, res) {
    const url = new URL(req.url, "http://local");
    let p = decodeURIComponent(url.pathname);
    if (p === "/") p = "/index.html";
    const file = path.normalize(path.join(PUBLIC, p));
    if (!file.startsWith(PUBLIC + path.sep)) {
        res.writeHead(403, { "content-type": "text/plain" });
        res.end("forbidden");
        return;
    }
    try {
        const data = await readFile(file);
        res.writeHead(200, { "content-type": MIME[path.extname(file)] || "application/octet-stream" });
        res.end(data);
    } catch {
        res.writeHead(404, { "content-type": "text/plain" });
        res.end("not found");
    }
}

/**
 * Start the lab server.
 * @param {object} opts
 * @param {number} [opts.port=8787] 0 = ephemeral (tests)
 * @param {string} [opts.host="127.0.0.1"] must be loopback; anything else
 *   throws before the server listens (AI-045)
 * @param {() => number} [opts.now] controllable clock for the KV store
 * @param {number} [opts.maxBodyBytes=1000000] request body cap (413 beyond)
 * @param {boolean} [opts.allowReports=false] enable POST /report.
 *   Reports are caller-asserted UUIDs with no authentication: a single
 *   caller can submit both "agreeing" reports and mint Elo for arbitrary
 *   UUIDs. Keep disabled until a production identity design exists.
 */
export async function start({ port = 8787, host = "127.0.0.1", now, maxBodyBytes = 1_000_000, allowReports = false } = {}) {
    if (!isLoopbackHost(host)) {
        throw new Error(`refusing to bind non-loopback host "${host}": lobby-lab is local-development-only`);
    }
    const kv = createKv(now ? { now } : {});
    const env = { IC_KV: kv };

    const server = http.createServer(async (req, res) => {
        try {
            // AI-045 local-only boundary: checked before routing, body
            // buffering, or any state change.
            const hh = hostHeaderHostname(req);
            if (!hh) {
                res.writeHead(400, { "content-type": "application/json" });
                res.end(JSON.stringify({ error: "Host header required" }));
                return;
            }
            if (!isLoopbackHost(hh)) {
                res.writeHead(403, { "content-type": "application/json" });
                res.end(JSON.stringify({ error: "refusing non-loopback Host: lobby-lab serves loopback only" }));
                return;
            }
            const mutating = req.method !== "GET" && req.method !== "HEAD" && req.method !== "OPTIONS";
            if (mutating) {
                const oh = requestOriginHostname(req);
                if (oh !== null && !isLoopbackHost(oh)) {
                    res.writeHead(403, { "content-type": "application/json" });
                    res.end(JSON.stringify({ error: "cross-origin mutation refused: lobby-lab accepts browser mutations from loopback origins only" }));
                    return;
                }
            }
            const url = new URL(req.url, "http://local");
            if (!isApiPath(url.pathname)) {
                await serveStatic(req, res);
                return;
            }
            if (!allowReports && req.method === "POST" && url.pathname === "/report") {
                res.writeHead(403, { "content-type": "application/json" });
                res.end(JSON.stringify({
                    error: "match reporting is disabled in this lab build: caller-asserted UUIDs are not authentication. Set LAB_ALLOW_REPORTS=1 to exercise the contract locally.",
                }));
                return;
            }
            let body;
            try {
                body = await readBody(req, maxBodyBytes);
            } catch (e) {
                if (e.statusCode === 413) {
                    res.writeHead(413, { "content-type": "application/json", "connection": "close" });
                    // Drain without buffering and let HTTP close gracefully after the
                    // response. Destroying the request here can reset the Windows
                    // socket before the client receives the 413 response.
                    req.resume();
                    res.end(JSON.stringify({ error: "request body too large" }));
                    return;
                }
                throw e;
            }
            const wReq = new Request(`http://local${req.url}`, {
                method: req.method,
                headers: req.headers,
                body: req.method === "GET" || req.method === "HEAD" ? undefined : body,
            });
            const wRes = await worker.fetch(wReq, env);
            const buf = Buffer.from(await wRes.arrayBuffer());
            const headers = {};
            wRes.headers.forEach((v, k) => { headers[k] = v; });
            res.writeHead(wRes.status, headers);
            res.end(buf);
        } catch (err) {
            console.error("request failed:", err.message);
            if (!res.headersSent) {
                res.writeHead(err.statusCode || 500, { "content-type": "application/json" });
            }
            try { res.end(JSON.stringify({ error: err.statusCode ? err.message : "internal error" })); } catch { /* noop */ }
        }
    });

    await new Promise((resolve, reject) => {
        server.once("error", reject);
        server.listen(port, host, resolve);
    });
    const addr = server.address();
    return {
        server,
        kv,
        host: addr.address,
        port: addr.port,
        close: () => new Promise((resolve, reject) => server.close((e) => (e ? reject(e) : resolve()))),
    };
}

const isMain = process.argv[1] === fileURLToPath(import.meta.url);
if (isMain) {
    const port = parseInt(process.env.PORT || "8787", 10);
    const host = process.env.HOST || "127.0.0.1";
    const maxBodyBytes = parseInt(process.env.LAB_MAX_BODY || "1000000", 10);
    const allowReports = process.env.LAB_ALLOW_REPORTS === "1";
    const { server, port: boundPort, host: boundHost } = await start({ port, host, maxBodyBytes, allowReports });
    console.log(`lobby-lab listening on http://${boundHost}:${boundPort}`);
    console.log(`match reporting: ${allowReports ? "ENABLED (local contract exercise only)" : "disabled"}; body cap: ${maxBodyBytes} bytes`);
    console.log("local development only — do not expose beyond loopback without real identity");
    const shutdown = () => {
        console.log("shutting down...");
        server.close(() => process.exit(0));
        setTimeout(() => process.exit(0), 2000).unref();
    };
    process.on("SIGINT", shutdown);
    process.on("SIGTERM", shutdown);
}
