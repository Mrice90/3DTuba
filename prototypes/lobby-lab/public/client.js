/**
 * client.js — reusable JavaScript adapter for the lobby-lab local service (AI-042).
 *
 * No dependencies. Works in modern browsers and Node 18+.
 *
 *   import { LobbyClient, LobbyError } from "./client.js";
 *   const client = new LobbyClient({ baseUrl: "http://127.0.0.1:8787" });
 *   const lobbies = await client.listLobbies();
 *
 * Design notes:
 * - baseUrl must be a loopback URL (127.0.0.1, ::1, localhost) unless you
 *   pass { allowRemote: true }. This adapter is for the local dev service;
 *   the guard prevents accidentally pointing it at a production host.
 * - Every method accepts { signal } for caller cancellation and enforces a
 *   configurable timeout (default 8s).
 * - Failures throw LobbyError with a human-readable message, the HTTP
 *   status when one exists, and a machine-readable code.
 */

/**
 * @typedef {object} LobbySummary
 * @property {string} code        6-char uppercase lobby code
 * @property {string} hostName    display name (sanitized, <= 24 chars)
 * @property {number} hostRating  Elo rating at creation
 * @property {string} wssUrl      host tunnel URL (wss:// hostname)
 * @property {string} [dataVersion]
 */

/**
 * @typedef {object} CreateLobbyRequest
 * @property {string} hostName
 * @property {string} wssUrl      must be wss:// with a hostname (no IP literals)
 * @property {number} [hostRating=1000]
 * @property {string} [dataVersion="lab-1"]
 */

/**
 * @typedef {object} QueuePollResult
 * @property {"waiting"|"host"|"ready"} status
 * @property {string} [reason]          set when waiting after expiry/cancel
 * @property {string} [opponentUuid]    set when status === "host"
 * @property {string} [opponentName]   set when status === "host" | "ready"
 * @property {number} [opponentRating] set when status === "host" | "ready"
 * @property {string} [wssUrl]         set when status === "ready"
 * @property {string} [code]           set when status === "ready"
 */

export class LobbyError extends Error {
    /**
     * @param {string} message human-readable description
     * @param {object} [opts]
     * @param {number|null} [opts.status] HTTP status, if the server responded
     * @param {string} [opts.code] machine-readable code:
     *   "http" | "unreachable" | "timeout" | "cancelled" | "invalid-response"
     */
    constructor(message, { status = null, code = "http", cause } = {}) {
        super(message, cause ? { cause } : undefined);
        this.name = "LobbyError";
        this.status = status;
        this.code = code;
    }
}

function isLoopbackHostname(hostname) {
    const h = hostname.toLowerCase().replace(/^\[|\]$/g, "");
    return h === "127.0.0.1" || h === "::1" || h === "localhost";
}

function combineSignals(signals) {
    const live = signals.filter(Boolean);
    if (live.length === 0) return undefined;
    if (live.length === 1) return live[0];
    if (typeof AbortSignal.any === "function") return AbortSignal.any(live);
    // Fallback for runtimes without AbortSignal.any.
    const ctrl = new AbortController();
    for (const s of live) {
        if (s.aborted) {
            ctrl.abort(s.reason);
            break;
        }
        s.addEventListener("abort", () => ctrl.abort(s.reason), { once: true });
    }
    return ctrl.signal;
}

export class LobbyClient {
    /**
     * @param {object} [opts]
     * @param {string} [opts.baseUrl="http://127.0.0.1:8787"]
     * @param {number} [opts.timeoutMs=8000] per-request timeout
     * @param {boolean} [opts.allowRemote=false] skip the loopback guard
     * @param {typeof fetch} [opts.fetchImpl=globalThis.fetch]
     */
    constructor({ baseUrl = "http://127.0.0.1:8787", timeoutMs = 8000, allowRemote = false, fetchImpl = globalThis.fetch } = {}) {
        let url;
        try {
            url = new URL(baseUrl);
        } catch {
            throw new LobbyError(`invalid baseUrl: ${baseUrl}`, { code: "invalid-response" });
        }
        if (!allowRemote && !isLoopbackHostname(url.hostname)) {
            throw new LobbyError(
                `refusing non-loopback baseUrl "${baseUrl}": this adapter targets the local lab service; pass allowRemote:true to override`,
                { code: "unreachable" },
            );
        }
        this.baseUrl = url.toString().replace(/\/+$/, "");
        this.timeoutMs = timeoutMs;
        this.fetchImpl = fetchImpl;
    }

