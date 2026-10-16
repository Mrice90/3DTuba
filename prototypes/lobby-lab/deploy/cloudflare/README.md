# Ranked beta on Cloudflare (ready, not deployed)

This folder deploys the lab's relay and ranked service as one Cloudflare
Worker, `infinite-conquest-ranked-beta`. It is separate from the live alpha
lobby Worker, which keeps running untouched. **Nothing here has been
deployed.** Deploying needs Matt's go-ahead and his Cloudflare login.

| Piece | Where it runs |
|---|---|
| `GET /rooms/:id/ws` lockstep relay | `MatchRoom` Durable Object, one per room (`../../relay.js`) |
| `/v3/*` identities, ranked queue, transcripts, verdicts, ratings | `RankedLedger` Durable Object, one global instance (`worker.js`) |
| `/v2/*` and the v1 routes (casual) | the Worker itself over the `IC_KV` namespace |

The ledger keeps everything in its own strongly consistent storage
(`do-storage-kv.js`), so ratings apply exactly once. When a relay session
ends, the `MatchRoom` posts the transcript to the ledger, signed with
`LAB_RELAY_SECRET`. The referee polls the ledger with `LAB_REFEREE_SECRET`.

## Checked locally in the real runtime

`e2e-wrangler.mjs` starts this Worker under `wrangler dev` (workerd, the
Cloudflare runtime), plays a full ranked match between two headless clients
in separate processes, runs the referee and checks the leaderboard. Last run
2026-10-15: 13 turns, 22 intents, 12 per-turn hash checks passed, winner
rated 1220 and loser 1180. `wrangler deploy --dry-run` bundles cleanly
(67 KiB, 16 KiB gzipped).

That run also found a bug in `MatchRoom`: it accepted sockets with the
hibernation API (`state.acceptWebSocket`) but listened with
`addEventListener`, so in the real runtime no relay message ever arrived and
the match hung. It now uses `server.accept()`.

## Deploy steps (for when Matt says go)

From this folder, logged in with `npx wrangler login`:

1. `npx wrangler kv namespace create IC_KV`, then paste the id into `wrangler.toml`.
2. Set the three secrets, each a fresh random value (`openssl rand -hex 32`):
   `npx wrangler secret put LAB_REFEREE_SECRET`, `LAB_RELAY_SECRET`, `LAB_V2_SECRET`.
3. `npx wrangler deploy`.
4. Smoke test: `curl https://infinite-conquest-ranked-beta.<account>.workers.dev/v3/ranked/leaderboard`
   should return `{"beta":true,...,"rows":[]}`.
5. Give the referee host `LAB_REFEREE_SECRET` and the Worker URL (see `referee-workflow.yml`).

To take it down: `npx wrangler delete`.

## Free plan budget (Workers Free, checked 2026-10-15)

From Cloudflare's [Durable Objects pricing page](https://developers.cloudflare.com/durable-objects/platform/pricing):
100,000 Durable Object requests a day, 13,000 GB-s of duration a day,
5 million SQLite rows read and 100,000 written a day, 5 GB stored. Only
SQLite-backed Durable Objects are available on the free plan, which is what
`wrangler.toml` declares. Workers KV on the free plan allows far fewer
writes (1,000 a day), so ranked state stays out of KV.

What one ranked match costs, roughly: about 15 ledger requests (register,
queue polls, transcript, verdict), one relay object, and relay WebSocket
messages billed at 20 to one request. The relay keeps its sockets with
`accept()`, which bills duration for the whole match: at 128 MB, 13,000 GB-s
is about 28 hours of open match time a day, so roughly 80 twenty-minute
matches a day before the free limit. Moving the relay to the hibernation API
(saving session state to storage between messages) removes that limit; it is
the first thing to do if beta traffic gets near it. Exceeding a free limit
makes calls fail until 00:00 UTC; it never charges.

## Not covered yet

- The referee needs the real rules engine (`../../engine/README.md`,
  `ic-engine-1`). Until then rated matches stay "verifying".
- The `/v3` rate limit (60 calls a minute per player or IP) uses the Workers
  rate-limiting binding; tune it after real traffic.
- The 0.4.5 client uses Codex's net-lab relay, not this one. Porting the
  `result` and transcript hooks there waits for AI-097-RELAY-RESUME-HARDENING.
