/**
 * worker.js — Cloudflare entry point for Infinite Conquest ranked beta
 * (AI-010 / AI-011). READY TO DEPLOY, NOT DEPLOYED: deploying needs Matt's
 * go-ahead (see README.md in this folder).
 *
 * Routes:
 *   GET  /rooms/:id/ws        relay upgrade -> MatchRoom Durable Object (one per room)
 *   *    /v3/*                ranked service -> RankedLedger Durable Object (one, global)
 *   *    /v2/*, everything else  casual v2 router over the pinned v1 worker (IC_KV)
 *
 * The RankedLedger is the single serialized writer: identities, nonces, the
 * ranked queue, transcripts, verdicts and ratings all live in its strongly
 * consistent storage. MatchRoom hands it each finished transcript, HMAC
 * signed with LAB_RELAY_SECRET, the same as an outside relay driver would.
 */

import v1worker from "../../upstream/worker.js";
import { createV2Router } from "../../worker-v2.js";
import { MatchRoom as RelayMatchRoom, createRelayRouter } from "../../relay.js";
import { createRankedService, macHeaders } from "../../ranked.js";
import { createDoStorageKv } from "./do-storage-kv.js";

const LEDGER_NAME = "ledger-v1";
const SWEEP_MS = 15 * 60_000;
const ledgerStub = (env) => env.RANKED_LEDGER.get(env.RANKED_LEDGER.idFromName(LEDGER_NAME));
const json = (data, status = 200) => new Response(JSON.stringify(data), { status, headers: { "content-type": "application/json" } });

export class RankedLedger {
    constructor(state, env) {
        this.state = state;
        this.kv = createDoStorageKv(state.storage);
        if (!env.LAB_REFEREE_SECRET || !env.LAB_RELAY_SECRET) {
            this.misconfigured = true;
            return;
        }
        this.service = createRankedService({
            kv: this.kv,
            roomKv: this.kv, // ranked rooms stay in strongly consistent storage, not KV
            refereeSecret: env.LAB_REFEREE_SECRET,
            relaySecret: env.LAB_RELAY_SECRET,
            dataVersion: env.IC_DATAVERSION || "lab-2",
        });
    }

    async fetch(request) {
        if (this.misconfigured) return json({ error: "ranked secrets are not set (LAB_REFEREE_SECRET, LAB_RELAY_SECRET)" }, 503);
        const url = new URL(request.url);
        // Reachable only through this object's stub; the worker never forwards /__internal/.
        const m = url.pathname.match(/^\/__internal\/v2room\/([A-Z0-9]{6,24})$/);
        if (m) {
            const raw = await this.kv.get(`v2:room:${m[1]}`);
            return raw ? new Response(raw, { headers: { "content-type": "application/json" } }) : json({ error: "no room" }, 404);
        }
        if ((await this.state.storage.getAlarm()) == null) await this.state.storage.setAlarm(Date.now() + SWEEP_MS);
        return this.service.fetch(request);
    }

    async alarm() {
        await this.kv.sweepExpired();
        await this.state.storage.setAlarm(Date.now() + SWEEP_MS);
    }
}

/** The relay Durable Object, plus the ranked hand-off of finished transcripts. */
export class MatchRoom extends RelayMatchRoom {
    onTranscript(transcript) {
        if (!this.env.RANKED_LEDGER || !this.env.LAB_RELAY_SECRET) return;
        const path = "/v3/ranked/transcripts";
        const body = JSON.stringify(transcript);
        const send = (async () => {
            const headers = { "content-type": "application/json", ...(await macHeaders({ secret: this.env.LAB_RELAY_SECRET, method: "POST", path, body })) };
            // Casual rooms come back 409 "not a ranked room"; that is expected.
            await ledgerStub(this.env).fetch(new Request("https://ledger" + path, { method: "POST", headers, body }));
        })().catch(() => {});
        this.state.waitUntil?.(send);
    }
}

/** v2 room lookup for the relay: casual rooms in IC_KV, ranked rooms in the ledger. */
async function getRoom(roomId, env) {
    const raw = await env.IC_KV.get(`v2:room:${roomId}`);
    if (raw) return JSON.parse(raw);
    if (!env.RANKED_LEDGER || !/^[A-Z0-9]{6,24}$/.test(roomId)) return null;
    const res = await ledgerStub(env).fetch(new Request(`https://ledger/__internal/v2room/${roomId}`));
    return res.ok ? res.json() : null;
}

let router = null;
function routerFor(env) {
    if (router) return router;
    const v2 = createV2Router({
        v1fetch: (req, e) => v1worker.fetch(req, e),
        dataVersionMin: env.IC_DATAVERSION || "lab-2",
        secret: env.LAB_V2_SECRET,
    });
    const ranked = async (request, e) => {
        const path = new URL(request.url).pathname;
        if (!path.startsWith("/v3/")) return v2.fetch(request, e);
        if (e.RANKED_RATE_LIMIT) {
            const who = request.headers.get("x-ic-player") || request.headers.get("cf-connecting-ip") || "anon";
            const { success } = await e.RANKED_RATE_LIMIT.limit({ key: who });
            if (!success) return json({ error: "rate limited" }, 429);
        }
        return ledgerStub(e).fetch(request);
    };
    router = createRelayRouter({ fallback: ranked, getRoom, dataVersionMin: env.IC_DATAVERSION || "lab-2" });
    return router;
}

export default {
    fetch(request, env, ctx) {
        return routerFor(env).fetch(request, env, ctx);
    },
};
