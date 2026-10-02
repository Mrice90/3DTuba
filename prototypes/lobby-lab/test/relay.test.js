/**
 * Relay tests (AI-097).
 *
 * Two layers:
 *  1. createRelaySession unit tests — the pure state machine: seed
 *     commit-reveal, intent ordering, turn claims, hash exchange, timers,
 *     reconnect log, forfeits. No sockets.
 *  2. Integration tests — the REAL lab server on an ephemeral loopback
 *     port: WebSocket upgrade path, a full two-seat relay flow, and
 *     reconnect resume. Uses a minimal stdlib WS client (inline).
 *
 * Run from prototypes/lobby-lab:  npm test
 */

import test from "node:test";
import assert from "node:assert/strict";
import { createHash, randomBytes } from "node:crypto";
import net from "node:net";
import { start } from "../server.js";
import { createRelaySession } from "../relay.js";

const U = (c) => `${c}${c}${c}${c}${c}${c}${c}${c}-1111-4111-8111-${c}${c}${c}${c}${c}${c}${c}${c}${c}${c}${c}${c}`;
const SEAT_A = U("a");
const SEAT_B = U("b");
const SEAT_C = U("c");

const commitOf = (seed, salt) =>
    createHash("sha256").update(seed + "|" + salt).digest("hex");

/** Drive a session with in-memory outboxes. */
function harness(room = { roomId: "ROOM1", seatA: SEAT_A, seatB: SEAT_B }, opts = {}) {
    const clock = { t: 1_000_000 };
    const out = { [SEAT_A]: [], [SEAT_B]: [], [SEAT_C]: [] };
    const flags = [];
    let ended = false;
    const session = createRelaySession({
        room,
        now: () => clock.t,
        turnTimeoutMs: opts.turnTimeoutMs ?? 120_000,
        send: (seat, msg) => out[seat].push(msg),
        broadcast: (msg) => { out[SEAT_A].push(msg); out[SEAT_B].push(msg); },
        persist: opts.persist ?? (() => {}),
        onFlag: (reason, detail) => flags.push({ reason, detail }),
        onEnd: () => { ended = true; },
    });
    return { session, out, flags, clock, ended: () => ended };
}

function last(out, seat) { return out[seat][out[seat].length - 1]; }

/** Seed phase → play, the honest path. */
async function seedBoth(h) {
    const { session } = h;
    session.handleMessage(SEAT_A, JSON.stringify({ type: "hello", seat: SEAT_A }));
    session.handleMessage(SEAT_B, JSON.stringify({ type: "hello", seat: SEAT_B }));
    const sA = "seed-a", sB = "seed-b", saltA = "salt-a", saltB = "salt-b";
    await session.handleMessage(SEAT_A, JSON.stringify({ type: "seed-commit", commit: commitOf(sA, saltA) }));
    await session.handleMessage(SEAT_B, JSON.stringify({ type: "seed-commit", commit: commitOf(sB, saltB) }));
    await session.handleMessage(SEAT_A, JSON.stringify({ type: "seed-reveal", seed: sA, salt: saltA }));
    await session.handleMessage(SEAT_B, JSON.stringify({ type: "seed-reveal", seed: sB, salt: saltB }));
    return { sA, sB, saltA, saltB };
}

// --- session unit tests ---

test("hello before any other message; unknown seat ignored", () => {
    const h = harness();
    h.session.handleMessage(SEAT_A, JSON.stringify({ type: "intent", actionId: "x" }));
    assert.match(last(h.out, SEAT_A).type, /^error$/);
    assert.match(last(h.out, SEAT_A).reason, /hello first/);
    // Unknown seat: the driver never routes here; nothing is sent anywhere.
    const before = h.out[SEAT_A].length + h.out[SEAT_B].length;
    h.session.handleMessage(SEAT_C, JSON.stringify({ type: "hello" }));
    assert.equal(h.out[SEAT_A].length + h.out[SEAT_B].length, before);
});

test("seed commit-reveal produces a shared seed neither side picked", async () => {
    const h = harness();
    const { sA, sB } = await seedBoth(h);
    // The relay hashes seedA|seedB in lexicographic SEAT order.
    const seatOrdered = [SEAT_A, SEAT_B].sort();
    const seeds = { [SEAT_A]: sA, [SEAT_B]: sB };
    const want = createHash("sha256")
        .update(seeds[seatOrdered[0]] + "|" + seeds[seatOrdered[1]]).digest("hex");
    assert.notEqual(want, sA);
    assert.notEqual(want, sB);
    const seedMsg = h.out[SEAT_A].find((m) => m.type === "seed");
    assert.ok(seedMsg);
    assert.equal(seedMsg.seed, want);
    assert.equal(h.session.snapshot().phase, "play");
    assert.equal(h.session.snapshot().seedSet, true);
});

