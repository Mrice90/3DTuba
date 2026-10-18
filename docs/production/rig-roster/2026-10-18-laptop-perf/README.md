# Laptop frame time with the animated rigged roster (2026-10-18)

Owner: Claude (AI-060-LAPTOP-BUDGET-RIG). Laptop: RTX 3060 Laptop GPU, Ryzen 9 5900HS, 16 GB, on AC power.

Probe build `playtest/unity-build-rig-perf-2026-10-18` = the rig-combat build plus `Scripts/RigPerfProbe.cs` (opt-in `-rigPerf`, no other change). Run: `InfiniteConquestPlaytest.exe -playback -rigPerf -rigPerfSeconds 30 -rigPerfResult out.json`. vSync and the game's 60 fps cap are lifted during the probe so the numbers show headroom. The game's window-fit code sized the window to about 1280x720 in both runs.

| Run | Phase | Avg fps | p50 ms | p95 ms | p99 ms | Frames over 16.7 ms |
|---|---|---|---|---|---|---|
| windowed | match playback at 1x (up to 11 pieces) | 148 | 5.5 | 12.4 | 29.9 | 2.7% |
| windowed | + one animated rigged character on all 37 hexes (41 skinned visible) | 152 | 6.3 | 8.6 | 9.8 | 0.3% |
| borderless | match playback | 262 | 3.8 | 4.9 | 6.9 | 0.2% |
| borderless | full board, 41 skinned visible | 172 | 5.6 | 7.7 | 8.5 | 0.1% |

Result: a full board of animated 30K rigged characters holds well over 60 fps (p99 under 10 ms). Memory about 450 MB allocated.

One hitch: spawning all 37 at once took one frame of 0.5 to 0.9 s (first load of each model plus the bounds fit). In a match pieces arrive one per summon, so expect a short hitch the first time each character is summoned; preloading the deck's models at match start would remove it. Not measured: on battery, or the integrated GPU.

Files: `perf-1600x900.json` (windowed run), `perf-fullscreen.json`, `logs/`, `RigPerfProbe.cs`.
