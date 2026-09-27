/**
 * kv.js — in-memory KV store mimicking the Cloudflare Workers KV subset
 * used by upstream/worker.js: get / put (with expirationTtl) / delete / list.
 *
 * Expiry is evaluated against an injectable clock so tests can deterministically
 * advance time (AI-040). Production default is Date.now.
 *
 * Local development only: no persistence, no replication.
 */

export function createKv({ now = () => Date.now() } = {}) {
    // key -> { value: string, expiresAt: number|null }
    const store = new Map();

    function isExpired(entry) {
        return entry.expiresAt !== null && entry.expiresAt <= now();
    }

    return {
        /** The clock this store evaluates TTL against (for tests). */
        now,

        async get(key) {
            const entry = store.get(key);
            if (!entry) return null;
            if (isExpired(entry)) {
                store.delete(key);
                return null;
            }
            return entry.value;
        },

        async put(key, value, opts = {}) {
            const ttl = opts.expirationTtl;
            store.set(String(key), {
                value: String(value),
                expiresAt: Number.isFinite(ttl) && ttl > 0 ? now() + ttl * 1000 : null,
            });
        },

        async delete(key) {
            store.delete(key);
        },

        async list({ prefix = "", limit = 1000 } = {}) {
            const keys = [];
            for (const [key, entry] of store) {
                if (!key.startsWith(prefix)) continue;
                if (isExpired(entry)) {
                    store.delete(key);
                    continue;
                }
                keys.push({ name: key });
                if (keys.length >= limit) break;
            }
            return { keys, list_complete: true };
        },

        /** Test/debug helper: number of live (unexpired) keys. */
        liveCount(prefix = "") {
            let n = 0;
            for (const [key, entry] of store) {
                if (prefix && !key.startsWith(prefix)) continue;
                if (!isExpired(entry)) n++;
            }
            return n;
        },
    };
}
