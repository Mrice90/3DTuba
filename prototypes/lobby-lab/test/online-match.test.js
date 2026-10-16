/**
 * Online match tests (AI-097 two-client, AI-010 referee).
 *
 * Two headless lockstep clients run as separate OS processes (standing in
 * for two PCs) against the lab server over real HTTP and WebSockets: signed
 * ranked queue, relay seed commit-reveal, intents, per-turn hash checks and
 * result claims. The referee then replays the relay transcript through the
 * rules engine (in-process and over the ic-engine-1 subprocess protocol) and
 * the match is rated exactly once.
 *
 * The engine here is engine/duel-engine.js, a tiny reference engine; the
 * native C# core plugs in through the same ic-engine-1 protocol.
 *
 * Run from prototypes/lobby-lab:  npm test
 */

import test from "node:test";
import assert from "node:assert/strict";
import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";
import path from "node:path";
import { start } from "../server.js";
import { runRefereeOnce } from "../referee-runner.js";
import { replayWithEngine } from "../referee-engine.js";
import { createDuelEngine } from "../engine/duel-engine.js";
import { createJsonlEngine } from "../engine/jsonl-engine.js";
import { macHeaders } from "../ranked.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const lab = path.join(here, "..");
const REF = "referee-secret-online";
const RELAY = "relay-secret-online";
const duelHost = () => createJsonlEngine({ command: process.execPath, args: [path.join(lab, "engine", "duel-engine-host.js")] });

function client(base, name, faction, extra = []) {
    return new Promise((resolve) => {
        const c = spawn(process.execPath, [path.join(lab, "lockstep-client.js"), "--base", base, "--name", name, "--faction", faction, "--timeout-ms", "30000", ...extra], { stdio: ["ignore", "pipe", "pipe"] });
        let out = "";
        let err = "";
        c.stdout.on("data", (d) => { out += d; });
        c.stderr.on("data", (d) => { err += d; });
        c.on("exit", (code) => resolve({ code, result: JSON.parse(out.trim().split("\n").pop() || "{}"), log: err }));
    });
}

async function pendingTranscripts(base) {
    const p = "/v3/ranked/referee/pending";
    return (await (await fetch(base + p, { headers: await macHeaders({ secret: REF, method: "GET", path: p }) })).json()).pending;
}

test("two clients in separate processes finish a ranked match in lockstep; the referee replay rates it once", async () => {
    const srv = await start({ port: 0, refereeSecret: REF, relaySecret: RELAY });
    try {
        const base = `http://127.0.0.1:${srv.port}`;
        const [a, b] = await Promise.all([client(base, "Alice", "zeus"), client(base, "Bobby", "poseidon")]);
        for (const c of [a, b]) {
            assert.equal(c.code, 0, `${c.result.name} failed: ${JSON.stringify(c.result)}\n${c.log}`);
            assert.equal(c.result.endReason, "game-over");
            assert.equal(c.result.hashMismatch, 0);
            assert.ok(c.result.hashOk >= 2, "every finished turn was hash-checked");
            assert.deepEqual(c.result.errors, []);
        }
        assert.equal(a.result.roomId, b.result.roomId);
        assert.equal(a.result.finalHash, b.result.finalHash, "both clients reached the same final state");
        assert.notEqual(a.result.winner, b.result.winner);
        assert.equal(a.result.intentsApplied, b.result.intentsApplied);

        // The relay handed the transcript to the ledger; it waits for the referee.
        await new Promise((r) => setTimeout(r, 50));
        const pending = await pendingTranscripts(base);
        assert.equal(pending.length, 1);
        const t = pending[0].transcript;
        assert.equal(t.endReason, "game-over");
        assert.equal(t.intents.length, a.result.intentsApplied);

        // The in-process engine and the ic-engine-1 subprocess agree on the replay.
        const viaHost = await replayWithEngine(duelHost, { engineName: "duel-host" })({ ...t, transcript: t });
        const viaLocal = await replayWithEngine(createDuelEngine)({ ...t, transcript: t });
        assert.equal(viaHost.finalStateHash, viaLocal.finalStateHash);
        assert.equal(viaHost.finalStateHash, a.result.finalHash, "the referee reproduces the clients' final state");

        const out = await runRefereeOnce({ base, secret: REF, replay: replayWithEngine(duelHost, { engineName: "duel-host" }) });
        assert.equal(out.length, 1);
        assert.equal(out[0].status, 200, JSON.stringify(out[0]));
        const winner = a.result.winner === "me" ? a.result : b.result;
        const lb = await (await fetch(`${base}/v3/ranked/leaderboard`)).json();
        assert.equal(lb.rows[0].playerId, winner.playerId);
        assert.equal(lb.rows[0].games, 1);
        const match = await (await fetch(`${base}/v3/ranked/matches/${a.result.roomId}`)).json();
        assert.equal(match.status, "rated");
        assert.equal(match.verdict.finalStateHash, a.result.finalHash);
        assert.equal((await runRefereeOnce({ base, secret: REF, replay: replayWithEngine(createDuelEngine) })).length, 0, "rated exactly once");
    } finally {
        await srv.close();
    }
});

