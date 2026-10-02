# Worker v2 design (AI-096)

Prototype in `../worker-v2.js`, routed by the lab adapter (`server.js`):
every `/v2/*` path is handled by v2, everything else is delegated
**verbatim** to `upstream/worker.js` (the pinned v1 alpha contract — never
edited). The 2D alpha keeps working unchanged during the transition; v1
endpoints stay ungated.

## What v2 adds over v1

1. **dataVersion gate.** v1 stores `dataVersion` but never enforces it, so a
   stale client is silently accepted. v2 state-changing endpoints
   (`POST /v2/rooms/pair`, `POST /v2/results`) require `dataVersion` at or
   above the configured minimum (default `"lab-2"`, env
   `LAB_V2_DATAVERSION_MIN`). Known versions are an ordered list
   (`["lab-1", "lab-2"]`); unknown strings fail closed. Missing or old
   versions get `400` with an actionable message. v1 paths are deliberately
   ungated — backward compatibility for the 2D alpha is the point.

2. **Server-side room assignment.** v1 quick-match trusts the
   earliest-queued *client* to publish the pairing (`POST /pair`): it can
   race another host for the same opponent or go silent and strand the
   opponent. `POST /v2/rooms/pair {uuidA, uuidB, dataVersion}` pairs two
   **live v1 queue tickets** atomically on the server and returns a room
   `{roomId, seatA, seatB, dataVersion, createdAt, expiresIn}` (TTL 300s).
   Both queue tickets are consumed, exactly like v1's `publishPairing`,
   so a paired player cannot be paired twice. Re-pairing an already-paired
   seat pair returns the live room (idempotent). `GET /v2/rooms?uuid=`
   reads a seat's live room; `DELETE /v2/rooms/:roomId {uuid}` lets a seat
   holder close it (idempotent).

3. **Signed results.** `POST /v2/results` keeps the v1 two-claim agreement
   shape (`roomId, reporterUuid, winnerUuid, loserUuid, finalStateHash?,
   dataVersion`): the room must be live, the reporter must be a seat, and
   winner/loser must be the room's seats. One claim → `waiting`; two
   agreeing claims → `agreed` with a **signed receipt**
   `{v:2, roomId, winnerUuid, loserUuid, dataVersion, nonce, sig}` where
   `sig = HMAC-SHA256(secret, "v2-result|roomId|winner|loser|dataVersion|nonce")`;
   two disagreeing claims → `disputed`, flagged for review (mirrors the v1
   `/report` flag). `GET /v2/results/:roomId` reads the state;
   `POST /v2/results/verify {receipt}` recomputes the HMAC.

## Trust model — what v2 does NOT do (no auth theater)

- **Caller UUIDs are still not authentication.** Anyone holding a seat UUID
  can report for it, exactly as in v1. v2 never claims to know who a
  player is.
- **The HMAC secret is tamper-evidence, not identity.** `LAB_V2_SECRET`
  (ephemeral per process unless set) lets anyone verify that *this server*
  issued exactly this receipt. It does not prove who submitted the claims,
  and it does not stop a seat holder from submitting a false claim — that
  is what the two-seat agreement and the dispute flag are for.
- **Disagreements are flagged, never auto-resolved.** Same policy as v1.
- **v2 does not apply Elo.** Rating application stays with the v1 `/report`
  path (disabled in the lab by default) or a future ranked service under
  AI-010's authenticated design. A signed receipt is a record, not a
  ranking.
- **Local development only.** Same loopback guards, body caps, and
  Origin/Host checks as the rest of the lab (AI-045). Never expose beyond
  loopback without a real identity layer.

## Deliberate non-goals (future items)

- Game-traffic relay / reconnect / turn timers: that's **AI-097** (Durable
  Object relay, Muse + Claude Unity thread). v2 rooms carry no `wssUrl`;
  rendezvous stays on the v1 lobby/queue paths until AI-097 lands.
- Authenticated ranked identity: **AI-010**. v2 is the honest stepping
  stone — atomic pairing plus attributable, tamper-evident results —
  not the ranked backend.

## API summary

```
GET  /v2/version                 -> {worker:"v2", dataVersions, dataVersionMin,
                                     v1Compatible:true, secretMode, endpoints[]}
POST /v2/rooms/pair              {uuidA, uuidB, dataVersion} -> room | 400/404
GET  /v2/rooms?uuid=             -> room | 404
DELETE /v2/rooms/:roomId          {uuid} -> {ok:true} (seat only, idempotent)
POST /v2/results                  {roomId, reporterUuid, winnerUuid, loserUuid,
                                   finalStateHash?, dataVersion}
                                 -> {recorded, status:"waiting"|"agreed"|"disputed", receipt?}
GET  /v2/results/:roomId         -> {status, receipt?, agreedAt?} | 404
POST /v2/results/verify           {receipt} -> {valid:true} | {valid:false, reason}
```

Errors keep the lab convention: `{ "error": "<message>" }` with 4xx/5xx.

## Environment

- `LAB_V2_SECRET` — HMAC secret. Unset → ephemeral per-process secret
  (logged as `ephemeral`; receipts do not survive restarts).
- `LAB_V2_DATAVERSION_MIN` — minimum accepted dataVersion (default
  `"lab-2"`).

## Tests

`test/worker-v2.test.js` (runs under `npm test`): v1 backward
compatibility through the v2 router, the dataVersion gate
(missing/old/unknown), atomic pairing (ticket consumption, idempotency,
unqueued rejection), room read/close, the full results lifecycle
(waiting → agreed → verify, tampered receipt, dispute flag, non-seat
rejection), and unknown-room handling.
