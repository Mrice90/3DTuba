# Infinite Conquest: idle and signature sounds build (2026-10-21)

Owner: Claude (AI-215-IDLE-SIGNATURE-SFX-2026-10-21, laptop thread).

## Run

`playtest/unity-build-idle-sig-sfx-2026-10-21/PLAY-INFINITE-CONQUEST.cmd` (portable, no Java). The perf build `playtest/unity-build-perf-2026-10-18/` and its source `3DTuba-perf-2026-10-18/` were not modified.

## Sounds

No new generation was needed. The 123 idle and 32 signature sounds came from 3DTuba draft PR #24 (ElevenLabs, EBU R128 level-matched) and were already imported in the perf project under `Assets/Playtest/Resources/MuseSfx/<card>_idle.mp3` and `<card>_signature.mp3`, but nothing played them except the 8 top-rarity character summons.

| Sound | Cards | Types |
|---|---|---|
| idle | 123 | 48 characters, 35 lands, 34 structures, 6 capitals |
| signature | 32 | 8 characters (already played), 6 spells, 6 lands, 6 structures, 6 capitals (24 newly wired) |

## What changed

Only in the new copy `3DTuba-idle-sig-sfx-2026-10-21/UnityProof` (copied from `3DTuba-perf-2026-10-18/UnityProof`):

- New `Assets/Playtest/Scripts/IdleSignatureAudio.cs`:
  - Idle: during a running match (playback or live), every 5-9 s one piece in play that has its own idle sound plays it at its position at 35% gain, never the same piece twice in a row. Off while paused, at game over, fast-forwarded (speed 50+) or muted.
  - Signature: `ArrivalCue` picks the card's signature when it has one, otherwise the usual cast/place/deploy. Top-rarity spells, lands and structures now play their signature on arrival.
  - Capitals: once per match, when both capitals are in play, the viewer's capital plays its signature, then the opponent's 1.2 s later (80% gain, 0.8 s after the match-start sound).
  - Opt-in probe `-idleSigProbe <result.json> [-idleSigSeconds N]` that plays the bundled match with the listener silenced and logs which idle and signature sounds played.
- `PlaytestGame.cs`: arrival sounds go through `PlayArrival`, the idle tick runs in `Update`, capital signatures at match start, a smoke check, the probe hook (diff: `source/hooks.diff`).
- `SharedAudio.cs`: reset per match, capital signatures after the live capital reveal.
- New `Editor/IdleSigSfxBuild.cs` (entry `IdleSigSfxBuild.Build`, then the unchanged `PerfBuild.Build` chain).

Rules engine, native DLLs, Codex and ChatGPT folders, TubaExperiment and Desolate-Tuba untouched.

## Verified

| Check | Result |
|---|---|
| Unity build (`logs/unity-build.log`) | Build Finished: Success |
| Idle/signature editor check (`editor-checks/idle-sig-sfx-validation.json`) | 858 checks PASS: every idle sound belongs to a board card and plays as that card's idle; every signature is what its card plays on arrival; the other 107 cards keep their old arrival cue |
| Inherited editor suites | PERF_CHECK 236, CARD_SFX 4,468 and the rest PASS; the same 5 known rig "clip starts on the tile" notes as rig-combat and perf |
| Packaged smoke (`results/smoke.json`) | PASS, including the new AI-215 check (123 idle, 32 signature arrivals resolve to their own clips) |
| Packaged real-time probe (`results/idle-sig-probe.json`) | Seed-42 match played to the end at normal speed (235/235 events, 70 s): both capital signatures, 9 idles from 7 different cards, 101 card sounds, 0 generic fallbacks |

Earlier build attempts: the first failed because my folder copy skipped a package folder named `Build` (restored); the second hit a null reference when an inherited editor audio check called the capital reveal outside play mode (guarded). Log of that attempt kept as `logs/unity-build-attempt2.log`.

## Not verified

- Nobody has listened in game. Idle volume (35%) and spacing (5-9 s) are first guesses; both are constants at the top of `IdleSignatureAudio.cs`.
- The bundled seed-42 match has no top-rarity spell, land or structure, so their signature arrivals were checked in the editor and the smoke, not heard in the probe.
- Online matches use the same live path (capital reveal and arrival hooks) but no two-client run was done.
