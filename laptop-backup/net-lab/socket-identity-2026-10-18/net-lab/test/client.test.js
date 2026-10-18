/**
 * Tests for the reusable LobbyClient adapter (AI-042).
 *
 * Covers: happy path against the real lab server, unavailable service,
 * timeouts, invalid (non-JSON) responses, caller cancellation, the
 * loopback guard, and client-side request validation.
 *
 * Run from prototypes/lobby-lab:  npm test
 */

import test from "node:test";
import assert from "node:assert/strict";
import http from "node:http";
import { start } from "../server.js";
import { LobbyClient, LobbyError } from "../public/client.js";

const UUID_A = "aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa";

let ctx;
test.before(async () => {
    ctx = await start({ port: 0 });
});
test.after(async () => {
    await ctx.close();
});

const client = () => new LobbyClient({ baseUrl: `http://127.0.0.1:${ctx.port}` });

test("browser fetch receives the global receiver", async () => {
    const c = new LobbyClient({
        fetchImpl: function () {
            assert.equal(this, globalThis, "fetch must not receive LobbyClient as this");
            return Promise.resolve(new Response("[]", { status: 200 }));
        },
    });
    assert.deepEqual(await c.listLobbies(), []);
});

test("happy path: create -> list -> get -> delete through the adapter", async () => {
    const c = client();
    const { code, hostUuid } = await c.createLobby({
        hostName: "Adapter", wssUrl: "wss://adapter.tunnel.trycloudflare.com", hostRating: 1234,
    });
    assert.match(code, /^[A-Z0-9]{6}$/);
    assert.ok(hostUuid, "adapter returns the host secret");

    const lobbies = await c.listLobbies();
    assert.equal(lobbies.length, 1);
    assert.equal(lobbies[0].hostName, "Adapter");

    const one = await c.getLobby(code);
    assert.equal(one.wssUrl, "wss://adapter.tunnel.trycloudflare.com");

    const del = await c.deleteLobby(code, hostUuid);
    assert.equal(del.ok, true);
    assert.deepEqual(await c.listLobbies(), []);
});

test("server errors surface as LobbyError with status and message", async () => {
    const c = client();
    await assert.rejects(
        c.getLobby("ZZZZZZ"),
        (e) => e instanceof LobbyError && e.status === 404 && /unknown or expired/.test(e.message),
    );
});

test("client-side validation rejects bad createLobby input", async () => {
    const c = client();
    await assert.rejects(c.createLobby({ hostName: "", wssUrl: "wss://x/" }), /hostName/);
    await assert.rejects(c.createLobby({ hostName: "NoUrl" }), /wssUrl/);
});

test("unavailable service: clean LobbyError, not a raw TypeError", async () => {
    // Port 1 is (practically) never listening.
    const c = new LobbyClient({ baseUrl: "http://127.0.0.1:1", timeoutMs: 1500 });
    await assert.rejects(
        c.listLobbies(),
        (e) => e instanceof LobbyError && e.code === "unreachable" && /could not reach/.test(e.message),
    );
});

test("timeout: hanging server triggers LobbyError with code 'timeout'", async () => {
    const hanging = http.createServer(() => { /* never respond */ });
    await new Promise((r) => hanging.listen(0, "127.0.0.1", r));
    try {
        const c = new LobbyClient({
            baseUrl: `http://127.0.0.1:${hanging.address().port}`,
            timeoutMs: 300,
        });
        await assert.rejects(
            c.listLobbies(),
            (e) => e instanceof LobbyError && e.code === "timeout" && /timed out/.test(e.message),
        );
    } finally {
        hanging.close();
    }
});

test("invalid response: non-JSON body triggers LobbyError", async () => {
    const weird = http.createServer((req, res) => {
        res.writeHead(200, { "content-type": "text/html" });
        res.end("<html>not json</html>");
    });
    await new Promise((r) => weird.listen(0, "127.0.0.1", r));
    try {
        const c = new LobbyClient({ baseUrl: `http://127.0.0.1:${weird.address().port}` });
        await assert.rejects(
            c.listLobbies(),
            (e) => e instanceof LobbyError && e.code === "invalid-response" && /expected JSON/.test(e.message),
        );
    } finally {
        weird.close();
    }
});

test("cancellation: AbortController aborts the request", async () => {
    const hanging = http.createServer(() => { /* never respond */ });
    await new Promise((r) => hanging.listen(0, "127.0.0.1", r));
    try {
        const c = new LobbyClient({
            baseUrl: `http://127.0.0.1:${hanging.address().port}`,
            timeoutMs: 10000,
        });
        const ctrl = new AbortController();
        const p = c.listLobbies({ signal: ctrl.signal });
        ctrl.abort();
        await assert.rejects(
            p,
            (e) => e instanceof LobbyError && e.code === "cancelled" && /cancelled/.test(e.message),
        );
    } finally {
        hanging.close();
    }
});

test("loopback guard: non-loopback baseUrl is refused without allowRemote", () => {
    assert.throws(
        () => new LobbyClient({ baseUrl: "https://example.com" }),
        (e) => e instanceof LobbyError && /non-loopback/.test(e.message),
    );
    assert.doesNotThrow(() => new LobbyClient({ baseUrl: "https://example.com", allowRemote: true }));
    assert.doesNotThrow(() => new LobbyClient({ baseUrl: "http://localhost:8787" }));
    assert.throws(() => new LobbyClient({ baseUrl: "not a url" }), LobbyError);
});

test("queue methods round-trip through the adapter", async () => {
    const c = client();
    const uuid = "dddddddd-4444-4444-8444-dddddddddddd";
    const enq = await c.enqueue({ uuid, name: "AdapterQ", rating: 1000, dataVersion: "lab-1" });
    assert.equal(enq.queued, true);
    const poll = await c.pollQueue(uuid);
    assert.equal(poll.status, "waiting");
    const left = await c.leaveQueue(uuid);
    assert.equal(left.ok, true);
});
