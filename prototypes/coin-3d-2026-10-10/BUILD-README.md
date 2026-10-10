# Coin prototype build — AI-080-COIN-3D-PRESENTATION (2026-10-10, Claude Code)

This is an isolated prototype. It is the restoration solo build with one change: the starting-player coin is now a 3D coin flipped end over end. It is **not** a release candidate. Nothing in `playtest/unity-build-restoration/` was changed (its 10 listed hashes were re-verified after this work).

Source overlay, integration notes and the art spec for the real coin: `playtest/coin-prototype-2026-10-10/README.md`.

## Launch

- Double-click `PLAY-INFINITE-CONQUEST.cmd` and start a solo match from the menu. The coin plays after you confirm capital placement.
- Straight into a live match (skips the menu): `InfiniteConquestPlaytest.exe -live -seed 1 -humanSeat 0`
- Second skin: add `-coinSkin CapitalArtTest`. Any other name, or none, uses `Default`.
- Coin-only proof: `InfiniteConquestPlaytest.exe -screen-fullscreen 0 -screen-width 960 -screen-height 540 -coinTest <folder>` writes `coin-test.json` and frames, then quits.
- `-coinCapture <folder>` saves every frame of the live coin flip at a fixed 30 fps step.

`Bridge/` (rules engine JAR, classes and bundled JRE) was copied unchanged from `unity-build-restoration/Bridge`.

## What the coin does

The rules engine still picks the starting player. The coin only presents `state.starting_player` from the bridge's first response, through the existing `PresentCoinFlip(winner)` call. The coin rests on a hex pedestal, then gets tossed. It spins about its horizontal diameter (local X) for 5 full turns plus a half turn when needed. It rises and falls on a parabola and lands with a damped bounce and wobble. It settles flat with the winner's face up, readable and upright. The camera then pushes in and the face glows. A label reads "Zeus goes first" (gold) or "Poseidon goes first" (cyan). It takes about 3.7 s, the same as the old flat coin's 2.5 s + 1.2 s. The existing `card` cue plays at launch, `click` on landing and `turn` on reveal (no new sounds). In batch mode it jumps to the settled pose, so the smoke tests run as fast as before.

## Checklist (run on this build, 2026-10-10)

| Check | Result | Evidence |
|---|---|---|
| Editor coin checks (skin load/fallback, distinct faces, 3 submeshes, UVs in 0–1, winding, ±Y faces, size) | PASS 16/16 | `coin-validation.json`, `build.log` |
| Restoration chain unchanged: restoration 669, gameplay-priority 25, SP1 audio 88 cards / 230 cues | PASS (713 PASS lines vs 697 in the restoration build = +16 coin) | `restoration-validation.json`, `gameplay-priority-validation.json`, `audio-build-validation.json` |
| Player build | `Build Finished, Result: Success.`, Unity exit 0 | `build.log` |
| End-over-end flip | PASS. The flip axis stays horizontal (max \|right.y\| 0.088, from yaw/precession); 10–11 edge-on crossings per flip | `evidence/coin-test/coin-test.json`, GIFs |
| Lands on the correct side, both starting players, both start faces, both skins | PASS 8/8 (face-up seat read from the coin's transform) | `evidence/coin-test/coin-test.json` |
| Skin swap with a second texture set (capital card art) | PASS. Face texture and rim texture of the up face match the selected skin | `evidence/coin-test/`, `evidence/coin-flip-skin-swap-capitalart.gif` |
| Real engine result drives the face | PASS 6/6 live matches (seeds 1–6, human seat 0 and 1). The bridge transcript's `starting_player` = coin winner = face up: seat 1 ×2, seat 0 ×4 | `evidence/live-seed*-seat*/bridge-transcript.jsonl`, `player.log` |
| Start-of-match flow not regressed | PASS. All 6 live matches played to GAME_OVER (`result.json` passed: 45–201 actions, turn 16–22) | `evidence/live-*/result.json` |
| Playback smoke `-batchmode -playtestSmoke` | PASS (139 cards, 235/235 events) | `evidence/smoke/` |
| Human visual/feel acceptance | **OPEN**: Mathew to judge | — |

Clips: `evidence/coin-flip-live-seed1-poseidon.gif`, `evidence/coin-flip-live-seed2-zeus.gif`, `evidence/coin-flip-skin-swap-capitalart.gif`. Contact sheet: `evidence/sheet-live-seed1.png`. Settled frames: `evidence/settled-live-seed*.png`.

Not tested: the menu → capital placement → Confirm path by hand. It calls the same `StartLive` → `LiveLoop` → `PresentCoinFlip` code that `-gameplayLiveSmoke` exercised. Online two-client mode also goes through `PresentCoinFlip` and was not run.

Known limits: if a match is abandoned during the 3.7 s flip, the coin stage finishes its own timeline before disappearing. The build log has two harmless long-path `DirectoryNotFoundException`s for editor-only package DLLs (collab-proxy, pipeline code analysis); they come from the deep temp path of the scratch project.

## Key hashes (SHA-256; full list in `SHA256SUMS.txt`, 385 files, excludes `evidence/` and the DontShip folder)

```
96b492cb271111251fe42b8646e65370a1b7b566773a1e35b34c3f2d1ae70873  InfiniteConquestPlaytest.exe
2eddd14221c6c925b55d8b9e1951ba966dcc120aba9af14ddffbc2a68811f798  InfiniteConquestPlaytest_Data/Managed/Assembly-CSharp.dll
d3ab6c9739ece221fab5c9b0e31425eae172823cc5bb315403500ce9f3d086d6  InfiniteConquestPlaytest_Data/resources.assets
5278294bc5a4ed28482f41c12670f4d8c1c1edb9f35bf52890b7ad400740b2d9  InfiniteConquestPlaytest_Data/sharedassets0.assets
7c8127007131559a3daa04aa3a3e8d1477975fe449f59917c5c75d86fa1f904f  UnityPlayer.dll
163e25488d1fc37d76e696a6ea003c583f8d8e5543cbaeef0b8540949da7c89a  Bridge/infinite-conquest-alpha-0.7.15.jar
55d2dff4fe6c94dca13144cdfb6c1d3e8fd8d5a2bf1b3ab1c940fe8a25a58c06  PLAY-INFINITE-CONQUEST.cmd
```
