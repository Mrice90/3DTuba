/**
 * lockstep-client.js — headless online client for Infinite Conquest
 * (AI-097 two-client test, AI-010 ranked).
 *
 * Plays one full ranked match the way a real client must: registers a signed
 * player key, queues for ranked, joins the relay room with its seat, runs the
 * seed commit-reveal, then applies every relay intent to its own rules engine
 * in relay order, sends its own moves as intents, claims turns, answers the
 * relay's per-turn hash checks and reports the result at game over.
 *
 * Two of these in separate processes stand in for two PCs. The engine is
 * pluggable (engine/README.md); `--engine duel` uses the reference engine,
 * `--engine-cmd "<command>"` any `ic-engine-1` host such as the C# core.
 *
 * CLI: node lockstep-client.js --base http://127.0.0.1:8787 --name Alice
 *        --faction zeus [--engine duel | --engine-cmd "dotnet IC.Referee.dll"]
 * Prints one JSON line with the outcome; exit code 0 when the match ended
 * cleanly (game over, every hash check matched).
 */

import { fileURLToPath } from "node:url";
import { createHash, randomBytes } from "node:crypto";
import { b64url, playerIdFor, signRequest } from "./ranked.js";

const sha = (s) => createHash("sha256").update(s).digest("hex");
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

/** Default move policy: a seeded random legal action, ending the turn last. */
export function randomPolicy(seedText) {
    let x = sha(seedText);
    return (legal) => {
        const acts = legal.filter((a) => a.type !== "end_turn");
        x = sha(x);
        if (acts.length === 0 || parseInt(x.slice(0, 2), 16) < 40) return legal.find((a) => a.type === "end_turn") || legal[0];
        return acts[parseInt(x.slice(2, 10), 16) % acts.length];
    };
}

