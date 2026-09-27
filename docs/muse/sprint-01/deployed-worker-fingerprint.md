# Deployed lobby worker fingerprint — read-only behavioral probe

Date: 2026-09-27 ~13:40 EDT. Method: safe HTTP GETs only against the live
service; nothing was written, no credentials used. Local reference: lobby-lab
serving the verbatim pinned worker (`upstream/worker.js`, TubaExperiment
`992bc95c7164416ea0a25a4ce120f6ec0a0a167a`, SHA-256
`73bde885a6f7031b8ccaf07b076152b6efa5066bad17781ddbb824bbcef6f990`).

Live base: `https://infinite-conquest-lobby.infinite-conquest-lobby.workers.dev`

## Results

| Path | Pinned (lab) | Live | Verdict |
|---|---|---|---|
| `GET /lobbies` | 200 `[]` | 200 `[]` | match |
| `GET /lobbies/ZZZZZZ` | 404 `{"error":"unknown or expired lobby"}` | 404 `{"error":"unknown or expired lobby"}` | match — exact error string |
| `GET /queue/poll?uuid=<random>` | 200 `{"status":"waiting","reason":"ticket expired; re-enqueue"}` | 200 `{"status":"waiting","reason":"ticket expired; re-enqueue"}` | match — exact strings |
| `GET /leaderboard?limit=5` | 200 `[]` | 200 `[]` | match |
| `GET /rating/<random-uuid>` | 200 `{"rating":1000,"wins":0,"losses":0,"name":"Player"}` | 200 `{"rating":1000,"wins":0,"losses":0,"name":"Player"}` | match — unknown UUIDs return the default record, not 404 |
| `GET /no-such-route` | 404 plain-text `not found` | 404 `{"error":"not found"}` | explained below |

The `/no-such-route` difference is a lab-adapter artifact, not a worker
difference: the lab routes non-API paths to its static file server, whose own
404 is plain text. The live 404 body `{"error":"not found"}` is exactly what
the pinned worker's `bad("not found", 404)` produces (`json({ error: message },
status)`), which further confirms the deployed code matches.

Live state at probe time: no open lobbies, empty leaderboard.

## Conclusion

The deployed worker is behaviorally identical to the pinned 992bc95 contract
on every probed path, including exact error strings. Astra can treat the
pinned contract (and the lobby-lab slice built on it) as the integration
target for AI-008 with confidence; no contract drift was observed.

## Limits

Behavioral fingerprint only — not a code inspection of the deployment. Six
read-only paths probed; write paths (`POST /lobbies`, `/queue`, `/pair`,
`/report`) were deliberately not exercised against the live service. Live
state changes over time; this records 2026-09-27 ~13:40 EDT.
