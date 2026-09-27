# Lobby-lab reflection schedule (Rune)

Standing automated reflection on the 3DTuba lobby-lab slice, owned by the
Ship Infinite Conquest goal. Purpose: keep a minimally viable, verified
slice at the end of every step, and keep the books organized so everyone can
work effectively.

## Schedule

12 runs daily, America/New_York, at:

02:00, 03:00, 04:00, 08:00, 09:00, 10:00, 14:00, 15:00, 16:00, 20:00, 21:00, 22:00

Job IDs: `lobby-lab-reflection-0200` … `lobby-lab-reflection-2200`
(times may fire a few minutes early; that is the scheduler spreading load).

## What each run does

1. **Reads the backlog first** (`PRODUCT_BACKLOG.md`) and picks the
   highest-value unblocked task inside Rune's scope.
2. **Executes**: implement → full verification suite green → commit with
   message prefix `lobby-lab reflection:`.
3. **Updates the books**: backlog statuses with dated notes + evidence,
   `SPRINT_LOG.md` entries, product-state files where state changed.
4. **Verification pass every run** (even no-ops): branch-tip check, then a
   clean-clone run of `npm test` (32 pass), `python3 -m unittest
   test_validate_manifest` (29 OK), `node --test smoke.test.js` (1 pass),
   `node demo.js` and `node examples/client-demo.js` (exit 0).
5. **Reports to Mathew only** on failures, shipped improvements, real
   defects/blockers, or commits from others needing attention. Green
   no-ops are silent.

## Lanes — who owns what

- **Rune** (reflections + directed work): `prototypes/lobby-lab/` and
  `docs/muse/sprint-01/` on branch `muse/sprint-01-content-audit` only.
- **Astra**: Unity `Assets/`, `Packages/`, `ProjectSettings/`, root build
  config, integration, acceptance gates (AI-030/031).
- **Read-only for everyone**: TubaExperiment, Desolate-Tuba — the alpha is
  the rules reference; leave it alone.
- **Never**: deployments, live-service writes, credentials, paid jobs,
  force pushes, default-branch changes, releases.

## Verify it yourself

From a clean clone of `muse/sprint-01-content-audit`:

```sh
cd prototypes/lobby-lab && npm test            # 32 pass
node demo.js && node examples/client-demo.js    # exit 0
cd docs/muse/sprint-01 && python3 -m unittest test_validate_manifest  # 29 OK
cd lobby-smoke && node --test smoke.test.js     # 1 pass
```

Local UI check: `cd prototypes/lobby-lab && node server.js`, then open
`http://127.0.0.1:8787` (loopback only, by design).

## Coordination

The product backlog and sprint logs are the planning source of truth and
are kept current by every reflection run. Evidence docs produced by runs
land here in `docs/muse/sprint-01/`. Run bookkeeping lives under the
goal workspace (`workspace/goals/ship-infinite-conquest/hidden_files/`).
