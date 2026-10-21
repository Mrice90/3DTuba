# PR #27 rig fixes: Unity import check and playtest build (2026-10-21)

Owner: Claude (AI-216-RIG-FIXES-IMPORT-2026-10-21, laptop thread). Codex integration handoff: `HANDOFF.md` in this folder.

## Result

The 16 re-rigged characters from draft PR #27 import cleanly in Unity 6000.6.3f1. The 5 rig notes that every earlier build carried are gone (`RIGCOMBAT PASS 1345 checks, 0 failures`, previously 1317 checks with 5 failures). Thunder Ram is now rigged on the model the game textures, and its texture fits. All 48 Zeus and Poseidon characters animate.

Play: `playtest/unity-build-rig-fixes-2026-10-21/PLAY-INFINITE-CONQUEST.cmd` (portable). Base: the idle and signature sounds build (AI-215). The AI-215 package and source and every earlier package and source were not modified.

## What changed (only in the new copy `3DTuba-rig-fixes-2026-10-21/UnityProof`)

- 16 token FBXs replaced in place at `Assets/Playtest/Resources/Tokens/<card>/<card>.fbx`. The `.meta` files were kept, so the GUIDs and import settings are unchanged. The files come from project files `rig-combat-2026-10-18-fixes/models/`, and their checksums match that folder's `SHA256SUMS.txt`. Ten of them were joined from `models-split/` parts.
- 16 rig reports replaced or added in `Assets/Playtest/Editor/RigReports/`, including the new Thunder Ram report. `RigCombatHookup` reads these to set up clips and events.
- The old static Thunder Ram token was copied to `Assets/RigCompare/zeus_siege_thunder_ram.fbx`, so the side-by-side comparison and the roster list include it.
- The roster count changed from 47 to 48 in `RigCombatBuild.cs`, `PerfBuild.cs` and `PresentationValidation.cs` (diff: `source/count-48.diff`). No other source changed.

## Verified

| Check | Result |
|---|---|
| Unity build (`logs/unity-build.log`) | Build Finished: Success |
| Rig import (`editor-checks/rig-combat-validation.json`) | 1,345/1,345 PASS: 48 cards each have six clips, loops, events and controller triggers, the same in-game size as the old token, and every clip starts on the tile with nothing sinking into it |
| Size match for the 16 | Height ratio 0.986-1.02 and width ratio 1.00 against the old tokens. The nine Zeus figures that looked slimmer at 0.67-0.88 width are now at 1.00 |
| Cached bounds (PERF_CHECK) | 241 checks PASS for 48 rigged characters |
| Sounds (IDLE_SIG, CARD_SFX) | 858 and 4,468 PASS (unchanged) |
| Packaged smoke (`results/smoke.json`) | PASS, including 48/48 clip controllers, clips played on every board event 48/48, and animation events 48/48 |
| Pose sheets (`render/<card>_poses.png`, all 48) | Columns show the old token, then Idle, Walk, Attack (impact frame), Hit, Death (last frame) and Summon (land frame) |

## What the pose sheets show

- Thunder Ram's texture sits correctly on the new rig, and its idle matches the old token.
- Tidepool Surveyor and Undertow Stalker now end Death lying flat with legs splayed instead of standing on edge.
- Reef Tunneler's Attack stays on the tile.
- To look at in game: Keraunos Prime's rock base stretches into tall shards in its Death pose. Aegis Airguard's shield and cape bend sharply in Hit and Summon. Both are inside the stretch limits the cloud rigging used, but they are visible.

## Build notes

- The first two attempts failed in an inherited cosmetics build step with "No script asset for CoinSkin". This came from compiled-script caches in the copied Library. After I deleted `Library/ScriptAssemblies`, `Library/Bee`, `Library/ScriptMapper` and `Library/MonoManager.asset`, it built cleanly. Logs: `logs/unity-build-attempt1.log` and `logs/unity-build-attempt2.log`.
- Packaged bot matches were not re-run (the laptop has no Python for `run_packaged.py`). The smoke playback covers the bundled match.

## Not verified

- Nobody has watched the fixed characters in a live match.
- I did not check the two-client online path.
