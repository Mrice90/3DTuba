/**
 * e2e-wrangler.mjs — run the ranked beta Worker in the real Cloudflare
 * runtime locally (`wrangler dev`, workerd) and play one full online match
 * between two headless clients in separate processes, then referee and
 * check the leaderboard. Nothing is deployed and no account is needed.
 *
 * Needs wrangler: `npm i -D wrangler` somewhere, then from this folder:
 *   WRANGLER=/path/to/node_modules/.bin/wrangler node e2e-wrangler.mjs
 */

import { spawn } from "node:child_process";
import { writeFileSync, rmSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";
import { runRefereeOnce } from "../../referee-runner.js";
import { replayWithEngine } from "../../referee-engine.js";
import { createDuelEngine } from "../../engine/duel-engine.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const lab = path.join(here, "..", "..");
const port = Number(process.env.PORT || 8799);
const base = `http://127.0.0.1:${port}`;
const REF = "e2e-referee-secret";
const persist = path.join(here, ".wrangler-e2e-state");
rmSync(persist, { recursive: true, force: true });
writeFileSync(path.join(here, ".dev.vars"), `LAB_REFEREE_SECRET=${REF}\nLAB_RELAY_SECRET=e2e-relay-secret\nLAB_V2_SECRET=e2e-v2-secret\n`);

const dev = spawn(process.env.WRANGLER || "wrangler", ["dev", "--port", String(port), "--ip", "127.0.0.1", "--persist-to", persist], { cwd: here, stdio: ["ignore", "pipe", "pipe"] });
let devLog = "";
dev.stdout.on("data", (d) => { devLog += d; });
dev.stderr.on("data", (d) => { devLog += d; });
const fail = (msg) => { console.error(`FAIL: ${msg}\n--- wrangler log ---\n${devLog.slice(-4000)}`); dev.kill(); process.exit(1); };

for (let i = 0; ; i++) {
    try { if ((await fetch(`${base}/v3/ranked/leaderboard`)).ok) break; } catch { /* not up yet */ }
    if (i > 120) fail("wrangler dev did not start");
    await new Promise((r) => setTimeout(r, 500));
}

const client = (name, faction) => new Promise((resolve) => {
    const c = spawn(process.execPath, [path.join(lab, "lockstep-client.js"), "--base", base, "--name", name, "--faction", faction, "--timeout-ms", "60000"], { stdio: ["ignore", "pipe", "inherit"] });
    let out = "";
    c.stdout.on("data", (d) => { out += d; });
    c.on("exit", (code) => resolve({ code, r: JSON.parse(out.trim().split("\n").pop() || "{}") }));
});

const [a, b] = await Promise.all([client("Alice", "zeus"), client("Bobby", "poseidon")]);
for (const c of [a, b]) if (c.code !== 0) fail(`client ${c.r.name || "?"}: ${JSON.stringify(c.r)}`);
if (a.r.finalHash !== b.r.finalHash) fail("clients ended on different states");

// The MatchRoom hands the transcript to the RankedLedger asynchronously.
let out = [];
for (let i = 0; i < 20 && out.length === 0; i++) {
    out = await runRefereeOnce({ base, secret: REF, replay: replayWithEngine(createDuelEngine, { engineName: "duel" }) });
    if (out.length === 0) await new Promise((r) => setTimeout(r, 250));
}
if (out.length !== 1 || out[0].status !== 200) fail(`referee: ${JSON.stringify(out)}`);
const lb = await (await fetch(`${base}/v3/ranked/leaderboard`)).json();
const winner = a.r.winner === "me" ? a.r : b.r;
if (lb.rows[0]?.playerId !== winner.playerId) fail(`leaderboard: ${JSON.stringify(lb)}`);

console.log(JSON.stringify({
    ok: true, runtime: "wrangler dev (workerd)", roomId: a.r.roomId, turns: a.r.turns,
    intents: a.r.intentsApplied, hashChecksPassed: a.r.hashOk, winner: winner.name, leaderboard: lb.rows,
}, null, 2));
dev.kill();
rmSync(persist, { recursive: true, force: true });
rmSync(path.join(here, ".dev.vars"), { force: true });
process.exit(0);
