# Board size handoff (2026-10-11)

This is a client-only preparation for the 6 × 8 board Matt picked (48 hexes, 4 home rows per side). The engine is still 4 × 6 and was not touched.

- `overlay/`: every new and changed file, relative to `UnityProof/`. Copy it over `3DTuba-combined-2026-10-11/UnityProof`, or apply `board-vs-combined-2026-10-11.patch` there with `git apply -p1`.
- `ENGINE-HANDOFF-6x8.md`: exactly what the rules engine (C# core, native adapter, Java bridge) needs for 6 × 8, with file:line references. This is for ChatGPT.
- `DELIVERY.md`: the checks and their results, and what wasn't tested.
- `evidence/`: 1280×720 screenshots of the 6 × 8 board from the Zeus and Poseidon views, launch-menu captures, suite JSONs and packaged-run results.
- `run_packaged.py`, `make_launch.py`, `launch_*.ps1`: the scripts that produced the evidence. They expect the Infinite Conquest folder layout under `reviews/board-2026-10-11/`.

## Build and run

```
Unity -batchmode -quit -projectPath <board>/UnityProof -executeMethod BoardBuild.Build -buildPath <dir>/InfiniteConquestPlaytest.exe
```

The game starts on the engine's board. Add `-board 6x8` to the command line (or use `PLAY-BOARD-6x8-PREVIEW.cmd`) to draw the planned board. Once the engine DLL plays 6 × 8, the client follows on the next build with no code change.
