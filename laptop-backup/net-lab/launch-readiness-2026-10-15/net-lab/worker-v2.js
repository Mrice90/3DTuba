/**
 * worker-v2.js — AI-096 Worker v2 prototype (Infinite Conquest lobby-lab).
 *
 * A second-generation lobby worker that runs BESIDE the pinned v1 worker,
 * not instead of it:
 *
 * - Every non-/v2/* request is delegated VERBATIM to upstream/worker.js
 *   (the pinned alpha contract). The 2D alpha keeps working unchanged
 *   during the transition; v1 endpoints stay ungated.
 * - /v2/* endpoints add three things the v1 contract lacks:
 *     1. dataVersion gate — state-changing v2 calls must carry a
 *        dataVersion at or above the configured minimum (default "lab-2").
 *        v1 only stores dataVersion; v2 enforces it.
 *     2. server-side room assignment — POST /v2/rooms/pair atomically
 *        pairs two live v1 queue tickets into a room, replacing the
 *        v1 pattern where the earliest-queued client publishes its own
 *        pairing (POST /pair) and can race or go silent.
 *     3. signed results — POST /v2/results keeps the v1 two-client
 *        agreement shape, flags disagreements, and on agreement issues
 *        an HMAC-signed receipt that detects later tampering.
 *
 * TRUST MODEL (read docs/v2-design.md before relying on any of this):
 * caller-supplied UUIDs are still NOT authentication — anyone holding a
 * seat UUID can report for it. The HMAC secret (LAB_V2_SECRET, ephemeral
 * per process unless set) makes recorded receipts tamper-EVIDENT, not
 * tamper-PROOF against the submitter, and proves nothing about who
 * submitted. Disagreements are flagged, never auto-resolved. v2 does not
 * apply Elo — rating application stays with the v1 /report path (disabled
 * in the lab by default) or a future ranked service. Local dev only.
 *
 * Workers-compatible: uses only Web Platform APIs (URL, Request,
 * Response, crypto.subtle), so this file can deploy to Cloudflare
 * Workers as-is when Mathew is ready. The lab's node adapter (server.js)
 * routes /v2/* here through the same loopback guards as v1.
 */

const DATA_VERSIONS = ["lab-1", "lab-2"];
const DEFAULT_DATAVERSION_MIN = "lab-2";

const ROOM_TTL = 300; // seconds; rooms are rendezvous records, not matches
const RESULT_TTL = 24 * 3600; // one seat's claim waits a day for the other
const FLAG_TTL = 30 * 24 * 3600; // disagreements kept 30d for review

// KV keys. "queue:" mirrors upstream/worker.js (read-only coupling — v2
// consumes v1 queue tickets; the pinned worker owns that namespace).
const queueKey = (uuid) => `queue:${uuid}`;
const roomKey = (roomId) => `v2:room:${roomId}`;
const seatKey = (uuid) => `v2:seat:${uuid}`;
const resultKey = (roomId) => `v2:result:${roomId}`;
const flagKey = (roomId) => `v2:flag:${roomId}`;

const ROOMID_CHARS = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"; // unambiguous, v1-style

function makeRoomId(randomValues) {
    const bytes = randomValues || crypto.getRandomValues(new Uint8Array(12));
    let id = "";
    for (const b of bytes) id += ROOMID_CHARS[b % ROOMID_CHARS.length];
    return id;
}

function hex(bytes) {
    return [...new Uint8Array(bytes)].map((b) => b.toString(16).padStart(2, "0")).join("");
}

/** Same UUID shape the pinned worker accepts. */
function isUuid(value) {
    return typeof value === "string" && /^[0-9a-fA-F-]{1,64}$/.test(value);
}

function json(data, status = 200) {
    return new Response(JSON.stringify(data), {
        status,
        headers: { "content-type": "application/json" },
    });
}

function bad(message, status = 400) {
    return json({ error: message }, status);
}

async function readJson(request) {
    try {
        return await request.json();
    } catch {
        return null;
    }
}

/**
 * dataVersion gate. Returns an error string, or null when the supplied
 * version is known and >= the minimum. Unknown version strings fail closed:
 * a version the server has never heard of cannot be proven compatible.
 */
