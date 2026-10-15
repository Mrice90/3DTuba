/**
 * duel-engine-host.js — the reference engine served over `ic-engine-1`
 * (README.md). Tests use it to exercise jsonl-engine.js; it also shows the
 * exact reply shapes a C# host must produce.
 */

import { createInterface } from "node:readline";
import { createDuelEngine } from "./duel-engine.js";

const engine = createDuelEngine();
const out = (o) => process.stdout.write(JSON.stringify(o) + "\n");
const wire = (st) => ({ turn: st.turn, active: st.activeSeat, winner: st.winnerSeat, over: st.over });

createInterface({ input: process.stdin }).on("line", async (line) => {
    let req;
    try { req = JSON.parse(line); } catch { return out({ id: null, ok: false, error_code: "BAD_REQUEST", error: "not JSON" }); }
    const { id, op } = req;
    try {
        if (op === "new") return out({ id, ok: true, ...wire(await engine.start({ seed: req.seed })) });
        if (op === "legal") return out({ id, ok: true, legal: await engine.legal(req.player) });
        if (op === "act") {
            const r = await engine.act(req.player, req.action_id);
            return out(r.ok ? { id, ok: true } : { id, ok: false, error_code: "INVALID_ACTION", error: r.error });
        }
        if (op === "status") return out({ id, ok: true, ...wire(await engine.status()) });
        if (op === "hash") return out({ id, ok: true, state_hash: await engine.hash() });
        out({ id, ok: false, error_code: "BAD_REQUEST", error: `unknown op ${op}` });
    } catch (e) {
        out({ id, ok: false, error_code: "NO_MATCH", error: e.message });
    }
});