test("reveal that does not match its commit is rejected", async () => {
    const h = harness();
    const { session } = h;
    session.handleMessage(SEAT_A, JSON.stringify({ type: "hello", seat: SEAT_A }));
    await session.handleMessage(SEAT_A, JSON.stringify({ type: "seed-commit", commit: commitOf("s", "salt") }));
    await session.handleMessage(SEAT_A, JSON.stringify({ type: "seed-reveal", seed: "other", salt: "salt" }));
    assert.match(last(h.out, SEAT_A).reason, /does not match commit/);
    assert.equal(h.session.snapshot().phase, "seed");
});

test("intents are sequenced globally in arrival order and broadcast to both", async () => {
    const h = harness();
    await seedBoth(h);
    h.session.handleMessage(SEAT_A, JSON.stringify({ type: "intent", seat: SEAT_A, actionId: "r0-a1" }));
    h.session.handleMessage(SEAT_B, JSON.stringify({ type: "intent", seat: SEAT_B, actionId: "r0-a2" }));
    h.session.handleMessage(SEAT_A, JSON.stringify({ type: "intent", seat: SEAT_A, actionId: "r0-a3" }));
    const intentsA = h.out[SEAT_A].filter((m) => m.type === "intent");
    const intentsB = h.out[SEAT_B].filter((m) => m.type === "intent");
    assert.deepEqual(intentsA.map((m) => m.seq), [1, 2, 3]);
    assert.deepEqual(intentsB.map((m) => m.seq), [1, 2, 3]);
    assert.deepEqual(intentsA.map((m) => m.actionId), ["r0-a1", "r0-a2", "r0-a3"]);
    // The sender gets its own intent back — one code path for both seats.
    assert.equal(intentsA[0].seat, SEAT_A);
});

test("intent before the seed phase completes is rejected", () => {
    const h = harness();
    h.session.handleMessage(SEAT_A, JSON.stringify({ type: "hello", seat: SEAT_A }));
    h.session.handleMessage(SEAT_A, JSON.stringify({ type: "intent", actionId: "r0-a1" }));
    assert.match(last(h.out, SEAT_A).reason, /seed phase/);
});

test("malformed and unknown messages get errors, not crashes", async () => {
    const h = harness();
    await seedBoth(h);
    h.session.handleMessage(SEAT_A, "not json{");
    assert.equal(last(h.out, SEAT_A).type, "error");
    h.session.handleMessage(SEAT_A, JSON.stringify({ type: "teleport" }));
    assert.match(last(h.out, SEAT_A).reason, /unknown message type/);
    h.session.handleMessage(SEAT_A, JSON.stringify({ type: "intent" }));
    assert.match(last(h.out, SEAT_A).reason, /actionId is required/);
});

test("turn claim arms the timer; first claim per turn wins; hash requested for the completed turn", async () => {
    const h = harness();
    await seedBoth(h);
    h.session.handleMessage(SEAT_A, JSON.stringify({ type: "turn", turn: 1, activeSeat: SEAT_A }));
    // A competing claim for the same turn is ignored.
    h.session.handleMessage(SEAT_B, JSON.stringify({ type: "turn", turn: 1, activeSeat: SEAT_B }));
    assert.equal(h.session.snapshot().activeSeat, SEAT_A);
    // Turn 2 announced -> hash-request for turn 1 goes to both seats.
    h.session.handleMessage(SEAT_A, JSON.stringify({ type: "turn", turn: 2, activeSeat: SEAT_B }));
    const reqs = h.out[SEAT_A].filter((m) => m.type === "hash-request");
    assert.equal(reqs.length, 1);
    assert.equal(reqs[0].turn, 1);
});