    async #request(path, { method = "GET", body, signal } = {}) {
        const timeoutSignal = typeof AbortSignal.timeout === "function"
            ? AbortSignal.timeout(this.timeoutMs)
            : undefined;
        let timeoutId;
        let effectiveSignal = combineSignals([signal, timeoutSignal]);
        if (!timeoutSignal) {
            // Manual timeout for runtimes without AbortSignal.timeout.
            const ctrl = new AbortController();
            timeoutId = setTimeout(() => ctrl.abort(new Error("timeout")), this.timeoutMs);
            effectiveSignal = combineSignals([signal, ctrl.signal]);
        }
        let res;
        try {
            res = await this.fetchImpl(`${this.baseUrl}${path}`, {
                method,
                headers: { "content-type": "application/json" },
                body: body === undefined ? undefined : JSON.stringify(body),
                signal: effectiveSignal,
            });
        } catch (err) {
            if (err?.name === "AbortError" || err?.name === "TimeoutError") {
                if (signal?.aborted) {
                    throw new LobbyError("request cancelled", { code: "cancelled", cause: err });
                }
                throw new LobbyError(`request timed out after ${this.timeoutMs}ms`, { code: "timeout", cause: err });
            }
            throw new LobbyError(
                `could not reach the lobby service at ${this.baseUrl} (${err?.message || err})`,
                { code: "unreachable", cause: err },
            );
        } finally {
            if (timeoutId) clearTimeout(timeoutId);
        }

        let data = null;
        const text = await res.text().catch(() => "");
        if (text) {
            try {
                data = JSON.parse(text);
            } catch {
                throw new LobbyError(
                    `invalid response from ${path}: expected JSON, got ${text.slice(0, 80)}`,
                    { status: res.status, code: "invalid-response" },
                );
            }
        }
        if (!res.ok) {
            const detail = data && typeof data.error === "string" ? `: ${data.error}` : "";
            throw new LobbyError(`request failed (${res.status})${detail}`, { status: res.status, code: "http" });
        }
        return data;
    }

    /** @returns {Promise<LobbySummary[]>} */
    listLobbies({ signal } = {}) {
        return this.#request("/lobbies", { signal });
    }

    /**
     * @param {CreateLobbyRequest} req
     * @returns {Promise<{code: string}>}
     */
    async createLobby(req, { signal } = {}) {
        if (!req || typeof req.hostName !== "string" || !req.hostName.trim()) {
            throw new LobbyError("createLobby requires hostName", { code: "invalid-response" });
        }
        if (typeof req.wssUrl !== "string" || !req.wssUrl.trim()) {
            throw new LobbyError("createLobby requires wssUrl", { code: "invalid-response" });
        }
        const hostUuid = crypto.randomUUID();
        const data = await this.#request("/lobbies", {
            method: "POST",
            body: {
                hostUuid,
                hostName: req.hostName.trim(),
                hostRating: req.hostRating ?? 1000,
                wssUrl: req.wssUrl.trim(),
                dataVersion: req.dataVersion ?? "lab-1",
            },
            signal,
        });
        return { code: data.code, hostUuid };
    }

    /** @returns {Promise<LobbySummary>} */
    getLobby(code, { signal } = {}) {
        return this.#request(`/lobbies/${encodeURIComponent(code)}`, { signal });
    }

    /** @returns {Promise<{ok: true}>} */
    deleteLobby(code, hostUuid, { signal } = {}) {
        return this.#request(`/lobbies/${encodeURIComponent(code)}`, {
            method: "DELETE",
            body: { hostUuid },
            signal,
        });
    }

    /** @returns {Promise<{queued: true}>} */
    enqueue({ uuid, name, rating, dataVersion }, { signal } = {}) {
        return this.#request("/queue", {
            method: "POST",
            body: { uuid, name, rating, dataVersion },
            signal,
        });
    }

    /** @returns {Promise<QueuePollResult>} */
    pollQueue(uuid, { signal } = {}) {
        return this.#request(`/queue/poll?uuid=${encodeURIComponent(uuid)}`, { signal });
    }

    /** @returns {Promise<{ok: true}>} */
    publishPairing({ hostUuid, forUuid, wssUrl, code, hostName, hostRating }, { signal } = {}) {
        return this.#request("/pair", {
            method: "POST",
            body: { hostUuid, forUuid, wssUrl, code, hostName, hostRating },
            signal,
        });
    }

    /** @returns {Promise<{ok: true}>} */
    leaveQueue(uuid, { signal } = {}) {
        return this.#request(`/queue?uuid=${encodeURIComponent(uuid)}`, { method: "DELETE", signal });
    }
}
