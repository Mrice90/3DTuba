/**
 * demo.js — scripted two-client lobby lifecycle against the lab server (AI-040).
 *
 * Starts its own server on an ephemeral loopback port, runs a successful
 * lifecycle with two independent clients, prints each step with the expected
 * outcome, then shuts down cleanly.
 *
 * Run: node demo.js   (from prototypes/lobby-lab)
 */

import { start } from "./server.js";

const UUID_A = "aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa"; // host (client A)
const UUID_B = "bbbbbbbb-2222-4222-8222-bbbbbbbbbbbb"; // observer (client B)

let failures = 0;
function check(label, actual, expected) {
    const ok = actual === expected;
    if (!ok) failures++;
    console.log(`  ${ok ? "PASS" : "FAIL"}  ${label}: got ${JSON.stringify(actual)}, expected ${JSON.stringify(expected)}`);
}

const { port, close } = await start({ port: 0 });
const base = `http://127.0.0.1:${port}`;
console.log(`lobby-lab demo server on ${base}\n`);

async function api(path, opts = {}) {
    const res = await fetch(`${base}${path}`, {
        headers: { "content-type": "application/json" },
        ...opts,
    });
    return { status: res.status, json: await res.json().catch(() => ({})) };
}

console.log("[1] Client B lists lobbies on an empty server");
{
    const r = await api("/lobbies");
    check("status", r.status, 200);
    check("empty list", JSON.stringify(r.json), "[]");
}

console.log("[2] Client A (host) creates a named lobby");
let code;
{
    const r = await api("/lobbies", {
        method: "POST",
        body: JSON.stringify({
            hostUuid: UUID_A, hostName: "Demo Host", hostRating: 1100,
            wssUrl: "wss://demo.tunnel.trycloudflare.com", dataVersion: "lab-1",
        }),
    });
    check("status", r.status, 200);
    check("code shape", /^[A-Z0-9]{6}$/.test(r.json.code), true);
    code = r.json.code;
    console.log(`      lobby code: ${code}`);
}

console.log("[3] Client B refreshes and sees the lobby (discovery is public)");
{
    const r = await api("/lobbies");
    check("status", r.status, 200);
    check("one lobby listed", r.json.length, 1);
    check("host name visible", r.json[0].hostName, "Demo Host");
}

console.log("[4] Client B tries to remove A's lobby (not the owner)");
{
    const r = await api(`/lobbies/${code}`, {
        method: "DELETE", body: JSON.stringify({ hostUuid: UUID_B }),
    });
    check("status (ownership enforced)", r.status, 403);
}

console.log("[5] Client A removes its own lobby");
{
    const r = await api(`/lobbies/${code}`, {
        method: "DELETE", body: JSON.stringify({ hostUuid: UUID_A }),
    });
    check("status", r.status, 200);
    check("ok", r.json.ok, true);
}

console.log("[6] Client B lists again: lobby is gone");
{
    const r = await api("/lobbies");
    check("empty list", JSON.stringify(r.json), "[]");
}

await close();
console.log(failures === 0 ? "\nDemo complete: all expected outcomes matched." : `\nDemo complete with ${failures} mismatch(es).`);
// Let pending HTTP handles finish closing before Node shuts down on Windows.
process.exitCode = failures === 0 ? 0 : 1;
