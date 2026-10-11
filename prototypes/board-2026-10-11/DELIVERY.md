# AI-110-BOARD-6x8-CLIENT delivery (Claude Code, 2026-10-11, r2)

**Package:** `playtest/unity-build-board-2026-10-11/`, rebuilt in place for r2.
- `PLAY-INFINITE-CONQUEST.cmd`: the engine's 4 × 6 board.
- `PLAY-BOARD-6x8-PREVIEW.cmd`: the planned 6 × 8 rectangle.
- `PLAY-BOARD-HEX-PREVIEW.cmd`: the hexagon board.

**Installer:** `playtest/installer-board-2026-10-11/InfiniteConquest-Setup.exe`, 364 MB, sha256 `864bbc839b9468be7b0dd040b365443a6aa305f0832361514d4b259769badfba`.

**Source:** `3DTuba-board-2026-10-11/UnityProof`, an isolated copy of `3DTuba-combined-2026-10-11/UnityProof`. Its 52 overlay files equal the PR #15 overlay at 4920383.

**Scope:** I changed the Unity client and installer only. The rules engine (RulesCore/Native DLLs, NativeRules data, Java bridge, relay) is ChatGPT's and is **unchanged**: the packaged RulesCore `1a985fdc…` and Native `d20045d7…` are the pinned DLLs. Hand cards and the new rules are ChatGPT's and were not touched.

## What changed

**r1:**
- One board setting, `Assets/Proof/BoardConfig.cs`. The playable size (`Rules*`) is read from RulesCore `BoardPosition.Width/Height/PlotHeight`. `Planned*` is 6 × 8 with 4 home rows. `-board 6x8` draws it, with the 4 × 6 rules grid centred inside; the outer cells are drawn but inert.
- `BoardLayout`, `MovementState`, `BoardView`, capital placement, the plinth and the camera all read this setting.

**r2 (Matt: "please proceed with project of board size", and "a more hexagonal board might be more esthetic"):**
1. **Log-panel overlap fixed.**
   - On boards bigger than 4 × 6, a 29 px horizontal lens shift (`CameraRig.ScreenShiftPx`, projection only) moves the board into the gap between the open Abilities panel and the log panel. Snap distances also grow a little more (`CameraScale = 1 + 1.7 × (ViewScale − 1)`; 1.64 for 6 × 8).
   - Camera pose, target, snap views and click-picking are unchanged.
   - The 4 × 6 default is unchanged: no shift, scale 1.
2. **Hexagon preview, `-board hex`.**
   - Radius 3 = **37 hexes**, rows 4-5-6-7-6-5-4, centred on rules cell (1,3), so the whole 4 × 6 grid sits inside it.
   - 15 home hexes a side, plus a neutral, untinted middle row of 7.
   - Flat-topped hexagonal plinth that follows the outline.
   - Why radius 3: 37 is the regular hexagon closest to 48 (radius 4 is 61). An even split isn't possible, because a regular hexagon always has an odd number of rows.
3. **Installer.**
   - Start menu shortcuts "Infinite Conquest (6x8 board preview)" and "Infinite Conquest (hexagon board preview)", next to the normal one.
   - Setup now closes a running game only if it runs from the install folder.
   - New `IC_TEST_ROOT` switch for isolated test installs. Its uninstaller removes only that test install.
4. **`ENGINE-HANDOFF-6x8.md` section 7**: the engine side of the hexagon option (7 × 7 offset grid plus an on-board mask, middle-row rule, default capitals (3,0)/(4,6)).

**Changed files** (`overlay/`; `board-vs-combined-2026-10-11.patch` applies with `git apply -p1`, and the result equals `overlay/` apart from CRLF):
- `Assets/Proof/BoardConfig.cs` (new), `BoardLayout.cs`, `MovementState.cs`
- `Assets/Playtest/Scripts/BoardView.cs`, `CameraRig.cs`, `PlaytestGame.cs`, `MatchRestoration.cs`, `LiveMatchValidation.cs`
- `Assets/Playtest/Editor/BoardSizeChecks.cs` (new, includes `BoardBuild.Build`)
- `Tools/installer/build_installer.ps1`, `install.ps1`, `uninstall.ps1`

## Board sizes

