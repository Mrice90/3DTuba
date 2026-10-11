# Deck shop + board size merge (2026-10-11)

This folds ChatGPT's deck shop r2 handoff into the board-size client from `prototypes/board-2026-10-11`, so the shop runs on the 6 × 8 and hexagon previews. It's client-only: the rules engine still plays 4 × 6, and no rules code, data or DLLs changed.

Base: combined (2026-10-11) + purchase prototype + deck shop r2 + board r2 (commit `d0d0dca`).

- `overlay/`: every new and changed file, relative to `UnityProof/`, against combined (46 files, listed in `changed-paths.txt`). Copy it over a combined `UnityProof`.
- `deck-shop-board-vs-board.patch`: the same result as a binary-safe patch against the board checkout's `UnityProof` (36 files). Apply with `git apply --binary`.
- `DELIVERY.md`: the merge and its one conflict, the board-fit findings, every check and its result, and what wasn't tested.
- `evidence/`: editor suite JSONs, packaged-run results, 1280×720 shop and live screenshots on each board and seat, launcher captures, launcher copies and logs.
- `run_packaged.py`, `launch_smoke.ps1`: the scripts that produced the evidence. They expect the Infinite Conquest folder layout under `reviews/deck-shop-board-2026-10-11/`.
- `SHA256SUMS.txt`: checksums for the delivery as built.

## Build and run

```
Unity -batchmode -quit -projectPath <checkout>/UnityProof -executeMethod DeckShopBoardBuild.Build -buildPath <dir>/InfiniteConquestPlaytest.exe
```

`DeckShopBoardBuild.Build` runs the board-size checks, then the deck shop checks and the full combined build chain, in one call.

Launchers: `PLAY-INFINITE-CONQUEST.cmd` (main menu), `PLAY-BOARD-6x8-PREVIEW.cmd` and `PLAY-BOARD-HEX-PREVIEW.cmd`. On the command line, add `-board 6x8` or `-board hex` to draw a preview board, and `-shopPreview` to open the shop. The flags work together.
