# Deck shop r2 + board size: client merge (2026-10-11)

This folds ChatGPT's deck shop r2 handoff into the board-size client build. It's client-only: the rules engine still plays 4 × 6, and no rules code, data or DLLs changed. Nothing was pushed, merged or sent remotely, and no paid jobs were run.

- **Checkout:** `3DTuba-deck-shop-board-2026-10-11/UnityProof`, a new isolated copy of `3DTuba-deck-shop-r2/UnityProof`. Its Library was reused; Build, Logs and Temp were not copied.
- **Package:** `playtest/unity-build-deck-shop-2026-10-11/`. It contains `PLAY-INFINITE-CONQUEST.cmd` (main menu), `PLAY-BOARD-6x8-PREVIEW.cmd`, `PLAY-BOARD-HEX-PREVIEW.cmd` and `build.log`.
- **Build method:** `DeckShopBoardBuild.Build` (new file, `Assets/Playtest/Editor/DeckShopBoardBuild.cs`). It runs `BoardSizeChecks.Run()` and then `DeckShopBuild.Build()`. The second call runs the 39 shop checks and then `CombinedBuild.Build()`, which covers cue, native, R3, camera, coin, restoration, gameplay, audio and the player build. One call therefore runs both chains.

```
Unity.exe -batchmode -quit -projectPath <checkout>/UnityProof -executeMethod DeckShopBoardBuild.Build -buildPath <dir>/InfiniteConquestPlaytest.exe -logFile <log>
```

## Lineage check (step 1)

None of the checkouts is a git repo, so I confirmed lineage with a full file diff of `Assets`, `Tools`, `Packages` and `ProjectSettings` against `3DTuba-combined-2026-10-11/UnityProof`.

- `3DTuba-deck-shop-r2` differs from combined only in the files from the purchase prototype and deck shop r2. These are the new Market/DeckShop/CardFinish/HandMotion/PrototypeDeckEditor scripts, the edited shared partials, card-faces.json, the presets, the rules DLLs that came with the accepted purchase handoff, and `build_installer.ps1`. **It is combined-based.**
- The deck-shop checkout was missing `README.md`, `Tools/*.py` and `Tools/package_gameplay_playtest.ps1`. Those files are unchanged in combined, so I restored them from combined; they are not in the overlay.
- `3DTuba-board-2026-10-11` matches the handoff overlay in `3DTuba-handoff/board-2026-10-11/overlay` byte for byte (all 14 files).

## Merge (step 2) and conflicts

I ran a 3-way merge per file with `git merge-file`: base combined, ours deck-shop-r2, theirs board. Line endings were normalised to LF for the merge. The result is written with the board file's line endings: LF for the three .cs files, CRLF for `build_installer.ps1`. Before this, the three inputs had mixed CRLF and LF.

| File | Both sides? | Result |
|---|---|---|
| `Assets/Playtest/Scripts/PlaytestGame.cs` | yes | **Merged with no textual conflict.** Board hunks kept: `BoardConfig.ApplyCommandLine`, home distance × `BoardLayout.CameraScale`, `LiveLogRect`/`BoardShiftPx`, `rig.ScreenShiftPx` set before the log panel, and the `BoardView.H - 1` hover clamp. Shop hunks kept: `-shopPreview` start, `ClearMatch` hand/market reset, reaction auto-close, `InputEnabled` and `!marketOpen`, Escape closing the shop first, early return from hover/picking while shopping, `marketRect` in `OverHud`, `PurchaseMode`, the modal draw path, the `DrawPurchaseMarket` draw order after `DrawCardView`, You/Opponent labels, ASCII glyphs and `DrawPrintedHand`. |
| `Assets/Playtest/Scripts/LiveMatchValidation.cs` | yes | **1 conflict.** The shop added `bool printedHandShot=false;` and the board added `boardShots` to the same `bool openingShot...` line. I kept both declarations. Both features are used later in the file. |
| `Assets/Playtest/Scripts/MatchRestoration.cs` | yes | Clean. Board: capital placement through `BoardConfig` and `CameraScale`. Shop: the "Coin flip..." ASCII glyph. |
| `Tools/installer/build_installer.ps1` | yes | Clean. Board: the two preview `.cmd` files added to the payload. Shop: hidden iexpress window and a guarded staging cleanup. |
| BoardView, CameraRig, BoardLayout, MovementState, BoardConfig, BoardSizeChecks, install.ps1, uninstall.ps1 | board only | Byte-identical to the board checkout. |
| All shop and purchase files | shop only | Byte-identical to the deck-shop-r2 checkout, including the native DLLs. |

