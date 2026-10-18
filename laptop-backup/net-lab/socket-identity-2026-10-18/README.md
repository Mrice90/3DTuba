# Ranked socket identity candidate — October 18, 2026

Isolated successor to the released October 15 launch-readiness candidate. Local only; no Unity client or hosted service changed.

Ranked rooms now require an Ed25519-signed WebSocket upgrade. Reuse `signRequest` from `net-lab/ranked.js` with method `GET`, empty body, and the exact request pathname plus query, for example `/rooms/ROOM/ws?seat=SEAT&dataVersion=lab-2`. Use the exact dataVersion returned by matchmaking. Send the four returned `x-ic-*` headers on the upgrade. Use a fresh nonce and timestamp for every connection/reconnect. Matchmaking advertises `socketAuth: signed-upgrade-1`.

The account must own the requested seat. Verification happens before socket upgrade, session creation or replacement. Public seat IDs remain in relay messages; they confer no ranked connection authority. The existing replay guard serializes nonce consumption. Active-room reconnect still works after the five-minute rendezvous expiry. Stored ranked records protect pre-marker rooms; marked rooms with missing ranked records fail closed. Legacy local casual clients retain their old protocol.

Browser WebSocket APIs cannot set these headers; a browser client needs a separately designed short-lived ticket exchange. Native Unity clients need account key management and signed upgrade integration before using ranked rooms. This is intentionally a required change for ranked clients, not an optional security toggle. The generic Cloudflare MatchRoom adapter is unchanged and must not be presented as secured by this local adapter patch.

Run from `net-lab` in PowerShell:

```powershell
$env:IC_DOTNET = 'C:/Program Files/Unity/Hub/Editor/6000.6.3f1/Editor/Data/DotNetSdk/dotnet.exe'
node --test --test-reporter=tap 'test/**/*.test.js'
```

The adjacent `native` directory is an unchanged copy of the pinned October 15 runtime. Its manifest is checked by the native replay tests. The suite needs loopback access.

This does not secure hidden game state, client-influenced timers, production persistence or deployment. Hosted transport must use TLS and implement the same authentication boundary. Casual host/join, Unity reconnect, cross-PC testing, rig/audio integration and release acceptance remain open. Earlier sources and game packages are preserved.
