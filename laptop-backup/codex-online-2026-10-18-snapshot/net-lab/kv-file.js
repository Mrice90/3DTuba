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
    let store = new Map(existsSync(file) ? Object.entries(JSON.parse(readFileSync(file, "utf8"))) : []);
    const isExpired = (e) => e.expiresAt !== null && e.expiresAt <= now();
    const flush = (next = store) => {
        writeFileSync(file + ".tmp", JSON.stringify(Object.fromEntries(next)), {flush:true});
        renameSync(file + ".tmp", file);
        store = next;
    };
    return {
        now,
        async batch(operations) {
            const next = new Map(store);
            for (const op of operations) {
                if (typeof op.key !== 'string' || (!op.delete && typeof op.value !== 'string')) throw Error('Invalid batch operation');
                if (op.delete) next.delete(op.key);
                else next.set(op.key, {value:op.value,expiresAt:null});
            }
            flush(next);
        },
        async get(key) {
            const e = store.get(key);
            if (!e) return null;
            if (isExpired(e)) { store.delete(key); flush(); return null; }
            return e.value;
        },
        async put(key, value, opts = {}) {
            const ttl = opts.expirationTtl;
            const next = new Map(store);
            next.set(String(key), { value: String(value), expiresAt: Number.isFinite(ttl) && ttl > 0 ? now() + ttl * 1000 : null });
            flush(next);
        },
        async delete(key) {
            const next = new Map(store);
            if (next.delete(key)) flush(next);
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
