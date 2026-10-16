/**
 * Ranked foundation tests (AI-010 / AI-011).
 *
 * The acceptance list from PRODUCT_BACKLOG (AI-010/011) as controlled tests:
 * impersonation, forged / replayed / duplicate results and unauthorized
 * faction use fail; client agreement alone never moves a rating; ratings
 * apply exactly once; the leaderboard survives a restart.
 *
 * Layers: the service in-process (fake clock, in-memory or file KV), a real
 * relay session producing the transcript, and the lab HTTP server driven by
 * the referee runner.
 *
 * Run from prototypes/lobby-lab:  npm test
 */

import test from "node:test";
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdtempSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import { createKv } from "../kv.js";
import { createFileKv } from "../kv-file.js";
import { createRelaySession } from "../relay.js";
import { start } from "../server.js";
import { runRefereeOnce } from "../referee-runner.js";
import {
    createRankedService, signRequest, macHeaders, b64url, playerIdFor, START_RATING,
} from "../ranked.js";

const REF = "referee-secret-for-tests";
const RELAY = "relay-secret-for-tests";
const BASE = "http://local";

async function makeKeys() {
    const kp = await crypto.subtle.generateKey({ name: "Ed25519" }, true, ["sign", "verify"]);
    const pub = new Uint8Array(await crypto.subtle.exportKey("raw", kp.publicKey));
    return { privateKey: kp.privateKey, publicKey: b64url(pub), playerId: await playerIdFor(pub) };
}

function harness({ kv, clock } = {}) {
    clock = clock || { t: 1_800_000_000_000 };
    kv = kv || createKv({ now: () => clock.t });
    const roomKv = createKv({ now: () => clock.t });
    const svc = createRankedService({ kv, roomKv, now: () => clock.t, refereeSecret: REF, relaySecret: RELAY });
    return { svc, kv, roomKv, clock };
}

/** Signed call. `as` overrides which key signs (impersonation tests). */
async function call(h, method, p, { player, body, as, nonce, ts, tamper } = {}) {
    const text = body === undefined ? "" : JSON.stringify(body);
    const headers = player
        ? await signRequest({ privateKey: (as || player).privateKey, playerId: player.playerId, method, path: p, body: text, now: ts ?? h.clock.t, nonce })
        : {};
    const res = await h.svc.fetch(new Request(BASE + p, { method, headers, body: method === "GET" ? undefined : (tamper ?? text) }));
    return { status: res.status, body: await res.json() };
}

async function register(h, name) {
    const p = await makeKeys();
    const r = await call(h, "POST", "/v3/players", { player: p, body: { publicKey: p.publicKey, displayName: name } });
    assert.equal(r.status, 201, JSON.stringify(r.body));
    return p;
}

async function referee(h, method, p, body) {
    const text = body === undefined ? "" : JSON.stringify(body);
    const res = await h.svc.fetch(new Request(BASE + p, {
        method, headers: await macHeaders({ secret: REF, method, path: p, body: text, now: h.clock.t }), body: method === "GET" ? undefined : text,
    }));
    return { status: res.status, body: await res.json() };
}

/** Two registered players matched into a ranked room. */
async function matched(h) {
    const a = await register(h, "Alpha");
    const b = await register(h, "Bravo");
    assert.equal((await call(h, "POST", "/v3/ranked/queue", { player: a, body: { faction: "zeus" } })).body.status, "queued");
    const mb = (await call(h, "POST", "/v3/ranked/queue", { player: b, body: { faction: "poseidon" } })).body;
    const ma = (await call(h, "GET", "/v3/ranked/queue", { player: a })).body;
    return { a, b, ma, mb, roomId: ma.roomId };
}

const commitOf = (seed, salt) => createHash("sha256").update(seed + "|" + salt).digest("hex");
const H = (c) => c.repeat(64);

