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
override the bind (default is loopback-only). `LAB_ALLOW_REPORTS=1` enables
the unsafe match-reporting contract locally; `LAB_MAX_BODY` overrides the
1 MB body cap.

```sh
npm test    # all suites: HTTP routes + two-client contract tests (node:test)
node demo.js  # scripted two-client lifecycle demo (starts its own server)
```

Zero npm dependencies — the entire slice is Node standard library, so there
is no install step and no third-party supply-chain surface to audit.

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
- `public/client.js` — reusable JS adapter (`LobbyClient`/`LobbyError`):
  configurable loopback base URL (non-loopback refused unless opted in),
  per-request timeout, `AbortSignal` cancellation, typed/documented
  request/response shapes, and errors carrying status + message + code.
  The browser UI is wired through it.
- `examples/client-demo.js` — runnable adapter integration example.
- `docs/api.md` — endpoint schema + C# integration notes for Astra.
- Live-service conformance: the deployed worker was fingerprinted
  read-only on 2026-09-27 and matches the pinned contract on every probed
  path — see `../../docs/muse/sprint-01/deployed-worker-fingerprint.md`.

## Scope and trust limits (read before exposing this anywhere)

- **No authentication.** Lobby ownership is a caller-supplied `hostUuid`;
  anyone holding it can manage that lobby. Queue pairings are likewise
  caller-asserted — a local caller can publish a pairing for anyone's uuid.
- **Match reporting is disabled by default** (AI-041). `POST /report` accepts
  arbitrary caller-supplied UUIDs, so one caller could submit both "agreeing"
  reports and mint Elo for UUIDs that never played. The adapter returns 403
  unless started with `allowReports: true` / `LAB_ALLOW_REPORTS=1`, which
  exists only to exercise the contract locally. Do not enable it on any
  shared instance, and never treat lab ratings as meaningful.
- **Request bodies are capped** at 1 MB by default (`LAB_MAX_BODY`); larger
  bodies get 413. Error responses never include stack traces.
- **Local only.** Binds `127.0.0.1` by default. Do not forward or expose it
  without adding authentication, rate limiting, and body limits appropriate
  to your threat model.
- **Local-only boundary enforced** (AI-045). A non-loopback `HOST` /
  `start({host})` value is rejected before the server listens; every request's
  `Host` header must be loopback (DNS-rebinding defense); browser mutations
  (`POST`/`DELETE`/…) must carry a loopback `Origin`/`Referer`, so an
  unrelated website cannot create or delete lobbies through your browser.
  Non-browser clients (Node adapter, demo, curl) send no `Origin` and are
  unaffected. Reads (`GET`) stay open — responses carry no CORS headers, so
  a hostile page cannot read them. This is a development boundary, not
  authentication.
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
