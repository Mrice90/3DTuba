/**
 * jsonl-engine.js — drive an `ic-engine-1` subprocess (README.md) through the
 * engine interface. This is how the referee and test clients run the native
 * C# core: `createJsonlEngine({command: "dotnet", args: ["IC.Referee.dll"]})`.
 */

import { spawn } from "node:child_process";
import { createInterface } from "node:readline";

export function createJsonlEngine({ command, args = [], cwd, timeoutMs = 30_000 }) {
    const child = spawn(command, args, { cwd, stdio: ["pipe", "pipe", "inherit"] });
    const waiting = new Map();
    let nextId = 1;
    let dead = null;
    createInterface({ input: child.stdout }).on("line", (line) => {
        let msg;
        try { msg = JSON.parse(line); } catch { return; }
        const w = waiting.get(msg.id);
        if (w) { waiting.delete(msg.id); clearTimeout(w.timer); w.resolve(msg); }
    });
    child.on("exit", (code) => {
        dead = new Error(`engine exited (${code})`);
        for (const w of waiting.values()) { clearTimeout(w.timer); w.reject(dead); }
        waiting.clear();
    });
    child.on("error", (e) => { dead = e; });

    const call = (op, fields = {}) => new Promise((resolve, reject) => {
        if (dead) return reject(dead);
        const id = nextId++;
        const timer = setTimeout(() => { waiting.delete(id); reject(new Error(`engine ${op} timed out`)); }, timeoutMs);
        waiting.set(id, { resolve, reject, timer });
        child.stdin.write(JSON.stringify({ id, op, ...fields }) + "\n");
    });
    const must = async (op, fields) => {
        const r = await call(op, fields);
        if (!r.ok) throw new Error(`${op}: ${r.error_code || "ERROR"} ${r.error || ""}`.trim());
        return r;
    };
    const st = (r) => ({
        turn: r.turn, activeSeat: r.over ? null : r.active, winnerSeat: r.winner ?? null, over: !!r.over,
        ...(r.revision != null ? { revision: r.revision } : {}),
        ...(r.rules_version ? { rulesVersion: r.rules_version } : {}),
    });

    return {
        async start({ seed }) { return st(await must("new", { seed, players: 2 })); },
        async legal(seat) { return (await must("legal", { player: seat })).legal; },
        async act(seat, actionId) {
            const r = await call("act", { player: seat, action_id: actionId });
            return r.ok ? { ok: true } : { ok: false, error: r.error || r.error_code };
        },
        async status() { return st(await must("status")); },
        async hash() { return (await must("hash")).state_hash; },
        async close() { child.stdin.end(); },
    };
}
