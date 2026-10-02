# Lockstep match relay design (AI-097)

Prototype in `../relay.js`, wired for local testing through the lab adapter
(`server.js` + `ws-shim.js`). Production target: Cloudflare Workers +
**one Durable Object per match room**, relaying intents over WebSockets
inside the free tier (verify current Workers/Durable Object free-tier
limits at build time — noted as a pre-deploy check, not assumed).

## Why lockstep

v1.0 ships with **zero hosting cost**: no authoritative game server until
the game proves profitable (PO decision 2026-09-29 ~20:00). Each player's
PC runs the pinned Java engine behind the AI-079 rules bridge (deterministic:
same seed → byte-identical events on Linux and Windows, AI-066/AI-072).
Only **intents** travel: both clients start from the same match seed, send
each other the chosen action ids, apply them locally, and get the same
state. After every turn the clients exchange a **state hash**; a mismatch
flags the match. Bots (practice vs AI) run locally — offline practice mode
comes free with this design.

## Pieces

1. **MatchRoom Durable Object** (`relay.js`, exported class). One instance
   per v2 room (`AI-096` pairing hands both players the room; the room
   record carries the seats). `GET /rooms/:id/ws` upgrades to a WebSocket
   held by the room's DO. The DO is the single writer: arrival order **is**
   the global intent order.
2. **Relay session state machine** (`createRelaySession` in `relay.js`).
   Runtime-agnostic: the DO drives it in production; the lab drives it
   in-process over `ws-shim.js`. Same protocol, same tests.
3. **Lab WebSocket shim** (`ws-shim.js`). Minimal server-side WebSocket
   (RFC 6455 handshake + text frames, stdlib only) so the lab's node http
   server can exercise the relay without extra dependencies.
4. **Bridge additions** (AI-079, next increment): `{"op":"hash"}` returns a
   canonical hash of the full unredacted game state; seeded match setup
   both clients share. The Unity client (Unity lane) sends the local seat's
   actions to both the local bridge and the relay, and applies remote relay
   intents to the local bridge.

## Protocol (relay-1)

All messages are JSON text frames. `seat` is the seat UUID from the v2
room (bearer token — same trust model as v2, see below).

Client → server:

| type | fields | meaning |
|---|---|---|
| `hello` | `seat`, `lastSeq?`, `dataVersion` | claim a seat; `lastSeq` resumes after a drop |
| `intent` | `seat`, `actionId` | one action intent (AI-079 revision-scoped action id) |
| `turn` | `seat`, `turn`, `activeSeat` | announce whose turn `turn` is (first claim per turn wins; honest clients agree) |
| `hash` | `seat`, `turn`, `hash` | state hash for `turn` (bridge `op:hash`) |
| `seed-commit` | `seat`, `commit` | `SHA-256(seed \|\| salt)` — neither player picks the shuffle |
| `seed-reveal` | `seat`, `seed`, `salt` | reveal; relay verifies against the commit |

Server → client:

| type | fields | meaning |
|---|---|---|
| `welcome` | `seat`, `roomId`, `turn`, `log` | session state; `log` is intents after `lastSeq` (empty on first hello) |
| `intent` | `seq`, `seat`, `actionId` | globally-ordered intent, broadcast to **both** seats (sender included — one code path) |
| `hash-request` | `turn` | both clients must answer with `hash` |
| `hash-ok` | `turn` | both hashes matched |
| `hash-mismatch` | `turn`, `hashes` | hashes differ — match flagged, never auto-resolved |
| `seed` | `seed` | final seed = `SHA-256(seedA \|\| seedB)` once both reveal |
| `timer` | `seat`, `msRemaining` | turn-timer warning |
| `forfeit` | `seat`, `reason` | `timeout` or `no-reveal` |
| `error` | `reason` | malformed message, wrong seat, stale seq, unknown room |
| `bye` | `reason` | server closing (room expired, opponent forfeited) |

### Ordering

The DO numbers each accepted intent `seq = lastSeq + 1` on arrival and
broadcasts it. Clients apply intents in `seq` order, buffering any that
arrive out of order (TCP makes this rare; the buffer makes it safe).
A client `intent` carries no `seq` — the server is the only sequencer, so
there is nothing to disagree about.

