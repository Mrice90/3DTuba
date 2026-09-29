# HA-009 review sheet: UnityProof 3D playtest build (AI-080 / AI-052-ASSET / AI-030)

Claude (Unity thread, covering Astra), 2026-09-29 ~17:50 EDT. Branch `claude/unity-playtest-20260929`. This branch is not merged and has no PR; Mathew decides.

## What you can do in the build

- **Watch a real match (default mode).** The build replays the AI-066 event dump for seed 42 (235 events, Zeus beats Poseidon on turn 14) on the hex board:
  - cards drop in with a deploy sound and a burst
  - characters hop between hexes
  - attacks fire energy bolts, with a hit sound and a floating damage number
  - destroyed cards shrink and burst
  - capital hits shake the capital and lower its HP bar
  - spells play a faction-coloured VFX burst on the target hex

  The HUD shows the turn, the active player, the phase, GP, hand count, capital HP and an event log.
- **Board.** 24 hexes (4×6, odd-row offset, the same `BoardLayout` as the movement proof) sit on a plinth. The board has rim outlines and a faint home-row tint, and hovered hexes highlight. Selecting a piece shows pulsing legal-target markers: teal for reachable hexes, red for hexes holding an enemy. Pieces stack within a hex: land at the base, and structures or characters stand on the land top. Structures hold the centre and characters ring around them.
- **Camera.** Right-drag or Q/E orbits, the wheel zooms, middle-drag or WASD pans, and Home resets the view.
- **Card gallery.** Press G or the "Card gallery" button to see all 139 cards laid out by type. The filters are all, real (Meshy) and stand-ins. The "Stand-ins only" toggle swaps every real model for its typed stand-in, on the board or in the gallery.
- **Live play against the bot.** Pass `-bridgeCmd "<java ... RulesBridge>"` and optionally `-bridgeCwd`, `-seed` and `-humanSeat`. AI-079 (Muse's rules bridge) had **not** landed on `muse/sprint-01-content-audit` at `49b47fe`, so this mode is written and self-tested against canned protocol lines only (`BridgeClient`, 5 editor checks). It is not yet playable against the engine.

## How to playtest (Mathew)

1. Double-click `%TEMP%\claude\ic-playtest-build\InfiniteConquestPlaytest.exe`. Keep the `_Data`, `MonoBleedingEdge` and DLL files next to it.
2. The match plays by itself. Use **Pause / Step / 1x 2x 4x** at the bottom, and **Restart** to watch it again.
3. Click any piece to see its markers. Hover a hex to list its stack, which shows each piece's type, owner, damage, and whether it is a Meshy model or a stand-in.
4. Press **G** for the gallery, and use **real** or **stand-ins** to review the art. Toggle **Stand-ins only** to compare.

## Evidence

| Check | Result |
|---|---|
| Editor validation (`PlaytestBuild.Prepare`) | 29 model imports, the 14 AI-036 proof assertions, the seed-42 dump shape, the 139-card catalog, and the bridge client self-test all PASS (`playtest-validation.json`) |
| Windows build | `PLAYTEST_BUILD PASS`, 264 MB (Unity 6000.6.3f1, URP) |
| `InfiniteConquestPlaytest.exe` SHA-256 | `96b492cb271111251fe42b8646e65370a1b7b566773a1e35b34c3f2d1ae70873` (the Unity launcher stub) |
| `_Data` tree SHA-256 (sorted per-file sha256sum, hashed) | `7874b411c45e35816317049c5492043d4b977ed03f1d69490b81dddf40cc7d00` |
| `-batchmode -proofSmoke` | exit 0, `{"passed":true,"legal":true,"blocked":true}` (`proof-smoke.json`). The same build routes to the original MovementProof scene. |
| `-batchmode -playtestSmoke` | exit 0, with 8/8 checks: 139 cards, every card builds a token, 29/29 real models load, all five core SFX cues resolve for every card (58 card-specific, the rest generic), 235/235 events applied, winner seat 0 (`playtest-smoke.json`) |
| Screenshots | taken from the built player with `-playtestShots` (1280×720) |

## Screenshots

| File | What to look at |
|---|---|
| `01-board-overview.png` | Mid-match (event 180): real Olympus Citadel, Atlantis Nexus and Skyline Seer next to stand-in lands, structures and characters |
| `02-board-selection-markers.png` | Skyline Seer selected: legal-target markers, a hover highlight, and the stack tooltip |
| `03-board-overview-standins-only.png` | The same moment with every piece as its typed stand-in |
| `04-board-game-over.png` | The final state and the GAME OVER banner (Poseidon capital HP 7 → destroyed) |
| `05-gallery-real-models.png` | All 29 staged Meshy models at the AI-063 scale |
| `06-gallery-stand-ins.png` | All 110 stand-ins by type: CHARACTER robots, STRUCTURE towers, LAND slabs tinted by terrain, SPELL VFX markers |
| `07/08-closeup-real-*.png` | Close-ups of real capitals and characters (HA-009 style check) |
| `09-closeup-standins.png` | Close-up of stand-in structures and characters with name plates |

## Real models vs stand-ins (from `staging-report.json`)

- **Real Meshy models (29): 22 CHARACTER, 6 CAPITAL, 1 STRUCTURE.**
  - The 6 capitals are from batch-01.
  - Batch-02 supplies the Poseidon apex units and Abyssal Leviathan.
  - Batch-03 supplies the Poseidon rarity-3 units.
  - Batch-04 (img2-3d, newest) is preferred for the 9 Zeus units it covers. The rigged versions win for Olympian Storm Titan and Keraunos Prime. Rigged models are shown as static meshes; clips are not used yet.
  - The AI-052 `textured/` versions are used for Thunder Ram, Abyss Gate and Leviathan Wakeborn.
  - Skyline Seer is untextured and was decimated from 575k to 60k triangles.
- **Stand-ins (110):** all 35 LAND, 33 of 34 STRUCTURE, 26 of 48 CHARACTER and all 16 SPELL (SPELL is VFX only, by contract).
- **Selection rule:** rigged, then textured, then newest batch directory. `check_glb.py` runs first; any exit-2 rejection is skipped, and there were 0 rejections. Every file fails only `feet_at_origin` or `height`, because Meshy centres its models. `glb_to_playtest_token.py` normalises the origin to bottom centre. Unity then scales each model uniformly to its AI-063 budget, with a hex taken as 2.0 contract units across the flats:
  - CHARACTER: 1.8 units tall and no more than 0.8 hex wide
  - STRUCTURE: no more than 1.6 units tall and 0.9 hex wide
  - CAPITAL: no more than 2.2 units tall and 1 hex wide
  - LAND: 1 hex wide, top no higher than 0.25 units
- **SFX:** 94 picks for 14 cards (8 cues each for apex units, 5 for capitals) come from `elevenlabs/**/picks/<card_id>_<cue>.wav`. `SFX_INDEX.json` had not been written yet; it is used automatically once present. Every other card falls back to a synthesised generic cue for each cue, and the Poseidon versions are pitched lower.

## Style questions for HA-009

1. Batch-04 (img2-3d) or batch-02 for the Zeus apex units? The build prefers batch-04, and batch-02 remains as an alternative in the staging report.
2. Stand-in look (robot capsule, hex tower, spire, terrain-tinted slab): is it good enough for playtesting?
3. Board scale: a character stands about 0.9 hex tall. Is that readable at the default camera?

## Limits

- The capital positions come from the EventDump harness: (1,0) and (2,5). The engine emits no capital-placement event.
- GP comes only from events, clamped at 0, so it is approximate.
- Hands are counts only in playback.
- No animation clips are used yet.
- Staged GLBs and WAVs are copied locally by `UnityProof/Tools/stage_playtest_assets.py` and are git-ignored, so a fresh clone builds with stand-ins only until that script runs.