export function checkDataVersion(dataVersion, min) {
    if (typeof dataVersion !== "string" || !dataVersion) {
        return "dataVersion is required on v2 state-changing endpoints";
    }
    const i = DATA_VERSIONS.indexOf(dataVersion);
    if (i < 0) {
        return `unknown dataVersion "${dataVersion.slice(0, 32)}"; known versions: ${DATA_VERSIONS.join(", ")}`;
    }
    if (i < DATA_VERSIONS.indexOf(min)) {
        return `dataVersion "${dataVersion}" is below the v2 minimum "${min}" — update the client`;
    }
    return null;
}

function canonicalReceipt(r) {
    return ["v2-result", r.roomId, r.winnerUuid, r.loserUuid, r.dataVersion, r.nonce].join("|");
}

async function hmacHex(secretBytes, message) {
    const key = await crypto.subtle.importKey(
        "raw",
        secretBytes,
        { name: "HMAC", hash: "SHA-256" },
        false,
        ["sign", "verify"],
    );
    const sig = await crypto.subtle.sign("HMAC", key, new TextEncoder().encode(message));
    return hex(sig);
}

/**
 * Create the v2 router.
 * @param {object} opts
 * @param {(request: Request, env: object) => Promise<Response>} opts.v1fetch
 *   verbatim v1 handler (the pinned worker); required.
 * @param {() => number} [opts.now] clock for createdAt/expiresIn.
 * @param {string} [opts.dataVersionMin] minimum accepted dataVersion.
 * @param {string|Uint8Array} [opts.secret] HMAC secret; when omitted an
 *   ephemeral per-process secret is generated (local-dev default).
 */