### Semantic check: shop modal against the board's input paths

The shop modal also covers the board's camera and preview input:

- `rig.InputEnabled = ... && !marketOpen` turns off all of `CameraRig.HandleInput`. That includes the board's wheel and R/F zoom (now capped at `MaxDistance * BoardScale`), the 1-4, V and Home snap views, and right-drag orbit.
- The "View (V)" GUI button isn't drawn while the modal is open, because `OnGUI` returns from the modal path first.
- Hover and picking return early with `PointerOverHud = true`.
- `-board` is applied before `SetupScene` and doesn't depend on `-shopPreview`. The two flags work together, as the captures with `-board 6x8` and `-board hex` show.
- While the modal is open, the `ScreenShiftPx` assignment is skipped, so the previous frame's lens shift stays in place. The board doesn't jump when the shop opens or closes.

## Verification (steps 4-5)

**Unity 6000.6.3f1 build: exit 0, "Build Finished, Result: Success".** The log has 238 `warning CS` lines, the same number as ChatGPT's r2 delivered build, so the merge added no new warnings. The JSONs are in `evidence/editor-checks/`.

| Suite | Result |
|---|---|
| Board size (`BoardSizeChecks`) | 110 PASS |
| Deck shop (`DeckShopBuild`) | 39 PASS |
| Reaction cue / turn sound | 42 PASS |
| Native integration | 12 PASS, both seats, native |
| R3 | 35 PASS |
| Camera snap | 50 PASS |
| Coin | 16 PASS |
| Restoration | 669 PASS (570 audio bindings) |
| Gameplay priority | 25 PASS |
| Audio build | 230 cues, 88 cards, PASS |

The rules DLLs in the player are byte-identical to deck-shop-r2: Native `59945d8e…` and RulesCore `741af665…`. Assembly-CSharp is `989429fe…`.

**Packaged player (`run_packaged.py`): 24 runs, all exit 0 and passed.** Results are in `evidence/packaged-results.json` and `evidence/runs/`.

- Playback smoke passed.
- 20 live bot matches reached GAME_OVER: default board with seeds 42/713/7, and the 6x8 and hex previews with seeds 42/713, each as Zeus (seat 0) and as Poseidon (seat 1). Six of the 20 were the windowed 1280×720 shot runs, seed 42, both seats, on each board.
- Totals: 738 actions, 764 HP-snapshot and ability-list checks, 35 command-center activations, 9 capital-damage checks and 6 heal checks.
- The results are identical across boards for the same seed and seat, as expected while the engine plays 4 × 6.
- Shop capture at 1280×720 on the default, 6x8 and hex boards (`-shopPreview`): each opens the shop, shows the All tab, the Lands tab, affordable characters sorted high-first, an empty-filter state, and then a **real purchase**. All three runs report `actualPurchase: true`. On screen: All 40 → 39, Lands 10 → 9 "(used)", Empyrean Current 2 → 1 left, gold 3 → 2, other lands show "Category purchased this turn". Shots are in `evidence/screens/shop-*`.
- Live shots on each board, both seats, are in `evidence/screens/live-*`: the board from both views, summon slots, spell targets, the printed hand, the purchase market and game over.
- Launcher captures are in `evidence/launch-menu*.png`. All three `.cmd` files open the main menu. The 6x8 and hex launchers draw their board behind the menu.

## Board-fit findings (step 3)

I didn't change any rules. Most of the shop works on the bigger boards without changes; the one real problem is the hand hit area (item 3).