// ---- referee replay rules (no network) ---------------------------------

const SEED = "a".repeat(64);
const A = "11111111-1111-4111-8111-111111111111";
const B = "22222222-2222-4222-8222-222222222222";

/** Play the duel engine with a fixed policy and return the intents it produced. */
async function scriptedIntents(stopAfter = Infinity) {
    const e = createDuelEngine();
    let st = await e.start({ seed: SEED });
    const intents = [];
    while (!st.over && intents.length < stopAfter) {
        const legal = await e.legal(st.activeSeat);
        const a = legal.find((x) => x.type === "strike") || legal[0];
        await e.act(st.activeSeat, a.id);
        intents.push({ seq: intents.length + 1, seat: st.activeSeat === 0 ? A : B, actionId: a.id });
        st = await e.status();
    }
    return { intents, winner: st.winnerSeat };
}

test("referee: a finished game names the engine's winner", async () => {
    const { intents, winner } = await scriptedIntents();
    const v = await replayWithEngine(createDuelEngine)({ seed: SEED, seats: [A, B], intents });
    assert.equal(v.legal, true);
    assert.equal(v.winnerSeat, [A, B][winner]);
    assert.match(v.finalStateHash, /^[0-9a-f]{64}$/);
});

test("referee: a fabricated or out-of-turn intent loses the match for the seat that sent it", async () => {
    const { intents } = await scriptedIntents(6);
    const forged = [...intents, { seq: 7, seat: intents[5].seat, actionId: "r999-strike" }];
    const v1 = await replayWithEngine(createDuelEngine)({ seed: SEED, seats: [A, B], intents: forged });
    assert.equal(v1.legal, false);
    assert.equal(v1.offendingSeat, intents[5].seat);
    assert.equal(v1.failedSeq, 7);
    // The other seat acting during someone else's turn.
    const other = intents[0].seat === A ? B : A;
    const v2 = await replayWithEngine(duelHost)({ seed: SEED, seats: [A, B], intents: [{ seq: 1, seat: other, actionId: intents[0].actionId }] });
    assert.equal(v2.legal, false);
    assert.equal(v2.offendingSeat, other);
});

test("referee: an unfinished game is a concession only when a seat conceded, otherwise void", async () => {
    const { intents } = await scriptedIntents(4);
    const replay = replayWithEngine(createDuelEngine);
    const conceded = await replay({ seed: SEED, seats: [A, B], intents, transcript: { results: { [A]: { winnerSeat: B } } } });
    assert.equal(conceded.winnerSeat, B);
    assert.equal(conceded.concededBy, A);
    const timedOut = await replay({ seed: SEED, seats: [A, B], intents, transcript: { endReason: "timeout", forfeitSeat: B } });
    assert.equal(timedOut.winnerSeat, A, "a seat timed out mid-game loses");
    const { intents: full, winner } = await scriptedIntents();
    const afterOver = await replay({ seed: SEED, seats: [A, B], intents: full, transcript: { endReason: "timeout", forfeitSeat: [A, B][winner] } });
    assert.equal(afterOver.winnerSeat, [A, B][winner], "a timeout after game over does not take the win away");
    const claimedOnly = await replay({ seed: SEED, seats: [A, B], intents, transcript: { results: { [A]: { winnerSeat: A } } } });
    assert.equal(claimedOnly.unfinished, true, "claiming your own win proves nothing");
    assert.equal(claimedOnly.winnerSeat, undefined);
});