| | Hexes | Home split | Playable now |
|---|---|---|---|
| Engine / default | 4 × 6 = 24 | 12 / 12 | all 24 |
| `-board 6x8` | 48 | 24 / 24 (4 rows each) | the centre 24 |
| `-board hex` | 37 | 15 / 15, plus 7 neutral | the 24 inside |

## Checks (`BoardBuild.Build`, Unity 6000.6.3f1, exit 0)

| Suite | Result |
|---|---|
| Board size | **110 PASS**. Details below. |
| Camera snap | 200 PASS (50 × 4: 6 × 8, hexagon, rules grid, chain) |
| Reaction cue / native engine / R3 | 42 / 12 / 35 PASS |
| Coin / restoration / gameplay | 16 / 669 (570 audio bindings) / 25 PASS |
| SP1 audio | 230 cues / 88 cards PASS |

What the 110 board-size checks cover:
- **Engine and default layout:** the size is read from the engine DLL, and the default 24 cell centres are identical to the combined build.
- **Geometry (4 × 6, 6 × 8, hexagon):** no overlapping tiles, the board is centred, engine adjacency equals screen adjacency, and capital placement stays on the 12 + 12 engine home cells.
- **Hexagon shape:** 37 cells in rows 4-5-6-7-6-5-4, homes 15/15/7, and every tile corner sits on the plinth.
- **Tiles:** 24 input tiles plus inert preview tiles.
- **Camera at 1280×720:** for both seats, all 4 views, and **all 144 capital-placement pairs**, every drawn cell is in view. On both previews, no tile corner is under the log, Abilities, top-bar or player panels.

The 4 × 6 default's overlaps are reported, not asserted. Some placements put a corner under the log panel, and the top-down view touches the top bar. These are unchanged from the build Matt played.

## Packaged runs (`run_packaged.py`; all exit 0 and passed; 18 matches all reached GAME_OVER)

| Runs | Board | Result |
|---|---|---|
| Playback smoke | 4 × 6 | PASS |
| Seeds 42 / 713 / 7, Zeus and Poseidon seats (6) | 4 × 6 | 6/6 GAME_OVER. Transcripts **byte-identical** to the combined build |
| Seeds 42 / 713, both seats (4 per preview) | 6 × 8 and hex | 8/8 GAME_OVER. Transcripts byte-identical to the 4 × 6 runs |
| Windowed 1280×720 shot runs, seed 42, both seats | 6 × 8 and hex | 4/4 GAME_OVER, screenshots in `evidence/` |

Totals: 981 actions, 1,003 HP/ability-list checks, 194 command-center activations, 65 capital-damage and 12 heal checks. Assembly-CSharp `6766c75b…`.

**Folder launch:** all three `.cmd` files reach the main menu (`evidence/launch-menu*.png`).

**Installer** (`test_installer.ps1`, `evidence/installer-test.txt`):
- The test install into an isolated folder exited 0 and created the files, 4 Start menu shortcuts, a desktop shortcut and a test Apps entry.
- 0 Java files. Assembly-CSharp, RulesCore and Native equal the package.
- With Java off PATH and JAVA_HOME unset, all three shortcuts reached the main menu, with the right `-board` argument and 0 Java processes (`evidence/installed-menu*.png`).
- Uninstall removed all of it.
- Matt's real install (2026.10.11-native) was not touched: same version and exe hash before and after.

## Evidence (`evidence/`)

- `board-6x8-*` and `board-hex37-*`: Zeus view and Poseidon view (each seat's view 1), plus the opponent view (view 2) from each seat, at 1280×720. End turn, the Abilities panel and the log panel are all clear of the board.
- Game-over shots, launch and installed menu captures, every suite JSON, `packaged-results.json`, `installer-test.txt`.

## Not tested / open

- Neither preview board is fully playable: only the engine's 24 hexes take moves until the engine changes (ChatGPT; see `ENGINE-HANDOFF-6x8.md` §1-6 for 6 × 8 and §7 for the hexagon).
- Matt hasn't played this build or the new installer by hand.
- Online play between two PCs or through the relay: Node.js and the relay aren't set up here.
- I didn't install over the real install, on purpose. IExpress quiet `/Q` mode is still untested.
- The Java fallback build was not rebuilt.