1. **The shop modal is fine on 6x8 and the hexagon.** It's full screen: a 1000 px panel and a black backdrop drawn by `OnGUI`, with no board-size maths. It's identical on all three boards.
2. **"Buy from deck" button (12, 626, 180×30) doesn't overlap the board.** It sits left of the board, which starts at x ≈ 378 on the previews. It can overlap the bottom of the Abilities panel when that panel is tall. That comes from the shop and isn't board-related.
3. **The hand hit area isn't in the board framing checks.** `HandMotion.handRect` at 720p runs from y ≈ 450 to the bottom, and from the first card's x to the right edge. `OverHud` swallows clicks inside it. In the "Behind you" view, the 6x8 and hex previews end at about y ≈ 500, so the lower part of the nearest home-row hexes can fall under that rect when the hand is wide (about 4 or more cards). The default 4 × 6 board reaches y ≈ 545, so it already overlaps more; the previews are no worse. `BoardSizeChecks.HudPanels` lists the log, Abilities, top bar and player panels, but not the hand. A follow-up could add the hand rect to `HudPanels` or tighten `handRect`; I didn't change it here.
4. **No 4 × 6 assumptions on the shop side.** The shop-side scripts (PurchaseMarket, DeckMarketQuery, DeckShopPreview, PrototypeDeckEditor, HandMotion, CardFinish*, shop edits in PlaytestGame/PlaytestMenus/RestorationR3/ReactionCue/OnlineMatch/PlaytestActions) contain no board-size literals. The `< 4` in PrototypeDeckEditor and CardFinishPreview counts card copies, not board cells.
5. **No coordinates in the presets.** `faction-starters.json` and `previous-starters.json` contain only card IDs and copy counts. Default capitals stay in the engine (handoff section 7: Zeus (3,0) / Poseidon (4,6) for the hexagon).
6. **The hexagon's neutral middle row doesn't affect the shop.** Under section 7 the middle row can't hold capitals and gets no movement halving. The shop and presets don't care: buying is board-independent and play targets come from engine legal actions. No card text in `card-faces.json` mentions sides, halves or rows. One regex hit was a false match on "drowned".
7. **The presets were tuned on 4 × 6.** Land income 22, structure slot supply 20, and a character curve set for a 24-cell board. On 37 or 48 cells, travel time and capital distance change, so pacing and economy need a balance pass after the engine change. That's for ChatGPT and Matt.
8. **Inherited, not board-related:** the third line of a shop row ("Ready to buy") loses its descenders, showing as "buv". ChatGPT's r2 captures show the same thing.

## Not tested

- No human mouse/keyboard pass.
- Reaction windows and Storm Relay activations weren't reached in this matrix; the focused editor suites passed.
- The installer wasn't built or run. `build_installer.ps1` is merged but not executed.
- No online or two-PC play.
- No hands-on shop with several cards in hand on the previews (finding 3).
- The engine still plays 4 × 6, so the 6x8 and hex runs exercise drawing and camera only, not the rules.

## Ownership note

`SPRINT_LOG.md` row AI-110-DECK-SHOP-BOARD-CLIENT-MERGE. I recorded it at 16:20 EDT, after I'd started at 16:08 EDT. Codex has a separate claim, **AI-110-BOARD-SHOP-INTEGRATION**, for a fuller board + shop integration with a 6x8 engine in `3DTuba-board-shop/`. That claim overlaps this work in task scope only: there are no shared files, checkout, output folders or Unity editor. I didn't read or touch Codex's folders.

## Files

- `overlay/`: every changed or new file relative to `UnityProof/` against combined, 46 files listed in `changed-paths.txt`. It's the merged result for files touched by either handoff, plus `DeckShopBoardBuild.cs` and its .meta.
- `deck-shop-board-vs-board.patch`: 36 files, relative to `3DTuba-board-2026-10-11/UnityProof`, binary-safe. A test run of `git apply --binary` on a copy of the board's `Assets` and `Tools` reproduced the merged checkout byte for byte.
- `run_packaged.py`, `launch_smoke.ps1`: the scripts that produced the evidence.
- `evidence/`: `editor-checks/`, `packaged-results.json`, `runs/` (per-run result.json), `screens/`, `launch-menu*.png`, `launchers/` (copies of the .cmd files), `logs/` (Unity build log, packaged run output).
- `SHA256SUMS.txt`.