/** Play a short match through a real relay session; returns its transcript. */
async function playThroughRelay(h, m, { results, mismatch = false, timeoutSeat = null } = {}) {
    const raw = JSON.parse(await h.roomKv.get(`v2:room:${m.roomId}`));
    const seatOf = { a: m.ma.seat, b: m.mb.seat };
    let transcript = null;
    const s = createRelaySession({
        room: raw, now: () => h.clock.t, turnTimeoutMs: 1000,
        send: () => {}, broadcast: () => {}, onTranscript: (t) => { transcript = t; },
    });
    for (const k of ["a", "b"]) s.handleMessage(seatOf[k], { type: "hello" });
    for (const k of ["a", "b"]) await s.handleMessage(seatOf[k], { type: "seed-commit", commit: commitOf("seed-" + k, "salt-" + k) });
    for (const k of ["a", "b"]) await s.handleMessage(seatOf[k], { type: "seed-reveal", seed: "seed-" + k, salt: "salt-" + k });
    s.handleMessage(seatOf.a, { type: "turn", turn: 1, activeSeat: seatOf.a });
    s.handleMessage(seatOf.a, { type: "intent", actionId: "play:zeus_land_aurora_reach" });
    s.handleMessage(seatOf.a, { type: "intent", actionId: "pass" });
    s.handleMessage(seatOf.b, { type: "turn", turn: 2, activeSeat: seatOf.b });
    s.handleMessage(seatOf.a, { type: "hash", turn: 1, hash: H("1") });
    s.handleMessage(seatOf.b, { type: "hash", turn: 1, hash: mismatch ? H("2") : H("1") });
    s.handleMessage(seatOf.b, { type: "intent", actionId: "attack:capital" });
    if (timeoutSeat) {
        h.clock.t += 2000;
        s.tick();
    } else {
        for (const [k, winner] of Object.entries(results)) {
            s.handleMessage(seatOf[k], { type: "result", winnerSeat: seatOf[winner], finalHash: H("f") });
        }
    }
    assert.ok(transcript, "relay produced a transcript");
    return { transcript, seatOf };
}

async function rating(h, p) {
    return (await call(h, "GET", `/v3/ranked/players/${p.playerId}`)).body;
}

// ---- identity ----------------------------------------------------------

test("registration needs proof of possession and is idempotent", async () => {
    const h = harness();
    const p = await makeKeys();
    const other = await makeKeys();
    const forged = await call(h, "POST", "/v3/players", { player: p, as: other, body: { publicKey: p.publicKey, displayName: "Forger" } });
    assert.equal(forged.status, 401);
    assert.equal((await call(h, "POST", "/v3/players", { player: p, body: { publicKey: p.publicKey, displayName: "Real" } })).status, 201);
    const again = await call(h, "POST", "/v3/players", { player: p, body: { publicKey: p.publicKey, displayName: "Renamed" } });
    assert.equal(again.body.created, false);
    assert.equal(again.body.displayName, "Real");
    const badKey = await call(h, "POST", "/v3/players", { player: p, body: { publicKey: "abc", displayName: "Real" } });
    assert.equal(badKey.status, 400);
});

test("impersonation fails: wrong key, tampered body, unknown player, missing headers", async () => {
    const h = harness();
    const victim = await register(h, "Victim");
    const attacker = await register(h, "Attacker");
    assert.equal((await call(h, "POST", "/v3/ranked/queue", { player: victim, as: attacker, body: { faction: "zeus" } })).status, 401);
    assert.equal((await call(h, "POST", "/v3/ranked/queue", { player: victim, body: { faction: "zeus" }, tamper: JSON.stringify({ faction: "poseidon" }) })).status, 401);
    const stranger = await makeKeys();
    assert.equal((await call(h, "POST", "/v3/ranked/queue", { player: stranger, body: { faction: "zeus" } })).status, 401);
    const res = await h.svc.fetch(new Request(BASE + "/v3/ranked/queue", { method: "POST", body: "{}" }));
    assert.equal(res.status, 401);
});