export async function playRankedMatch({ base, name, faction = "zeus", makeEngine, policy, log = () => {}, timeoutMs = 120_000, maxIntents = 5_000 }) {
    const { publicKey, privateKey } = await crypto.subtle.generateKey({ name: "Ed25519" }, true, ["sign", "verify"]);
    const pub = new Uint8Array(await crypto.subtle.exportKey("raw", publicKey));
    const playerId = await playerIdFor(pub);
    const signed = async (method, path, data) => {
        const body = data === undefined ? "" : JSON.stringify(data);
        const headers = await signRequest({ privateKey, playerId, method, path, body });
        const res = await fetch(base + path, { method, headers: { ...headers, ...(body ? { "content-type": "application/json" } : {}) }, body: body || undefined });
        const out = await res.json();
        if (!res.ok) throw new Error(`${method} ${path} -> ${res.status} ${out.error}`);
        return out;
    };

    await signed("POST", "/v3/players", { publicKey: b64url(pub), displayName: name });
    let m = await signed("POST", "/v3/ranked/queue", { faction });
    const queueUntil = Date.now() + timeoutMs;
    while (m.status !== "matched") {
        if (Date.now() > queueUntil) throw new Error("no ranked match found in time");
        await sleep(250);
        m = await signed("GET", "/v3/ranked/queue");
    }
    log(`matched in room ${m.roomId} as side ${m.side} vs ${m.opponent.displayName}`);

    const me = m.side === "A" ? 0 : 1;
    const seat = m.seat;
    let oppSeat = null; // learned from the opponent's first intent; never handed out by the server
    const engine = await makeEngine();
    const choose = policy || randomPolicy(`${name}|${m.roomId}`);
    const turnHashes = new Map(); // turn -> state hash at the moment that turn ended
    const stats = { intentsSent: 0, intentsApplied: 0, hashOk: 0, hashMismatch: 0, errors: [] };

    const wsUrl = `${base.replace(/^http/, "ws")}/rooms/${m.roomId}/ws?seat=${seat}&dataVersion=${m.dataVersion}`;
    const ws = new WebSocket(wsUrl);
    const send = (o) => ws.send(JSON.stringify(o));
    let status = null;
    let inFlight = false;
    let resultSent = false;

    async function afterStateChange(prevTurn) {
        status = await engine.status();
        if (status.turn !== prevTurn) {
            turnHashes.set(prevTurn, await engine.hash());
            // The seat whose turn starts claims it (it is the only seat it can name).
            if (!status.over && status.activeSeat === me) send({ type: "turn", turn: status.turn, activeSeat: seat });
        }
        if (status.over) {
            if (!resultSent) {
                resultSent = true;
                const winnerSeat = status.winnerSeat === me ? seat : oppSeat;
                if (winnerSeat) send({ type: "result", winnerSeat, finalHash: await engine.hash() });
            }
            return;
        }
        if (status.activeSeat === me && !inFlight) {
            const legal = await engine.legal(me);
            if (legal.length === 0) throw new Error("engine offers no legal action on our turn");
            if (stats.intentsSent >= maxIntents) throw new Error("intent cap reached");
            inFlight = true;
            stats.intentsSent++;
            send({ type: "intent", seat, actionId: choose(legal, status).id });
        }
    }

    // Messages are handled strictly in arrival order (the relay's order).
    let queue = Promise.resolve();
    const outcome = new Promise((resolve, reject) => {
        const timer = setTimeout(() => reject(new Error("match did not finish in time")), timeoutMs);
        const finish = (v) => { clearTimeout(timer); resolve(v); };
        ws.addEventListener("open", async () => {
            send({ type: "hello", seat, dataVersion: m.dataVersion });
            const mySeed = randomBytes(32).toString("hex");
            const salt = randomBytes(16).toString("hex");
            send({ type: "seed-commit", seat, commit: sha(`${mySeed}|${salt}`) });
            send({ type: "seed-reveal", seat, seed: mySeed, salt });
        });
        ws.addEventListener("error", () => { clearTimeout(timer); reject(new Error("relay socket error")); });
        ws.addEventListener("close", () => finish({ closed: true }));
        ws.addEventListener("message", (ev) => {
            queue = queue.then(async () => {
                const msg = JSON.parse(ev.data);
                switch (msg.type) {
                    case "seed": {
                        status = await engine.start({ seed: msg.seed });
                        log(`seed ${msg.seed.slice(0, 12)}..., player ${status.activeSeat} starts`);
                        if (status.activeSeat === me) send({ type: "turn", turn: status.turn, activeSeat: seat });
                        await afterStateChange(status.turn);
                        break;
                    }
                    case "intent": {
                        const player = msg.seat === seat ? me : 1 - me;
                        if (msg.seat !== seat) oppSeat = msg.seat;
                        const prevTurn = (await engine.status()).turn;
                        const r = await engine.act(player, msg.actionId);
                        if (!r.ok) {
                            stats.errors.push(`relay intent ${msg.seq} rejected locally: ${r.error}`);
                            // A real client stops here and shows "match desynced"; the hash check flags it.
                        } else stats.intentsApplied++;
                        if (msg.seat === seat) inFlight = false;
                        await afterStateChange(prevTurn);
                        break;
                    }
                    case "hash-request": {
                        const h = turnHashes.get(msg.turn);
                        if (h) send({ type: "hash", seat, turn: msg.turn, hash: h });
                        else stats.errors.push(`no local hash for turn ${msg.turn}`);
                        break;
                    }
                    case "hash-ok": stats.hashOk++; break;
                    case "hash-mismatch": stats.hashMismatch++; break;
                    case "error": stats.errors.push(`relay: ${msg.reason}`); break;
                    case "bye":
                    case "forfeit":
                        ws.close();
                        finish({ endReason: msg.reason, forfeitSeat: msg.seat || null });
                        break;
                    default: break;
                }
            }).catch((e) => { stats.errors.push(e.message); ws.close(); });
        });
    });
    try {
        const end = await outcome;
        const st = status || {};
        return {
            name, playerId, roomId: m.roomId, side: m.side, seat,
            winner: st.over ? (st.winnerSeat === me ? "me" : "opponent") : null,
            turns: st.turn, finalHash: st.over ? await engine.hash() : null,
            ...end, ...stats,
        };
    } finally {
        await engine.close();
    }
}

const isMain = process.argv[1] === fileURLToPath(import.meta.url);
if (isMain) {
    const arg = (n, d) => { const i = process.argv.indexOf(n); return i > 0 ? process.argv[i + 1] : d; };
    const base = arg("--base", "http://127.0.0.1:8787");
    const cmd = arg("--engine-cmd");
    let makeEngine;
    if (cmd) {
        const { createJsonlEngine } = await import("./engine/jsonl-engine.js");
        const [command, ...args] = cmd.split(" ");
        makeEngine = () => createJsonlEngine({ command, args });
    } else {
        const { createDuelEngine } = await import("./engine/duel-engine.js");
        makeEngine = () => createDuelEngine();
    }
    try {
        const out = await playRankedMatch({
            base, name: arg("--name", "Player"), faction: arg("--faction", "zeus"), makeEngine,
            timeoutMs: Number(arg("--timeout-ms", "120000")),
            log: (s) => process.stderr.write(`[${arg("--name", "Player")}] ${s}\n`),
        });
        console.log(JSON.stringify(out));
        process.exit(out.endReason === "game-over" && out.hashMismatch === 0 && out.errors.length === 0 ? 0 : 1);
    } catch (e) {
        console.log(JSON.stringify({ error: e.message }));
        process.exit(2);
    }
}