### Seed commit-reveal

1. Both seats send `seed-commit` (hash of a 32-byte random seed + salt).
2. Both send `seed-reveal`; the relay checks each against its commit.
3. Relay broadcasts `seed = SHA-256(seedA || seedB)` (lexicographic seat
   order). Neither player controlled the shuffle; neither saw the other's
   seed before committing.
4. A seat that never reveals within the seed phase timeout forfeits
   (`no-reveal`). A revealed seed that doesn't match its commit is
   rejected with `error` (the seat may re-commit once).

### Turn timers

`TURN_TIMEOUT_MS` (default 120_000, configurable): the relay starts it on
each `turn` announcement for `activeSeat`. `timer` warnings go out at 30s
and 10s remaining. Expiry → `forfeit{seat, reason:"timeout"}` broadcast to
both, room closes. Any `intent` from the active seat resets the timer —
passing priority with no action still requires an explicit pass intent, so
"idle" is never ambiguous.

### Intent log and reconnect

Every broadcast intent is appended to the room's persisted log (DO storage;
in-memory in the lab). A dropped socket is **not** a forfeit: the client
reconnects with `hello{lastSeq}` and gets `welcome{log}` — every intent
after `lastSeq`, in order — and replays them through its local bridge.
Acceptance: a 60 s disconnect resumes from the log with no lost intents.
The persisted log is also the replay source (AI-089): seed + intents is
the whole match in a few KB.

### Hash exchange

After each `turn` announcement for turn N, the relay sends
`hash-request{turn:N-1}` (the turn that just completed). Both clients
answer `hash{turn:N-1, hash}` from the bridge's `op:hash` over the **full
unredacted** state — both lockstep clients hold it, so the hashes must
match. Match → `hash-ok`; mismatch → `hash-mismatch` broadcast, the room
record is flagged (like v2 result disputes: flagged, never auto-resolved),
and ranked stays out (`beta` until the authoritative server, AI-085).

## Trust model — what the relay does NOT do (no auth theater)

- **Seat UUIDs are still bearer tokens**, exactly as in v2. Anyone holding
  a seat UUID can connect as that seat. The relay knows *seats*, not
  players.
- **The relay never validates game logic.** It orders, stores, and
  forwards. A client that sends illegal action ids is caught by the other
  client's bridge (which rejects them) and then by the hash check — the
  honest client's state diverges from the cheater's, the hashes mismatch,
  the match is flagged.
- **Lockstep means every client holds the full match state.** A modified
  client *can* reveal the opponent's hand or deck order. Accepted for v1.0
  with mitigations: the seed commit-reveal means neither side picks its
  draws; the per-turn hash checks catch rule-breaking; flagged matches
  don't count toward ranked; ranked is labelled "beta" until the
  authoritative server (AI-085) gives real hidden information in phase 2.
- **Local development only** for the lab shim: same loopback guards, body
  caps, and Host/Origin checks as the rest of the lab (AI-045). The
  production Worker gets the same `dataVersion` gate as v2 (`lab-2`
  minimum) on the upgrade path.

## Limits

- Message cap: 8 KB per frame (intents are tiny; fail closed beyond).
- Room TTL: the relay session lives as long as the v2 room (300 s
  rendezvous TTL is **extended** while a match is live — the DO keeps the
  room alive until forfeit/close; idle rooms with no socket for 10 minutes
  are reaped).
- Intent log cap: 10_000 intents per room (far above any real match;
  fail closed with `error` rather than growing unbounded).
- Two seats exactly. Spectators are out of scope (AI-089 covers replays).

## Pre-deploy checklist (with Mathew)

- [ ] Verify current Cloudflare Workers + Durable Objects free-tier limits
      (requests, duration, storage) against expected launch traffic.
- [ ] `LAB_RELAY_SECRET` set (used for seed-commit salting audit only —
      not auth); `wrangler.toml` with the `MATCH_ROOM` DO binding.
- [ ] Deploy the worker with `relay.js` + `worker-v2.js` + pinned
      `upstream/worker.js` (v1 stays verbatim and ungated).
