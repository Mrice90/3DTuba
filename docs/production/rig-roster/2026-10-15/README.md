# Poseidon combat rigs, batch 07 (2026-10-16)

All 12 slimmed batch-07 Poseidon models (the 30K-triangle versions Matt approved on 2026-10-15) are rigged with six combat clips. No Meshy credits were spent; everything was done in headless Blender 5.2.2.

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
| poseidon_abyssal_molecrab | creature | 16 | 30000 | bite/claw | walk | Death 0.223% |
| poseidon_delphic_sonar_adept | humanoid | 21 | 29835 | cast | walk | Summon 0.801% |
| poseidon_fast_razorfin_lancer | humanoid | 15 | 29622 | thrust | hover | Death 0.709% |
| poseidon_keyword_breakwater_hoplite | humanoid | 21 | 28888 | thrust | walk | Summon 0.654% |
| poseidon_kraken_tendril_drone | creature | 7 | 30000 | bite/claw | walk | Death 0.014% |
| poseidon_naiad_flowshaper | humanoid | 21 | 29999 | cast | hover | Attack 1.423% |
| poseidon_nereid_current_rider | humanoid | 21 | 29999 | slash | hover | Summon 0.039% |
| poseidon_oceanid_pressure_mage | humanoid | 21 | 29987 | cast | hover | Summon 1.213% |
| poseidon_reefline_defender | humanoid | 21 | 28530 | thrust | walk | Summon 0.639% |
| poseidon_tidepool_surveyor | creature | 15 | 29999 | bite/claw | walk | Death 0.224% |
| poseidon_triton_waveguard | humanoid | 21 | 29829 | thrust | walk | Walk 1.069% |
| poseidon_undertow_stalker | creature | 14 | 30000 | bite/claw | walk | Death 0.778% |

"Worst stretch" is the share of mesh edges stretched past 2.5x their rest length on the single worst frame of any clip. Everything is under 1.5%, and most of it is in the folded-up poses of Death and Summon where a leg tucks into the robe.

## Files

- `<card>_rigged.fbx`, `<card>.glb`: the deliverables (in the project files folder `rig-combat-2026-10-15/models/` and on the PC under `playtest\rig-combat-2026-10-15\models` (FBX); too large for git, checksums in `SHA256SUMS.txt`).
- `<card>_rig_report.json`: clip lengths, event frames, bone count, attack style.
- `<card>_qa.json`: per-clip stretch QA.
- `<card>_fbx_verify.json`: the FBX re-imported and measured per take (min height per take).
- `review/<card>_sheet.png` (animated GIFs of all six clips are in the project files folder): four phases of each clip, three-quarter view.
- `cfg/<card>.json`: hand-placed joints, props (trident, lance, shield) and per-unit motion settings.
- `scripts/`: the pipeline (see below).

## How it works

The batch-07 models are posed statues on bases, not T-poses, so auto-rigging from a template does not fit them.

1. `probe.py` renders front, side and top views with a grid in units of model height; joints are placed by hand into `cfg/<card>.json` and checked with `overlay.py`.
2. `rig_posed.py` builds the skeleton, skins it with bone heat on a voxel proxy, then forces weapons and shields to be rigid to the gripping hand (capsule, box and plane-fit shield props), pins the base to `Root`, and cuts faces that bridge a weapon to the base.
3. `clips.py` holds the procedural clip library (thrust, slash, ranged, cast and bash attacks; walk or hover; creature gait by limb phase). Clips are additive rotations on the posed rest, so each model keeps its sculpted silhouette.
4. `qa.py` measures stretch; `sheet2.py` re-imports the exported FBX and renders the review sheet.

Rerun one model: `python3 -I scripts/rig_posed.py -- src/<card>.glb cfg/<card>.json out/<card> <card>` (needs `pip install bpy==5.2.2`).

## Still to do

- Unity import check on the PC (kit from `../2026-10-09/unity-check-kit`), pending the laptop coming back online.
- Rig the remaining Zeus and Poseidon roster characters once their source models are pulled from the PC.
- In-game hookup: play `Attack` on attack and fire damage at `impact`; `Hit` on damage; `Death` then remove at end; `Summon` on play with the land sound at `land`.
