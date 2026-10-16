# Ranked foundation (AI-010 / AI-011)

Claude Code, 2026-10-15. This is the lab implementation in `../ranked.js`, with tests in `../test/ranked.test.js`. It adds a `result` message and a transcript hook to `../relay.js`. A referee loop lives in `../referee-runner.js`, and a file-backed store in `../kv-file.js`. The lab server routes `/v3/*` to it.

## 1. Gap assessment (current code, before this change)

| Area | What exists | Why it isn't enough for ranked |
|---|---|---|
| Identity | v1 `upstream/worker.js` and v2 use caller-supplied UUIDs. Relay seats are bearer tokens. | Anyone holding a UUID is that player. Nothing ties a rating to a person or device. |
| Results | v1 `/report` applies Elo when both clients agree; the lab disables it (AI-041). v2 `/v2/results` gives HMAC receipts but applies no Elo. | Two clients agreeing proves nothing: one person can run both clients, or a modified client can lie. The backlog rules this out ("do not treat agreement between two untrusted clients as proof"). |
| Match record | The relay sequences every intent, checks per-turn hashes and keeps the log for reconnects. | It had no end-of-game message and no way to hand the log to anything. So no server-side record existed to verify a result against. |
| Verification | Deterministic rules core (Java bridge; native C# core AI-087), the same seed giving the same events. | Nothing replays a finished match on the server side. |
| Exactly-once | v1 marks a report as applied in KV. | Workers KV is eventually consistent, so two near-simultaneous reports can both apply. |
| Leaderboard | v1 `/leaderboard` and `/rating/:uuid` are live on the alpha Worker. | They are keyed by unauthenticated UUIDs and fed by agreement-only results. |
| Factions | Zeus and Poseidon are free (AI-012). Entitlements (AI-021) are not built. | Nothing stops a client from declaring an unowned faction in a ranked match. |

The released 0.4.5 online build uses Codex's local net-lab relay. That is a separate copy from this repo's lobby-lab, and only its file list was checked here. Section 5 says what has to carry over.

## 2. Design

The main idea: the relay is our own server and the only sequencer, so its record of the match can be trusted, while the clients cannot. A result comes from replaying that record, never from what the clients say.

1. **Identity (`POST /v3/players`).** A player is an Ed25519 key pair made on the device. The id is the first 32 hex characters of SHA-256 of the public key. Registration has to be signed by that key (proof of possession). Every ranked call carries `x-ic-player`, `x-ic-timestamp`, `x-ic-nonce` and `x-ic-signature`, signed over `ranked-1\nMETHOD\npath\ntimestamp\nnonce\nsha256(body)`. The server refuses unknown keys, bad signatures, timestamps more than 5 minutes off, and reused nonces (kept for 15 minutes, which is longer than the skew window).
2. **Ranked queue (`POST`/`GET /v3/ranked/queue`).** The server pairs two signed players. It mints a fresh seat UUID for each and returns each seat only to its owner. A player never sees the opponent's seat, so the relay's bearer seat now stands for a signed player. Each player's faction is checked against `isEntitled` (base factions only until AI-021 exists). The room is also written as a v2 room, so the existing relay upgrade path accepts it unchanged.
3. **Transcript.** The relay ends a session on a forfeit (timeout, no-reveal) or when both seats send `result {winnerSeat, finalHash}`. If only one seat sends it, the session ends 60 s later as `result-single`; if the two disagree, it ends as `result-disputed`. At the end it calls `onTranscript` with `{protocol, roomId, seats, seed, intents[], turn, hashOk[], flags[], results{}, endReason, forfeitSeat, endedAt}`. The service stores it with `transcriptHash = SHA-256(canonical JSON)`.
4. **Outcome rules.**
   - Any hash mismatch or a disputed result: **flagged**, never rated, kept for review.
   - A seat that never reveals its seed: rated at once (no game was played, nothing to replay).
   - A turn timeout: pending for the referee. If the replayed game is finished, its winner stands (a client that never sends `result` makes the relay time out after game over); if not, the timed-out seat loses.
   - Anything else: **pending** until a referee verdict.
5. **Referee (`GET /v3/ranked/referee/pending`, `POST /v3/ranked/referee/verdicts`).** A process holding `LAB_REFEREE_SECRET` (HMAC over method, path, timestamp and body hash) replays seed plus intents in the rules engine. It posts `{roomId, transcriptHash, legal, winnerSeat | offendingSeat, finalStateHash, engine}`. A verdict for a different transcript hash is refused. If the replay finds an illegal intent, the offending seat loses (only a modified client can send one). A claim that contradicts the verdict adds to the claimant's `disputesLost`, as a signal for later moderation.
6. **Exactly once.** A match is rated at most once. The same verdict again returns `duplicate: true` and changes nothing; a different verdict gets 409. Every write goes through one serialized writer. In production that writer is a **RankedLedger Durable Object** with transactional storage, because KV cannot guarantee this.
7. **Ratings and leaderboard (`GET /v3/ranked/leaderboard`, `GET /v3/ranked/players/:id`).** Ratings use Elo starting at 1200, with K = 40 for a player's first 10 games and K = 32 after. Records hold wins, losses, games, `disputesLost` and a provisional flag. Responses carry `beta: true` until the authoritative server (AI-085).

## 3. Acceptance mapping (AI-010 / AI-011)

| Backlog requirement | Test in `test/ranked.test.js` |
|---|---|
| Impersonation fails | "impersonation fails: wrong key, tampered body, unknown player, missing headers"; "registration needs proof of possession" |
| Replayed results and requests fail | "replayed and stale requests are refused"; transcript replay no-op in the exactly-once test |
| Forged results fail | "forged verdicts and forged transcripts are refused" |
| Duplicate results fail | "... a referee verdict does, exactly once" (same verdict no-op, conflicting verdict 409, games = 1) |
| Agreement alone is not proof | same test: two agreeing claims leave ratings at 1200 |
| Unauthorized faction use fails | "unauthorized faction use is refused" |
| Desync never counts | "hash-mismatch matches are flagged and never rated"; "disagreeing result claims ... disputed" |
| Persistent leaderboard across restart | "ratings, settled matches and used nonces survive a restart" (file-backed KV) |
| End to end | "lab server: /v3 over HTTP, relay transcript hook, referee runner rates the match" |

Run them from `prototypes/lobby-lab` with `npm test` (87 tests, no dependencies, Node 22+ for WebCrypto Ed25519).

## 3b. Online match, referee interface and Cloudflare config (2026-10-15, second pass)

- **Two-client online test.** `lockstep-client.js` is a headless online client: signed registration, ranked queue, relay seed commit-reveal, intents applied in relay order to its own engine, turn claims, per-turn hash answers and a result claim. `test/online-match.test.js` runs two of them as separate processes against the lab server and checks identical final hashes, zero mismatches, and a single rating from the referee's replay. `deploy/cloudflare/e2e-wrangler.mjs` plays the same match in the real Cloudflare runtime (`wrangler dev`).
- **Referee interface.** `referee-engine.js` turns any engine into the referee's `replay`: an intent the engine refuses loses the match for its seat; an unfinished game counts only as a concession (a seat whose result claim names its opponent), otherwise the verdict is `unfinished` and the ledger marks the match `void` (settled, never rated). Engines plug in in-process or as a subprocess speaking `ic-engine-1` (`engine/README.md`). The native C# host for `ic-engine-1` is the remaining step.
- **Seat order.** `matchView` now returns `side`: side A is engine player 0, side B player 1, matching the transcript's `seats` order the referee replays in.
- **Cloudflare.** `deploy/cloudflare/` holds the Worker entry, `wrangler.toml` (SQLite-backed `MatchRoom` and `RankedLedger` Durable Objects, rate limit, secrets list), deploy steps and free-plan budget. Not deployed. The run in workerd found and fixed a relay bug: `MatchRoom` used the hibernation `acceptWebSocket` with `addEventListener`, so no message ever arrived in production.

## 4. What this does not do yet (open, in order)

1. **Real referee engine host: built (2026-10-16).** `tools/referee-host` serves the native C# core over `ic-engine-1`, starting matches exactly as the Unity online client does. In online mode setup choices travel as intents (no deck, capital or faction choice in `new`), so seed plus intents is enough. Two clients played a full real-rules match and the native referee reproduced the final hash and rated it. It is pinned to the shipped 0.4.5 rules (`tools/referee-host/rules/0.4.5`, checksums match the release; 0.4.5's online `new` and seed mapping confirmed from its compiled client). 0.4.5 also negotiates tile cosmetics over its relay before the seed; that is cosmetic only and does not enter the rules. **Client requirement found:** the Unity online client (`OnlineMatch.cs`) never sends `result` at game over, so the relay ends such matches by turn timeout. Timeouts now go to the referee instead of being rated at once, and the replay's winner stands, but the client should still send `result {winnerSeat, finalHash}` at game over so matches settle in seconds, not after the turn timer.
2. **Hosting at $0.** Matches still play over the relay; only rating waits on the referee. The referee can run as a scheduled GitHub Actions job or on Mathew's PC. Results would show as "verifying" for a few minutes.
3. **Production deploy** (needs Mathew): the RankedLedger Durable Object, secrets (`LAB_REFEREE_SECRET`, `LAB_RELAY_SECRET`), the relay Durable Object calling `POST /v3/ranked/transcripts` with the relay HMAC, rate limits, and a check of current Workers and Durable Objects free-tier limits.
4. **Unity client.** Generate and store the key pair (Windows DPAPI-protected file), sign calls, add the ranked queue UI, send `result` at game over, and show "verifying" and beta labels. This touches the online/menu files that belong to the AI-097 owner.
5. **Accounts (AI-015).** Linking keys to an account, recovery after a lost key, multiple devices, and name moderation.
6. **Known limits of beta.** Lockstep clients still hold hidden information, so a modified client can see the opponent's hand. Win-trading between colluding accounts needs detection later. The referee proves the moves were legal, not that a human made them.

## 5. Carry-over to the 0.4.5 net-lab relay

The relay change is additive: a new `result` message, the `onTranscript` option and `transcript()`, with protocol `relay-1` unchanged for existing clients. Porting it to Codex's net-lab copy means applying the same `handleResult` / `transcript` / `end` hook. That copy is under Codex's AI-097-RELAY-RESUME-HARDENING claim (2026-10-15), so the port waits for that claim to be released.
