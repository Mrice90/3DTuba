/**
 * Security regression tests for the lobby-lab adapter (AI-041).
 *
 * These tests pin the adapter-level mitigations for weaknesses surfaced by
 * the contract tests. They exercise the REAL HTTP server; nothing here mocks
 * the handler.
 *
 * 1. POST /report is DISABLED by default: match reports are caller-asserted
 *    UUIDs with no authentication, and a single caller can submit both
 *    "agreeing" reports to mint Elo for arbitrary UUIDs. The adapter refuses
 *    them (403) unless explicitly opted in for local contract exercise.
 * 2. Request bodies are size-limited: the adapter previously buffered
 *    unbounded bodies before the worker ever saw them (memory exhaustion).
 *    Oversized bodies now get 413.
 *
 * Run from prototypes/lobby-lab:  npm test
 */

import test from "node:test";
import assert from "node:assert/strict";
import { start } from "../server.js";

const UUID_A = "aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa";
const UUID_B = "bbbbbbbb-2222-4222-8222-bbbbbbbbbbbb";

let ctx;
const base = () => `http://127.0.0.1:${ctx.port}`;

test.before(async () => {
    ctx = await start({ port: 0 }); // defaults: reports disabled, 1MB body cap
});

test.after(async () => {
    await ctx.close();
});

async function api(path, opts = {}) {
    const res = await fetch(`${base()}${path}`, {
        headers: { "content-type": "application/json" },
        ...opts,
    });
    const json = await res.json().catch(() => ({}));
    return { status: res.status, json };
}

test("Elo fabrication is blocked: POST /report is disabled by default", async () => {
    // Attack: one caller submits both "agreeing" reports for a fake match,
    // minting Elo for UUID_A without any game being played.
    const fake = {
        matchId: "fabricated-match",
        winnerUuid: UUID_A,
        loserUuid: UUID_B,
        dataVersion: "lab-1",
    };
    const r1 = await api("/report", {
        method: "POST",
        body: JSON.stringify({ ...fake, reporterUuid: UUID_A }),
    });
    assert.equal(r1.status, 403, "first fabricated report refused");
    assert.match(r1.json.error, /disabled/i);

    const r2 = await api("/report", {
        method: "POST",
        body: JSON.stringify({ ...fake, reporterUuid: UUID_B }),
    });
    assert.equal(r2.status, 403, "second fabricated report refused");

    const rating = await api(`/rating/${UUID_A}`);
    assert.equal(rating.json.wins, 0, "no Elo minted");
    assert.equal(rating.json.rating, 1000, "rating untouched");
});

test("opt-in escape hatch: LAB-style allowReports exercises the contract", async () => {
    const lab = await start({ port: 0, allowReports: true });
    try {
        const url = `http://127.0.0.1:${lab.port}`;
        const post = async (body) => {
            const res = await fetch(`${url}/report`, {
                method: "POST",
                headers: { "content-type": "application/json" },
                body: JSON.stringify(body),
            });
            return { status: res.status, json: await res.json().catch(() => ({})) };
        };
        const r1 = await post({
            matchId: "optin-match", reporterUuid: UUID_A,
            winnerUuid: UUID_A, loserUuid: UUID_B, dataVersion: "lab-1",
        });
        assert.equal(r1.status, 200);
        assert.equal(r1.json.applied, false);
        const r2 = await post({
            matchId: "optin-match", reporterUuid: UUID_B,
            winnerUuid: UUID_A, loserUuid: UUID_B, dataVersion: "lab-1",
        });
        assert.equal(r2.json.applied, true, "contract still exercisable when opted in");
    } finally {
        await lab.close();
    }
});

test("oversized request bodies are rejected with 413", async () => {
    const big = "x".repeat(2 * 1024 * 1024); // 2MB > 1MB cap
    let status = null;
    let json = {};
    try {
        const res = await fetch(`${base()}/lobbies`, {
            method: "POST",
            headers: { "content-type": "application/json" },
            body: JSON.stringify({ hostUuid: UUID_A, hostName: "Big", wssUrl: "wss://x/", pad: big }),
        });
        status = res.status;
        json = await res.json().catch(() => ({}));
        await res.arrayBuffer().catch(() => {});
    } catch (e) {
        // Connection reset by the server is also an acceptable refusal,
        // but we prefer a clean 413; fail loudly if we cannot tell.
        assert.fail(`expected a 413 response, fetch threw: ${e.message}`);
    }
    assert.equal(status, 413, "oversized body refused");
    assert.match(json.error || "", /too large/i);
});

test("normal-sized bodies still pass through untouched", async () => {
    const r = await api("/lobbies", {
        method: "POST",
        body: JSON.stringify({
            hostUuid: UUID_A, hostName: "Normal", hostRating: 1000,
            wssUrl: "wss://normal.tunnel.trycloudflare.com", dataVersion: "lab-1",
        }),
    });
    assert.equal(r.status, 200);
    assert.match(r.json.code, /^[A-Z0-9]{6}$/);
    await api(`/lobbies/${r.json.code}`, {
        method: "DELETE", body: JSON.stringify({ hostUuid: UUID_A }),
    });
});

test("error responses never leak stack traces", async () => {
    const r = await api("/lobbies", { method: "POST", body: "{broken" });
    assert.equal(r.status, 400);
    assert.ok(!JSON.stringify(r.json).includes("at "), "no stack trace in error body");
});
