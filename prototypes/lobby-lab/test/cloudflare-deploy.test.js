/**
 * Cloudflare deploy config (deploy/cloudflare): the Durable Object storage
 * KV adapter the RankedLedger runs on. The whole Worker is exercised in the
 * real runtime by deploy/cloudflare/e2e-wrangler.mjs (needs wrangler).
 */

import test from "node:test";
import assert from "node:assert/strict";
import { createDoStorageKv } from "../deploy/cloudflare/do-storage-kv.js";

/** The subset of DurableObjectStorage the adapter uses. */
function fakeStorage() {
    const m = new Map();
    return {
        m,
        async get(k) { return m.has(k) ? structuredClone(m.get(k)) : undefined; },
        async put(k, v) { m.set(k, structuredClone(v)); },
        async delete(k) {
            if (Array.isArray(k)) { let n = 0; for (const x of k) n += m.delete(x) ? 1 : 0; return n; }
            return m.delete(k);
        },
        async list({ prefix = "" } = {}) {
            return new Map([...m].filter(([k]) => k.startsWith(prefix)).sort(([a], [b]) => a.localeCompare(b)));
        },
    };
}

test("DO storage KV: get/put/delete/list with TTL, like Workers KV", async () => {
    const clock = { t: 1_000_000 };
    const st = fakeStorage();
    const kv = createDoStorageKv(st, { now: () => clock.t });
    await kv.put("r1:player:a", "{\"x\":1}");
    await kv.put("r1:nonce:a:n1", "1", { expirationTtl: 60 });
    await kv.put("r1:queue:a", "q", { expirationTtl: 120 });
    assert.equal(await kv.get("r1:player:a"), "{\"x\":1}");
    assert.equal(await kv.get("missing"), null);
    assert.deepEqual((await kv.list({ prefix: "r1:queue:" })).keys, [{ name: "r1:queue:a" }]);
    clock.t += 61_000;
    assert.equal(await kv.get("r1:nonce:a:n1"), null, "expired values read as missing");
    assert.equal(st.m.has("r1:nonce:a:n1"), false, "and are deleted on read");
    await kv.put("r1:nonce:a:n2", "1", { expirationTtl: 60 });
    clock.t += 200_000;
    assert.deepEqual((await kv.list({ prefix: "r1:queue:" })).keys, [], "list skips expired entries");
    assert.equal(await kv.sweepExpired(), 1, "the alarm sweep clears unread expired nonces");
    assert.equal(await kv.get("r1:player:a"), "{\"x\":1}", "values without a TTL never expire");
    await kv.delete("r1:player:a");
    assert.equal(await kv.get("r1:player:a"), null);
});