test("matching hashes -> hash-ok; mismatched hashes -> flagged mismatch", async () => {
    const h = harness();
    await seedBoth(h);
    h.session.handleMessage(SEAT_A, JSON.stringify({ type: "turn", turn: 1, activeSeat: SEAT_A }));
    h.session.handleMessage(SEAT_A, JSON.stringify({ type: "turn", turn: 2, activeSeat: SEAT_B }));
    const good = "ab".repeat(32);
    h.session.handleMessage(SEAT_A, JSON.stringify({ type: "hash", turn: 1, hash: good }));
    h.session.handleMessage(SEAT_B, JSON.stringify({ type: "hash", turn: 1, hash: good }));
    const oks = h.out[SEAT_A].filter((m) => m.type === "hash-ok");
    assert.equal(oks[oks.length - 1].type, "hash-ok");
    assert.equal(h.flags.length, 0);

    h.session.handleMessage(SEAT_A, JSON.stringify({ type: "turn", turn: 3, activeSeat: SEAT_A }));
    h.session.handleMessage(SEAT_A, JSON.stringify({ type: "hash", turn: 2, hash: good }));
    h.session.handleMessage(SEAT_B, JSON.stringify({ type: "hash", turn: 2, hash: "cd".repeat(32) }));
    const mm = h.out[SEAT_B].filter((m) => m.type === "hash-mismatch");
    assert.equal(mm.length, 1);
    assert.equal(mm[0].turn, 2);
    assert.equal(h.flags.length, 1);
    assert.equal(h.flags[0].reason, "hash-mismatch");
});

test("turn timeout forfeits the active seat; disconnect alone does not forfeit", async () => {
    const h = harness({ roomId: "ROOM1", seatA: SEAT_A, seatB: SEAT_B }, { turnTimeoutMs: 60_000 });
    await seedBoth(h);
    h.session.handleMessage(SEAT_A, JSON.stringify({ type: "turn", turn: 1, activeSeat: SEAT_B }));
    // Seat B drops mid-turn: not a forfeit by itself.
    h.session.handleDisconnect(SEAT_B);
    h.clock.t += 30_000;
    h.session.tick();
    assert.equal(h.ended(), false);
    // Timer warnings fire.
    const timers = h.out[SEAT_A].filter((m) => m.type === "timer");
    assert.ok(timers.length >= 1);
    assert.equal(timers[0].seat, SEAT_B);
    // Past the deadline: forfeit.
    h.clock.t += 31_000;
    h.session.tick();
    assert.equal(h.ended(), true);
    const forfeit = h.out[SEAT_A].filter((m) => m.type === "forfeit");
    assert.equal(forfeit.length, 1);
    assert.equal(forfeit[0].seat, SEAT_B);
    assert.equal(forfeit[0].reason, "timeout");
});

test("an intent from the active seat resets the turn timer", async () => {
    const h = harness({ roomId: "ROOM1", seatA: SEAT_A, seatB: SEAT_B }, { turnTimeoutMs: 60_000 });
    await seedBoth(h);
    h.session.handleMessage(SEAT_A, JSON.stringify({ type: "turn", turn: 1, activeSeat: SEAT_A }));
    h.clock.t += 50_000;
    h.session.handleMessage(SEAT_A, JSON.stringify({ type: "intent", actionId: "r0-a1" }));
    h.clock.t += 50_000; // 100s after the turn started, 50s after the intent
    h.session.tick();
    assert.equal(h.ended(), false);
    h.clock.t += 11_000;
    h.session.tick();
    assert.equal(h.ended(), true);
});

test("seed phase timeout forfeits with no-reveal", () => {
    const h = harness();
    h.session.handleMessage(SEAT_A, JSON.stringify({ type: "hello", seat: SEAT_A }));
    h.clock.t += 120_001;
    h.session.tick();
    assert.equal(h.ended(), true);
    const forfeit = h.out[SEAT_A].filter((m) => m.type === "forfeit");
    assert.equal(forfeit[0].reason, "no-reveal");
});

test("reconnect with lastSeq replays only the missed intents", async () => {
    const h = harness();
    await seedBoth(h);
    for (let i = 1; i <= 3; i++) {
        h.session.handleMessage(SEAT_A, JSON.stringify({ type: "intent", actionId: `r0-a${i}` }));
    }
    // Seat B drops and reconnects having seen only seq 1.
    h.session.handleDisconnect(SEAT_B);
    h.out[SEAT_B].length = 0;
    h.session.handleMessage(SEAT_B, JSON.stringify({ type: "hello", seat: SEAT_B, lastSeq: 1 }));
    const welcome = last(h.out, SEAT_B);
    assert.equal(welcome.type, "welcome");
    assert.deepEqual(welcome.log.map((e) => e.seq), [2, 3]);
    assert.equal(welcome.phase, "play");
});

test("persist hook sees every intent in order", async () => {
    const persisted = [];
    const h = harness({ roomId: "ROOM1", seatA: SEAT_A, seatB: SEAT_B }, { persist: (e) => persisted.push(e) });
    await seedBoth(h);
    h.session.handleMessage(SEAT_B, JSON.stringify({ type: "intent", actionId: "r0-a9" }));
    h.session.handleMessage(SEAT_A, JSON.stringify({ type: "intent", actionId: "r0-a10" }));
    assert.deepEqual(persisted.map((e) => e.seq), [1, 2]);
});

