/**
 * ranked.js — AI-010 / AI-011 ranked foundation (Infinite Conquest lobby-lab).
 *
 * What ranked adds on top of v2 rooms and the relay (docs/ranked-design.md):
 *
 * 1. Player identity. A player is an Ed25519 public key. Every ranked call is
 *    signed with the matching private key over method, path, timestamp,
 *    nonce and body hash. Unknown keys, bad signatures, stale timestamps and
 *    reused nonces are refused. Seat UUIDs stop being the identity: the
 *    service mints them per match and hands each only to its signed owner.
 * 2. Relay-attested transcripts. The relay (relay.js) is our own server and
 *    the single sequencer, so its transcript (seed, ordered intents, hash
 *    checks, result claims, end reason) is the record. Clients cannot edit it.
 * 3. Referee verdicts. A referee process with the rules engine replays the
 *    transcript (seed + intents) and posts the winner. Only a referee verdict
 *    (HMAC-authenticated, bound to the transcript hash) or a relay-observed
 *    forfeit moves ratings. Agreeing client claims are never enough.
 * 4. Exactly-once ratings. Each room is rated at most once; a repeated or
 *    conflicting verdict changes nothing. All writes go through one
 *    serialized writer (a Durable Object in production; a promise chain in
 *    the lab), because Workers KV is eventually consistent.
 * 5. Persistent leaderboard and per-player stats, labelled beta until the
 *    authoritative server (AI-085).
 *
 * Workers-compatible: Web Platform APIs only (crypto.subtle, Request,
 * Response, URL). Local development only until deployed with Mathew.
 */

export const RANKED_VERSION = "ranked-1";
export const BASE_FACTIONS = ["zeus", "poseidon"];
export const START_RATING = 1200;
export const PROVISIONAL_GAMES = 10;
export const MAX_CLOCK_SKEW_MS = 5 * 60_000;
const NONCE_TTL = 15 * 60; // seconds; > 2x clock skew so a nonce can't be replayed in-window
const QUEUE_TTL = 120;
const ROOM_TTL = 300; // v2 rendezvous TTL; the relay session outlives it once connected
const MAX_BODY = 16 * 1024;

const k = {
    player: (id) => `r1:player:${id}`,
    nonce: (id, n) => `r1:nonce:${id}:${n}`,
    queue: (id) => `r1:queue:${id}`,
    matchFor: (id) => `r1:match-for:${id}`,
    room: (roomId) => `r1:room:${roomId}`,
    v2room: (roomId) => `v2:room:${roomId}`, // read by the relay upgrade path
    match: (roomId) => `r1:match:${roomId}`,
    pending: (roomId) => `r1:pending:${roomId}`,
    rating: (id) => `r1:rating:${id}`,
};

const enc = (s) => new TextEncoder().encode(s);
const hex = (buf) => [...new Uint8Array(buf)].map((b) => b.toString(16).padStart(2, "0")).join("");
export const sha256Hex = async (data) => hex(await crypto.subtle.digest("SHA-256", typeof data === "string" ? enc(data) : data));

export function b64url(bytes) {
    let s = "";
    for (const b of new Uint8Array(bytes)) s += String.fromCharCode(b);
    return btoa(s).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}
export function fromB64url(str) {
    if (typeof str !== "string" || !/^[A-Za-z0-9_-]+$/.test(str)) return null;
    const s = atob(str.replace(/-/g, "+").replace(/_/g, "/") + "===".slice((str.length + 3) % 4));
    return Uint8Array.from(s, (c) => c.charCodeAt(0));
}

/** Deterministic JSON (sorted keys) so a transcript hashes the same everywhere. */
export function canonicalJson(v) {
    if (v === null || typeof v !== "object") return JSON.stringify(v ?? null);
    if (Array.isArray(v)) return "[" + v.map(canonicalJson).join(",") + "]";
    return "{" + Object.keys(v).sort().filter((key) => v[key] !== undefined)
        .map((key) => JSON.stringify(key) + ":" + canonicalJson(v[key])).join(",") + "}";
}

