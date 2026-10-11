# AI-110-BOARD-6x8-CLIENT delivery (Claude Code, 2026-10-11)

**Package:** `playtest/unity-build-board-2026-10-11/`
- `PLAY-INFINITE-CONQUEST.cmd` plays on the engine's 4 × 6 board.
- `PLAY-BOARD-6x8-PREVIEW.cmd` draws the planned 6 × 8 board.

**Source:** `3DTuba-board-2026-10-11/UnityProof`, a new isolated copy of `3DTuba-combined-2026-10-11/UnityProof`. Before I copied it, I checked that its 52 overlay files were identical to the PR #15 overlay (`claude/combined-2026-10-11` @ 4920383).

**Scope:** Unity client only. Matt chose a 6 × 8 board. The rules engine (RulesCore/Native DLLs, NativeRules data, Java bridge, relay) is ChatGPT's and was **not changed**: the packaged RulesCore `1a985fdc…` and Native `d20045d7…` are the same pinned DLLs. Hand cards and new rules are ChatGPT's and were not touched.

## Board size

| | Before | After |
|---|---|---|
| Engine (plays) | 4 × 6 = 24 hexes, 3 home rows each | unchanged: 4 × 6 = 24 |
| Client default | 4 × 6 hard-coded (`MovementState`, `BoardLayout`, `4`/`6`/`3` in capital placement) | follows the engine: `BoardConfig.Rules*` read from `RulesCore BoardPosition.Width/Height/PlotHeight` |
| Planned (`-board 6x8`) | none | 6 × 8 = **48 hexes** (2×), 4 home rows each, drawn with the 4 × 6 rules grid centred at drawn (1,1) |

There is one setting, `Assets/Proof/BoardConfig.cs`:
- `RulesWidth/Height/PlotHeight` come from the engine.
- `PlannedWidth/Height/PlotHeight = 6/8/4`.
- `Width/Height/PlotHeight` are what gets drawn: `-board 6x8|planned|rules`.

`BoardLayout`, `MovementState`, `BoardView`, capital placement, plinth and camera all read it. In the preview, row parity stays tied to the rules rows, so hexes that are adjacent in the engine are adjacent on screen (checked).

## Changed files (overlay/)

New:
- `Assets/Proof/BoardConfig.cs`
- `Assets/Playtest/Editor/BoardSizeChecks.cs`, with `BoardBuild.Build` = board checks, then `CombinedBuild.Build`

Changed:
- `Proof/BoardLayout.cs`
- `Proof/MovementState.cs`
- `Playtest/Scripts/BoardView.cs` (preview tiles, plinth scale)
- `CameraRig.cs` (snap and zoom distances scale with the board)
- `PlaytestGame.cs` (reads `-board`, home distance, one hard-coded row clamp)
- `MatchRestoration.cs` (capital placement from the engine constants)
- `LiveMatchValidation.cs` (board shots from both views; board fields in the result)

`board-vs-combined-2026-10-11.patch` is the full diff against the combined sources. It applies cleanly with `git apply -p1`, and the result is identical to `overlay/`.

## Checks (`BoardBuild.Build`, Unity 6000.6.3f1, exit 0)

| Suite | Result |
|---|---|
| Board size (new) | 45 PASS. Engine size read from the DLL; default 24 cell centres identical to before; 6 × 8: 48 cells, no overlaps, centred; engine adjacency equals screen adjacency (4 × 6 and 6 × 8); capital cells 12 + 12; 24 input + 24 inert tiles; framing at 1280×720 shows 48/48 cells in all 4 views for both seats |
| Camera snap | 150 PASS (50 × 3: 6 × 8 preview, rules grid, and the chain) |
| Reaction cue / native engine / R3 | 42 / 12 / 35 PASS |
| Coin / restoration / gameplay | 16 / 669 (570 audio bindings) / 25 PASS |
| SP1 audio | 230 cues PASS (build + validate) |

## Packaged runs (`run_packaged.py`, all exit 0, all `passed`)

| Run | Board | Result |
|---|---|---|
| Playback smoke | 4 × 6 | PASS |
| Live, seeds 42 / 713 / 7 × Zeus and Poseidon seats (6 matches) | 4 × 6 | 6/6 GAME_OVER. Transcripts **byte-identical** to the combined build's same runs |
| Live preview, seeds 42 / 713 × both seats (4 matches) | 6 × 8 drawn | 4/4 GAME_OVER. Transcripts byte-identical to the 4 × 6 runs (the preview changes nothing in the rules) |
| Windowed 1280×720 shot runs, seed 42 × both seats | 6 × 8 drawn | 2/2 GAME_OVER, screenshots below |

Totals over the 12 live matches: 675 actions, 689 HP/ability-list checks, 129 command-center activations, 41 capital-damage and 8 heal checks. Assembly-CSharp `1d47a270…`.

Launch through the `.cmd`: both launchers reached the main menu (1296×688, responding), then closed (`evidence/launch-menu*.png`). The payload has 0 Java files.

## Evidence (evidence/)

- `board-6x8-zeus-view-1280x720.png`: Zeus seat, view 1 (behind you)
- `board-6x8-poseidon-view-1280x720.png`: Poseidon seat, view 1
- `board-6x8-poseidon-view-from-zeus-seat-1280x720.png` and `…zeus-view-from-poseidon-seat…`: view 2 (opponent)
- End turn (bottom-left) and the Abilities panel (left) are visible in all of them.
- `board-6x8-game-over-zeus-seat.png`, the suite JSONs and `packaged-results.json`

## Not tested / open

- The 6 × 8 board is **not playable** yet. Only the centre 24 hexes take moves until the engine changes (`ENGINE-HANDOFF-6x8.md`, ChatGPT).
- Human hands-on play of this build hasn't been done.
- I did not run a two-PC or relay online match: Node.js and the relay aren't set up on this machine.
- The Java fallback was not rebuilt, since this delivery doesn't change it.
- The installer was not rebuilt; `playtest/installer-combined-2026-10-11` is unchanged.
- At 1280×720 the far-right corner of the 6 × 8 board slightly underlaps the semi-transparent log panel in the behind-you view. Overview (4) and top-down (3) show it fully.