// --- integration tests: real server, real WebSocket upgrades ---

/** Minimal stdlib WebSocket client for the integration tests. */
function wsConnect(port, path) {
    return new Promise((resolve, reject) => {
        const key = randomBytes(16).toString("base64");
        const socket = net.connect(port, "127.0.0.1", () => {
            socket.write(
                `GET ${path} HTTP/1.1\r\n` +
                `Host: 127.0.0.1:${port}\r\n` +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                `Sec-WebSocket-Key: ${key}\r\n` +
                "Sec-WebSocket-Version: 13\r\n" +
                "\r\n",
            );
        });
        const inbox = [];
        let buf = Buffer.alloc(0);
        let handshook = false;
        let closed = false;
        let settled = false;
        const waiters = [];

        const failConnect = (reason) => {
            if (settled) return;
            settled = true;
            reject(new Error("upgrade rejected: " + reason));
            try { socket.destroy(); } catch { /* noop */ }
        };

        function pump() {
            if (!handshook) {
                const idx = buf.indexOf("\r\n\r\n");
                if (idx < 0) return;
                const head = buf.subarray(0, idx).toString("latin1");
                buf = buf.subarray(idx + 4);
                if (!head.startsWith("HTTP/1.1 101")) {
                    failConnect(head.split("\r\n")[0]);
                    return;
                }
                handshook = true;
            }
            while (buf.length >= 2) {
                const b0 = buf[0], b1 = buf[1];
                const opcode = b0 & 0x0f;
                let len = b1 & 0x7f, off = 2;
                if (len === 126) { if (buf.length < 4) return; len = buf.readUInt16BE(2); off = 4; }
                else if (len === 127) { if (buf.length < 10) return; len = Number(buf.readBigUInt64BE(2)); off = 10; }
                if (buf.length < off + len) return;
                const payload = buf.subarray(off, off + len);
                buf = buf.subarray(off + len);
                if (opcode === 0x8) { closed = true; socket.end(); drain(); return; }
                if (opcode === 0x1) { inbox.push(JSON.parse(payload.toString("utf8"))); drain(); }
            }
        }
        function drain() {
            while (waiters.length > 0 && inbox.length > 0) waiters.shift()(inbox.shift());
        }
        socket.on("data", (c) => { buf = Buffer.concat([buf, c]); pump(); });
        socket.on("close", () => {
            closed = true;
            if (!handshook) failConnect("socket closed before handshake");
            while (waiters.length) waiters.shift()(null);
        });
        socket.on("error", (e) => { if (!handshook) failConnect(e.message); });
        // Resolve once the handshake completes (pump on first data).
        const check = () => {
            if (settled) return;
            if (handshook) { settled = true; resolve(api); }
            else if (closed) failConnect("socket closed before handshake");
            else setTimeout(check, 5);
        };
        setTimeout(check, 5);

        function sendFrame(obj) {
            const payload = Buffer.from(JSON.stringify(obj), "utf8");
            const mask = randomBytes(4);
            const masked = Buffer.alloc(payload.length);
            for (let i = 0; i < payload.length; i++) masked[i] = payload[i] ^ mask[i % 4];
            let header;
            if (payload.length < 126) header = Buffer.from([0x81, 0x80 | payload.length]);
            else { header = Buffer.alloc(4); header[0] = 0x81; header[1] = 0x80 | 126; header.writeUInt16BE(payload.length, 2); }
            socket.write(Buffer.concat([header, mask, masked]));
        }
        const api = {
            send: (obj) => sendFrame(obj),
            next: () => new Promise((res) => { drain(); inbox.length ? res(inbox.shift()) : waiters.push(res); }),
            close: () => socket.end(),
            get closed() { return closed; },
        };
    });
}

let ictx;
const iclock = { t: Date.now() };
const ibase = () => `http://127.0.0.1:${ictx.port}`;

test.before(async () => {
    ictx = await start({ port: 0, now: () => iclock.t });
});

test.after(async () => {
    await ictx.close();
});

async function iapi(path, opts = {}) {
    const res = await fetch(`${ibase()}${path}`, {
        headers: { "content-type": "application/json" },
        ...opts,
    });
    const json = await res.json().catch(() => ({}));
    return { status: res.status, json };
}
const ipost = (path, body) => iapi(path, { method: "POST", body: JSON.stringify(body) });

async function iPairRoom(uuidA, uuidB) {
    for (const [u, n] of [[uuidA, "A"], [uuidB, "B"]]) {
        const r = await ipost("/queue", { uuid: u, name: n, rating: 1000 });
        assert.equal(r.status, 200);
    }
    const r = await ipost("/v2/rooms/pair", { uuidA, uuidB, dataVersion: "lab-2" });
    assert.equal(r.status, 200);
    return r.json.roomId;
}

