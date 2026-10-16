# Zeus and Poseidon combat rigs (2026-10-16)

All 48 Zeus and Poseidon characters are rigged with six combat clips: the 12 slimmed batch-07 Poseidon models Matt approved on 2026-10-15, plus the other 36 roster models (sources from the PC's `3DTuba\assets\staging\meshy\batch-04b/04c/04d` folders; the storm titan is the spear-trim candidate from `playtest\sp1-models-candidates-2026-10-08`). No Meshy credits were spent; everything was done in headless Blender 5.2.2.

## What each model has

Six takes, keyed at 24 fps, named exactly `Idle`, `Walk`, `Attack`, `Hit`, `Death`, `Summon` (FBX NLA tracks and GLB animations).

| Take | Frames | Loop | Gameplay event frame |
|---|---|---|---|
| Idle | 49 | yes | |
| Walk | 25 | yes (hover drift for floating units) | |
| Attack | 31 to 35 by style | no | `impact` (see each `_rig_report.json`) |
| Hit | 18 | no | |
| Death | 40 to 44 | no | `down` (body hits the ground) |
| Summon | 31 | no | `land` (unit settles on its tile) |

Every frame is grounded (the lowest vertex sits on y=0 in Unity; frame counts include both end frames, so loops repeat their first pose), so no root motion is needed. Skeleton is Mixamo-named (`mixamorig:Hips` etc.) under a non-deforming `Root`, max 4 influences per vertex. Creatures use `Hips`, `Spine1..n`, `Tail1..n` and limb chains instead of arms and legs.

Unity import: FBX, Y-up, -Z forward, Animation Type Generic (the creature rigs are not humanoid; the humanoid rigs also work as Generic so one controller path covers both).

## Models

| Card | Kind | Bones | Tris | Attack | Move | Worst stretch (QA) |
|---|---|---|---|---|---|---|
| poseidon_ability_reefwarden | humanoid | 21 | 30492 | bash | walk | Summon 1.172% |
| poseidon_abyssal_molecrab | creature | 16 | 30000 | bite/claw | walk | Death 0.223% |
| poseidon_apex_abysswalker_nereid | humanoid | 21 | 30810 | cast | walk | Attack 1.521% |
| poseidon_apex_atlantis_tide_sovereign | humanoid | 21 | 29325 | cast | walk | Death 0.172% |
| poseidon_apex_kraken_prime_avatar | creature | 12 | 31232 | bite/claw | hover | Death 0.562% |
| poseidon_delphic_sonar_adept | humanoid | 21 | 29835 | cast | walk | Summon 0.801% |
| poseidon_fast_razorfin_lancer | humanoid | 15 | 29622 | thrust | hover | Death 0.705% |
| poseidon_keyword_abyssal_leviathan | creature | 16 | 31310 | bite/claw | walk | Death 1.204% |
| poseidon_keyword_breakwater_hoplite | humanoid | 21 | 28888 | thrust | walk | Summon 0.302% |
| poseidon_keyword_maelstrom_bulwark | creature | 4 | 31071 | bite/claw | hover | Attack 1.867% |
| poseidon_keyword_reef_tunneler | creature | 12 | 50516 | bite/claw | hover | Death 0.138% |
| poseidon_keyword_trench_stalker | humanoid | 21 | 31112 | slash | walk | Death 0.908% |
| poseidon_kraken_tendril_drone | creature | 7 | 30000 | bite/claw | walk | Death 0.014% |
| poseidon_leviathan_wakeborn | creature | 14 | 10434 | bite/claw | walk | Idle 0.0% |
| poseidon_naiad_flowshaper | humanoid | 21 | 29999 | cast | hover | Attack 1.423% |
| poseidon_nereid_current_rider | humanoid | 21 | 29999 | slash | hover | Summon 0.039% |
| poseidon_oceanid_pressure_mage | humanoid | 21 | 29987 | cast | hover | Summon 1.213% |
| poseidon_poseidons_trident_core | creature | 9 | 30418 | bite/claw | hover | Attack 0.412% |
| poseidon_reefline_defender | humanoid | 21 | 28530 | thrust | walk | Summon 0.577% |
| poseidon_sharp_tidewall_harpooner | humanoid | 21 | 27927 | ranged | hover | Death 0.69% |
| poseidon_siege_kraken_sapper | creature | 8 | 30990 | bite/claw | walk | Summon 1.025% |
| poseidon_tidepool_surveyor | creature | 15 | 29999 | bite/claw | walk | Death 0.224% |
| poseidon_triton_waveguard | humanoid | 21 | 29829 | thrust | walk | Walk 1.016% |
| poseidon_undertow_stalker | creature | 14 | 30000 | bite/claw | walk | Death 0.778% |
| zeus_ability_skyline_seer | humanoid | 15 | 29244 | cast | hover | Attack 1.5% |
| zeus_aegis_airguard | humanoid | 21 | 29435 | thrust | walk | Summon 0.23% |
| zeus_apex_keraunos_seraph | humanoid | 21 | 30377 | thrust | hover | Summon 0.709% |
| zeus_apex_olympian_storm_titan | humanoid | 21 | 28621 | thrust | walk | Summon 2.725% |
| zeus_apex_skyfather_archon | humanoid | 15 | 30989 | cast | walk | Death 0.462% |
| zeus_arc_relay_scout | humanoid | 21 | 30605 | cast | walk | Walk 1.237% |
| zeus_boltwing_cavalier | creature | 20 | 30060 | bite/claw | walk | Death 1.162% |
| zeus_cloudline_courier | humanoid | 23 | 30864 | ranged | walk | Walk 0.32% |
| zeus_cyclone_marksman | humanoid | 21 | 29908 | ranged | walk | Summon 1.287% |
| zeus_eagle_of_the_high_grid | creature | 14 | 30127 | bite/claw | hover | Death 0.025% |
| zeus_fast_tempest_duelist | humanoid | 23 | 30960 | slash | walk | Death 0.417% |
| zeus_hera_protocol_warden | humanoid | 23 | 30024 | cast | walk | Death 0.67% |
| zeus_iris_signal_runner | humanoid | 23 | 30910 | cast | walk | Summon 0.257% |
| zeus_keraunos_prime | humanoid | 21 | 31006 | cast | walk | Summon 1.411% |
| zeus_keyword_aetherbolt_avatar | humanoid | 23 | 30836 | thrust | walk | Death 0.651% |
| zeus_keyword_cloudline_raider | humanoid | 23 | 30936 | slash | walk | Summon 0.415% |
| zeus_keyword_sparkstep_runner | humanoid | 23 | 30976 | slash | walk | Death 0.542% |
| zeus_keyword_stormgate_sentinel | humanoid | 21 | 30726 | cast | walk | Death 0.457% |
| zeus_keyword_thunderhead_guardian | humanoid | 23 | 31052 | cast | walk | Attack 0.25% |
| zeus_sharp_aether_spotter | creature | 5 | 29237 | bite/claw | walk | Death 1.436% |
| zeus_siege_thunder_ram | creature | 4 | 26174 | bite/claw | walk | Attack 0.266% |
| zeus_stormgate_adept | humanoid | 23 | 30596 | cast | walk | Attack 0.624% |
| zeus_tempest_oracle | humanoid | 23 | 30874 | cast | walk | Death 1.568% |
| zeus_thunderhead_skirmisher | humanoid | 21 | 31304 | bash | walk | Summon 0.602% |

"Worst stretch" is the share of mesh edges stretched past 2.5x their rest length on the single worst frame of any clip. Everything except the storm titan is under 1.9%; the titan's wide stance stretches the inner thighs in Death and Summon (2.7%). Most of it is in the folded-up poses of Death and Summon where a leg tucks into the robe.

## Files

- `<card>_rigged.fbx`: the deliverables, in the project files folder `rig-combat-2026-10-15/models/` (the first 12 are also on the PC under `playtest\rig-combat-2026-10-15\models`); too large for git, checksums in `SHA256SUMS.txt`.
- `<card>_rig_report.json`: clip lengths, event frames, bone count, attack style.
- `<card>_qa.json`: per-clip stretch QA.
- `<card>_fbx_verify.json`: the FBX re-imported and measured per take (min height per take).
- `review/<card>_sheet.png` (animated GIFs of all six clips are in the project files folder): four phases of each clip, three-quarter view.
- `cfg/<card>.json`: hand-placed joints, props (trident, lance, shield, wings) and per-unit motion settings. Extra knobs: `arm_down` (lower A-pose arms), `anchored` (seated or plinth units: hips rotate only), `death_topple`, `crouch`, `lunge`, `bob`.
- `scripts/`: the pipeline (see below).

## How it works

The batch-07 models are posed statues on bases, not T-poses, so auto-rigging from a template does not fit them.

1. `probe.py` renders front, side and top views with a grid in units of model height; joints are placed by hand into `cfg/<card>.json` and checked with `overlay.py`.
2. `rig_posed.py` builds the skeleton, skins it with bone heat on a voxel proxy, then forces weapons and shields to be rigid to the gripping hand (capsule, box and plane-fit shield props), pins the base to `Root`, and cuts faces that bridge a weapon to the base.
3. `clips.py` holds the procedural clip library (thrust, slash, ranged, cast and bash attacks; walk or hover; creature gait by limb phase). Clips are additive rotations on the posed rest, so each model keeps its sculpted silhouette.
4. `qa.py` measures stretch; `sheet2.py` re-imports the exported FBX and renders the review sheet.

Rerun one model: `python3 -I scripts/rig_posed.py -- src/<card>.glb cfg/<card>.json out/<card> <card>` (needs `pip install bpy==5.2.2`).

## Fixes in this pass

- Weighting: forcing bases and weapons rigid removed vertex groups while iterating them, which left stray limb weights on some rigid vertices (bases sank, weapons bent slightly). Fixed, and all 24 units with a base or weapon were re-rigged.
- Ground solve now iterates, so vertices blended with the static base settle exactly on the ground.

## Still to do

- Unity import check on the PC for all 48 (kit in the project files folder), when the laptop is back online.
- Copy the 36 new FBX files to the PC (`playtest\rig-combat-2026-10-15\models`) at the same time.
- In-game hookup: `hookup/` holds the editor script that builds the Animator controllers and the TokenFactory patch; try it in a separate worktree copy of 0.4.5, never in UnityProof.
