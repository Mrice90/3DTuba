/**
 * HTTP-level tests for the lobby-lab server (AI-039).
 *
 * Starts the real server on an ephemeral loopback port and exercises the
 * actual HTTP routes — not just the worker handler in isolation.
 *
 * Run: node --test test/   (from prototypes/lobby-lab)
 */

import test from "node:test";
import assert from "node:assert/strict";
import { start } from "../server.js";

let ctx;
const base = () => `http://127.0.0.1:${ctx.port}`;

test.before(async () => {
    ctx = await start({ port: 0 });
    assert.ok(ctx.port > 0, "server bound an ephemeral port");
});

test.after(async () => {
    await ctx.close();
});

async function api(path, opts = {}) {
    const res = await fetch(`${base()}${path}`, {
        headers: { "content-type": "application/json" },
        ...opts,
    });
    const json = await res.json().catch(() => ({}));
    return { status: res.status, json };
}

const HOST_A = "aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa";
const HOST_B = "bbbbbbbb-2222-4222-8222-bbbbbbbbbbbb";

test("lobby lifecycle over HTTP: empty -> create -> list -> get -> delete -> empty", async () => {
    const empty = await api("/lobbies");
    assert.equal(empty.status, 200);
    assert.deepEqual(empty.json, []);

    const created = await api("/lobbies", {
        method: "POST",
        body: JSON.stringify({
            hostUuid: HOST_A, hostName: "HTTP Host", hostRating: 1042,
            wssUrl: "wss://http-test.trycloudflare.com", dataVersion: "lab-1",
        }),
    });
    assert.equal(created.status, 200);
    assert.match(created.json.code, /^[A-Z0-9]{6}$/);
    const code = created.json.code;

    const listed = await api("/lobbies");
    assert.equal(listed.json.length, 1);
    assert.equal(listed.json[0].code, code);
    assert.equal(listed.json[0].hostName, "HTTP Host");

    const one = await api(`/lobbies/${code}`);
    assert.equal(one.status, 200);
    assert.equal(one.json.wssUrl, "wss://http-test.trycloudflare.com");

    // Ownership: another caller cannot delete.
    const denied = await api(`/lobbies/${code}`, {
        method: "DELETE", body: JSON.stringify({ hostUuid: HOST_B }),
    });
    assert.equal(denied.status, 403);

    const removed = await api(`/lobbies/${code}`, {
        method: "DELETE", body: JSON.stringify({ hostUuid: HOST_A }),
    });
    assert.equal(removed.status, 200);
    assert.equal(removed.json.ok, true);

    const gone = await api(`/lobbies/${code}`);
    assert.equal(gone.status, 404);
    const emptyAgain = await api("/lobbies");
    assert.deepEqual(emptyAgain.json, []);
});

test("malformed JSON body is rejected with 400", async () => {
    const res = await fetch(`${base()}/lobbies`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: "{not valid json",
    });
    assert.equal(res.status, 400);
});

test("missing required fields are rejected with 400", async () => {
    const res = await api("/lobbies", { method: "POST", body: JSON.stringify({}) });
    assert.equal(res.status, 400);
});

test("IP-literal tunnel URLs are rejected with 400", async () => {
    const res = await api("/lobbies", {
        method: "POST",
        body: JSON.stringify({ hostUuid: HOST_A, hostName: "Sneaky", wssUrl: "wss://203.0.113.7/" }),
    });
    assert.equal(res.status, 400);
});

test("unknown routes return 404", async () => {
    const res = await api("/nope");
    assert.equal(res.status, 404);
    const put = await api("/lobbies", { method: "PUT", body: JSON.stringify({}) });
    assert.equal(put.status, 404);
});

test("UI is served at /", async () => {
    const res = await fetch(`${base()}/`);
    assert.equal(res.status, 200);
    assert.match(res.headers.get("content-type"), /text\/html/);
    const html = await res.text();
    assert.ok(html.includes("Lobby Lab"), "serves the lobby UI");
});
