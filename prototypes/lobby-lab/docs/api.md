# Lobby Lab — HTTP API schema (for integrators)

Base URL (local dev): `http://127.0.0.1:8787`. All request/response bodies are
JSON. There is **no authentication**: lobby ownership and queue/report
identities are caller-supplied UUIDs. Treat this service as a local
development stand-in, not a production backend.

Conventions:

- `UUID` = lowercase `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` (validated).
- Errors: `{ "error": "<message>" }` with 4xx/5xx status.
- Lobby codes: 6 chars, uppercase alphanumeric, case-sensitive on lookup.
- `dataVersion` is stored and echoed but **never enforced** — a client on a
  different version is accepted. Integrators should surface mismatches
  themselves.
- TTLs (pinned worker values): lobbies 120s, queue tickets 60s, pairings
  300s. Expired entries vanish from list/get/poll.

## Lobbies

### `POST /lobbies` — register a lobby

```jsonc
// request
{
  "hostUuid": "aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa",  // required, caller-chosen secret
  "hostName": "Mathew",                              // required, sanitized, max 24 chars
  "hostRating": 1042,                                // optional, default 1000
  "wssUrl": "wss://my-tunnel.trycloudflare.com",     // required, wss:// + hostname (no IP literals)
  "dataVersion": "lab-1"                             // optional
}
// 200 response
{ "code": "K7Q2XA" }
// 400: invalid JSON / missing fields / bad wssUrl
```

### `GET /lobbies` — list open lobbies

```jsonc
// 200 response
[
  {
    "code": "K7Q2XA",
    "hostName": "Mathew",
    "hostRating": 1042,
    "wssUrl": "wss://my-tunnel.trycloudflare.com",
    "dataVersion": "lab-1"
  }
]
```

### `GET /lobbies/{code}` — lobby details

`200` → same shape as a list entry. `404` → `{ "error": "unknown or expired lobby" }`.

### `DELETE /lobbies/{code}` — close a lobby (owner only)

```jsonc
// request
{ "hostUuid": "aaaaaaaa-1111-4111-8111-aaaaaaaaaaaa" }
// 200 response
{ "ok": true }
// 403: wrong/missing hostUuid — "only the host may close this lobby"
// 404: unknown or expired lobby
```

## Quick match

### `POST /queue` — enter matchmaking

```jsonc
// request
{ "uuid": "<player uuid>", "name": "Mathew", "rating": 1042, "dataVersion": "lab-1" }
// 200 response
{ "queued": true }
```

### `GET /queue/poll?uuid=<uuid>` — poll for a match

```jsonc
// 200 — nobody suitable yet
{ "status": "waiting" }
// 200 — our ticket expired or was cancelled
{ "status": "waiting", "reason": "ticket expired; re-enqueue" }
// 200 — we enqueued earliest: we host, opponent waits for our pairing
{ "status": "host", "opponentUuid": "<uuid>", "opponentName": "Rhea", "opponentRating": 1010 }
// 200 — host published a pairing for us (consumed on read)
{
  "status": "ready",
  "wssUrl": "wss://host-tunnel.trycloudflare.com",
  "code": "QM1",
  "opponentName": "Mathew",
  "opponentRating": 1042
}
```

### `POST /pair` — host publishes pairing for the opponent

```jsonc
// request
{
  "hostUuid": "<host uuid>", "forUuid": "<opponent uuid>",
  "wssUrl": "wss://host-tunnel.trycloudflare.com",
  "code": "QM1",            // optional, max 16 chars
  "hostName": "Mathew",     // optional
  "hostRating": 1042        // optional
}
// 200 response
{ "ok": true }
```

### `DELETE /queue?uuid=<uuid>` — leave matchmaking

`200` → `{ "ok": true }` (idempotent).

## Ratings (disabled by default in the lab)

`POST /report` is refused with `403` unless the server is started with
`allowReports` / `LAB_ALLOW_REPORTS=1`, because reports are caller-asserted
UUIDs with no authentication. Contract, for reference:

```jsonc
// request
{
  "matchId": "match-123",
  "reporterUuid": "<uuid>", "winnerUuid": "<uuid>", "loserUuid": "<uuid>",
  "dataVersion": "lab-1"
}
// 200 — first report stored, waiting for the opponent's
{ "applied": false, "reason": "waiting for opponent's report" }
// 200 — agreeing second report: Elo applied
{ "applied": true, "rating": 1016, "delta": 16 }
// 200 — disagreeing reports: flagged, nothing applied
{ "applied": false, "reason": "reports disagree — flagged for review" }
```

### `GET /leaderboard?limit=25` — top ratings (name + rating only)

### `GET /rating/{uuid}` — one player's record

```jsonc
{ "rating": 1016, "wins": 1, "losses": 0, "name": "Mathew" }
```

## Worker v2 (AI-096) — `/v2/*`

Runs beside v1: all paths above keep their exact v1 behavior (v1 is
delegated verbatim, `dataVersion` still stored-but-never-enforced there).
v2 adds a `dataVersion` gate, server-side room assignment, and signed
results. Design + trust limits: `docs/v2-design.md`.

