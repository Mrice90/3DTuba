/**
 * Offline smoke test for the Infinite Conquest lobby Worker (AI-038).
 *
 * Exercises the real endpoint contracts of worker.js (POST /lobbies,
 * GET /lobbies, GET /lobbies/:code, DELETE /lobbies/:code) against an
 * in-memory mocked KV store.
 *
 * No network, no Cloudflare account, no writes to the deployed service.
 *
 * Run: node --test smoke.test.js   (Node 18+)
 */

import test from "node:test";
import assert from "node:assert/strict";
import worker from "./worker.js";

function fakeKv() {
    const store = new Map();
    return {
        async get(k) { return store.has(k) ? store.get(k) : null; },
        async put(k, v) { store.set(k, v); },
        async delete(k) { store.delete(k); },
        async list({ prefix, limit = 1000 }) {
            const keys = [...store.keys()]
                .filter((k) => k.startsWith(prefix))
                .slice(0, limit)
                .map((name) => ({ name }));
            return { keys, list_complete: true };
        },
    };
}

const env = () => ({ IC_KV: fakeKv() });
const HOST_UUID = "aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa";
const OTHER_UUID = "bbbbbbbb-2222-4222-8222-bbbbbbbbbbbb";

function post(path, body) {
    return new Request(`https://lobby.local${path}`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify(body),
    });
}

function req(path, method = "GET", body) {
    return new Request(`https://lobby.local${path}`, {
        method,
        headers: { "content-type": "application/json" },
        body: body ? JSON.stringify(body) : undefined,
    });
}

async function call(e, request) {
    const res = await worker.fetch(request, e);
    return { status: res.status, json: await res.json() };
}

test("lobby lifecycle: empty list -> create -> list -> remove -> empty", async () => {
    const e = env();

    // 1. Empty lobby list.
    const empty = await call(e, req("/lobbies"));
    assert.equal(empty.status, 200);
    assert.deepEqual(empty.json, []);

    // 2. Create a lobby (actual POST /lobbies contract).
    const created = await call(e, post("/lobbies", {
        hostUuid: HOST_UUID,
        hostName: "Smoke Host",
        hostRating: 1042,
        wssUrl: "wss://smoke-test.trycloudflare.com",
        dataVersion: "ic-net-1",
    }));
    assert.equal(created.status, 200);
    assert.match(created.json.code, /^[A-Z0-9]{6}$/);
    const code = created.json.code;

    // 3. List shows the new lobby with the registered fields.
    const listed = await call(e, req("/lobbies"));
    assert.equal(listed.status, 200);
    assert.equal(listed.json.length, 1);
    assert.equal(listed.json[0].code, code);
    assert.equal(listed.json[0].hostName, "Smoke Host");
    assert.equal(listed.json[0].hostRating, 1042);
    assert.equal(listed.json[0].wssUrl, "wss://smoke-test.trycloudflare.com");

    // 4a. Wrong owner cannot remove (actual contract: 403).
    const denied = await call(e, req(`/lobbies/${code}`, "DELETE", { hostUuid: OTHER_UUID }));
    assert.equal(denied.status, 403);

    // 4b. Owner removes the lobby.
    const removed = await call(e, req(`/lobbies/${code}`, "DELETE", { hostUuid: HOST_UUID }));
    assert.equal(removed.status, 200);
    assert.equal(removed.json.ok, true);

    // 5. List is empty again; direct get is 404.
    const emptyAgain = await call(e, req("/lobbies"));
    assert.equal(emptyAgain.status, 200);
    assert.deepEqual(emptyAgain.json, []);
    const gone = await call(e, req(`/lobbies/${code}`));
    assert.equal(gone.status, 404);
});
