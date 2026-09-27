/**
 * Contract tests for the lobby-lab server (AI-040).
 *
 * Two independent clients exercise the REAL HTTP server on an ephemeral
 * loopback port: no mocks of the handler, no network beyond 127.0.0.1.
 *
 * Covers: create/list/delete ownership, malformed JSON/fields, invalid
 * methods/routes, TTL expiry via a controllable clock, stale entries,
 * queue cancellation, quick-match lifecycle, and version-mismatch behavior
 * (documented as unsupported by the pinned worker contract).
 *
 * Run from prototypes/lobby-lab:  npm test
 */

import test from "node:test";
import assert from "node:assert/strict";
import { start } from "../server.js";

const UUID_A = "aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa";
const UUID_B = "bbbbbbbb-2222-4222-8222-bbbbbbbbbbbb";
const UUID_C = "cccccccc-3333-4333-8333-cccccccccccc";

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

function lobbyBody(uuid, name, extra = {}) {
    return JSON.stringify({
        hostUuid: uuid,
        hostName: name,
        hostRating: 1000,
        wssUrl: "wss://contract-test.trycloudflare.com",
        dataVersion: "lab-1",
        ...extra,
    });
}

test("two clients: A creates, B sees it, B cannot delete it, A can", async () => {
    const created = await api("/lobbies", { method: "POST", body: lobbyBody(UUID_A, "Alice") });
    assert.equal(created.status, 200);
    const code = created.json.code;

    // Client B lists: discovery is public.
    const listed = await api("/lobbies");
    assert.equal(listed.json.length, 1);
    assert.equal(listed.json[0].hostName, "Alice");

    // Client B tries to delete with its own UUID: denied.
    const denied = await api(`/lobbies/${code}`, {
        method: "DELETE", body: JSON.stringify({ hostUuid: UUID_B }),
    });
    assert.equal(denied.status, 403);

    // Client B tries with no body at all: denied.
    const noBody = await api(`/lobbies/${code}`, { method: "DELETE" });
    assert.equal(noBody.status, 403);

    // Owner A deletes: success.
    const removed = await api(`/lobbies/${code}`, {
        method: "DELETE", body: JSON.stringify({ hostUuid: UUID_A }),
    });
    assert.equal(removed.status, 200);
    assert.equal(removed.json.ok, true);

    const gone = await api(`/lobbies/${code}`);
    assert.equal(gone.status, 404);
});

test("malformed JSON and missing fields are rejected", async () => {
    const badJson = await fetch(`${base()}/lobbies`, {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: '{"hostUuid":',
    });
    assert.equal(badJson.status, 400);

    const missing = await api("/lobbies", { method: "POST", body: JSON.stringify({ hostName: "NoUuid" }) });
    assert.equal(missing.status, 400);

    const emptyBody = await api("/lobbies", { method: "POST" });
    assert.equal(emptyBody.status, 400);
});

test("invalid methods and routes return 404", async () => {
    assert.equal((await api("/lobbies", { method: "PUT", body: "{}" })).status, 404);
    assert.equal((await api("/lobbies/ABCDEF", { method: "PUT", body: "{}" })).status, 404);
    assert.equal((await api("/definitely-not-here")).status, 404);
    assert.equal((await api("/queue", { method: "PUT", body: "{}" })).status, 404);
});

test("lobby codes are case-sensitive on lookup (pinned contract behavior)", async () => {
    const created = await api("/lobbies", { method: "POST", body: lobbyBody(UUID_A, "Case") });
    const code = created.json.code;
    // The router does NOT uppercase lookups: only the exact code resolves.
    const lower = await api(`/lobbies/${code.toLowerCase()}`);
    assert.equal(lower.status, 404);
    const exact = await api(`/lobbies/${code}`);
    assert.equal(exact.status, 200);
    await api(`/lobbies/${code}`, { method: "DELETE", body: JSON.stringify({ hostUuid: UUID_A }) });
});

test("TTL expiry: lobby disappears after LOBBY_TTL with controllable clock", async () => {
    const created = await api("/lobbies", { method: "POST", body: lobbyBody(UUID_A, "Expiring") });
    const code = created.json.code;
    assert.equal((await api(`/lobbies/${code}`)).status, 200);

    // LOBBY_TTL is 120s in the pinned worker. Advance past it.
    clock.t += 121_000;

    const listed = await api("/lobbies");
    assert.deepEqual(listed.json, [], "expired lobby is not listed");
    assert.equal((await api(`/lobbies/${code}`)).status, 404, "expired lobby detail is gone");
    assert.equal(ctx.kv.liveCount("lobby:"), 0, "no live lobby keys remain");
});

