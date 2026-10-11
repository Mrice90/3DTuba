# Engine handoff: changing the board from 4 × 6 to 6 × 8

**Owner:** ChatGPT (rules). Claude did not change any engine code, data or DLLs in this delivery.
**Target (Matt, 2026-10-11):** 6 columns × 8 rows = 48 hexes, double today's 24. Each side's home half (plot) becomes 4 rows: Zeus rows 0–3, Poseidon rows 4–7.

Paths below are relative to `Infinite Conquest\`. The C# line numbers come from `reviews\native-csharp\handoff\source\` (the AI-087 handoff that produced the pinned `InfiniteConquest.RulesCore.dll` `1a985fdc…` and `InfiniteConquest.Native.dll` `d20045d7…`). The Java line numbers come from `reviews\csharp-core-draft\java-oracle-source\game-core\src\main\java\com\infiniteconquest\core\` (Desolate-Tuba e0e565b) and `reviews\native-csharp\oracle\RulesBridge.java`.

## 1. Required: the C# rules core (`rules-core/`)

| File:line | Today | Change for 6 × 8 |
|---|---|---|
| `Core/BoardPosition.cs:13` | `public const int Width = 4;` | `6` |
| `Core/BoardPosition.cs:14` | `public const int Height = 6;` | `8` |
| `Core/BoardPosition.cs:15` | `public const int PlotHeight = 3;` | `4`. This sets each side's home half. |
| `Core/BoardPosition.cs:23` | `"Position outside 4x6 battlefield: …"` | Build the text from `Width`/`Height`, e.g. `$"Position outside {Width}x{Height} battlefield: …"`. If the error text is compared against Java, keep both identical. |
| `Core/BoardPosition.cs:36-49` | `IsOnPlayerSide` / `IsOnEnemySide` use `PlotHeight` | No code change; they follow `PlotHeight`. Check the rules intent: invading the enemy half halves movement (`MovementRules.cs:23`), and capitals must start in their own half (`CapitalDeployment.cs:23`). |
| `Core/BoardGeometry.cs:53` | `if (a.Y * 4 + a.X > b.Y * 4 + b.X)` | **Hard-coded width.** Change to `a.Y * BoardPosition.Width + a.X > …`. It decides the canonical direction of a line-of-sight trace. With `*4` on a 6-wide board, different hexes get the same key: (4,0) and (0,1) both give 4, and (5,0) and (1,1) both give 5. Such pairs are never canonicalised, so line of sight between them can depend on which side looks. |
| `Core/BoardGeometry.cs:38-39, 72` | Neighbour scan and trace bounds use `Width`/`Height` | No change. |
| `Core/BoardState.cs:20-21` | Board cells built from `Width`/`Height` | No change. The board grows to 48 cells in the same row-major order. |
| `Core/LineOfSightRules.cs:~40-57` | SQUARE (Bresenham) path | No constants, so no change. Only the HEX path is used in matches (`MatchRules.Hex()`, `Core/MatchRules.cs:44`). |
| `Core/GameEngine.cs:454-466` | Land territory = adjacent to own land or capital | No constants, so no change. |
| `Core/SnapshotRedactor.cs:16` | Board serialised in Y-then-X order | No change. Snapshots carry plain `x`/`y` with no size field. Optionally add `board: {width, height, plot_height}` to `client_metadata`, so the client can check it is drawing the engine's size (see section 5). |
| `Bot/BotPlayer.cs` | Heuristics use distance, sight and capital exposure only, with no board constants | No code change needed. **Retune advised:** `move` = 35 and exploration width 3 were tuned on 24 hexes; with longer walks the bots may stall in the middle. |
| `Bot/ActionHints.cs`, `Bot/CommandProcessor.cs:156` | Parse/print `x y` | No change. Coordinates are validated by `BoardPosition`. |

## 2. Required: the native adapter (`adapter/`, inside `InfiniteConquest.Native.dll`)

| File:line | Today | Change |
|---|---|---|
| `NativeRulesSession.cs:99` | Default capitals when the player hasn't placed one, and for the bot: `new BoardPosition(p==0?1:2, p==0?0:5)`, i.e. Zeus (1,0), Poseidon (2,5) | Poseidon's default row must become **7** (the back row). The column is a design choice: (1,0)/(2,5) are the near-centre columns of a 4-wide board. On 6 wide the centre columns are 2–3, so (2,0) and (3,7) are a sensible pair. The Java bridge must use exactly the same values (section 3). |
| `NativeActionHints.cs:61-62` | `int capacity = destinations.Count + 1 > 12 ? 32 : 16;` then `OrderBy(p => (31*p.X+p.Y)&(capacity-1))` | This copies Java `HashSet` iteration order for movement hints. Java's table doubles again past 24 entries (64 buckets). A 6 × 8 board can give one character more than 24 destinations, so extend it: `n > 24 ? 64 : n > 12 ? 32 : 16` (n = count + 1, as now). Leaving it as is gives a different hint order from Java and breaks the native/Java transcript match. |
| `PlaytestBot.cs` | Parses coordinates only | No change. |

After the edits, rebuild both DLLs and replace `UnityProof/Assets/Plugins/NativeRules/InfiniteConquest.RulesCore.dll` and `InfiniteConquest.Native.dll`. The handoff hash pins in `reviews/native-csharp/HANDOFF.md` will change, so record the new ones.

## 3. Required if the Java fallback/oracle stays: Java core and bridge

| File:line | Change |
|---|---|
| `BoardPosition.java:4-6` | `WIDTH = 6`, `HEIGHT = 8`, `PLOT_HEIGHT = 4` |
| `BoardPosition.java:10` | `"Position outside 4x6 battlefield"`: same change as the C# message |
| `BoardGeometry.java:31` | `a.y() * 4 + a.x()`: use `WIDTH`, the same change as C# `BoardGeometry.cs:53` |
| `BoardGeometry.java:21, 46`; `BoardState.java:9`; `CapitalDeployment.java:14`; `MovementRules.java:15` | Follow the constants, so no edit |
| `reviews/native-csharp/oracle/RulesBridge.java:458` | Bot/default capital `new BoardPosition(humanPlayer == 0 ? 1 : 2, humanPlayer == 0 ? 0 : 5)`: the same new default pair as `NativeRulesSession.cs:99` |
| `RulesBridge.java:484-485` | Multiplayer defaults `new BoardPosition(1, 0)` / `new BoardPosition(2, 5)`: same pair |

Then rebuild the Java bridge (jar + `RulesBridge.class`).

## 4. Online relay

`reviews/launch-readiness/net-lab/relay.js` and `worker-v2.js` contain no board constants: they forward engine messages. **No relay change is needed.** Whatever engine each online client or host runs must be the 6 × 8 build, or the two sides will reject each other's moves. Don't mix 4 × 6 and 6 × 8 builds in one online room.

## 5. Unity client (already prepared in this delivery)

- `Assets/Proof/BoardConfig.cs` reads `RulesWidth/Height/PlotHeight` **directly from the RulesCore DLL constants** (`BoardPosition.Width/Height/PlotHeight`). When the 6 × 8 DLL is dropped in and Unity recompiles, the playable board, capital placement, tints, plinth and camera all follow, with no client code edits. At that point `-board 6x8` becomes a no-op, because the drawn and rules sizes match.
- `BoardSizeChecks` deliberately **fails** on "engine still plays 4x6 with 3 home rows" once the engine changes. That is the reminder to update the client tests in the same change:
  - `Playtest/Editor/BoardSizeChecks.cs`: change the expected engine size to 6 × 8 / 4, the expected capital cells (12 + 12 becomes 24 + 24), and the default-layout regression (it pins the old 4 × 6 centres).
  - `Playtest/Editor/RestorationBuild.cs:13-16`: `ValidCapitalCell` cases. `(4,5)` "outside board" becomes inside on 6 × 8, so use `(6,7)`.
  - `Playtest/Editor/CameraSnapChecks.cs:18` and `BoardSizeChecks.CameraFraming`: capitals at (1,0)/(2,5). Move them to the new default pair.
  - `Playtest/Scripts/PlaytestGame.cs` `PlaceDemoCapitals` (2,5) is used only for the recorded seed-42 demo replay, which was recorded on 4 × 6. Keep it, or re-record the demo on 6 × 8.
  - `Playtest/Editor/R3Checks.cs` and `InterruptCueChecks.cs` use fixed 4 × 6 coordinates. They stay valid on a bigger board, but rerun them.
- Camera framing at 1280×720 is already checked for 6 × 8: all 48 cells are in view in all four snap views, for both seats.

## 6. Data

`Assets/StreamingAssets/NativeRules/` (`cards/*.json`, `faction-starters.json`, `structure-slots.properties`) has **no coordinates or board-size values** (checked 2026-10-11). No data change is needed for the size alone. Card text that names ranges or rows should still be reread for the new rules (e.g. "enemy territory", "back row").

## 7. Re-verify after the change

1. Native and Java parity: regenerate the native-vs-Java transcript comparison. The old 4 × 6 transcripts and pins no longer apply.
2. Unity: run `BoardBuild.Build` (board checks, cue, native, R3, camera, coin, restoration, gameplay, audio, player build), then `reviews/board-2026-10-11/run_packaged.py` (both seats, several seeds, until GAME_OVER).
3. Online: both clients on the same engine build.
