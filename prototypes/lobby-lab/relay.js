/**
 * relay.js — AI-097 lockstep match relay (Infinite Conquest lobby-lab).
 *
 * One relay session per v2 room (AI-096 pairing). The session is a
 * runtime-agnostic state machine: the Cloudflare Durable Object
 * (`MatchRoom`, below) drives it in production; the lab adapter drives it
 * in-process over ws-shim.js. Same protocol, same tests.
 *
 * Protocol "relay-1" is specified in docs/relay-design.md. Summary:
 * - GET /rooms/:id/ws upgrades to a WebSocket held by the room's session.
 * - Clients send intents {type:"intent", seat, actionId}; the session is
 *   the only sequencer and broadcasts {type:"intent", seq, seat, actionId}
 *   to both seats in arrival order.
 * - Seed commit-reveal gives both clients a shared seed neither picked.
 * - Per-turn state hashes are compared; mismatches are flagged, never
 *   auto-resolved.
 * - The intent log persists (reconnect resume + AI-089 replays).
 * - Turn timers forfeit idle seats.
 *
 * TRUST MODEL (read docs/relay-design.md before relying on any of this):
 * seat UUIDs are bearer tokens (as in v2); the relay never validates game
 * logic — it orders, stores and forwards. Lockstep means each client holds
 * the full match state, so a modified client can see hidden information;
 * the seed commit-reveal, per-turn hashes and flagging are the mitigations
 * until the authoritative server (AI-085). Local development only for the
 * lab shim.
 *
 * Workers-compatible: uses only Web Platform APIs plus the Durable Object
 * `MatchRoom` class. No Node APIs in this file.
 */

export const RELAY_PROTOCOL = "relay-1";
export const INTENT_LOG_CAP = 10_000;
export const MAX_FRAME_BYTES = 8 * 1024;
const SEED_PHASE_TIMEOUT_MS = 120_000;

function isUuid(value) {
    return typeof value === "string" && /^[0-9a-fA-F-]{1,64}$/.test(value);
}

function sha256Hex(bytes) {
    return crypto.subtle.digest("SHA-256", bytes).then((d) =>
        [...new Uint8Array(d)].map((b) => b.toString(16).padStart(2, "0")).join(""),
    );
}

function textEncode(s) {
    return new TextEncoder().encode(s);
}

/**
 * Create a relay session for one room.
 *
 * @param {object} opts
 * @param {{roomId:string, seatA:string, seatB:string}} opts.room v2 room seats.
 * @param {() => number} [opts.now] clock (ms).
 * @param {number} [opts.turnTimeoutMs] per-turn inactivity budget.
 * @param {(seatUuid:string, msg:object) => void} opts.send deliver one message to a seat.
 * @param {(msg:object, exceptSeat?:string) => void} opts.broadcast deliver to both seats.
 * @param {(entry:object) => void} [opts.persist] called with each appended intent.
 * @param {(reason:string, detail:object) => void} [opts.onFlag] hash mismatch etc.
 * @param {() => void} [opts.onEnd] session is over; the driver may reap it.
 */
