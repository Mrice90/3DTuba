# AI-209 laptop profiling: card-sfx build (2026-10-18)

**Answer: yes, it runs smoothly on this laptop.** With a full board of 37 animated rigged characters (plus the replayed match underneath, up to 53 pieces and 41 visible skinned models) summoning, attacking, getting hit and dying with their card sounds, the game holds its 60 fps cap: 59.6-59.8 fps average, 99% of frames under 17 ms. With the cap lifted the same scene runs at about 225 fps (4.4 ms a frame), so there is roughly 3.5x headroom. The only visible imperfection was a short hitch when a character is summoned, and that is now mostly fixed.

## Setup

- Laptop: AMD Ryzen 9 5900HS, NVIDIA RTX 3060 Laptop GPU (6 GB; the game picked it over the Radeon iGPU), 16 GB RAM, on mains power, Windows "Performance" plan.
- Build under test: `playtest/unity-build-card-sfx-2026-10-18` source (Codex 0.4.5 + structures, 47 rigged characters, 690 card sounds), rebuilt from an isolated copy with only an opt-in probe added. Default settings: PC quality level, 60 fps cap, vSync off, maximized window. The window came out at 1280x649 because the display is running at 1280x720.
- Probe: `PerfProbe.cs` (runs only with `-perfProbe`; folds in the abandoned AI-060-LAPTOP-BUDGET-RIG `RigPerfProbe`). Phases of 30 s each: normal match playback; fill every hex with a random rigged character (staggered summons); crowded battle at the 60 fps cap (every 0.6 s a third of the board attacks or takes a hit; every 2 s three characters die and are re-summoned, every event plays its card sound); the same battle uncapped. Launcher `run_probe.ps1` records wall-clock time and peak process memory. Two runs per build, alternated, with nothing else running (no Unity editor or other player).

## Numbers

| | Before (2 runs) | After fix (2 runs) |
|---|---|---|
| Normal playback | 59.8-59.9 fps, p99 16.8 ms | 59.8-59.9 fps, p99 16.7-16.8 ms |
| Crowded board, 60 cap | 59.6 fps, p99 16.9-17.0 ms | 59.7-59.8 fps, p99 16.9 ms |
| Crowded: frames over 33 ms per 30 s | 4, 4 | 2, 2 |
| Crowded: worst frame | 61 ms, 61 ms | 57 ms, 48 ms |
| Crowded uncapped | 224-228 fps, worst 40 ms | 228-229 fps, worst 20-25 ms |
| Summoning a character already seen this match (median) | 7.1-7.2 ms | 0.5 ms |
| Summoning a character for the first time (median, worst) | 14-17 ms, 25 ms | 12-16 ms, 25 ms (not targeted, see below) |
| Process start to first pieces on board | 2.9 s | 2.9-3.0 s |
| Peak memory (working set / private) | 508-534 MB / 1.17-1.18 GB | 524 MB / 1.15-1.16 GB |
| Unity "Total Used Memory", crowded | 495-499 MB | 481-485 MB |

Per frame on the crowded board: about 6.7 million triangles (shadow cascades included), 130-150 SetPass calls, 190 shadow casters. Playback alone is about 0.73 million triangles and 83 SetPass calls. Draw-call and batch counters are not exposed in a release player, so SetPass calls stand in for them.

## Biggest costs

1. **Summons (fixed).** Every summon re-fitted the character's skinned bounds by baking the whole mesh and walking every vertex on the main thread, about 6 ms per character even when that model was already on the board. Three summons landing in the same frame made the 40-60 ms hitches. Now each model is fitted once per match and reused.
2. **First appearance of a model (left as is).** The first summon of each card still loads its FBX and textures synchronously, typically 12-17 ms and up to 25 ms. In a real match summons arrive one action at a time, so this is a single dropped frame at most; only the stress test stacks several in one frame. Preloading both decks' models during match setup would remove it but touches the match start flow in `PlaytestGame.cs`, so it is left for the release owner.
3. **Triangles and shadows (fine here).** About 150K triangles per rigged character, drawn into 4 shadow cascades (2048 shadow map, soft shadows high). The GPU finishes the crowded frame in about 4.4 ms, so nothing needed changing. On a weaker laptop or a 1080p/1440p screen the first things to try are 2 shadow cascades and a 30K-triangle pass on the characters, like the Poseidon 30K swap.
4. **Textures (fine).** 405 MB of the 674 MB build is textures, but the crowded board uses about 500 MB of memory in total, comfortable on 16 GB. Each token also ships an unused, read/write-enabled `Token_MetallicRoughness.png`; it is never loaded, so it costs disk size only. Dropping it from the token folders would shrink the download.
5. **Audio (fine).** 370 card sounds in 30 s on the crowded board caused no hitch the probe could see; sounds load on first play and the clips are small.

## What changed

Only in the copy `3DTuba-perf-2026-10-18/UnityProof` (copied from `3DTuba-card-sfx-2026-10-18/UnityProof`):

- `Assets/Playtest/Scripts/TokenFactory.cs`: cache of fitted skinned bounds per prefab (diff in `source/TokenFactory.diff`).
- `Assets/Playtest/Scripts/PerfProbe.cs`: new opt-in probe, inactive without `-perfProbe`.
- `Assets/Playtest/Editor/PerfBuild.cs`: new build entry `PerfBuild.Build` that first checks all 47 rigged characters place, size, cull and stand exactly the same with the cached fit as with the full fit (236 checks PASS), then runs the unchanged `CardSfxBuild.Build` chain.

Build: Success, the same inherited editor checks as the baseline build (1,257 PASS lines plus the new PERF_CHECK; the same 6 known rig "clip starts on the tile" notes as rig-combat), packaged smoke 39/39 PASS including rig clips 47/47.

Output: `playtest/unity-build-perf-2026-10-18/` (play with `PLAY-INFINITE-CONQUEST.cmd`; `baseline-probe/` is the unchanged game with the probe, kept for comparison). The card-sfx, rig-combat and every other build and source folder are unchanged. Not touched: Codex or ChatGPT folders, the rules engine, rigs, TubaExperiment, Desolate-Tuba.

## Re-running

```
powershell -File reviews/perf-2026-10-18/run_probe.ps1 -Exe playtest/unity-build-perf-2026-10-18/InfiniteConquestPlaytest.exe -Out reviews/perf-2026-10-18/results/new.json
```
Raw results: `results/*.json` (probe) and `results/*.summary.json` (wall time, peak memory, memory timeline). Logs: `logs/`.

Note: the orphaned AI-060-LAPTOP-BUDGET-RIG thread left `RigPerfProbe.cs` in `3DTuba-rig-combat-2026-10-18/UnityProof` and a build in `playtest/unity-build-rig-perf-2026-10-18`; both are opt-in and were left as they are.
