/**
 * kv-file.js — file-backed KV for the ranked lab (AI-011 persistence).
 *
 * Same get/put/delete/list subset as kv.js, but every write is flushed to a
 * JSON file (write to a temp file, then rename, so a crash never leaves a
 * half-written store). A new process opened on the same file sees the same
 * players, ratings, matches and nonces — the restart test relies on this.
 * Production keeps this state in Durable Object storage instead.
 *
 * Node only (node:fs). Local development only.
 */

import { readFileSync, writeFileSync, renameSync, existsSync } from "node:fs";

export function createFileKv(file, { now = () => Date.now() } = {}) {
    const store = new Map(existsSync(file) ? Object.entries(JSON.parse(readFileSync(file, "utf8"))) : []);
    const isExpired = (e) => e.expiresAt !== null && e.expiresAt <= now();
    const flush = () => {
        writeFileSync(file + ".tmp", JSON.stringify(Object.fromEntries(store)));
        renameSync(file + ".tmp", file);
    };
    return {
        now,
        async get(key) {
            const e = store.get(key);
            if (!e) return null;
            if (isExpired(e)) { store.delete(key); flush(); return null; }
            return e.value;
        },
        async put(key, value, opts = {}) {
            const ttl = opts.expirationTtl;
            store.set(String(key), { value: String(value), expiresAt: Number.isFinite(ttl) && ttl > 0 ? now() + ttl * 1000 : null });
            flush();
        },
        async delete(key) {
            if (store.delete(key)) flush();
        },
        async list({ prefix = "", limit = 1000 } = {}) {
            const keys = [];
            for (const [key, e] of store) {
                if (!key.startsWith(prefix) || isExpired(e)) continue;
                keys.push({ name: key });
                if (keys.length >= limit) break;
            }
            return { keys, list_complete: true };
        },
    };
}
