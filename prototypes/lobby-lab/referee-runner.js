/**
 * referee-runner.js — ranked referee loop (AI-010).
 *
 * Polls the ranked service for finished, unflagged matches, replays each
 * transcript (seed + ordered intents) through a rules engine, and posts a
 * verdict bound to the transcript hash. The service rates a match only from
 * a verdict like this (or from a relay-observed forfeit).
 *
 * The engine is pluggable: `replay({seed, seats, intents})` must return
 * `{legal, winnerSeat, finalStateHash, offendingSeat?}`. The real adapter
 * runs the native C# core (or the Java rules bridge) headless on the
 * transcript; that adapter is the open next step, see docs/ranked-design.md.
 * Tests use a deterministic fake engine.
 *
 * Usage (lab): node referee-runner.js --base http://127.0.0.1:8787 --engine <module.js>
 *   or          node referee-runner.js --base URL --engine-cmd "<ic-engine-1 host command>"
 *   with LAB_REFEREE_SECRET set to the server's referee secret. --engine loads
 *   a module exporting replay(); --engine-cmd replays through any engine
 *   speaking ic-engine-1 (engine/README.md), such as the C# core host.
 */

import { fileURLToPath, pathToFileURL } from "node:url";
import { macHeaders } from "./ranked.js";

export async function runRefereeOnce({ base, secret, replay, fetchImpl = fetch, now = () => Date.now() }) {
    const path = "/v3/ranked/referee/pending";
    const res = await fetchImpl(base + path, { headers: await macHeaders({ secret, method: "GET", path, now: now() }) });
    if (!res.ok) throw new Error(`pending fetch failed: ${res.status}`);
    const { pending } = await res.json();
    const results = [];
    for (const p of pending) {
        let out;
        try {
            out = await replay({ seed: p.transcript.seed, seats: p.transcript.seats, intents: p.transcript.intents, transcript: p.transcript });
        } catch (e) {
            results.push({ roomId: p.roomId, error: `replay failed: ${e.message}` }); // left pending for a later run
            continue;
        }
        const vpath = "/v3/ranked/referee/verdicts";
        const body = JSON.stringify({ roomId: p.roomId, transcriptHash: p.transcriptHash, ...out });
        const r = await fetchImpl(base + vpath, {
            method: "POST",
            headers: { "content-type": "application/json", ...(await macHeaders({ secret, method: "POST", path: vpath, body, now: now() })) },
            body,
        });
        results.push({ roomId: p.roomId, status: r.status, body: await r.json() });
    }
    return results;
}

const isMain = process.argv[1] === fileURLToPath(import.meta.url);
if (isMain) {
    const arg = (n) => { const i = process.argv.indexOf(n); return i > 0 ? process.argv[i + 1] : undefined; };
    const base = arg("--base") || "http://127.0.0.1:8787";
    const engine = arg("--engine");
    const engineCmd = arg("--engine-cmd");
    const secret = process.env.LAB_REFEREE_SECRET;
    if ((!engine && !engineCmd) || !secret) {
        console.error("usage: LAB_REFEREE_SECRET=... node referee-runner.js --base URL (--engine ./adapter.js | --engine-cmd \"cmd args\")");
        process.exit(2);
    }
    let replay;
    if (engineCmd) {
        const { createJsonlEngine } = await import("./engine/jsonl-engine.js");
        const { replayWithEngine } = await import("./referee-engine.js");
        const [command, ...args] = engineCmd.split(" ");
        replay = replayWithEngine(() => createJsonlEngine({ command, args }), { engineName: command });
    } else {
        ({ replay } = await import(pathToFileURL(engine).href));
    }
    console.log(JSON.stringify(await runRefereeOnce({ base, secret, replay }), null, 2));
}