test("replayed and stale requests are refused", async () => {
    const h = harness();
    const p = await register(h, "Replayer");
    const first = await call(h, "POST", "/v3/ranked/queue", { player: p, body: { faction: "zeus" }, nonce: "fixed-nonce-0001" });
    assert.equal(first.status, 200);
    const replay = await call(h, "POST", "/v3/ranked/queue", { player: p, body: { faction: "zeus" }, nonce: "fixed-nonce-0001" });
    assert.equal(replay.status, 401);
    assert.match(replay.body.error, /replayed/);
    const stale = await call(h, "GET", "/v3/ranked/queue", { player: p, ts: h.clock.t - 10 * 60_000 });
    assert.equal(stale.status, 401);
});

test("unauthorized faction use is refused", async () => {
    const h = harness();
    const p = await register(h, "Hadesfan");
    const r = await call(h, "POST", "/v3/ranked/queue", { player: p, body: { faction: "hades" } });
    assert.equal(r.status, 403);
});

// ---- matchmaking -------------------------------------------------------

test("queue pairs two players and gives each only their own seat", async () => {
    const h = harness();
    const m = await matched(h);
    assert.equal(m.ma.status, "matched");
    assert.equal(m.mb.roomId, m.ma.roomId);
    assert.notEqual(m.ma.seat, m.mb.seat);
    assert.equal(m.ma.opponent.playerId, m.b.playerId);
    assert.equal(m.mb.opponent.faction, "zeus");
    assert.equal(JSON.stringify(m.mb).includes(m.ma.seat), false, "a player never sees the opponent's seat token");
    const v2 = JSON.parse(await h.roomKv.get(`v2:room:${m.roomId}`));
    assert.deepEqual([v2.seatA, v2.seatB].sort(), [m.ma.seat, m.mb.seat].sort(), "relay upgrade path can find the room");
});

// ---- results -----------------------------------------------------------

test("agreeing client claims alone never move a rating; a referee verdict does, exactly once", async () => {
    const h = harness();
    const m = await matched(h);
    const { transcript, seatOf } = await playThroughRelay(h, m, { results: { a: "a", b: "a" } });
    assert.equal(transcript.endReason, "game-over");
    const rec = await h.svc.recordTranscript(transcript);
    assert.equal(rec.status, "pending");
    assert.equal((await rating(h, m.a)).rating, START_RATING, "no rating change from claims");

    assert.equal((await referee(h, "GET", "/v3/ranked/referee/pending")).body.pending.length, 1);
    const wrongHash = await referee(h, "POST", "/v3/ranked/referee/verdicts", { roomId: m.roomId, transcriptHash: H("0"), winnerSeat: seatOf.a });
    assert.equal(wrongHash.status, 409);
    const v = await referee(h, "POST", "/v3/ranked/referee/verdicts", { roomId: m.roomId, transcriptHash: rec.transcriptHash, winnerSeat: seatOf.a, finalStateHash: H("f"), engine: "fake-1" });
    assert.equal(v.status, 200);
    assert.equal((await rating(h, m.a)).rating, START_RATING + 20);
    assert.equal((await rating(h, m.b)).rating, START_RATING - 20);

    const dup = await referee(h, "POST", "/v3/ranked/referee/verdicts", { roomId: m.roomId, transcriptHash: rec.transcriptHash, winnerSeat: seatOf.a });
    assert.equal(dup.body.duplicate, true);
    const flip = await referee(h, "POST", "/v3/ranked/referee/verdicts", { roomId: m.roomId, transcriptHash: rec.transcriptHash, winnerSeat: seatOf.b });
    assert.equal(flip.status, 409);
    assert.equal((await rating(h, m.a)).games, 1, "rated exactly once");
    assert.equal((await h.svc.recordTranscript(transcript)).duplicate, true, "transcript replay is a no-op");
    assert.equal((await referee(h, "GET", "/v3/ranked/referee/pending")).body.pending.length, 0);
});

