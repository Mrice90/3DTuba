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
   - Timeout or no-reveal forfeit seen by the relay: rated at once (the relay saw who left).
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

## 4. What this does not do yet (open, in order)

1. **Real referee engine adapter.** `referee-runner.js` takes any `replay({seed, seats, intents})`, and the tests use a fake engine. The real one needs a headless replay entry point in the native C# core (or the Java bridge): match setup plus seed plus ordered intents in, then legality, winner and final state hash out. If capital, deck or tile choices are not sent as relay intents, the transcript also needs a setup record. Find out how the 0.4.5 native protocol carries them before building the adapter.
2. **Hosting at $0.** Matches still play over the relay; only rating waits on the referee. The referee can run as a scheduled GitHub Actions job or on Mathew's PC. Results would show as "verifying" for a few minutes.
3. **Production deploy** (needs Mathew): the RankedLedger Durable Object, secrets (`LAB_REFEREE_SECRET`, `LAB_RELAY_SECRET`), the relay Durable Object calling `POST /v3/ranked/transcripts` with the relay HMAC, rate limits, and a check of current Workers and Durable Objects free-tier limits.
4. **Unity client.** Generate and store the key pair (Windows DPAPI-protected file), sign calls, add the ranked queue UI, send `result` at game over, and show "verifying" and beta labels. This touches the online/menu files that belong to the AI-097 owner.
5. **Accounts (AI-015).** Linking keys to an account, recovery after a lost key, multiple devices, and name moderation.
6. **Known limits of beta.** Lockstep clients still hold hidden information, so a modified client can see the opponent's hand. Win-trading between colluding accounts needs detection later. The referee proves the moves were legal, not that a human made them.

## 5. Carry-over to the 0.4.5 net-lab relay

The relay change is additive: a new `result` message, the `onTranscript` option and `transcript()`, with protocol `relay-1` unchanged for existing clients. Porting it to Codex's net-lab copy means applying the same `handleResult` / `transcript` / `end` hook. That copy is under Codex's AI-097-RELAY-RESUME-HARDENING claim (2026-10-15), so the port waits for that claim to be released.
# Candidate update — October 15

This inherited PR22 design is historical. The enclosing candidate now adds native replay and atomic rating settlement, removes automatic timeout Elo, fixes faction/seat order, and preserves reconnect/cosmetic improvements. See `../../README.md` and the launch-readiness delivery report for the current behavior and public-security gates. The production deployment and Unity integration sections below remain unfulfilled.

