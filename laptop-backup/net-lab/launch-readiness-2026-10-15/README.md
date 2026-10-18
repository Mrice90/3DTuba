# Native ranked referee and launch integration candidate

Local candidate for review; not deployed. The installed game and all prior sources remain unchanged.

## Contents

- `net-lab/`: reconnect-hardened 0.4.5 relay merged with PR22 ranked foundation, native replay adapter, and regression tests.
- `native/`: .NET 8 protocol runner plus the exact native rules DLLs and data from the current 37-hex game. `manifest.json` pins all 19 inputs. Both rules DLLs are byte-identical to the October 15 Poseidon 30K Windows player.
- Native profile supported: purchase-v3-hex37, Zeus at seat zero, Poseidon at seat one, default capitals and faction starter decks. Other setups remain unresolved rather than receiving a guessed verdict.

## Run tests on this workstation

From `net-lab`, set `IC_DOTNET` to `C:/Program Files/Unity/Hub/Editor/6000.6.3f1/Editor/Data/DotNetSdk/dotnet.exe`, then run `node --test --test-reporter=tap "test/**/*.test.js"`. Node must be able to bind and connect to loopback. Test secrets and generated test identities are only local fixtures.

For an integration match using the latest existing player: `node test-unity-online.mjs`. It launches two hidden test players, saves logs under `reviews/launch-readiness-2026-10-15/`, and closes its own processes. This test does not establish two-PC or player reconnect acceptance.

For local service exercise, configure `LAB_RANKED_KV` to an absolute writable JSON-file path and supply distinct `LAB_REFEREE_SECRET` and `LAB_RELAY_SECRET` through the environment, then run `node server.js`. No secrets are bundled. Keep the same secrets across runs. Without the file variable the service is ephemeral. Only one server process may open a file store.

With the same referee secret and `IC_DOTNET`, run `node referee-runner.js --base http://127.0.0.1:8787 --engine ./native-referee.js`. This processes pending transcripts once; it does not install a scheduler. Engine/process/protocol failures leave matches pending. Complete legal transcripts use the engine's winner and hash; illegal actions identify the submitting seat. Timeout claims alone no longer award ratings.

## Integration notes

PR22 source pin: `9ec76530f15dd89629b0959b5d0ee0bc6c0ce960`. Original server/relay are preserved under `net-lab/pr22-baseline/`. Reconnect input is `worktrees/relay-resume-2026-10-15/net-lab`. The deliverable is a complete reviewed candidate, not a blind overlay onto the older PR: retain the already-tested cosmetics, exact-sequence checkpoints, socket identity guards and terminal-room behavior.

Ratings now require storage with an atomic `batch` operation. Both bundled stores provide it. A production Durable Object adapter must implement a real storage transaction; sequential KV puts are insufficient. The file store flushes the temporary file and renames before publishing new in-memory state. It is a single-process lab store, not a distributed database or a power-loss certification.

The replay fixtures are extracted from two saved real Unity sessions (374 and 521 actions). Their seed suffixes are normalized because this client uses only the first eight hex digits; the known engine seed, action order, expected winner and exact final hash are preserved. These are replay fixtures, not newly generated matches.

## Public release gates

Do not expose this lab server publicly. Seat UUIDs are still bearer credentials and are disclosed in relay messages; signed HTTP accounts do not secure WebSocket seat possession. Lockstep clients possess hidden game state. Production needs authenticated socket binding with separate public seat IDs, server-authoritative gameplay/redaction, trusted timeout adjudication, durable active matches, complete content/setup negotiation, Unity account/queue/result integration and two-machine acceptance. The Windows game does not yet submit ranked results itself; the HTTP referee test exercises that missing boundary with a harness.

Full findings and current project priorities: `../../reviews/launch-readiness-2026-10-15/DELIVERY.md`.