test("forged verdicts and forged transcripts are refused", async () => {
    const h = harness();
    const m = await matched(h);
    const { transcript, seatOf } = await playThroughRelay(h, m, { results: { a: "b", b: "b" } });
    const rec = await h.svc.recordTranscript(transcript);
    // A player (or anyone without the referee secret) posting a verdict.
    const p = "/v3/ranked/referee/verdicts";
    const body = JSON.stringify({ roomId: m.roomId, transcriptHash: rec.transcriptHash, winnerSeat: seatOf.a });
    const forged = await h.svc.fetch(new Request(BASE + p, { method: "POST", headers: await macHeaders({ secret: "guess", method: "POST", path: p, body, now: h.clock.t }), body }));
    assert.equal(forged.status, 401);
    // A transcript posted over HTTP without the relay secret, and one for a non-ranked room.
    const tp = "/v3/ranked/transcripts";
    const tb = JSON.stringify({ ...transcript, roomId: "FAKEROOM1234" });
    assert.equal((await h.svc.fetch(new Request(BASE + tp, { method: "POST", headers: await macHeaders({ secret: "guess", method: "POST", path: tp, body: tb, now: h.clock.t }), body: tb }))).status, 401);
    assert.equal((await h.svc.fetch(new Request(BASE + tp, { method: "POST", headers: await macHeaders({ secret: RELAY, method: "POST", path: tp, body: tb, now: h.clock.t }), body: tb }))).status, 409);
    const swapped = { ...transcript, roomId: m.roomId, seats: ["11111111-1111-4111-8111-111111111111", transcript.seats[1]] };
    assert.equal((await h.svc.recordTranscript(swapped)).ok, false);
    assert.equal((await rating(h, m.a)).games, 0);
});

test("hash-mismatch matches are flagged and never rated", async () => {
    const h = harness();
    const m = await matched(h);
    const { transcript, seatOf } = await playThroughRelay(h, m, { results: { a: "a", b: "a" }, mismatch: true });
    const rec = await h.svc.recordTranscript(transcript);
    assert.equal(rec.status, "flagged");
    const v = await referee(h, "POST", "/v3/ranked/referee/verdicts", { roomId: m.roomId, transcriptHash: rec.transcriptHash, winnerSeat: seatOf.a });
    assert.equal(v.status, 409);
    assert.equal((await rating(h, m.a)).games, 0);
});

test("a relay-observed timeout waits for the referee, which can see the game was already over", async () => {
    const h = harness();
    const m = await matched(h);
    const { transcript, seatOf } = await playThroughRelay(h, m, { timeoutSeat: "b" });
    assert.equal(transcript.endReason, "timeout");
    const rec = await h.svc.recordTranscript(transcript);
    assert.equal(rec.status, "pending", "a client that skips `result` makes the relay time out after game over");
    assert.equal((await rating(h, m.a)).games, 0);
    // The replay found the game finished with B winning, so B wins despite the timeout.
    const v = await referee(h, "POST", "/v3/ranked/referee/verdicts", { roomId: m.roomId, transcriptHash: rec.transcriptHash, winnerSeat: seatOf.b, legal: true });
    assert.equal(v.status, 200);
    assert.equal((await rating(h, m.b)).wins, 1);
});

test("a seat that never reveals its seed forfeits at once (nothing to replay)", async () => {
    const h = harness();
    const m = await matched(h);
    const raw = JSON.parse(await h.roomKv.get(`v2:room:${m.roomId}`));
    let t = null;
    const s = createRelaySession({ room: raw, now: () => h.clock.t, send: () => {}, broadcast: () => {}, onTranscript: (x) => { t = x; } });
    for (const seat of [m.ma.seat, m.mb.seat]) s.handleMessage(seat, { type: "hello" });
    await s.handleMessage(m.ma.seat, { type: "seed-commit", commit: commitOf(m.ma.seat, "x") });
    await s.handleMessage(m.ma.seat, { type: "seed-reveal", seed: m.ma.seat, salt: "x" });
    h.clock.t += 121_000;
    s.tick();
    assert.equal(t.endReason, "no-reveal");
    const rec = await h.svc.recordTranscript(t);
    assert.equal(rec.status, "rated");
    assert.equal((await rating(h, m.a)).wins, 1, "seat A wins when B never reveals");
    const match = (await call(h, "GET", `/v3/ranked/matches/${m.roomId}`)).body;
    assert.equal(match.verdict.source, "relay-forfeit");
});

