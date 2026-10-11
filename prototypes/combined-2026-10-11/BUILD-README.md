# Infinite Conquest playtest: combined build (2026-10-11)

Double-click `PLAY-INFINITE-CONQUEST.cmd`. **Java is no longer needed.** The rules now run inside the game (native C#), so there is no `Bridge/` folder, no Java runtime and no JAR. Keep the folder together.

## What is in it

- **R3 and R3b** (from `unity-build-2026-10-11-r2`):
  - capital HP that follows the engine, including Storm Relay damage and heals, with ±N numbers;
  - the ability command center, with readable rows;
  - line-of-sight wording on Storm Relay / Eagle's Perch;
  - online clients that also get capital HP metadata.
- **Reaction cue and calmer turn sounds** (from `unity-build-interrupt-2026-10-11`). Matt checked both on 2026-10-11 and approved them:
  - the REACT NOW banner, edge glow, outlines on castable spells and the Pass button;
  - one soft chime on your own turn, silence on turn end and on the opponent's turn;
  - Esc → Settings → Turn chime: Calm / Off.
- **Native C# rules** (Codex AI-087 handoff, `reviews/native-csharp/HANDOFF.md`). `InfiniteConquest.RulesCore.dll` (`1a985fdc…`) and `InfiniteConquest.Native.dll` (`d20045d7…`) are the pinned handoff binaries. The rules run on a worker thread in the game process.

`../unity-build-combined-2026-10-11-java/` is the same game on the old Java bridge. It is kept as a fallback and a comparison point.

## Checks run on this build

| Check | Result |
|---|---|
| Editor: reaction cue/turn sounds, native in-process engine (both seats, redaction, stale rejection, hash), R3/R3b, camera, coin, restoration, gameplay | 42, 12, 35, 50, 16, 669 (570 audio bindings), 25: all PASS |
| SP1 audio build check | 230 cues / 88 cards PASS |
| Packaged playback smoke | PASS |
| Packaged live matches, both seats, 12 seeds, run with Java removed from PATH and JAVA_HOME unset | 24/24 PASS to GAME_OVER |
| Native compared with the Java build (same seeds, same automated play) | All 24 request/response transcripts identical after parsing (2,274 engine responses), so winners, turns and every R3 total match |
| Java payload scan | 0 `.jar`, `.class`, `java*.exe` or `jvm.dll` files |
| Short-window run (1280×600, seed 8) | PASS; the reaction cue is in `reviews/combined-2026-10-11/shots-native-*/05-reaction-window.png` |
| Launch through `PLAY-INFINITE-CONQUEST.cmd` | Reached the main menu (1296×688, responding), then closed |

Hashes are in `SHA256SUMS.txt`. Not tested: a packaged two-PC online match, because Node.js and the relay aren't set up on this machine. Human hands-on play of this combined build is still open.
