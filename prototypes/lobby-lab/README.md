# Lobby Lab — local multiplayer-lobby development slice

A locally runnable lobby service for Infinite Conquest 3D development, built
around the **pinned alpha worker contract** (TubaExperiment
`992bc95c7164416ea0a25a4ce120f6ec0a0a167a`, `lobby-worker/worker.js`).
This is a **local development tool**, not production multiplayer and not a game.

## One-command start

```sh
cd prototypes/lobby-lab
node server.js
# → lobby-lab listening on http://127.0.0.1:8787
```

Then open http://127.0.0.1:8787 in a browser. `PORT` / `HOST` env vars
override the bind (default is loopback-only).

```sh
npm test    # all suites: HTTP routes + two-client contract tests (node:test)
node demo.js  # scripted two-client lifecycle demo (starts its own server)
```

Clean shutdown: `Ctrl-C` (SIGINT/SIGTERM handled).

## What it is

- `upstream/worker.js` — **verbatim, immutable** copy of the alpha lobby Worker
  (SHA-256 + provenance in `upstream/PROVENANCE.md`).
- `server.js` — thin Node adapter: converts Node HTTP requests to the worker's
  `fetch(request, env)` signature; serves the UI from `public/`.
- `kv.js` — in-memory KV store implementing the `get`/`put`/`delete`/`list`
  subset the worker uses, with TTL expiry and a controllable clock for tests.
- `public/index.html` — browser UI: create a named lobby, list/refresh, remove
  your own lobbies. Loading / empty / error states. **Discovery only**: the
  worker contract has no join endpoint; joining happens out-of-band via the
  host's tunnel URL.
- `test/http.test.js` — real-HTTP route tests on an ephemeral port.
- `test/contract.test.js` — two-independent-client contract tests: ownership,
  malformed input, TTL expiry with a controllable clock, queue cancellation,
  quick-match lifecycle, report/Elo flow. Version mismatch is pinned as
  *unsupported* by the worker contract (stored/echoed, never enforced).
- `demo.js` — runnable lifecycle demo printing expected outcomes.

## Scope and trust limits (read before exposing this anywhere)

- **No authentication.** Lobby ownership is a caller-supplied `hostUuid`;
  anyone holding it can manage that lobby. Rating reports (`POST /report`)
  are likewise caller-asserted — trust review lands in AI-041.
- **Local only.** Binds `127.0.0.1` by default. Do not forward or expose it
  without adding authentication, rate limiting, and body limits appropriate
  to your threat model.
- **Ephemeral.** All state lives in process memory; restarting wipes it.
- The deployed Worker (`infinite-conquest-lobby.*.workers.dev`) is never
  touched by this lab — no network calls leave loopback.

## Layout

```
prototypes/lobby-lab/
  upstream/      immutable pinned worker copy + provenance
  public/        browser UI (+ client.js adapter)
  test/          node:test suites (run: npm test)
  server.js      HTTP adapter (node server.js)
  kv.js          in-memory KV with TTL + controllable clock
```