test("an illegal intent found by the referee costs the offender the match; lying claims are counted", async () => {
    const h = harness();
    const m = await matched(h);
    // Both claim B won, but the replay finds B's intent illegal.
    const { transcript, seatOf } = await playThroughRelay(h, m, { results: { a: "b", b: "b" } });
    const rec = await h.svc.recordTranscript(transcript);
    const v = await referee(h, "POST", "/v3/ranked/referee/verdicts", { roomId: m.roomId, transcriptHash: rec.transcriptHash, legal: false, offendingSeat: seatOf.b });
    assert.equal(v.status, 200);
    assert.equal((await rating(h, m.a)).wins, 1);
    assert.equal((await rating(h, m.b)).losses, 1);
    assert.equal((await rating(h, m.b)).disputesLost, 1);
});

// ---- persistence and leaderboard ---------------------------------------

test("ratings, settled matches and used nonces survive a restart", async () => {
    const file = path.join(mkdtempSync(path.join(tmpdir(), "ranked-")), "ranked.json");
    const clock = { t: 1_800_000_000_000 };
    const h1 = harness({ kv: createFileKv(file, { now: () => clock.t }), clock });
    const m = await matched(h1);
    const { transcript, seatOf } = await playThroughRelay(h1, m, { results: { a: "a", b: "a" } });
    const rec = await h1.svc.recordTranscript(transcript);
    await referee(h1, "POST", "/v3/ranked/referee/verdicts", { roomId: m.roomId, transcriptHash: rec.transcriptHash, winnerSeat: seatOf.a });
    const nonce = "persisted-nonce-01";
    assert.equal((await call(h1, "GET", "/v3/ranked/queue", { player: m.a, nonce })).status, 200);
    const before = (await call(h1, "GET", "/v3/ranked/leaderboard")).body;

    // "Restart": a brand-new service on the same file.
    const h2 = harness({ kv: createFileKv(file, { now: () => clock.t }), clock });
    const after = (await call(h2, "GET", "/v3/ranked/leaderboard")).body;
    assert.deepEqual(after, before);
    assert.equal(after.rows[0].playerId, m.a.playerId);
    assert.equal(after.beta, true);
    const dup = await referee(h2, "POST", "/v3/ranked/referee/verdicts", { roomId: m.roomId, transcriptHash: rec.transcriptHash, winnerSeat: seatOf.b });
    assert.equal(dup.status, 409, "a settled match stays settled after restart");
    assert.equal((await call(h2, "GET", "/v3/ranked/queue", { player: m.a, nonce })).status, 401, "nonce replay still caught after restart");
});

test("leaderboard orders by rating and marks provisional players", async () => {
    const h = harness();
    for (let i = 0; i < 2; i++) {
        const m = await matched(h);
        const { transcript, seatOf } = await playThroughRelay(h, m, { results: { a: "a", b: "a" } });
        const rec = await h.svc.recordTranscript(transcript);
        await referee(h, "POST", "/v3/ranked/referee/verdicts", { roomId: m.roomId, transcriptHash: rec.transcriptHash, winnerSeat: seatOf.a });
    }
    const lb = (await call(h, "GET", "/v3/ranked/leaderboard?limit=10")).body;
    assert.equal(lb.rows.length, 4);
    assert.deepEqual(lb.rows.map((r) => r.rank), [1, 2, 3, 4]);
    assert.ok(lb.rows[0].rating >= lb.rows[3].rating);
    assert.ok(lb.rows.every((r) => r.provisional));
});

// ---- lab server + referee runner ---------------------------------------

