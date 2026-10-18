# Infinite Conquest: rigged roster playtest build (2026-10-18)

Owner: Claude (AI-060-RIG-COMBAT, laptop thread "Rigged characters in the game").

## Run

`playtest/unity-build-rig-combat-2026-10-18/PLAY-INFINITE-CONQUEST.cmd` (portable, no Java). The previous build `playtest/unity-build-poseidon30k-2026-10-15/` is untouched.

## What changed

- Base: `3DTuba-poseidon30k-2026-10-15/UnityProof` (Codex 0.4.5 + structures + Poseidon 30K), copied to the new `3DTuba-rig-combat-2026-10-18/UnityProof`. The source copy was not modified.
- 47 of the 48 Zeus/Poseidon character tokens in `Assets/Playtest/Resources/Tokens/<card>/<card>.fbx` were replaced in place by the rigged FBXs from draft PR #26 (same path, name and `.meta` GUID; models verified against that branch's SHA256SUMS, copies in `playtest/rig-combat-2026-10-15/models-48/`). Previous tokens are kept in `Assets/RigCompare/` (outside Resources, not shipped).
- **Thunder Ram is not swapped.** Its rigged mesh renders as a scrambled blob with the in-game texture (the rig used a different source mesh than the token texture). It keeps the static token. Needs a re-rig from the in-game token mesh.

### Fixes needed to make the clips play

1. `Editor/PlaytestBuild.cs` (`PlaytestTokenImport` postprocessor) forced `importAnimation = false` on every token, so no clip could ever import. It now keeps animation for cards that have a rig report in `Editor/RigReports/`.
2. Skinned-mesh bounds from the FBX import are loose (about 1.8x the mesh height). TokenFactory scales and grounds models from renderer bounds, so rigged models came out about 55% size and floating up to 29% of their height. `TokenFactory.FitSkinnedBounds` now fits the bounds to the posed mesh before the budget scale, then pads them 25% after placement so raised weapons are not culled.
3. Hookup from PR #26: `Editor/RigCombatHookup.cs` (Generic import, Idle/Walk loop, impact/down/land events, `<card>_Rig.controller` with Move/Attack/Ability/Hit/Summon/Destroy triggers), `Scripts/RigEvents.cs`, TokenFactory assigns the controller.
4. `PresentationMotion`: a rigged Death now holds still until the clip's `down` frame, then sinks (before, the piece was despawned 0.6 s in, mid-fall). After a Move the Walk loop crossfades back to Idle. `Settle` returns a rigged piece to Idle.

## Verified

| Check | Result |
|---|---|
| Unity build `RigCombatBuild.Build` -> full inherited chain (`logs/unity-build.log`) | Build Finished: Success |
| Rig import (`editor-checks/rig-combat-validation.json`) | 1,317 checks, 1,312 pass: 6 takes, loops, events, controller triggers, Animator + receiver, Token material, real model via TokenFactory for all 47 |
| In-game size vs the previous token | 47/47 within 15% (most within 1%); Hera +14%, Tempest Oracle +11%, Aetherbolt +6% |
| Ground contact, 13 samples of every clip | 47/47 never sink more than 4% of height (eagle 4.1% in Attack). 4 hover units start Attack/Walk lifted: Reef Tunneler Attack 20%, Keraunos Seraph Attack 12%, Naiad Walk 4%, Eagle Attack 4% (a short pop, smoothed by the 0.08 s blend) |
| Packaged smoke, in-game clip check (`checks/smoke`) | PASS: 47 rigged characters; Summon/Move/Attack/Hit/Ability/Destroy each enter the right clip 47/47; land/impact/down events fire 47/47 |
| Packaged bot matches, both seats, seeds 42/713/7/101/3/2026 (`packaged-results.json`) | 12/12 PASS; 614 actions, totals and winners identical to the Poseidon 30K build |
| Inherited editor suites | structures 142, audio 1,336 + 18 + 230 cues, board 37 hexes, shop, interrupt, coin, camera, model refs 139: all PASS |
| Renders (`render/sheet_0..3.png`, per card `render/<card>_poses.png`) | previous token, then Idle, Walk, Attack (impact), Hit, Death (end), Summon (land) |

## Not verified / open

- No human hands-on: bot matches run at instant speed, so clips are only exercised by the smoke check and renders. Matt should watch a few summons, attacks and deaths.
- Several Zeus cards were rigged from a different model version than the in-game token (Aegis Airguard, Arc Relay Scout, Hera Protocol Warden, Keraunos Prime, Stormgate Sentinel, Tempest Oracle, Iris/Sparkstep/Cloudline Raider are slimmer). Textures fit, but the pose or silhouette differs from what players saw before: art call for Matt.
- Tidepool Surveyor and Undertow Stalker Death clips end reared up vertically, not lying down.
- Laptop frame time with many skinned 30K models on the board is unmeasured.
- Runtime assembly changed (TokenFactory, PresentationMotion, PresentationValidation, RigEvents), so this is no longer byte-identical to 0.4.5.