/** The string a player signs. Shared by client and server. */
export async function signingString(method, path, timestamp, nonce, body) {
    return [RANKED_VERSION, method.toUpperCase(), path, String(timestamp), nonce, await sha256Hex(body || "")].join("\n");
}

/** Client helper: headers for a signed ranked call. `privateKey` is a CryptoKey (Ed25519). */
export async function signRequest({ privateKey, playerId, method, path, body = "", now = Date.now(), nonce }) {
    const n = nonce || b64url(crypto.getRandomValues(new Uint8Array(16)));
    const sig = await crypto.subtle.sign("Ed25519", privateKey, enc(await signingString(method, path, now, n, body)));
    return { "x-ic-player": playerId, "x-ic-timestamp": String(now), "x-ic-nonce": n, "x-ic-signature": b64url(sig) };
}

/** Referee/relay helper: HMAC-SHA256 hex over a string. */
export async function hmacHex(secret, message) {
    const key = await crypto.subtle.importKey("raw", enc(secret), { name: "HMAC", hash: "SHA-256" }, false, ["sign"]);
    return hex(await crypto.subtle.sign("HMAC", key, enc(message)));
}

function timingSafeEqualHex(a, b) {
    if (typeof a !== "string" || typeof b !== "string" || a.length !== b.length) return false;
    let d = 0;
    for (let i = 0; i < a.length; i++) d |= a.charCodeAt(i) ^ b.charCodeAt(i);
    return d === 0;
}

/** Player id = first 32 hex of SHA-256(public key). Stable, not guessable from a name. */
export const playerIdFor = async (pubBytes) => (await sha256Hex(pubBytes)).slice(0, 32);

/** Standard Elo with a higher K while a player is provisional. */
export function eloUpdate(winner, loser) {
    const kOf = (p) => (p.games < PROVISIONAL_GAMES ? 40 : 32);
    const expW = 1 / (1 + 10 ** ((loser.rating - winner.rating) / 400));
    return {
        winner: Math.round(winner.rating + kOf(winner) * (1 - expW)),
        loser: Math.round(loser.rating + kOf(loser) * (0 - (1 - expW))),
    };
}

const json = (data, status = 200) => new Response(JSON.stringify(data), { status, headers: { "content-type": "application/json" } });
const bad = (error, status = 400) => json({ error }, status);

function uuid() {
    const b = crypto.getRandomValues(new Uint8Array(16));
    b[6] = (b[6] & 0x0f) | 0x40; b[8] = (b[8] & 0x3f) | 0x80;
    const h = hex(b);
    return `${h.slice(0, 8)}-${h.slice(8, 12)}-${h.slice(12, 16)}-${h.slice(16, 20)}-${h.slice(20)}`;
}
const ROOMID_CHARS = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
const roomId = () => [...crypto.getRandomValues(new Uint8Array(12))].map((b) => ROOMID_CHARS[b % ROOMID_CHARS.length]).join("");

/**
 * @param {object} opts
 * @param {object} opts.kv KV-shaped store (get/put/delete/list). Production: Durable Object storage.
 * @param {string} opts.refereeSecret shared with the referee runner (HMAC).
 * @param {string} opts.relaySecret shared with the relay driver for transcript posts (HMAC).
 * @param {() => number} [opts.now]
 * @param {(playerId:string, faction:string) => Promise<boolean>} [opts.isEntitled] AI-021 hook; base factions only by default.
 * @param {string} [opts.dataVersion] room dataVersion handed to the relay (v2 gate).
 * @param {object} [opts.roomKv] store the relay reads v2 rooms from (defaults to kv).
 */