export function createRelaySession({
    room,
    now = () => Date.now(),
    turnTimeoutMs = 120_000,
    send,
    broadcast,
    persist = () => {},
    onFlag = () => {},
    onEnd = () => {},
}) {
    if (!room || !isUuid(room.seatA) || !isUuid(room.seatB) || room.seatA === room.seatB) {
        throw new Error("createRelaySession: room with two distinct seat UUIDs is required");
    }
    if (typeof send !== "function" || typeof broadcast !== "function") {
        throw new Error("createRelaySession: send and broadcast are required");
    }
    const seats = [room.seatA, room.seatB];
    const isSeat = (u) => seats.includes(u);

    const state = {
        roomId: room.roomId,
        phase: "seed", // seed -> play -> ended
        seq: 0,
        log: [], // {seq, seat, actionId}
        connected: { [room.seatA]: false, [room.seatB]: false },
        commits: {}, // seat -> commit hex
        reveals: {}, // seat -> {seed, salt}
        seed: null,
        seedDeadline: now() + SEED_PHASE_TIMEOUT_MS,
        turn: 0,
        turnClaims: {}, // turn -> activeSeat (first claim wins)
        activeSeat: null,
        turnDeadline: null,
        warned30: false,
        warned10: false,
        pendingHashes: {}, // turn -> {seat: hash}
        ended: false,
        endReason: null,
    };

    const err = (seat, reason) => send(seat, { type: "error", reason: String(reason).slice(0, 200) });

    function end(reason, forfeitedSeat = null) {
        if (state.ended) return;
        state.ended = true;
        state.phase = "ended";
        state.endReason = reason;
        broadcast(forfeitedSeat
            ? { type: "forfeit", seat: forfeitedSeat, reason }
            : { type: "bye", reason });
        onEnd();
    }

    function checkSeedTimeout() {
        if (state.phase === "seed" && now() >= state.seedDeadline) {
            const missing = seats.filter((s) => !state.reveals[s]);
            end("no-reveal", missing[0] || null);
            return true;
        }
        return false;
    }

    function armTurnTimer() {
        state.turnDeadline = state.activeSeat ? now() + turnTimeoutMs : null;
        state.warned30 = false;
        state.warned10 = false;
    }

    /** Driver calls this on a timer (DO alarm / setInterval in the lab). */
    function tick() {
        if (state.ended) return;
        if (checkSeedTimeout()) return;
        if (state.phase !== "play" || state.turnDeadline == null) return;
        const remaining = state.turnDeadline - now();
        if (remaining <= 0) {
            end("timeout", state.activeSeat);
            return;
        }
        if (remaining <= 30_000 && !state.warned30) {
            state.warned30 = true;
            broadcast({ type: "timer", seat: state.activeSeat, msRemaining: remaining });
        } else if (remaining <= 10_000 && !state.warned10) {
            state.warned10 = true;
            broadcast({ type: "timer", seat: state.activeSeat, msRemaining: remaining });
        }
    }

    function requestHash(turn) {
        if (turn < 1) return;
        state.pendingHashes[turn] = {};
        broadcast({ type: "hash-request", turn });
    }

    async function handleSeedCommit(seat, msg) {
        if (state.phase !== "seed") return err(seat, "seed phase is over");
        const commit = msg.commit;
        if (typeof commit !== "string" || !/^[0-9a-fA-F]{64}$/.test(commit)) {
            return err(seat, "commit must be a 64-hex SHA-256 digest");
        }
        if (state.reveals[seat]) return err(seat, "already revealed; commit is locked");
        state.commits[seat] = commit.toLowerCase();
    }

    async function handleSeedReveal(seat, msg) {
        if (state.phase !== "seed") return err(seat, "seed phase is over");
        const { seed, salt } = msg;
        if (typeof seed !== "string" || typeof salt !== "string" || !seed || !salt
            || textEncode(seed).length > 128 || textEncode(salt).length > 128) {
            return err(seat, "seed and salt are required (<=128 bytes each)");
        }
        const commit = state.commits[seat];
        if (!commit) return err(seat, "commit first, then reveal");
        const digest = await sha256Hex(textEncode(seed + "|" + salt));
        if (digest !== commit) return err(seat, "reveal does not match commit");
        state.reveals[seat] = { seed, salt };
        if (seats.every((s) => state.reveals[s])) {
            const ordered = [...seats].sort();
            const finalSeed = await sha256Hex(
                textEncode(state.reveals[ordered[0]].seed + "|" + state.reveals[ordered[1]].seed),
            );
            state.seed = finalSeed;
            state.phase = "play";
            broadcast({ type: "seed", seed: finalSeed });
        }
    }

    function handleIntent(seat, msg) {
        if (state.phase !== "play") return err(seat, "match has not started (seed phase)");
        const actionId = msg.actionId;
        if (typeof actionId !== "string" || !actionId || textEncode(actionId).length > 512) {
            return err(seat, "actionId is required (<=512 bytes)");
        }
        if (state.log.length >= INTENT_LOG_CAP) {
            return err(seat, "intent log cap reached; match cannot continue");
        }
        state.seq += 1;
        const entry = { seq: state.seq, seat, actionId };
        state.log.push(entry);
        persist(entry);
        // The active seat acted: the turn timer restarts. Passing priority
        // with no action still requires an explicit pass intent, so idle is
        // never ambiguous.
        if (seat === state.activeSeat) armTurnTimer();
        broadcast({ type: "intent", ...entry });
    }

    function handleTurn(seat, msg) {
        if (state.phase !== "play") return err(seat, "match has not started (seed phase)");
        const turn = msg.turn;
        const activeSeat = msg.activeSeat;
        if (!Number.isInteger(turn) || turn < 1 || !isSeat(activeSeat)) {
            return err(seat, "turn must be a positive integer and activeSeat a room seat");
        }
        if (state.turnClaims[turn]) return; // first claim per turn wins; honest clients agree
        state.turnClaims[turn] = activeSeat;
        const prevTurn = state.turn;
        state.turn = Math.max(state.turn, turn);
        state.activeSeat = activeSeat;
        armTurnTimer();
        // The turn that just completed gets its hashes compared now.
        if (turn > prevTurn) requestHash(turn - 1);
    }

    function handleHash(seat, msg) {
        const turn = msg.turn;
        const hash = msg.hash;
        if (!Number.isInteger(turn) || turn < 1
            || typeof hash !== "string" || !/^[0-9a-fA-F]{64}$/.test(hash)) {
            return err(seat, "hash needs a positive turn and a 64-hex digest");
        }
        const pending = state.pendingHashes[turn];
        if (!pending) return err(seat, `no hash requested for turn ${turn}`);
        pending[seat] = hash.toLowerCase();
        if (seats.every((s) => pending[s])) {
            delete state.pendingHashes[turn];
            const [h1, h2] = seats.map((s) => pending[s]);
            if (h1 === h2) {
                broadcast({ type: "hash-ok", turn });
            } else {
                const detail = { turn, hashes: { [seats[0]]: h1, [seats[1]]: h2 } };
                onFlag("hash-mismatch", detail);
                broadcast({ type: "hash-mismatch", ...detail });
            }
        }
    }

    function handleHello(seat, msg) {
        const lastSeq = Number.isInteger(msg.lastSeq) && msg.lastSeq >= 0 ? msg.lastSeq : 0;
        state.connected[seat] = true;
        send(seat, {
            type: "welcome",
            protocol: RELAY_PROTOCOL,
            seat,
            roomId: state.roomId,
            phase: state.phase,
            turn: state.turn,
            seed: state.seed,
            log: state.log.filter((e) => e.seq > lastSeq),
        });
    }

    function handleMessage(seatUuid, raw) {
        if (state.ended) return;
        if (!isSeat(seatUuid)) return; // unknown seat: the driver never routes here
        let msg;
        try {
            msg = typeof raw === "string" ? JSON.parse(raw) : raw;
        } catch {
            return err(seatUuid, "message must be JSON");
        }
        if (!msg || typeof msg.type !== "string") return err(seatUuid, "message needs a type");
        if (textEncode(JSON.stringify(msg)).length > MAX_FRAME_BYTES) {
            return err(seatUuid, "message too large");
        }
        // hello is the only message allowed from a not-yet-connected seat.
        if (!state.connected[seatUuid] && msg.type !== "hello") {
            return err(seatUuid, "send hello first");
        }
        switch (msg.type) {
            case "hello": return handleHello(seatUuid, msg);
            case "seed-commit": return handleSeedCommit(seatUuid, msg);
            case "seed-reveal": return handleSeedReveal(seatUuid, msg);
            case "intent": return handleIntent(seatUuid, msg);
            case "turn": return handleTurn(seatUuid, msg);
            case "hash": return handleHash(seatUuid, msg);
            default: return err(seatUuid, `unknown message type "${msg.type.slice(0, 32)}"`);
        }
    }

    function handleConnect(seatUuid) {
        // The socket is up; the seat still hellos to (re)join.
        if (!isSeat(seatUuid)) return false;
        return true;
    }

    function handleDisconnect(seatUuid) {
        if (isSeat(seatUuid)) state.connected[seatUuid] = false;
        // A dropped socket is NOT a forfeit: the seat may reconnect with
        // hello{lastSeq} inside the turn timer. The timer keeps running —
        // a disconnected player can still time out, which is fair.
    }

    return {
        handleMessage,
        handleConnect,
        handleDisconnect,
        tick,
        end,
        snapshot() {
            return {
                roomId: state.roomId,
                phase: state.phase,
                seq: state.seq,
                logLength: state.log.length,
                turn: state.turn,
                activeSeat: state.activeSeat,
                connected: { ...state.connected },
                seedSet: state.seed !== null,
                ended: state.ended,
                endReason: state.endReason,
            };
        },
    };
}

