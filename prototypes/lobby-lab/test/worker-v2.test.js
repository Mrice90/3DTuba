/**
 * Worker v2 tests (AI-096).
 *
 * Exercises the REAL HTTP server on an ephemeral loopback port: the v2
 * router wrapping the pinned v1 worker. Covers v1 backward compatibility
 * through the router, the dataVersion gate, atomic room pairing, room
 * read/close, and the signed-results lifecycle (waiting -> agreed ->
 * verify, tampering, disputes, non-seat rejection).
 *
 * Run from prototypes/lobby-lab:  npm test
 */

import test from "node:test";
import assert from "node:assert/strict";
import { start } from "../server.js";

const U = (c) => `${c}${c}${c}${c}${c}${c}${c}${c}-1111-4111-8111-${c}${c}${c}${c}${c}${c}${c}${c}${c}${c}${c}${c}`;
const UUID_A = U("a");
const UUID_B = U("b");
const UUID_C = U("c");
const UUID_D = U("d");
const UUID_E = U("e");
const UUID_F = U("f");
const UUID_G = U("1");
const UUID_H = U("2");

let ctx;
const clock = { t: Date.now() };
const base = () => `http://127.0.0.1:${ctx.port}`;

test.before(async () => {
    ctx = await start({ port: 0, now: () => clock.t });
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

const post = (path, body) => api(path, { method: "POST", body: JSON.stringify(body) });

async function enqueue(uuid, name) {
    const r = await post("/queue", { uuid, name, rating: 1000 });
    assert.equal(r.status, 200, `enqueue ${name}`);
}

async function pairRoom(uuidA, uuidB, dataVersion = "lab-2") {
    await enqueue(uuidA, "SeatA");
    await enqueue(uuidB, "SeatB");
    const r = await post("/v2/rooms/pair", { uuidA, uuidB, dataVersion });
    assert.equal(r.status, 200, "pair room");
    return r.json;
}

// --- v1 backward compatibility through the v2 router ---

test("v1 endpoints still work verbatim through the v2 router (lab-1 ungated)", async () => {
    const created = await post("/lobbies", {
        hostUuid: UUID_A,
        hostName: "V1Host",
        hostRating: 1000,
        wssUrl: "wss://v2-compat.trycloudflare.com",
        dataVersion: "lab-1", // old version: v1 must NOT gate it
    });
    assert.equal(created.status, 200);
    const listed = await api("/lobbies");
    assert.equal(listed.status, 200);
    assert.ok(listed.json.some((l) => l.code === created.json.code && l.dataVersion === "lab-1"));
    const gone = await api(`/lobbies/${created.json.code}`, {
        method: "DELETE",
        body: JSON.stringify({ hostUuid: UUID_A }),
    });
    assert.equal(gone.status, 200);
});

test("GET /v2/version advertises the gate and v1 compatibility", async () => {
    const r = await api("/v2/version");
    assert.equal(r.status, 200);
    assert.equal(r.json.worker, "v2");
    assert.equal(r.json.dataVersionMin, "lab-2");
    assert.equal(r.json.v1Compatible, true);
    assert.ok(Array.isArray(r.json.endpoints) && r.json.endpoints.length >= 7);
});

// --- dataVersion gate ---

test("v2 state-changing endpoints reject missing, old, and unknown dataVersion", async () => {
    const body = { uuidA: UUID_A, uuidB: UUID_B };
    const missing = await post("/v2/rooms/pair", body);
    assert.equal(missing.status, 400);
    assert.match(missing.json.error, /dataVersion is required/);

    const old = await post("/v2/rooms/pair", { ...body, dataVersion: "lab-1" });
    assert.equal(old.status, 400);
    assert.match(old.json.error, /below the v2 minimum/);

    const unknown = await post("/v2/rooms/pair", { ...body, dataVersion: "lab-9" });
    assert.equal(unknown.status, 400);
    assert.match(unknown.json.error, /unknown dataVersion/);

    const resMissing = await post("/v2/results", { roomId: "X", reporterUuid: UUID_A });
    assert.equal(resMissing.status, 400);
});

// --- room assignment ---

test("pair consumes both queue tickets and both seats read the room", async () => {
    const room = await pairRoom(UUID_A, UUID_B);
    assert.ok(/^[A-Z2-9]{12}$/.test(room.roomId));
    assert.equal(room.seatA, UUID_A);
    assert.equal(room.seatB, UUID_B);
    assert.equal(room.dataVersion, "lab-2");

    // Tickets are consumed: both seats poll back to waiting/expired.
    for (const u of [UUID_A, UUID_B]) {
        const poll = await api(`/queue/poll?uuid=${u}`);
        assert.equal(poll.json.status, "waiting");
        assert.match(poll.json.reason || "", /expired/);
        const mine = await api(`/v2/rooms?uuid=${u}`);
        assert.equal(mine.status, 200);
        assert.equal(mine.json.roomId, room.roomId);
    }
});

test("re-pairing an already-paired seat pair is idempotent", async () => {
    const again = await post("/v2/rooms/pair", { uuidA: UUID_A, uuidB: UUID_B, dataVersion: "lab-2" });
    assert.equal(again.status, 200);
    // Same live room returned even though the tickets are long gone.
    const mine = await api(`/v2/rooms?uuid=${UUID_A}`);
    assert.equal(again.json.roomId, mine.json.roomId);
});

test("pairing requires both players to hold live queue tickets", async () => {
    await enqueue(UUID_C, "Lonely");
    const r = await post("/v2/rooms/pair", { uuidA: UUID_C, uuidB: UUID_F, dataVersion: "lab-2" });
    assert.equal(r.status, 400);
    assert.match(r.json.error, /live queue tickets/);
    // C's own ticket is untouched by the failed pairing.
    const poll = await api(`/queue/poll?uuid=${UUID_C}`);
    assert.equal(poll.json.status, "waiting");
    assert.ok(!("reason" in poll.json));
    await api(`/queue?uuid=${UUID_C}`, { method: "DELETE" });
});

test("pairing rejects malformed input", async () => {
    const r1 = await post("/v2/rooms/pair", { uuidA: "nope", uuidB: UUID_B, dataVersion: "lab-2" });
    assert.equal(r1.status, 400);
    const r2 = await post("/v2/rooms/pair", { uuidA: UUID_B, uuidB: UUID_B, dataVersion: "lab-2" });
    assert.equal(r2.status, 400);
});

test("room read 404s for strangers; close is seat-only and idempotent", async () => {
    const missing = await api(`/v2/rooms?uuid=${UUID_F}`);
    assert.equal(missing.status, 404);

    const room = await pairRoom(UUID_D, UUID_E);
    const forbidden = await api(`/v2/rooms/${room.roomId}`, {
        method: "DELETE",
        body: JSON.stringify({ uuid: UUID_F }),
    });
    assert.equal(forbidden.status, 403);

    const closed = await api(`/v2/rooms/${room.roomId}`, {
        method: "DELETE",
        body: JSON.stringify({ uuid: UUID_D }),
    });
    assert.equal(closed.status, 200);
    const gone = await api(`/v2/rooms?uuid=${UUID_D}`);
    assert.equal(gone.status, 404);
    // Idempotent close of an already-closed room.
    const closedAgain = await api(`/v2/rooms/${room.roomId}`, {
        method: "DELETE",
        body: JSON.stringify({ uuid: UUID_D }),
    });
    assert.equal(closedAgain.status, 200);
});

// --- signed results ---

test("results: waiting -> agreed, receipt verifies", async () => {
    const room = await pairRoom(UUID_G, UUID_H);
    const dv = { dataVersion: "lab-2" };

    const first = await post("/v2/results", {
        roomId: room.roomId, reporterUuid: UUID_G, winnerUuid: UUID_G, loserUuid: UUID_H, ...dv,
    });
    assert.equal(first.status, 200);
    assert.equal(first.json.status, "waiting");

    const waiting = await api(`/v2/results/${room.roomId}`);
    assert.equal(waiting.json.status, "waiting");

    const second = await post("/v2/results", {
        roomId: room.roomId, reporterUuid: UUID_H, winnerUuid: UUID_G, loserUuid: UUID_H,
        finalStateHash: "abc123", ...dv,
    });
    assert.equal(second.status, 200);
    assert.equal(second.json.status, "agreed");
    const receipt = second.json.receipt;
    assert.equal(receipt.v, 2);
    assert.equal(receipt.roomId, room.roomId);
    assert.equal(receipt.winnerUuid, UUID_G);
    assert.ok(typeof receipt.sig === "string" && receipt.sig.length === 64);

    const verify = await post("/v2/results/verify", { receipt });
    assert.deepEqual(verify.json, { valid: true });

    const stored = await api(`/v2/results/${room.roomId}`);
    assert.equal(stored.json.status, "agreed");
    assert.deepEqual(stored.json.receipt, receipt);
});

test("results: tampered receipt fails verification", async () => {
    const room = await pairRoom(U("3"), U("4"));
    const dv = { dataVersion: "lab-2" };
    await post("/v2/results", { roomId: room.roomId, reporterUuid: U("3"), winnerUuid: U("3"), loserUuid: U("4"), ...dv });
    const agreed = await post("/v2/results", { roomId: room.roomId, reporterUuid: U("4"), winnerUuid: U("3"), loserUuid: U("4"), ...dv });
    const tampered = { ...agreed.json.receipt, winnerUuid: U("4") }; // flip the winner
    const verify = await post("/v2/results/verify", { receipt: tampered });
    assert.equal(verify.json.valid, false);

    const malformed = await post("/v2/results/verify", { receipt: { v: 2 } });
    assert.equal(malformed.json.valid, false);
});

test("results: disagreement is flagged, never auto-resolved", async () => {
    const room = await pairRoom(U("5"), U("6"));
    const dv = { dataVersion: "lab-2" };
    await post("/v2/results", { roomId: room.roomId, reporterUuid: U("5"), winnerUuid: U("5"), loserUuid: U("6"), ...dv });
    const disputed = await post("/v2/results", { roomId: room.roomId, reporterUuid: U("6"), winnerUuid: U("6"), loserUuid: U("5"), ...dv });
    assert.equal(disputed.status, 200);
    assert.equal(disputed.json.status, "disputed");
    assert.ok(!("receipt" in disputed.json));

    const stored = await api(`/v2/results/${room.roomId}`);
    assert.equal(stored.json.status, "disputed");
});

test("results: non-seat reporter rejected, unknown room 404s, seats enforced", async () => {
    const room = await pairRoom(U("7"), U("8"));
    const dv = { dataVersion: "lab-2" };
    const outsider = await post("/v2/results", {
        roomId: room.roomId, reporterUuid: UUID_F, winnerUuid: U("7"), loserUuid: U("8"), ...dv,
    });
    assert.equal(outsider.status, 403);

    const unknown = await post("/v2/results", {
        roomId: "ZZZZZZZZZZZZ", reporterUuid: U("7"), winnerUuid: U("7"), loserUuid: U("8"), ...dv,
    });
    assert.equal(unknown.status, 404);

    const notSeats = await post("/v2/results", {
        roomId: room.roomId, reporterUuid: U("7"), winnerUuid: U("7"), loserUuid: UUID_F, ...dv,
    });
    assert.equal(notSeats.status, 400);

    const unknownResults = await api("/v2/results/ZZZZZZZZZZZZ");
    assert.equal(unknownResults.status, 404);
});

test("results: same reporter re-reporting overwrites (idempotent retries)", async () => {
    const room = await pairRoom(U("9"), U("0"));
    const dv = { dataVersion: "lab-2" };
    const once = await post("/v2/results", {
        roomId: room.roomId, reporterUuid: U("9"), winnerUuid: U("9"), loserUuid: U("0"), ...dv,
    });
    assert.equal(once.json.status, "waiting");
    // Retry with a corrected claim: still one claim, still waiting.
    const twice = await post("/v2/results", {
        roomId: room.roomId, reporterUuid: U("9"), winnerUuid: U("0"), loserUuid: U("9"), ...dv,
    });
    assert.equal(twice.json.status, "waiting");
    // Opponent agrees with the corrected claim -> agreed on U("0") winning.
    const agreed = await post("/v2/results", {
        roomId: room.roomId, reporterUuid: U("0"), winnerUuid: U("0"), loserUuid: U("9"), ...dv,
    });
    assert.equal(agreed.json.status, "agreed");
    assert.equal(agreed.json.receipt.winnerUuid, U("0"));
});