test("ledger: an unfinished verdict voids the match, never rates it, exactly once", async () => {
    const srv = await start({ port: 0, refereeSecret: REF, relaySecret: RELAY });
    try {
        const base = `http://127.0.0.1:${srv.port}`;
        // Two clients play; a referee that cannot see a finish posts `unfinished`.
        await Promise.all([client(base, "Carol", "zeus"), client(base, "David", "poseidon")]);
        await new Promise((r) => setTimeout(r, 50));
        const voidReplay = async () => ({ legal: true, unfinished: true, finalStateHash: "0".repeat(64), engine: "test" });
        const out = await runRefereeOnce({ base, secret: REF, replay: voidReplay });
        assert.equal(out[0].body.status, "void");
        const lb = await (await fetch(`${base}/v3/ranked/leaderboard`)).json();
        assert.equal(lb.rows.length, 0, "a void match moves no ratings");
        // Re-posting the same verdict is a no-op; a conflicting one is refused.
        const rec = await (await fetch(`${base}/v3/ranked/matches/${out[0].roomId}`)).json();
        const p = "/v3/ranked/referee/verdicts";
        const post = async (data) => {
            const body = JSON.stringify({ roomId: out[0].roomId, transcriptHash: rec.transcriptHash, ...data });
            const r = await fetch(base + p, { method: "POST", headers: { "content-type": "application/json", ...(await macHeaders({ secret: REF, method: "POST", path: p, body })) }, body });
            return { status: r.status, body: await r.json() };
        };
        assert.equal((await post({ legal: true, unfinished: true })).body.duplicate, true);
        assert.equal((await post({ legal: true, winnerSeat: "x" })).status, 409);
    } finally {
        await srv.close();
    }
});

// ---- the real rules: native C# core through tools/referee-host ----------
// Runs when the host is built (tools/referee-host: dotnet build -c Release -o out,
// which uses the pinned 0.4.5 rules build); skipped otherwise.

import { existsSync } from "node:fs";

const hostDir = path.join(lab, "..", "..", "tools", "referee-host");
const hostDll = path.join(hostDir, "out", "IC.RefereeHost.dll");
const nativeData = path.join(hostDir, "rules", "0.4.5", "data");
const haveNative = existsSync(hostDll) && existsSync(nativeData);
const nativeCmd = `dotnet ${hostDll} ${nativeData}`;
const nativeHost = () => createJsonlEngine({ command: "dotnet", args: [hostDll, nativeData] });

test("native rules: two clients play a full real match in lockstep; the native referee reproduces it and rates it", { skip: !haveNative && "native referee host not built", timeout: 300_000 }, async () => {
    const srv = await start({ port: 0, refereeSecret: REF, relaySecret: RELAY });
    try {
        const base = `http://127.0.0.1:${srv.port}`;
        const extra = ["--engine-cmd", nativeCmd, "--timeout-ms", "240000"];
        const [a, b] = await Promise.all([client(base, "Alice", "zeus", extra), client(base, "Bobby", "poseidon", extra)]);
        for (const c of [a, b]) {
            assert.equal(c.code, 0, `${c.result.name} failed: ${JSON.stringify(c.result)}\n${c.log}`);
            assert.equal(c.result.hashMismatch, 0);
            assert.ok(c.result.hashOk >= 2);
        }
        assert.equal(a.result.finalHash, b.result.finalHash);
        await new Promise((r) => setTimeout(r, 50));
        const [p] = await pendingTranscripts(base);
        const v = await replayWithEngine(nativeHost, { engineName: "native" })({ ...p.transcript, transcript: p.transcript });
        assert.equal(v.legal, true);
        assert.equal(v.rulesVersion, "purchase-v3-hex37", "the referee runs the shipped 0.4.5 rules");
        assert.ok(Number.isInteger(v.finalRevision));
        assert.equal(v.finalStateHash, a.result.finalHash, "the referee reaches the clients' final state");
        assert.equal(v.winnerSeat, a.result.winner === "me" ? a.result.seat : b.result.seat);

        // A forged intent spliced into the real transcript loses the match for its sender.
        const k = Math.floor(p.transcript.intents.length / 2);
        const forged = p.transcript.intents.map((it, i) => (i === k ? { ...it, actionId: "r0-a999" } : it));
        const cheat = await replayWithEngine(nativeHost)({ ...p.transcript, intents: forged });
        assert.equal(cheat.legal, false);
        assert.equal(cheat.offendingSeat, p.transcript.intents[k].seat);

        const out = await runRefereeOnce({ base, secret: REF, replay: replayWithEngine(nativeHost, { engineName: "native" }) });
        assert.equal(out[0].status, 200);
        const lb = await (await fetch(`${base}/v3/ranked/leaderboard`)).json();
        assert.equal(lb.rows[0].playerId, (a.result.winner === "me" ? a.result : b.result).playerId);
    } finally {
        await srv.close();
    }
});
