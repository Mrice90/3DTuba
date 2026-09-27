# lobby-smoke provenance (AI-038)

- `worker.js`: **verbatim copy** of `lobby-worker/worker.js` from TubaExperiment
  @ `992bc95c7164416ea0a25a4ce120f6ec0a0a167a`
  (branch `strip/zeus-poseidon-desktop`). Unmodified; verified byte-identical
  with `diff`. The source repo was not touched.
- `smoke.test.js`: authored for this sprint. Exercises worker.js's actual
  endpoint contracts (`POST /lobbies`, `GET /lobbies`, `GET /lobbies/:code`,
  `DELETE /lobbies/:code`) against an in-memory mocked KV store.
- Offline by construction: no network requests, no Cloudflare account, no
  writes to the deployed service
  (`https://infinite-conquest-lobby.infinite-conquest-lobby.workers.dev`).
- Run: `node --test smoke.test.js` (Node 18+; verified on Node v24.20.0).
- This harness is a contract smoke test, not a playable game and not an
  end-to-end pass against the deployed Worker.