/**
 * Cloudflare Durable Object: one per room. The DO is the single writer —
 * arrival order is the global intent order — and its storage persists the
 * intent log for reconnects and replays.
 *
 * wrangler.toml needs:
 *   [[durable_objects.bindings]]
 *   name = "MATCH_ROOM"
 *   class_name = "MatchRoom"
 *   [[migrations]] new_classes = ["MatchRoom"]
 */
export class MatchRoom {
    constructor(state, env) {
        this.state = state;
        this.env = env;
        this.sessions = new Map(); // roomId -> session (one per DO instance)
        this.sockets = new Map(); // seatUuid -> WebSocket
    }

    sessionFor(room) {
        let s = this.sessions.get(room.roomId);
        if (!s) {
            s = createRelaySession({
                room,
                send: (seat, msg) => this.sendTo(seat, msg),
                broadcast: (msg) => {
                    for (const seat of [room.seatA, room.seatB]) this.sendTo(seat, msg);
                },
                persist: (entry) => {
                    // Fire-and-forget append; ordering is by seq.
                    this.state.storage.put(`intent:${entry.seq}`, entry).catch(() => {});
                },
                onFlag: (reason, detail) => {
                    this.state.storage.put("flag", { reason, detail, at: Date.now() }).catch(() => {});
                },
                onEnd: () => this.sessions.delete(room.roomId),
            });
            this.sessions.set(room.roomId, s);
            // Turn-timer alarms: re-arm on every intent via the session.
            this.state.storage.setAlarm(Date.now() + 15_000).catch(() => {});
        }
        return s;
    }