test("stale entries do not block new lobbies", async () => {
    const first = await api("/lobbies", { method: "POST", body: lobbyBody(UUID_A, "Stale") });
    clock.t += 121_000; // expire it
    const second = await api("/lobbies", { method: "POST", body: lobbyBody(UUID_B, "Fresh") });
    assert.equal(second.status, 200);
    const listed = await api("/lobbies");
    assert.equal(listed.json.length, 1);
    assert.equal(listed.json[0].hostName, "Fresh");
    await api(`/lobbies/${second.json.code}`, {
        method: "DELETE", body: JSON.stringify({ hostUuid: UUID_B }),
    });
});

test("queue cancellation: DELETE /queue?uuid= removes the ticket", async () => {
    const enq = await api("/queue", {
        method: "POST",
        body: JSON.stringify({ uuid: UUID_A, name: "Quentin", rating: 1000, dataVersion: "lab-1" }),
    });
    assert.equal(enq.status, 200);
    assert.equal(enq.json.queued, true);

    const cancelled = await api(`/queue?uuid=${UUID_A}`, { method: "DELETE" });
    assert.equal(cancelled.status, 200);
    assert.equal(cancelled.json.ok, true);

    // Polling after cancellation: no ticket -> "waiting" with re-enqueue reason.
    const poll = await api(`/queue/poll?uuid=${UUID_A}`);
    assert.equal(poll.status, 200);
    assert.equal(poll.json.status, "waiting");
    assert.match(poll.json.reason, /re-enqueue/);

    // leaveQueue is idempotent even for unknown uuids.
    const again = await api(`/queue?uuid=${UUID_B}`, { method: "DELETE" });
    assert.equal(again.status, 200);
    assert.equal(again.json.ok, true);
});

test("quick-match lifecycle: earliest ticket hosts, host publishes pairing", async () => {
    // Contract (pinned): POST /queue {uuid,...} -> {queued:true};
    // GET /queue/poll?uuid= -> waiting | host | ready;
    // POST /pair {hostUuid, forUuid, wssUrl, ...} publishes the pairing.
    const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

    const a = await api("/queue", {
        method: "POST",
        body: JSON.stringify({ uuid: UUID_A, name: "QuickA", rating: 1000, dataVersion: "lab-1" }),
    });
    assert.equal(a.json.queued, true);
    await sleep(5); // ensure distinct enqueuedAt (uses real Date.now)
    const b = await api("/queue", {
        method: "POST",
        body: JSON.stringify({ uuid: UUID_B, name: "QuickB", rating: 1010, dataVersion: "lab-1" }),
    });
    assert.equal(b.json.queued, true);

    // A enqueued first -> A is told to host.
    const aPoll = await api(`/queue/poll?uuid=${UUID_A}`);
    assert.equal(aPoll.status, 200);
    assert.equal(aPoll.json.status, "host");
    assert.equal(aPoll.json.opponentUuid, UUID_B);
    assert.equal(aPoll.json.opponentName, "QuickB");

    // B enqueued later -> waits for the host's pairing.
    const bPoll = await api(`/queue/poll?uuid=${UUID_B}`);
    assert.equal(bPoll.status, 200);
    assert.equal(bPoll.json.status, "waiting");

    // A (host) publishes the pairing out-of-band details.
    const pair = await api("/pair", {
        method: "POST",
        body: JSON.stringify({
            hostUuid: UUID_A, forUuid: UUID_B,
            wssUrl: "wss://quick.tunnel.trycloudflare.com",
            code: "QM1", hostName: "QuickA", hostRating: 1000,
        }),
    });
    assert.equal(pair.status, 200);
    assert.equal(pair.json.ok, true);

    // B polls: pairing is ready and consumed on read.
    const bReady = await api(`/queue/poll?uuid=${UUID_B}`);
    assert.equal(bReady.status, 200);
    assert.equal(bReady.json.status, "ready");
    assert.equal(bReady.json.wssUrl, "wss://quick.tunnel.trycloudflare.com");
    assert.equal(bReady.json.opponentName, "QuickA");

    const bAgain = await api(`/queue/poll?uuid=${UUID_B}`);
    assert.equal(bAgain.json.status, "waiting");
    assert.match(bAgain.json.reason, /re-enqueue/, "pairing consumed; tickets cleared");

    // Bad pairing payloads are rejected.
    const badPair = await api("/pair", {
        method: "POST", body: JSON.stringify({ hostUuid: UUID_A }),
    });
    assert.equal(badPair.status, 400);
});

