/**
 * do-storage-kv.js — the KV subset ranked.js uses (get / put with
 * expirationTtl / delete / list({prefix})) on top of Durable Object storage.
 *
 * Why: Workers KV is eventually consistent, so two near-simultaneous writes
 * can both win. Durable Object storage is strongly consistent and private to
 * one object, which makes the RankedLedger the single serialized writer that
 * docs/ranked-design.md asks for. Works on the SQLite-backed storage the
 * Workers Free plan offers (its key-value API is the same).
 *
 * TTL is emulated: each value is stored as {v, exp}; reads ignore and lazily
 * delete expired entries, and sweepExpired() (run from the ledger's alarm)
 * clears ones nothing reads again, such as used nonces.
 */

export function createDoStorageKv(storage, { now = () => Date.now() } = {}) {
    const live = (e) => e && (e.exp == null || e.exp > now());
    return {
        now,
        async get(key) {
            const e = await storage.get(key);
            if (e == null) return null;
            if (!live(e)) { await storage.delete(key); return null; }
            return e.v;
        },
        async put(key, value, opts = {}) {
            const ttl = opts.expirationTtl;
            await storage.put(String(key), {
                v: String(value),
                exp: Number.isFinite(ttl) && ttl > 0 ? now() + ttl * 1000 : null,
            });
        },
        async delete(key) {
            await storage.delete(key);
        },
        async list({ prefix = "", limit = 1000 } = {}) {
            const keys = [];
            const dead = [];
            for (const [name, e] of await storage.list({ prefix })) {
                if (!live(e)) { dead.push(name); continue; }
                keys.push({ name });
                if (keys.length >= limit) break;
            }
            if (dead.length) await storage.delete(dead);
            return { keys, list_complete: true };
        },
        /** Delete expired entries under the prefixes that grow without reads. */
        async sweepExpired(prefixes = ["r1:nonce:", "r1:queue:", "r1:match-for:", "v2:room:"]) {
            let n = 0;
            for (const prefix of prefixes) {
                const dead = [];
                for (const [name, e] of await storage.list({ prefix })) if (!live(e)) dead.push(name);
                // storage.delete takes at most 128 keys per call.
                for (let i = 0; i < dead.length; i += 128) n += await storage.delete(dead.slice(i, i + 128));
            }
            return n;
        },
    };
}