test("lab server: /v3 over HTTP, relay transcript hook, referee runner rates the match", async () => {
    const srv = await start({ port: 0, refereeSecret: REF, relaySecret: RELAY });
    try {
        const base = `http://127.0.0.1:${srv.port}`;
        const h = {
            clock: { t: Date.now() },
            roomKv: srv.kv,
            svc: { fetch: (req) => fetch(base + new URL(req.url).pathname + new URL(req.url).search, { method: req.method, headers: req.headers, body: req.method === "GET" ? undefined : req.body, duplex: "half" }), recordTranscript: srv.ranked.recordTranscript },
        };
        const m = await matched(h);
        // The lab relay driver hands the transcript to the ranked service on session end.
        const raw = JSON.parse(await srv.kv.get(`v2:room:${m.roomId}`));
        let done;
        const recorded = new Promise((r) => { done = r; });
        const s = createRelaySession({
            room: raw, send: () => {}, broadcast: () => {},
            onTranscript: (t) => srv.ranked.recordTranscript(t).then(done),
        });
        for (const seat of [m.ma.seat, m.mb.seat]) s.handleMessage(seat, { type: "hello" });
        for (const seat of [m.ma.seat, m.mb.seat]) await s.handleMessage(seat, { type: "seed-commit", commit: commitOf(seat, "x") });
        for (const seat of [m.ma.seat, m.mb.seat]) await s.handleMessage(seat, { type: "seed-reveal", seed: seat, salt: "x" });
        s.handleMessage(m.ma.seat, { type: "intent", actionId: "pass" });
        for (const seat of [m.ma.seat, m.mb.seat]) s.handleMessage(seat, { type: "result", winnerSeat: m.mb.seat, finalHash: H("e") });
        assert.equal((await recorded).status, "pending");

        // Fake deterministic engine standing in for the rules core: B wins.
        const replay = async ({ seats, intents }) => ({ legal: true, winnerSeat: seats.includes(m.mb.seat) ? m.mb.seat : null, finalStateHash: H("e"), intentsReplayed: intents.length });
        const out = await runRefereeOnce({ base, secret: REF, replay });
        assert.equal(out.length, 1);
        assert.equal(out[0].status, 200);
        const lb = await (await fetch(`${base}/v3/ranked/leaderboard`)).json();
        assert.equal(lb.rows[0].playerId, m.b.playerId);
        assert.equal((await runRefereeOnce({ base, secret: REF, replay })).length, 0, "nothing left pending");
        assert.equal((await fetch(`${base}/v3/ranked/referee/pending`)).status, 401, "referee endpoints need the secret");
    } finally {
        await srv.close();
    }
});

// ---- relay additions (result claims, transcript) -----------------------

test("relay: disagreeing result claims end the match as disputed (flagged, unrated)", async () => {
    const h = harness();
    const m = await matched(h);
    const { transcript } = await playThroughRelay(h, m, { results: { a: "a", b: "b" } });
    assert.equal(transcript.endReason, "result-disputed");
    assert.equal((await h.svc.recordTranscript(transcript)).status, "flagged");
});

test("relay: one result claim ends the match after the grace period; transcript carries the log", async () => {
    const h = harness();
    const m = await matched(h);
    const raw = JSON.parse(await h.roomKv.get(`v2:room:${m.roomId}`));
    let t = null;
    const s = createRelaySession({ room: raw, now: () => h.clock.t, send: () => {}, broadcast: () => {}, onTranscript: (x) => { t = x; } });
    for (const seat of [m.ma.seat, m.mb.seat]) s.handleMessage(seat, { type: "hello" });
    for (const seat of [m.ma.seat, m.mb.seat]) await s.handleMessage(seat, { type: "seed-commit", commit: commitOf(seat, "x") });
    for (const seat of [m.ma.seat, m.mb.seat]) await s.handleMessage(seat, { type: "seed-reveal", seed: seat, salt: "x" });
    s.handleMessage(m.ma.seat, { type: "intent", actionId: "pass" });
    s.handleMessage(m.ma.seat, { type: "result", winnerSeat: m.ma.seat, finalHash: H("a") });
    s.tick();
    assert.equal(t, null, "still waiting for the other seat");
    h.clock.t += 61_000;
    s.tick();
    assert.equal(t.endReason, "result-single");
    assert.equal(t.intents.length, 1);
    assert.equal(t.seed.length, 64);
    assert.equal(t.results[m.ma.seat].winnerSeat, m.ma.seat);
    assert.equal((await h.svc.recordTranscript(t)).status, "pending", "a single claim still waits for the referee");
});
