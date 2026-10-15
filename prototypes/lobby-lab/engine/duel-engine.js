/**
 * duel-engine.js — tiny deterministic reference engine (NOT the game rules).
 *
 * It implements the engine interface in README.md so the lockstep client,
 * relay, ranked ledger and referee can be tested end to end without the
 * native C# core. Two players at 20 HP alternate turns; on a turn the active
 * player may "strike" once (2-5 damage, drawn from a seeded generator) and
 * must "end" the turn. Who starts and every strike value come from the seed,
 * so both clients and the referee compute identical states and hashes.
 */

import { createHash } from "node:crypto";
import { canonicalJson } from "../ranked.js";

export function createDuelEngine() {
    let s = null;
    const sha = (x) => createHash("sha256").update(x).digest("hex");
    const roll = () => {
        s.rng = sha(s.rng);
        return 2 + (parseInt(s.rng.slice(0, 8), 16) % 4);
    };
    const status = () => ({ turn: s.turn, activeSeat: s.over ? null : s.active, winnerSeat: s.winner, over: s.over });

    return {
        async start({ seed }) {
            if (!/^[0-9a-f]{64}$/.test(seed)) throw new Error("seed must be 64 hex");
            s = { seed, rng: seed, rev: 0, turn: 1, active: parseInt(seed.slice(0, 2), 16) % 2, hp: [20, 20], struck: false, winner: null, over: false };
            return status();
        },
        async legal(seat) {
            if (s.over || seat !== s.active) return [];
            const out = [];
            if (!s.struck) out.push({ id: `r${s.rev}-strike`, type: "strike" });
            out.push({ id: `r${s.rev}-end`, type: "end_turn" });
            return out;
        },
        async act(seat, actionId) {
            const legal = await this.legal(seat);
            const a = legal.find((x) => x.id === actionId);
            if (!a) return { ok: false, error: `illegal or stale action ${actionId} for seat ${seat} (revision ${s.rev})` };
            s.rev++;
            if (a.type === "strike") {
                s.struck = true;
                const foe = 1 - seat;
                s.hp[foe] = Math.max(0, s.hp[foe] - roll());
                if (s.hp[foe] === 0) { s.over = true; s.winner = seat; }
            } else {
                s.turn++;
                s.active = 1 - s.active;
                s.struck = false;
            }
            return { ok: true };
        },
        async status() { return status(); },
        async hash() { return sha(canonicalJson(s)); },
        async close() {},
    };
}