    sendTo(seat, msg) {
        const ws = this.sockets.get(seat);
        if (!ws) return;
        try {
            ws.send(JSON.stringify(msg));
        } catch {
            this.sockets.delete(seat);
        }
    }

    async fetch(request) {
        if (request.headers.get("Upgrade") !== "websocket") {
            return new Response("expected websocket", { status: 426 });
        }
        // The worker stamps the verified room + seat on headers before
        // routing here (see createRelayRouter): it checked the v2 room
        // record, so these are authoritative for this upgrade. The room is
        // persisted to DO storage so later sockets find it.
        const roomId = request.headers.get("X-Relay-Room");
        const seat = request.headers.get("X-Relay-Seat");
        const seatA = request.headers.get("X-Relay-SeatA");
        const seatB = request.headers.get("X-Relay-SeatB");
        let room = await this.state.storage.get("room");
        if (!room) {
            if (!roomId || !seat || !seatA || !seatB) {
                return new Response("missing relay headers", { status: 400 });
            }
            room = { roomId, seatA, seatB };
            await this.state.storage.put("room", room);
        }
        if (room.roomId !== roomId) return new Response("room mismatch", { status: 400 });
        if (seat !== room.seatA && seat !== room.seatB) {
            return new Response("not a room seat", { status: 403 });
        }
        const pair = new WebSocketPair();
        const [client, server] = Object.values(pair);
        this.state.acceptWebSocket(server);
        const session = this.sessionFor(room);
        server.addEventListener("message", (ev) => session.handleMessage(seat, ev.data));
        server.addEventListener("close", () => {
            this.sockets.delete(seat);
            session.handleDisconnect(seat);
        });
        server.addEventListener("error", () => {
            this.sockets.delete(seat);
            session.handleDisconnect(seat);
        });
        this.sockets.set(seat, server);
        session.handleConnect(seat);
        return new Response(null, { status: 101, webSocket: client });
    }

