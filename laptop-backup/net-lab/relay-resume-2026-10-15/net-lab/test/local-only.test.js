/**
 * Local-only boundary tests for the lobby-lab adapter (AI-045).
 *
 * The lab server must never be reachable beyond this machine. These tests
 * pin three guards over REAL HTTP (no mocks):
 *
 * 1. Bind config: start({ host }) with a non-loopback host is rejected
 *    BEFORE the server listens (covers HOST env too — the main block passes
 *    HOST straight into start()).
 * 2. Host header: requests whose Host is not loopback are refused
 *    (defeats DNS rebinding: evil.example resolving to 127.0.0.1).
 * 3. Browser origin: state-changing requests from a non-loopback Origin
 *    (or Referer) are refused with 403, so an unrelated website cannot
 *    create/delete lobbies through the user's browser. Non-browser clients
 *    (no Origin header: Node LobbyClient, demo, curl) are unaffected, and
 *    same-origin browser traffic keeps working.
 *
 * This is a local-development boundary, NOT production authentication:
 * caller-supplied UUIDs still own lobbies (see security.test.js).
 *
 * Run from prototypes/lobby-lab:  node --test test/local-only.test.js
 */

import test from "node:test";
import assert from "node:assert/strict";
import net from "node:net";
import { start, isLoopbackHost } from "../server.js";

const UUID = "aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa";

/** Raw TCP HTTP request — full control over Host/Origin (fetch forbids Host). */
function rawRequest(port, { method = "GET", path = "/", hostHeader = `127.0.0.1:${port}`, headers = {}, body = "" }) {
    return new Promise((resolve, reject) => {
        const sock = net.connect(port, "127.0.0.1", () => {
            const lines = [`${method} ${path} HTTP/1.1`];
            if (hostHeader !== null) lines.push(`Host: ${hostHeader}`);
            lines.push("Connection: close");
            for (const [k, v] of Object.entries(headers)) lines.push(`${k}: ${v}`);
            if (body) lines.push(`Content-Length: ${Buffer.byteLength(body)}`);
            sock.write(lines.join("\r\n") + "\r\n\r\n" + body);
        });
        let data = "";
        sock.on("data", (c) => { data += c.toString("latin1"); });
        sock.on("end", () => resolve(data));
        sock.on("error", reject);
    });
}

const statusOf = (raw) => parseInt(raw.split(" ")[1], 10);
const bodyOf = (raw) => raw.slice(raw.indexOf("\r\n\r\n") + 4);

let ctx;
const base = () => `http://127.0.0.1:${ctx.port}`;

test.before(async () => {
    ctx = await start({ port: 0 });
});

test.after(async () => {
    await ctx.close();
});

// --- 1. bind-config guard -------------------------------------------------

test("loopback classifier: only loopback hosts pass", () => {
    for (const h of ["127.0.0.1", "127.0.1.2", "127.255.255.255", "::1", "[::1]", "localhost", "LOCALHOST"]) {
        assert.equal(isLoopbackHost(h), true, `${h} should be loopback`);
    }
    for (const h of ["0.0.0.0", "192.168.1.10", "10.0.0.5", "8.8.8.8", "example.com", "", null, undefined, "127.999.1.1"]) {
        assert.equal(isLoopbackHost(h), false, `${h} should NOT be loopback`);
    }
});

test("bind config: non-loopback host is rejected before listening", async () => {
    let bound;
    try {
        bound = await start({ port: 0, host: "0.0.0.0" });
    } catch (e) {
        assert.match(e.message, /non-loopback/i, "rejection names the policy");
        return;
    }
    await bound.close(); // reachable only before the fix: the vuln is demonstrated
    assert.fail("server bound 0.0.0.0 — expected rejection before listen");
});

// --- 2. Host header guard --------------------------------------------------

test("spoofed Host header is refused", async () => {
    const raw = await rawRequest(ctx.port, { path: "/lobbies", hostHeader: "evil.example" });
    assert.equal(statusOf(raw), 403, `expected 403, got: ${raw.split("\r\n")[0]}`);
    assert.match(bodyOf(raw), /loopback/i);
});

test("missing Host header is refused", async () => {
    const raw = await rawRequest(ctx.port, { path: "/lobbies", hostHeader: null });
    assert.ok([400, 403].includes(statusOf(raw)), `expected 400/403, got: ${raw.split("\r\n")[0]}`);
});

// --- 3. browser-origin guard on mutations ----------------------------------

test("cross-origin POST is refused with 403", async () => {
    const raw = await rawRequest(ctx.port, {
        method: "POST",
        path: "/lobbies",
        headers: { "Content-Type": "application/json", Origin: "http://evil.example" },
        body: JSON.stringify({ ...{ hostUuid: UUID, hostName: "T", hostRating: 1000, wssUrl: "wss://t.example", dataVersion: "lab-1" }, hostName: "evil" }),
    });
    assert.equal(statusOf(raw), 403, `expected 403, got: ${raw.split("\r\n")[0]}`);
    assert.match(bodyOf(raw), /cross-origin/i);
});

test("cross-origin DELETE is refused with 403", async () => {
    // create via the trusted path first
    const created = await fetch(`${base()}/lobbies`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ hostUuid: UUID, hostName: "T", hostRating: 1000, wssUrl: "wss://t.example", dataVersion: "lab-1" }),
    });
    assert.equal(created.status, 200);
    const { code } = await created.json();
    const raw = await rawRequest(ctx.port, {
        method: "DELETE",
        path: `/lobbies/${code}`,
        headers: { Origin: "http://evil.example" },
        body: JSON.stringify({ hostUuid: UUID }),
    });
    assert.equal(statusOf(raw), 403, `expected 403, got: ${raw.split("\r\n")[0]}`);
});

test("cross-origin Referer without Origin is refused", async () => {
    const raw = await rawRequest(ctx.port, {
        method: "POST",
        path: "/lobbies",
        headers: { "Content-Type": "application/json", Referer: "http://evil.example/page" },
        body: JSON.stringify({ ...{ hostUuid: UUID, hostName: "T", hostRating: 1000, wssUrl: "wss://t.example", dataVersion: "lab-1" }, hostName: "evil" }),
    });
    assert.equal(statusOf(raw), 403, `expected 403, got: ${raw.split("\r\n")[0]}`);
});

// --- positive controls: legitimate traffic keeps working -------------------

test("same-origin browser POST still works", async () => {
    const res = await fetch(`${base()}/lobbies`, {
        method: "POST",
        headers: { "content-type": "application/json", Origin: base() },
        body: JSON.stringify({ ...{ hostUuid: UUID, hostName: "T", hostRating: 1000, wssUrl: "wss://t.example", dataVersion: "lab-1" }, hostName: "ui" }),
    });
    assert.equal(res.status, 200, "same-origin UI traffic must keep working");
});

test("non-browser POST without Origin still works", async () => {
    const res = await fetch(`${base()}/lobbies`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ ...{ hostUuid: UUID, hostName: "T", hostRating: 1000, wssUrl: "wss://t.example", dataVersion: "lab-1" }, hostName: "client" }),
    });
    assert.equal(res.status, 200, "Node LobbyClient / demo / curl path must keep working");
});

test("cross-origin GET list is still served (reads are not mutations)", async () => {
    const raw = await rawRequest(ctx.port, {
        path: "/lobbies",
        headers: { Origin: "http://evil.example" },
    });
    // Deliberate scope: GET responses carry no CORS headers, so a hostile page
    // cannot read them; only state changes are blocked.
    assert.equal(statusOf(raw), 200, `expected 200, got: ${raw.split("\r\n")[0]}`);
});