export function createV2Router({ v1fetch, now = () => Date.now(), dataVersionMin = DEFAULT_DATAVERSION_MIN, secret } = {}) {
    if (typeof v1fetch !== "function") throw new Error("createV2Router: v1fetch is required");
    if (!DATA_VERSIONS.includes(dataVersionMin)) {
        throw new Error(`createV2Router: unknown dataVersionMin "${dataVersionMin}"`);
    }
    const secretBytes =
        secret instanceof Uint8Array
            ? secret
            : typeof secret === "string" && secret
              ? new TextEncoder().encode(secret)
              : crypto.getRandomValues(new Uint8Array(32));
    const ephemeralSecret = secret == null || secret === "";

    const isSeat = (room, uuid) => room.seatA === uuid || room.seatB === uuid;

    function roomView(room) {
        return {
            roomId: room.roomId,
            seatA: room.seatA,
            seatB: room.seatB,
            dataVersion: room.dataVersion,
            createdAt: room.createdAt,
            expiresIn: ROOM_TTL,
        };
    }

    /**
     * POST /v2/rooms/pair {uuidA, uuidB, dataVersion}
     * Atomically pairs two live v1 queue tickets into a room. Idempotent
     * for an already-paired seat pair (returns the live room).
     */
    async function pairRoom(request, env) {
        const body = await readJson(request);
        const dvErr = checkDataVersion(body && body.dataVersion, dataVersionMin);
        if (dvErr) return bad(dvErr, 400);
        const { uuidA, uuidB } = body;
        if (!isUuid(uuidA) || !isUuid(uuidB)) return bad("uuidA and uuidB are required");
        if (uuidA === uuidB) return bad("uuidA and uuidB must differ");

        const existingId = await env.IC_KV.get(seatKey(uuidA));
        if (existingId) {
            const raw = await env.IC_KV.get(roomKey(existingId));
            if (raw) {
                const room = JSON.parse(raw);
                if (isSeat(room, uuidB)) return json(roomView(room));
            }
        }

        const [tickA, tickB] = await Promise.all([
            env.IC_KV.get(queueKey(uuidA)),
            env.IC_KV.get(queueKey(uuidB)),
        ]);
        if (!tickA || !tickB) {
            return bad("both players must hold live queue tickets; re-enqueue and retry", 400);
        }

        const room = {
            roomId: makeRoomId(),
            seatA: uuidA,
            seatB: uuidB,
            dataVersion: body.dataVersion.slice(0, 32),
            createdAt: now(),
        };
        await env.IC_KV.put(roomKey(room.roomId), JSON.stringify(room), { expirationTtl: ROOM_TTL });
        await env.IC_KV.put(seatKey(uuidA), room.roomId, { expirationTtl: ROOM_TTL });
        await env.IC_KV.put(seatKey(uuidB), room.roomId, { expirationTtl: ROOM_TTL });
        // Consume the tickets, exactly like v1's publishPairing does, so a
        // paired player cannot be paired again while the room is live.
        await env.IC_KV.delete(queueKey(uuidA));
        await env.IC_KV.delete(queueKey(uuidB));
        return json(roomView(room));
    }

    /** GET /v2/rooms?uuid= — the caller's live room, if any. */
    async function getRoom(url, env) {
        const uuid = url.searchParams.get("uuid");
        if (!isUuid(uuid)) return bad("uuid is required");
        const roomId = await env.IC_KV.get(seatKey(uuid));
        if (!roomId) return bad("no live room for uuid", 404);
        const raw = await env.IC_KV.get(roomKey(roomId));
        if (!raw) return bad("no live room for uuid", 404);
        return json(roomView(JSON.parse(raw)));
    }

    /** DELETE /v2/rooms/:roomId {uuid} — a seat holder closes the room. */
    async function closeRoom(roomId, request, env) {
        const body = await readJson(request);
        const raw = await env.IC_KV.get(roomKey(roomId));
        if (!raw) return json({ ok: true }); // idempotent
        const room = JSON.parse(raw);
        if (!body || !isSeat(room, body.uuid)) {
            return bad("only a room seat may close the room", 403);
        }
        await env.IC_KV.delete(roomKey(roomId));
        await env.IC_KV.delete(seatKey(room.seatA));
        await env.IC_KV.delete(seatKey(room.seatB));
        // Results/flags outlive the room (TTL-bounded) so signed receipts
        // stay verifiable after the room closes.
        return json({ ok: true });
    }

    /**
     * POST /v2/results {roomId, reporterUuid, winnerUuid, loserUuid,
     *                   finalStateHash?, dataVersion}
     * Two-seat agreement with signed receipts. Mirrors the v1 /report
     * agreement shape; disagreements are flagged, never auto-resolved.
     * v2 does NOT apply Elo — see the module trust notes.
     */
    async function recordResult(request, env) {
        const body = await readJson(request);
        const dvErr = checkDataVersion(body && body.dataVersion, dataVersionMin);
        if (dvErr) return bad(dvErr, 400);
        const { roomId, reporterUuid, winnerUuid, loserUuid } = body;
        if (typeof roomId !== "string" || !roomId) return bad("roomId is required");
        if (!isUuid(reporterUuid) || !isUuid(winnerUuid) || !isUuid(loserUuid)) {
            return bad("reporterUuid, winnerUuid and loserUuid are required");
        }
        if (winnerUuid === loserUuid) return bad("winner and loser must differ");

        const roomRaw = await env.IC_KV.get(roomKey(roomId));
        if (!roomRaw) return bad("unknown or expired room", 404);
        const room = JSON.parse(roomRaw);
        const seats = [room.seatA, room.seatB];
        if (!seats.includes(reporterUuid)) return bad("only a room seat may report", 403);
        if (!seats.includes(winnerUuid) || !seats.includes(loserUuid)) {
            return bad("winner and loser must be the room's seats", 400);
        }

        const key = resultKey(roomId);
        const raw = await env.IC_KV.get(key);
        const claims = raw ? JSON.parse(raw).claims || {} : {};
        // Idempotent on (roomId, reporterUuid): retries overwrite.
        claims[reporterUuid] = {
            winnerUuid,
            loserUuid,
            finalStateHash:
                typeof body.finalStateHash === "string" ? body.finalStateHash.slice(0, 128) : null,
            dataVersion: body.dataVersion.slice(0, 32),
            at: now(),
        };
        const reporters = Object.keys(claims);

        if (reporters.length < 2) {
            await env.IC_KV.put(key, JSON.stringify({ claims }), { expirationTtl: RESULT_TTL });
            return json({ recorded: true, status: "waiting", reason: "waiting for the other seat's report" });
        }

        const [a, b] = reporters.map((r) => claims[r]);
        if (a.winnerUuid !== b.winnerUuid || a.loserUuid !== b.loserUuid) {
            await env.IC_KV.put(
                flagKey(roomId),
                JSON.stringify({ reason: "disagreement", claims }),
                { expirationTtl: FLAG_TTL },
            );
            await env.IC_KV.delete(key);
            return json({ recorded: true, status: "disputed", reason: "reports disagree — flagged for review" });
        }

        const receipt = {
            v: 2,
            roomId,
            winnerUuid: a.winnerUuid,
            loserUuid: a.loserUuid,
            dataVersion: body.dataVersion.slice(0, 32),
            nonce: hex(crypto.getRandomValues(new Uint8Array(16))),
        };
        receipt.sig = await hmacHex(secretBytes, canonicalReceipt(receipt));
        await env.IC_KV.put(key, JSON.stringify({ claims, receipt, agreedAt: now() }), {
            expirationTtl: RESULT_TTL,
        });
        return json({ recorded: true, status: "agreed", receipt });
    }

    /** GET /v2/results/:roomId — agreement state and receipt, if any. */
    async function getResult(roomId, env) {
        const flagRaw = await env.IC_KV.get(flagKey(roomId));
        if (flagRaw) {
            return json({ status: "disputed", reason: "reports disagree — flagged for review" });
        }
        const raw = await env.IC_KV.get(resultKey(roomId));
        if (!raw) return bad("no results recorded for room", 404);
        const stored = JSON.parse(raw);
        if (stored.receipt) {
            return json({ status: "agreed", receipt: stored.receipt, agreedAt: stored.agreedAt });
        }
        return json({ status: "waiting", reason: "waiting for the other seat's report" });
    }

    /**
     * POST /v2/results/verify {receipt} — recompute the HMAC over the
     * receipt fields. {valid:true} means this server issued exactly this
     * receipt; it says nothing about who submitted the underlying claims.
     */
    async function verifyReceipt(request) {
        const body = await readJson(request);
        const r = body && body.receipt;
        if (!r || r.v !== 2 || typeof r.roomId !== "string" || !isUuid(r.winnerUuid)
            || !isUuid(r.loserUuid) || typeof r.dataVersion !== "string"
            || typeof r.nonce !== "string" || typeof r.sig !== "string") {
            return json({ valid: false, reason: "malformed receipt" });
        }
        const expected = await hmacHex(secretBytes, canonicalReceipt(r));
        return json(expected === r.sig ? { valid: true } : { valid: false, reason: "signature mismatch" });
    }

    async function v2fetch(request, env) {
        const url = new URL(request.url);
        const path = url.pathname;
        const method = request.method.toUpperCase();

        if (method === "GET" && path === "/v2/version") {
            return json({
                worker: "v2",
                dataVersions: DATA_VERSIONS,
                dataVersionMin,
                v1Compatible: true,
                secretMode: ephemeralSecret ? "ephemeral" : "provided",
                endpoints: [
                    "GET /v2/version",
                    "POST /v2/rooms/pair",
                    "GET /v2/rooms?uuid=",
                    "DELETE /v2/rooms/:roomId",
                    "POST /v2/results",
                    "GET /v2/results/:roomId",
                    "POST /v2/results/verify",
                ],
            });
        }
        if (method === "POST" && path === "/v2/rooms/pair") return pairRoom(request, env);
        if (method === "GET" && path === "/v2/rooms") return getRoom(url, env);
        if (method === "DELETE" && path.startsWith("/v2/rooms/")) {
            return closeRoom(decodeURIComponent(path.slice("/v2/rooms/".length)), request, env);
        }
        if (method === "POST" && path === "/v2/results") return recordResult(request, env);
        if (method === "POST" && path === "/v2/results/verify") return verifyReceipt(request);
        if (method === "GET" && path.startsWith("/v2/results/")) {
            return getResult(decodeURIComponent(path.slice("/v2/results/".length)), env);
        }
        return bad("not found", 404);
    }

    return {
        /** Routes /v2/* to the v2 handler, everything else to v1 verbatim. */
        fetch: async (request, env) => {
            const path = new URL(request.url).pathname;
            if (path === "/v2" || path.startsWith("/v2/")) return v2fetch(request, env);
            return v1fetch(request, env);
        },
        dataVersionMin,
        dataVersions: DATA_VERSIONS,
        secretMode: ephemeralSecret ? "ephemeral" : "provided",
    };
}

export { DATA_VERSIONS, DEFAULT_DATAVERSION_MIN, ROOM_TTL, RESULT_TTL };
