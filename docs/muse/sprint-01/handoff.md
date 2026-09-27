# AI-028 Handoff — Latest Alpha Work, Service Status, Known Issues

**Inspected:** TubaExperiment @ `992bc95c7164416ea0a25a4ce120f6ec0a0a167a`
(`strip/zeus-poseidon-desktop`, remote default branch), **v0.7.15**, tag `v0.7.15-alpha`.

## Latest alpha work (git log, 2026-09-26)
- `992bc95` — "0.7.15: version bump"
- `1a6da8a` — "UI rework: docked reaction panel, mulligan layout, coin-flip staging, themed dialogs, lobby sections"
- `4a6aac0` — "0.7.14: version bump"
- `GameVersion.VERSION = "0.7.15"` (`game-gui/.../gui/GameVersion.java:9`); network `DATA_VERSION = "ic-net-2"` (`net-server/.../net/Protocol.java:32`).
- `dist/`: `Start-InfiniteConquest.ps1`, `windows/` (PowerShell-first packaging path; `.exe` later per `review/FINDINGS.md`).

## Active branches / refs observed
- Remote branches: `strip/zeus-poseidon-desktop` (= `992bc95`, default via `origin/HEAD`) and `main` (= `0d59e55c0547ed0084e81a5d0623abbcc21e4c77`, not inspected).
- Tags `v0.7.2-alpha` through `v0.7.15-alpha`.
- No other local work: both clones verified clean (`git status --porcelain` empty).

## Known bugs / open items (repo evidence + transcript context)
1. **Multiplayer screen opened empty** (user report; repo-side diagnosis was halted at the user's request). Likely root cause per repo evidence: unset lobby server URL — **now mitigated in-code**: `GameSettings.DEFAULT_LOBBY_WORKER_URL = "https://infinite-conquest-lobby.infinite-conquest-lobby.workers.dev"` (`game-gui/.../gui/GameSettings.java:46`); blank setting falls back to this default (lines 63-69). Whether the empty screen is fully resolved is **unverified** (no live client test in this audit).
2. **Starter decks reference runtime-only tutor IDs** (`zeus_tutor_land_1`, `zeus_tutor_structure_1`, `poseidon_tutor_land_1`, `poseidon_tutor_structure_1` in `game-cli/src/main/resources/cards/faction-starters.json`) — flagged at `review/FINDINGS.md:119`. Works only because `FactionTutorExpansion` runs before deck building; fragile if the generator is ever removed.
3. **Stale doc:** `lobby-worker/README.md:4` still says "**this Worker is not deployed yet**" and `GameSettings.java:44` says "Set to the studio's deployed Worker origin once it exists" — both outdated (see below).
4. `review/FINDINGS.md` (written against the original, 366-card six-faction codebase) documents the Zeus/Poseidon strip-down plan that produced this alpha, including the open **ally-deck decision** (keep cross-faction ally mixing vs pure 1v1).

## Test evidence (repo only — suite not executed here)
- **72 `*Test.java` files** (76 test sources total) across `game-core`, `game-cli`, `game-gui` — incl. `CardCatalogTest`, `CardAbilityRulesTest`, `FactionCardSetTest`, `CardArtFactoryTest`.
- CI: `.github/workflows/ci.yml` runs `gradle test :game-gui:distZip` and uploads `infinite-conquest-desktop-alpha`; `balance-run.yml` (sims on PRs), `gui-screenshots.yml` (Xvfb screenshot verification). Per `FINDINGS.md §8`, CI was green at review time; not re-verified in this audit.

## Lobby / rating service — deployment reconciliation (evidence only, no redeploy)
- **Service URL:** `https://infinite-conquest-lobby.infinite-conquest-lobby.workers.dev` (rendezvous + Elo rating Worker; KV namespace `IC_KV`; source in `lobby-worker/worker.js`, `wrangler.toml`).
- **Repo docs claim:** "not deployed yet" (`lobby-worker/README.md:4`).
- **Live evidence 2026-09-27 ~12:45 EDT (plain HTTPS GET, no writes):**
  - `GET /lobbies` → `[]`
  - `GET /leaderboard` → `[]`
  - Both return valid application JSON shaped by the worker's router (`worker.js:371-393`). An undeployed/missing worker would return a Cloudflare error page or DNS failure, not the app's empty-list responses.
- **Reconciliation:** the Worker **is deployed and serving**; the README's "not deployed yet" is stale documentation from before the 2026-09-25 deployment. Nothing was redeployed or modified in this audit. Lobbies/leaderboard are currently empty (no active players at probe time), which is consistent with — but does not by itself explain — the earlier "multiplayer screen opened empty" report; that report predates the baked-in default URL and remains unverified end-to-end.