test("queue ticket expires after QUEUE_TTL", async () => {
    const enq = await api("/queue", {
        method: "POST",
        body: JSON.stringify({ uuid: UUID_C, name: "Tickety", rating: 1000, dataVersion: "lab-1" }),
    });
    assert.equal(enq.json.queued, true);
    clock.t += 61_000; // QUEUE_TTL is 60s
    const poll = await api(`/queue/poll?uuid=${UUID_C}`);
    assert.equal(poll.status, 200);
    assert.equal(poll.json.status, "waiting");
    assert.match(poll.json.reason, /re-enqueue/, "expired ticket asks client to re-enqueue");
});

test("version mismatch is NOT enforced by the pinned contract (documented)", async () => {
    // The worker stores and echoes dataVersion but never compares it.
    // This test pins that observed behavior so a future change is visible.
    const created = await api("/lobbies", {
        method: "POST",
        body: lobbyBody(UUID_A, "OldClient", { dataVersion: "alpha-0.7.15" }),
    });
    assert.equal(created.status, 200);
    const listed = await api("/lobbies");
    assert.equal(listed.json[0].dataVersion, "alpha-0.7.15", "version echoed, not rejected");
    await api(`/lobbies/${created.json.code}`, {
        method: "DELETE", body: JSON.stringify({ hostUuid: UUID_A }),
    });
});

test("queue enqueue/poll validate their fields", async () => {
    const noUuid = await api("/queue", { method: "POST", body: JSON.stringify({ name: "NoUuid" }) });
    assert.equal(noUuid.status, 400);

    const noParam = await api("/queue/poll");
    assert.equal(noParam.status, 400, "poll requires uuid");

    const badParam = await api("/queue/poll?uuid=not-a-uuid");
    assert.equal(badParam.status, 400, "poll requires a valid uuid");
});

test("report flow: two agreeing reports apply Elo; disagreement is flagged", async () => {
    // Reports are disabled on the default lab server (see security.test.js).
    // This exercises the pinned contract with an explicit local opt-in.
    const lab = await start({ port: 0, allowReports: true });
    const url = `http://127.0.0.1:${lab.port}`;
    const post = async (body) => {
        const res = await fetch(`${url}/report`, {
            method: "POST",
            headers: { "content-type": "application/json" },
            body: JSON.stringify(body),
        });
        return { status: res.status, json: await res.json().catch(() => ({})) };
    };
    const get = async (path) => {
        const res = await fetch(`${url}${path}`);
        return { status: res.status, json: await res.json().catch(() => ({})) };
    };
    try {
        const matchId = "contract-match-1";
        // First report alone: stored, not applied.
        const r1 = await post({
            matchId, reporterUuid: UUID_A, winnerUuid: UUID_A, loserUuid: UUID_B,
            dataVersion: "lab-1",
        });
        assert.equal(r1.status, 200);
        assert.equal(r1.json.applied, false);

        // Agreeing second report: Elo applied.
        const r2 = await post({
            matchId, reporterUuid: UUID_B, winnerUuid: UUID_A, loserUuid: UUID_B,
            dataVersion: "lab-1",
        });
        assert.equal(r2.status, 200);
        assert.equal(r2.json.applied, true);

        const rating = await get(`/rating/${UUID_A}`);
        assert.equal(rating.status, 200);
        assert.equal(rating.json.wins, 1);
        assert.ok(rating.json.rating > 1000, "winner gains rating");

        // Disagreeing reports: flagged, nothing applied.
        const d1 = await post({
            matchId: "contract-match-2", reporterUuid: UUID_A,
            winnerUuid: UUID_A, loserUuid: UUID_B, dataVersion: "lab-1",
        });
        assert.equal(d1.json.applied, false);
        const d2 = await post({
            matchId: "contract-match-2", reporterUuid: UUID_B,
            winnerUuid: UUID_B, loserUuid: UUID_A, dataVersion: "lab-1",
        });
        assert.equal(d2.json.applied, false);
        assert.match(d2.json.reason, /disagree/);

        // Invalid report payloads are rejected.
        const bad = await post({});
        assert.equal(bad.status, 400);
        const self = await post({
            matchId: "x", reporterUuid: UUID_A, winnerUuid: UUID_A, loserUuid: UUID_A,
        });
        assert.equal(self.status, 400, "winner and loser must differ");
    } finally {
        await lab.close();
    }
});
