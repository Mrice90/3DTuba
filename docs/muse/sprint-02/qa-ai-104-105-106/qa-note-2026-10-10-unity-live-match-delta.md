# QA note — `claude/unity-live-match` delta review (2026-10-10, Rune)

**Scope:** static code + data review of the four commits on `claude/unity-live-match` since the QA'd tip `f49a1c0` (Rune QA of `c86e23b`, 2026-10-02 ~20:00 EDT — no defects found). Diff: `f49a1c0..fc30be3`.

| Commit | Date | Change |
|---|---|---|
| `d2ef129` | 2026-10-02 | AI-097 client: `icnet-lockstep.yml` fetches alpha source before building jar (Rune's own CI fix; already green) |
| `5e1dda9` | 2026-10-02 | Installable playtest: main menu, pause, settings, game over; 90 models; installer (`PlaytestMenus.cs` new, `PlaytestGame.cs` menu/pause flow, `Tools/installer/`) |
| `fc30be3` | 2026-10-07 | Audio routing: positional board cues, UI event cues, per-event overrides (`SfxBank.cs`, `stage_playtest_assets.py` tiers, `cards.json` model staging) |

**Not reviewed from execution:** no Unity build or playthrough here (needs Unity/Windows). This is a read-only review — no Unity-lane files were edited.

## Result: 2 findings (1 medium, 1 low), no blockers

### F1 — MEDIUM: pausing a live match does not pause the match
`PlaytestMenus.cs` `TogglePause()` sets `paused = true`, and `PlaytestGame.Update()` returns early when paused (input blocked, `rig.InputEnabled = false`, pause overlay drawn). But `LiveLoop()` (`PlaytestGame.cs:357`) never checks `paused`: it keeps polling the bridge, enqueuing and `Apply()`-ing events while the overlay is up, and the bridge process keeps taking bot turns. In Playback mode pause works (`Run()` stalls on `playing = false`), so the pause menu is honest in demo mode but cosmetic in live play — the match (and the bot) advances behind the "Paused" screen.

Fix direction (Unity lane): gate the `LiveLoop` apply loop on `!paused` (events still arrive; queue them without applying), and consider a bridge-side hold so the bot does not use the pause window. Acceptance: pause mid-live-match, wait through a full bot turn, unpause — no events applied and the bot turn has not advanced.

### F2 — LOW: installer `.png/.log/.jsonl` exclusion is dead code
`UnityProof/Tools/installer/build_installer.ps1`: the ship filter `$_.Name -notmatch '\\.(png|log|jsonl)$'` can never match — `.Name` never contains a backslash, and in the regex `\\.` means "literal backslash + any char". So top-level `*.png`/`*.log`/`*.jsonl` in the build dir are shipped into the installer, contrary to the comment. Fix: `-notmatch '\.(png|log|jsonl)$'` (single backslash). Impact today is small (`-playtestShots` writes to a caller-chosen dir, and `playtest-smoke.json`/`PLAY-INFINITE-CONQUEST.bat` are excluded by name), but the filter does not do what it says.

## Observations (no defects)

- **"90 models" claim checks out in data:** `cards.json` now has 90/139 cards with models (was 29; 110 empty → 49 empty). The 49 model-less are 33 STRUCTURE + 16 SPELL. All 35 lands have models (batch-05-lands-pilot/hybrid), all 48 characters + 6 capitals have models. Spells are model-less by design (AI-063). 33 structures still on stand-ins = the AI-081-STRUCTURES-FULL backlog.
- **`rig.InputEnabled` is safe:** `CameraRig.cs:12` already declares `public bool InputEnabled = true;` (predates this diff) — no missing member; `Update()` gating compiles.
- **New SFX cues are dormant by design:** `SfxBank.Cues` gains `place`, `cast`, `resolve_impact` with sane fallbacks (`place`/`cast` → `deploy`, `resolve_impact` → `ability`), but no `cards.json` cue list and no call site emits them yet. Harmless.
- **Positional audio design is coherent:** 12 board voices, `spatialBlend = 0.6`, linear rolloff 15–60 units vs a 3–40-unit camera — panned, never fully attenuated, as the comment claims. Pitch/volume jitter on repeats. UI cues stay 2D with per-event inspector overrides and `Resources/Sfx/ui_<cue>[_2.._4].wav` fallback, plus synthesized `turn_end`/`card`/`victory`/`defeat` fallbacks.
- **Menu/pause plumbing is sound:** `OpenMenu()` disposes the bridge and stops coroutines; `Rematch()` respawns; `LoadSettings()` runs in `Start()` after `Awake()` creates the `SfxBank` (no null); `DrawGameOver` only runs when `MatchOver` (liveState non-null in Live); failed bridge start falls back to playback (`RestartPlayback()` in the catch). IMGUI seat/difficulty toggles behave as radio buttons (clicking the selected one cannot deselect it). Game-over jingle is `defeat` only when the human loses a live match.
- **Installer shape is sane:** per-user install to `%LOCALAPPDATA%\Programs\Infinite Conquest` (no admin), Start-menu + desktop shortcuts, Apps uninstall entry, self-deleting uninstall script, settings preserved on reinstall (documented). `build_installer.ps1` hard-requires the exe, `_Data`, `Bridge\jre\bin\java.exe`, `Bridge\classes\RulesBridge.class` — the trimmed-JRE packaging step is assumed done on the build machine.
- **AI-097 wiring still open:** `BridgeClient.New(seed, seat, difficulty)` is wired through; the IC.Net relay-1 `RelayClient` is untouched by these commits (no PlaytestGame wiring yet) — consistent with the backlog row (Claude's lane next).

## Evidence
- Diff reviewed: `git diff f49a1c0..fc30be3` (13 files, +625/−187) on a read-only clone of `claude/unity-live-match`.
- Data check: 139 cards, 90 with models, 49 without (33 STRUCTURE, 16 SPELL).
- No Unity build, no playthrough, no lane files edited.