async function iSeedFlow(a, b, seatA, seatB) {
    a.send({ type: "hello", seat: seatA });
    b.send({ type: "hello", seat: seatB });
    assert.equal((await a.next()).type, "welcome");
    assert.equal((await b.next()).type, "welcome");
    const sA = "ws-seed-a", sB = "ws-seed-b";
    a.send({ type: "seed-commit", commit: commitOf(sA, "sa") });
    b.send({ type: "seed-commit", commit: commitOf(sB, "sb") });
    a.send({ type: "seed-reveal", seed: sA, salt: "sa" });
    b.send({ type: "seed-reveal", seed: sB, salt: "sb" });
    const seedA = await a.next();
    const seedB = await b.next();
    assert.equal(seedA.type, "seed");
    assert.equal(seedB.type, "seed");
    assert.equal(seedA.seed, seedB.seed);
}

test("upgrade requires dataVersion, a live room, and a seat", async () => {
    const roomId = await iPairRoom(U("d"), U("e"));
    await assert.rejects(wsConnect(ictx.port, `/rooms/${roomId}/ws?seat=${U("d")}`), /upgrade rejected/);
    await assert.rejects(
        wsConnect(ictx.port, `/rooms/${roomId}/ws?dataVersion=lab-2&seat=${U("f")}`),
        /upgrade rejected/,
    );
    await assert.rejects(
        wsConnect(ictx.port, `/rooms/NOPE/ws?dataVersion=lab-2&seat=${U("d")}`),
        /upgrade rejected/,
    );
});

test("full two-seat relay flow over real WebSockets", async () => {
    const uuidA = U("1"), uuidB = U("2");
    const roomId = await iPairRoom(uuidA, uuidB);
    const a = await wsConnect(ictx.port, `/rooms/${roomId}/ws?dataVersion=lab-2&seat=${uuidA}`);
    const b = await wsConnect(ictx.port, `/rooms/${roomId}/ws?dataVersion=lab-2&seat=${uuidB}`);
    await iSeedFlow(a, b, uuidA, uuidB);

    a.send({ type: "intent", actionId: "r3-a1" });
    const ia = await a.next();
    const ib = await b.next();
    assert.equal(ia.type, "intent");
    assert.equal(ia.seq, 1);
    assert.equal(ib.seq, 1);
    assert.equal(ib.actionId, "r3-a1");

    b.send({ type: "intent", actionId: "r3-a2" });
    assert.equal((await a.next()).seq, 2);
    assert.equal((await b.next()).seq, 2);

    // Turn + hash exchange.
    a.send({ type: "turn", turn: 1, activeSeat: uuidA });
    a.send({ type: "turn", turn: 2, activeSeat: uuidB });
    const hr = await a.next();
    assert.equal(hr.type, "hash-request");
    assert.equal(hr.turn, 1);
    await b.next(); // b's copy
    const hh = "ef".repeat(32);
    a.send({ type: "hash", turn: 1, hash: hh });
    b.send({ type: "hash", turn: 1, hash: hh });
    assert.equal((await a.next()).type, "hash-ok");
    assert.equal((await b.next()).type, "hash-ok");

    a.close(); b.close();
});

test("dropped socket resumes from the intent log", async () => {
    const uuidA = U("3"), uuidB = U("4");
    const roomId = await iPairRoom(uuidA, uuidB);
    let a = await wsConnect(ictx.port, `/rooms/${roomId}/ws?dataVersion=lab-2&seat=${uuidA}`);
    const b = await wsConnect(ictx.port, `/rooms/${roomId}/ws?dataVersion=lab-2&seat=${uuidB}`);
    await iSeedFlow(a, b, uuidA, uuidB);

    a.send({ type: "intent", actionId: "r5-a1" });
    await a.next(); await b.next();
    a.close();
    await new Promise((r) => setTimeout(r, 50));
    // Seat B keeps playing while A is gone.
    b.send({ type: "intent", actionId: "r5-a2" });
    await b.next();
    // A reconnects, having seen only seq 1.
    a = await wsConnect(ictx.port, `/rooms/${roomId}/ws?dataVersion=lab-2&seat=${uuidA}`);
    a.send({ type: "hello", seat: uuidA, lastSeq: 1 });
    const welcome = await a.next();
    assert.equal(welcome.type, "welcome");
    assert.deepEqual(welcome.log.map((e) => e.seq), [2]);
    assert.equal(welcome.log[0].actionId, "r5-a2");
    a.close(); b.close();
});