Conventions: `dataVersion` is **required** on the two state-changing
endpoints and must be at or above the server minimum (default `lab-2`;
unknown versions fail closed). Errors keep the `{ "error": "<message>" }`
shape.

### `GET /v2/version` — capability discovery

```jsonc
{ "worker": "v2", "dataVersions": ["lab-1", "lab-2"], "dataVersionMin": "lab-2",
  "v1Compatible": true, "secretMode": "ephemeral",
  "endpoints": ["GET /v2/version", "POST /v2/rooms/pair", /* ... */] }
```

### `POST /v2/rooms/pair` — server-side atomic pairing

```jsonc
// request
{ "uuidA": "<uuid>", "uuidB": "<uuid>", "dataVersion": "lab-2" }
// 200 response
{ "roomId": "K7Q2XAB4M9D2", "seatA": "<uuidA>", "seatB": "<uuidB>",
  "dataVersion": "lab-2", "createdAt": 1759274400000, "expiresIn": 300 }
// 400: dataVersion missing/old/unknown, bad UUIDs, or either player lacks a
//      live v1 queue ticket (POST /queue first)
```

Both v1 queue tickets are consumed, so a paired player cannot be paired
again while the room is live. Re-pairing an already-paired seat pair
returns the live room (idempotent).

### `GET /v2/rooms?uuid=<uuid>` — the caller's live room

`200` → the room object above. `404` → `{ "error": "no live room for uuid" }`.

### `DELETE /v2/rooms/{roomId}` — close a room (seat only)

```jsonc
// request
{ "uuid": "<seat uuid>" }
// 200 response
{ "ok": true }
// 403: not a seat of this room. Idempotent: closing twice still 200.
```

### `POST /v2/results` — two-seat agreement with signed receipts

```jsonc
// request
{ "roomId": "<roomId>", "reporterUuid": "<seat uuid>",
  "winnerUuid": "<seat uuid>", "loserUuid": "<seat uuid>",
  "finalStateHash": "abc123",          // optional, max 128 chars
  "dataVersion": "lab-2" }
// 200 — first claim recorded
{ "recorded": true, "status": "waiting", "reason": "waiting for the other seat's report" }
// 200 — both seats agree: tamper-evident receipt issued
{ "recorded": true, "status": "agreed",
  "receipt": { "v": 2, "roomId": "<roomId>", "winnerUuid": "<uuid>",
               "loserUuid": "<uuid>", "dataVersion": "lab-2",
               "nonce": "<32 hex chars>", "sig": "<64 hex chars>" } }
// 200 — seats disagree: flagged, nothing applied
{ "recorded": true, "status": "disputed", "reason": "reports disagree — flagged for review" }
// 403: reporter is not a seat. 404: unknown/expired room.
// 400: winner/loser are not the room's seats, or the dataVersion gate fails.
```

v2 does **not** apply Elo — rating application stays with the v1 `/report`
path (disabled in the lab) or a future ranked service. A receipt proves
*this server* issued exactly these fields; it says nothing about who
submitted the claims (caller UUIDs remain untrusted — no auth theater).

### `GET /v2/results/{roomId}` — agreement state

`200` → `{ "status": "waiting" }`, `{ "status": "agreed", "receipt": {...},
"agreedAt": <ms> }`, or `{ "status": "disputed", "reason": "..." }`.
`404` → no results recorded for the room.

### `POST /v2/results/verify` — verify a receipt

```jsonc
// request
{ "receipt": { "v": 2, /* ... */ } }
// 200 response
{ "valid": true }
// or
{ "valid": false, "reason": "signature mismatch" }
```

## C# integration notes (Unity)

- Target the **lab server on loopback** during development; the deployed
  Worker URL is a separate deployment concern owned by Astra.
- Reuse the semantics in `../public/client.js` as the reference behavior:
  per-request timeout, cancellation via `CancellationToken`, typed DTOs,
  and errors that carry status + message. `HttpClient` maps naturally.
- Discovery flow for a host: `POST /lobbies` → share `code` out-of-band →
  `DELETE /lobbies/{code}` with the stored `hostUuid` to close.
- Discovery flow for a browser: poll `GET /lobbies`, show `code`/`hostName`/
  `hostRating`, connect to `wssUrl` directly — there is no server-side join.
- Quick-match flow: `POST /queue` → poll `GET /queue/poll?uuid=` →
  `host` publishes via `POST /pair` → opponent's next poll returns `ready`.
- Worker v2 flow (AI-096): `POST /queue` (both players) →
  `POST /v2/rooms/pair` (atomic server-side pairing, `dataVersion: "lab-2"`
  required) → `GET /v2/rooms?uuid=` to read the room →
  `POST /v2/results` per seat → `agreed` returns a signed receipt, verifiable
  via `POST /v2/results/verify`. Game-traffic relay is AI-097's scope;
  v2 rooms carry no `wssUrl` yet.
- Never ship a build that reports match results to a service without
  authenticated player identity; the lab disables `/report` for this reason.
