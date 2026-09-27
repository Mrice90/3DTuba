/**
 * lobby-lab server (AI-039) — locally runnable adapter around the pinned
 * alpha lobby worker contract.
 *
 * - Imports upstream/worker.js VERBATIM (see upstream/PROVENANCE.md).
 * - Adapts Node http requests to the worker's fetch(request, env) signature.
 * - In-memory KV with TTL + controllable clock (see kv.js).
 * - Serves the browser UI from public/.
 * - Binds loopback (127.0.0.1) by default; override with HOST/PORT env.
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

function readBody(req) {
    return new Promise((resolve, reject) => {
        const chunks = [];
        req.on("data", (c) => chunks.push(c));
        req.on("end", () => resolve(Buffer.concat(chunks)));
        req.on("error", reject);
        req.on("aborted", () => reject(new Error("request aborted by client")));
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
 * @param {string} [opts.host="127.0.0.1"]
 * @param {() => number} [opts.now] controllable clock for the KV store
 */
export async function start({ port = 8787, host = "127.0.0.1", now } = {}) {
    const kv = createKv(now ? { now } : {});
    const env = { IC_KV: kv };

    const server = http.createServer(async (req, res) => {
        try {
            const url = new URL(req.url, "http://local");
            if (!isApiPath(url.pathname)) {
                await serveStatic(req, res);
                return;
            }
            const body = await readBody(req);
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
                res.writeHead(500, { "content-type": "application/json" });
            }
            try { res.end(JSON.stringify({ error: "internal error" })); } catch { /* noop */ }
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
    const { server, port: boundPort, host: boundHost } = await start({ port, host });
    console.log(`lobby-lab listening on http://${boundHost}:${boundPort}`);
    console.log("local development only — do not expose beyond loopback without real identity");
    const shutdown = () => {
        console.log("shutting down...");
        server.close(() => process.exit(0));
        setTimeout(() => process.exit(0), 2000).unref();
    };
    process.on("SIGINT", shutdown);
    process.on("SIGTERM", shutdown);
}