export function createRankedService({ kv, refereeSecret, relaySecret, now = () => Date.now(), isEntitled, dataVersion = "lab-2", roomKv = kv }) {
    if (!kv || typeof kv.get !== "function") throw new Error("createRankedService: kv is required");
    if (!refereeSecret || !relaySecret) throw new Error("createRankedService: refereeSecret and relaySecret are required");
    const entitled = isEntitled || (async (_p, f) => BASE_FACTIONS.includes(f));

    // Single writer: every state change runs strictly one after another.
    let chain = Promise.resolve();
    const serial = (fn) => {
        const run = chain.then(fn, fn);
        chain = run.catch(() => {});
        return run;
    };

    const getJson = async (key) => { const v = await kv.get(key); return v == null ? null : JSON.parse(v); };
    const putJson = (key, v, opts) => kv.put(key, JSON.stringify(v), opts);

    async function ratingOf(id) {
        return (await getJson(k.rating(id))) || { playerId: id, rating: START_RATING, games: 0, wins: 0, losses: 0, disputesLost: 0 };
    }

    // ---- identity -------------------------------------------------------
    async function verifySigned(request, path, body, newPlayer = null) {
        const id = request.headers.get("x-ic-player");
        const ts = Number(request.headers.get("x-ic-timestamp"));
        const nonce = request.headers.get("x-ic-nonce");
        const sig = fromB64url(request.headers.get("x-ic-signature") || "");
        if (!id || !/^[0-9a-f]{32}$/.test(id) || !nonce || !/^[A-Za-z0-9_-]{8,64}$/.test(nonce) || !sig) {
            return { error: bad("signed request headers required (x-ic-player, x-ic-timestamp, x-ic-nonce, x-ic-signature)", 401) };
        }
        if (!Number.isFinite(ts) || Math.abs(now() - ts) > MAX_CLOCK_SKEW_MS) return { error: bad("timestamp outside the allowed clock skew", 401) };
        const player = newPlayer && newPlayer.playerId === id ? newPlayer : await getJson(k.player(id));
        if (!player) return { error: bad("unknown player", 401) };
        const key = await crypto.subtle.importKey("raw", fromB64url(player.publicKey), { name: "Ed25519" }, false, ["verify"]);
        const ok = await crypto.subtle.verify("Ed25519", key, sig, enc(await signingString(request.method, path, ts, nonce, body)));
        if (!ok) return { error: bad("bad signature", 401) };
        // Replay guard. Checked after the signature so strangers cannot burn a player's nonces.
        const seen = await serial(async () => {
            if (await kv.get(k.nonce(id, nonce))) return true;
            await kv.put(k.nonce(id, nonce), "1", { expirationTtl: NONCE_TTL });
            return false;
        });
        if (seen) return { error: bad("nonce already used (replayed request)", 401) };
        return { player };
    }

    async function register(request, path, body) {
        let data;
        try { data = JSON.parse(body); } catch { return bad("body must be JSON"); }
        const pub = fromB64url(data?.publicKey);
        const name = typeof data?.displayName === "string" ? data.displayName.trim() : "";
        if (!pub || pub.length !== 32) return bad("publicKey must be a base64url raw Ed25519 key (32 bytes)");
        if (!/^[\p{L}\p{N} _.-]{3,24}$/u.test(name)) return bad("displayName must be 3-24 letters, digits, spaces, _ . -");
        const id = await playerIdFor(pub);
        // Proof of possession: the registration itself must be signed by the new key.
        const v = await verifySigned(request, path, body, { playerId: id, publicKey: data.publicKey });
        if (v.error) return v.error;
        return serial(async () => {
            const existing = await getJson(k.player(id));
            if (existing) return json({ playerId: id, displayName: existing.displayName, created: false });
            await putJson(k.player(id), { playerId: id, publicKey: data.publicKey, displayName: name, createdAt: now() });
            return json({ playerId: id, displayName: name, created: true }, 201);
        });
    }

    // ---- queue and rooms ------------------------------------------------
    async function enqueue(player, body) {
        let data;
        try { data = JSON.parse(body || "{}"); } catch { return bad("body must be JSON"); }
        const faction = String(data.faction || "").toLowerCase();
        if (!(await entitled(player.playerId, faction))) return bad(`faction "${faction}" is not available to this player`, 403);
        return serial(async () => {
            const already = await getJson(k.matchFor(player.playerId));
            if (already) return json(await matchView(already, player.playerId));
            const { keys } = await kv.list({ prefix: "r1:queue:" });
            for (const { name } of keys) {
                const other = await getJson(name);
                if (!other || other.playerId === player.playerId) continue;
                await kv.delete(name);
                await kv.delete(k.queue(player.playerId));
                const room = {
                    roomId: roomId(), createdAt: now(), dataVersion,
                    seatA: uuid(), seatB: uuid(),
                    players: { A: other.playerId, B: player.playerId },
                    factions: { A: other.faction, B: faction },
                };
                await putJson(k.room(room.roomId), room);
                await roomKv.put(k.v2room(room.roomId), JSON.stringify( { roomId: room.roomId, seatA: room.seatA, seatB: room.seatB, dataVersion, createdAt: room.createdAt, expiresIn: ROOM_TTL }), { expirationTtl: ROOM_TTL });
                await putJson(k.matchFor(other.playerId), room.roomId, { expirationTtl: ROOM_TTL });
                await putJson(k.matchFor(player.playerId), room.roomId, { expirationTtl: ROOM_TTL });
                return json(await matchView(room.roomId, player.playerId));
            }
            await putJson(k.queue(player.playerId), { playerId: player.playerId, faction, at: now() }, { expirationTtl: QUEUE_TTL });
            return json({ status: "queued", faction });
        });
    }

    async function matchView(rid, playerId) {
        const room = await getJson(k.room(rid));
        if (!room) return { status: "expired" };
        const side = room.players.A === playerId ? "A" : "B";
        const opp = side === "A" ? "B" : "A";
        const oppRating = await ratingOf(room.players[opp]);
        const oppPlayer = await getJson(k.player(room.players[opp]));
        return {
            status: "matched", roomId: rid, dataVersion: room.dataVersion,
            side, // "A" is engine player 0, "B" is player 1 (the referee replays in that order)
            seat: side === "A" ? room.seatA : room.seatB, // only ever returned to its signed owner
            faction: room.factions[side],
            opponent: { playerId: room.players[opp], displayName: oppPlayer?.displayName, rating: oppRating.rating, faction: room.factions[opp] },
        };
    }

    async function pollQueue(player) {
        const rid = await getJson(k.matchFor(player.playerId));
        if (rid) return json(await matchView(rid, player.playerId));
        return json({ status: (await kv.get(k.queue(player.playerId))) ? "queued" : "idle" });
    }

    // ---- transcripts, verdicts, ratings ---------------------------------
    /** Relay driver hands over a finished session's transcript. Called in-process or via HMAC POST. */
    async function recordTranscript(transcript) {
        return serial(async () => {
            const room = await getJson(k.room(transcript?.roomId));
            if (!room) return { ok: false, error: "not a ranked room" };
            if (canonicalJson([...transcript.seats].sort()) !== canonicalJson([room.seatA, room.seatB].sort())) {
                return { ok: false, error: "transcript seats do not match the room" };
            }
            if (await kv.get(k.match(room.roomId))) return { ok: true, duplicate: true };
            const transcriptHash = await sha256Hex(canonicalJson(transcript));
            const record = { roomId: room.roomId, transcriptHash, transcript, status: "pending", recordedAt: now() };
            const flagged = transcript.flags?.length > 0 || transcript.endReason === "result-disputed";
            if (flagged) {
                record.status = "flagged"; // never rated; kept for review (same policy as v1/v2 disputes)
            } else if (transcript.forfeitSeat && ["timeout", "no-reveal"].includes(transcript.endReason)) {
                // The relay observed the forfeit itself; no replay needed to know who left.
                const loserSide = transcript.forfeitSeat === room.seatA ? "A" : "B";
                await applyRating(room, record, loserSide === "A" ? "B" : "A", { source: "relay-forfeit", reason: transcript.endReason });
            } else {
                await putJson(k.pending(room.roomId), { roomId: room.roomId, transcriptHash, recordedAt: record.recordedAt });
            }
            await putJson(k.match(room.roomId), record);
            return { ok: true, status: record.status, transcriptHash };
        });
    }

    async function applyRating(room, record, winnerSide, verdict) {
        const loserSide = winnerSide === "A" ? "B" : "A";
        const w = await ratingOf(room.players[winnerSide]);
        const l = await ratingOf(room.players[loserSide]);
        const next = eloUpdate(w, l);
        const delta = { [room.players[winnerSide]]: next.winner - w.rating, [room.players[loserSide]]: next.loser - l.rating };
        w.rating = next.winner; w.games++; w.wins++;
        l.rating = next.loser; l.games++; l.losses++;
        // Claims that contradict the verified result count against the claimant.
        for (const [seat, claim] of Object.entries(record.transcript.results || {})) {
            const side = seat === room.seatA ? "A" : "B";
            const claimedSide = claim.winnerSeat === room.seatA ? "A" : "B";
            if (claimedSide !== winnerSide) (side === winnerSide ? w : l).disputesLost++;
        }
        await putJson(k.rating(w.playerId), w);
        await putJson(k.rating(l.playerId), l);
        record.status = "rated";
        record.verdict = { ...verdict, winnerPlayerId: room.players[winnerSide], loserPlayerId: room.players[loserSide], delta, ratedAt: now() };
    }

    async function verdict(data) {
        return serial(async () => {
            const rec = await getJson(k.match(data.roomId));
            if (!rec) return bad("unknown match", 404);
            if (rec.transcriptHash !== data.transcriptHash) return bad("transcriptHash does not match the recorded transcript", 409);
            if (rec.status === "rated" || rec.status === "rejected" || rec.status === "void") {
                // Exactly once: the same verdict again is a harmless no-op; a different one is refused.
                const same = rec.verdict?.source === "referee" && rec.verdict.legal === (data.legal !== false)
                    && (rec.status === "void" ? data.unfinished === true : rec.verdict.winnerSeat === data.winnerSeat);
                return same ? json({ status: rec.status, duplicate: true }) : bad("match already settled", 409);
            }
            if (rec.status === "flagged") return bad("flagged matches are never rated", 409);
            const room = await getJson(k.room(data.roomId));
            const v = { source: "referee", winnerSeat: data.winnerSeat, legal: data.legal !== false, finalStateHash: data.finalStateHash, engine: String(data.engine || "").slice(0, 80) };
            if (v.legal && data.unfinished === true) {
                // The replay ended without a winner (draw, or abandoned with no
                // concession and no relay forfeit): settled, never rated.
                rec.status = "void";
                rec.verdict = { ...v, winnerSeat: null, unfinished: true, draw: data.draw === true, ratedAt: now() };
                await putJson(k.match(rec.roomId), rec);
                await kv.delete(k.pending(rec.roomId));
                return json({ status: rec.status });
            }
            let winnerSide;
            if (v.legal) {
                if (data.winnerSeat !== room.seatA && data.winnerSeat !== room.seatB) return bad("winnerSeat must be a room seat");
                winnerSide = data.winnerSeat === room.seatA ? "A" : "B";
            } else {
                // An illegal intent can only come from a modified client: that seat loses.
                if (data.offendingSeat !== room.seatA && data.offendingSeat !== room.seatB) return bad("offendingSeat required when legal is false");
                v.offendingSeat = data.offendingSeat;
                winnerSide = data.offendingSeat === room.seatA ? "B" : "A";
                v.winnerSeat = winnerSide === "A" ? room.seatA : room.seatB;
            }
            await applyRating(room, rec, winnerSide, v);
            await putJson(k.match(rec.roomId), rec);
            await kv.delete(k.pending(rec.roomId));
            return json({ status: rec.status, delta: rec.verdict.delta });
        });
    }

    async function verifyHmac(request, path, body, secret) {
        const ts = Number(request.headers.get("x-ic-timestamp"));
        const mac = request.headers.get("x-ic-mac") || "";
        if (!Number.isFinite(ts) || Math.abs(now() - ts) > MAX_CLOCK_SKEW_MS) return false;
        const expected = await hmacHex(secret, [RANKED_VERSION, request.method.toUpperCase(), path, ts, await sha256Hex(body || "")].join("\n"));
        return timingSafeEqualHex(mac, expected);
    }

    async function leaderboard(limit) {
        const { keys } = await kv.list({ prefix: "r1:rating:" });
        const rows = [];
        for (const { name } of keys) {
            const r = await getJson(name);
            if (!r || r.games === 0) continue;
            const p = await getJson(k.player(r.playerId));
            rows.push({ playerId: r.playerId, displayName: p?.displayName, rating: r.rating, games: r.games, wins: r.wins, losses: r.losses, provisional: r.games < PROVISIONAL_GAMES });
        }
        rows.sort((a, b) => b.rating - a.rating || b.wins - a.wins || a.playerId.localeCompare(b.playerId));
        return rows.slice(0, limit).map((r, i) => ({ rank: i + 1, ...r }));
    }

    // ---- HTTP -----------------------------------------------------------
    async function fetch(request) {
        const url = new URL(request.url);
        const path = url.pathname;
        if (!path.startsWith("/v3/")) return bad("not found", 404);
        const body = request.method === "GET" ? "" : await request.text();
        if (enc(body).length > MAX_BODY) return bad("body too large", 413);
        const route = `${request.method} ${path}`;

        if (route === "POST /v3/players") return register(request, path, body);
        if (route === "GET /v3/ranked/leaderboard") {
            const limit = Math.min(Math.max(parseInt(url.searchParams.get("limit") || "50", 10) || 50, 1), 200);
            return json({ beta: true, version: RANKED_VERSION, rows: await leaderboard(limit) });
        }
        let m = path.match(/^\/v3\/ranked\/players\/([0-9a-f]{32})$/);
        if (m && request.method === "GET") {
            const p = await getJson(k.player(m[1]));
            if (!p) return bad("unknown player", 404);
            const r = await ratingOf(m[1]);
            return json({ playerId: p.playerId, displayName: p.displayName, ...r, provisional: r.games < PROVISIONAL_GAMES, beta: true });
        }
        m = path.match(/^\/v3\/ranked\/matches\/([A-Z0-9]{6,24})$/);
        if (m && request.method === "GET") {
            const rec = await getJson(k.match(m[1]));
            if (!rec) return bad("unknown match", 404);
            return json({ roomId: rec.roomId, status: rec.status, transcriptHash: rec.transcriptHash, verdict: rec.verdict || null });
        }
        if (route === "POST /v3/ranked/queue" || route === "GET /v3/ranked/queue") {
            const v = await verifySigned(request, path, body);
            if (v.error) return v.error;
            return request.method === "POST" ? enqueue(v.player, body) : pollQueue(v.player);
        }
        if (route === "GET /v3/ranked/referee/pending" || route === "POST /v3/ranked/referee/verdicts") {
            if (!(await verifyHmac(request, path, body, refereeSecret))) return bad("referee authentication failed", 401);
            if (request.method === "GET") {
                const { keys } = await kv.list({ prefix: "r1:pending:" });
                const out = [];
                for (const { name } of keys.slice(0, 20)) {
                    const pend = await getJson(name);
                    const rec = pend && (await getJson(k.match(pend.roomId)));
                    if (rec) out.push({ roomId: rec.roomId, transcriptHash: rec.transcriptHash, transcript: rec.transcript });
                }
                return json({ pending: out });
            }
            let data;
            try { data = JSON.parse(body); } catch { return bad("body must be JSON"); }
            return verdict(data || {});
        }
        if (route === "POST /v3/ranked/transcripts") {
            if (!(await verifyHmac(request, path, body, relaySecret))) return bad("relay authentication failed", 401);
            let t;
            try { t = JSON.parse(body); } catch { return bad("body must be JSON"); }
            const r = await recordTranscript(t);
            return r.ok ? json(r) : bad(r.error, 409);
        }
        return bad("not found", 404);
    }

    return { fetch, recordTranscript };
}

/** Referee/relay client helper: headers for an HMAC-authenticated call. */
export async function macHeaders({ secret, method, path, body = "", now = Date.now() }) {
    const mac = await hmacHex(secret, [RANKED_VERSION, method.toUpperCase(), path, now, await sha256Hex(body)].join("\n"));
    return { "x-ic-timestamp": String(now), "x-ic-mac": mac };
}
