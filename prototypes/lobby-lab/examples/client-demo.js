/**
 * examples/client-demo.js — runnable integration example for LobbyClient (AI-042).
 *
 * Starts its own lab server on an ephemeral loopback port, drives the full
 * lobby lifecycle through the reusable adapter, prints each step, and exits.
 *
 * Run from prototypes/lobby-lab:  node examples/client-demo.js
 */

import { start } from "../server.js";
import { LobbyClient } from "../public/client.js";

const { port, close } = await start({ port: 0 });
console.log(`lab server: http://127.0.0.1:${port}`);

const client = new LobbyClient({ baseUrl: `http://127.0.0.1:${port}`, timeoutMs: 5000 });

console.log("\ncreateLobby ->");
const { code, hostUuid } = await client.createLobby({
    hostName: "Example Host",
    wssUrl: "wss://example.tunnel.trycloudflare.com",
    hostRating: 1050,
});
console.log(`  code=${code}`);

console.log("listLobbies ->");
for (const l of await client.listLobbies()) {
    console.log(`  ${l.code}  ${l.hostName}  rating=${l.hostRating}  ${l.wssUrl}`);
}

console.log("getLobby ->");
const one = await client.getLobby(code);
console.log(`  ${one.code}: ${one.hostName} (${one.dataVersion})`);

console.log("deleteLobby ->");
console.log(`  ok=${(await client.deleteLobby(code, hostUuid)).ok}`);

console.log("listLobbies ->", JSON.stringify(await client.listLobbies()));

await close();
console.log("\ndone.");