    async alarm() {
        for (const s of this.sessions.values()) s.tick();
        if (this.sessions.size > 0) {
            await this.state.storage.setAlarm(Date.now() + 15_000);
        }
    }
}

/**
 * Worker-side router for the relay upgrade path.
 *
 * @param {object} opts
 * @param {(request, env) => Promise<Response>} opts.fallback downstream
 *   fetch for non-relay paths (the v2 router).
 * @param {(roomId, env) => Promise<object|null>} opts.getRoom v2 room lookup.
 * @param {string} [opts.dataVersionMin] gate for the upgrade path.
 */
export function createRelayRouter({ fallback, getRoom, dataVersionMin = "lab-2" } = {}) {
    if (typeof fallback !== "function") throw new Error("createRelayRouter: fallback is required");
    if (typeof getRoom !== "function") throw new Error("createRelayRouter: getRoom is required");

    return {
        fetch: async (request, env) => {
            const url = new URL(request.url);
            const m = url.pathname.match(/^\/rooms\/([^/]+)\/ws$/);
            if (!m || request.method !== "GET") return fallback(request, env);
            const roomId = decodeURIComponent(m[1]);

            const dataVersion = url.searchParams.get("dataVersion");
            if (dataVersion !== dataVersionMin && dataVersion !== "lab-3") {
                return new Response(JSON.stringify({ error: "dataVersion lab-2+ required on the relay path" }), {
                    status: 400,
                    headers: { "content-type": "application/json" },
                });
            }
            const seat = url.searchParams.get("seat");
            if (!isUuid(seat)) {
                return new Response(JSON.stringify({ error: "seat is required" }), {
                    status: 400,
                    headers: { "content-type": "application/json" },
                });
            }
            const room = await getRoom(roomId, env);
            if (!room) {
                return new Response(JSON.stringify({ error: "unknown or expired room" }), {
                    status: 404,
                    headers: { "content-type": "application/json" },
                });
            }
            if (seat !== room.seatA && seat !== room.seatB) {
                return new Response(JSON.stringify({ error: "not a room seat" }), {
                    status: 403,
                    headers: { "content-type": "application/json" },
                });
            }
            if (!env.MATCH_ROOM) {
                return new Response(JSON.stringify({ error: "relay not bound (MATCH_ROOM)" }), {
                    status: 501,
                    headers: { "content-type": "application/json" },
                });
            }
            const id = env.MATCH_ROOM.idFromName(roomId);
            const stub = env.MATCH_ROOM.get(id);
            // Stamp the verified room + seats; the DO re-checks storage too.
            const headers = new Headers(request.headers);
            headers.set("X-Relay-Room", roomId);
            headers.set("X-Relay-Seat", seat);
            headers.set("X-Relay-SeatA", room.seatA);
            headers.set("X-Relay-SeatB", room.seatB);
            return stub.fetch(new Request(request.url, { headers }));
        },
    };
}
