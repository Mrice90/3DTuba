# AI-060-RIG-ROSTER: Sparkstep Runner and Iris Signal Runner rigs (2026-10-09)

> Repository copy of `Infinite Conquest\playtest\rig-roster-2026-10-09\`. The FBX and GLB files (12-15 MB each) stay in that playtest folder; their hashes are listed below.

Status: **STAGED, awaiting the Unity import check and art review.** Nothing was imported into UnityProof or any other project, and no Meshy credits were spent. The static tokens stay the fallback.

## What was made

Two Desolate-Tuba Zeus humans from batch 04c, both clean A-pose meshes, now have a skeleton, skin weights, and the same **Idle** (46 frames, 1.875 s) and **Walk** (26 frames, 1.042 s, in place) clips as the Keraunos Prime pilot, at 24 fps.

| Card | Source mesh | Tris | Bones | Idle lowest point | Walk lowest point |
|---|---|---|---|---|---|
| zeus_keyword_sparkstep_runner | batch-04c `zeus_keyword_sparkstep_runner.glb` | 30,976 | 22 | 0.000 to 0.006 m | 0.000 to 0.084 m |
| zeus_iris_signal_runner | batch-04c `zeus_iris_signal_runner.glb` | 30,910 | 22 | 0.000 to 0.004 m | 0.000 to 0.069 m |

Lowest-point ranges are measured on the re-imported FBX, across every frame. The low end is the planted foot on the token base; the high end is mid-stride, where the in-place walk lifts the hips (the Keraunos pilot does the same, 0.074 m).

## Method (Blender 5.2.2 headless, `scripts/`)

1. `fit.py` places 27 joints on the A-pose mesh: the torso column from the front surface, legs by mean-shift in horizontal slabs, arms along a 50° A-pose line, all mirrored left/right. Iris's right arm is mirrored from her left because her scarf hangs off the right arm. Joint overlays: `*_joints_front.png`.
2. `rig.py` builds a clean 22-bone skeleton with Mixamo names (`mixamorig:Hips` … `ToeBase`, no fingers), so a Unity Humanoid avatar can map it later.
3. Skinning: a voxel-remeshed proxy (0.008 × height, largest connected piece only) gets Blender bone-heat weights, solved at 10× scale; the weights are then transferred to the real mesh, limited to 4 influences and normalized. Every vertex is weighted.
4. Retarget: the Keraunos Prime Meshy clips (`Idle_02`, `Walking`) are copied by **joint direction**, not by local rotation, because the Keraunos bind pose has raised arms while these meshes are in A-pose. The hips' travel is scaled by the hip-height ratio.
5. Grounding: hips are offset so the lowest point of each cycle sits at Z = 0, as in the pilot.
6. Export: FBX for Unity (Y-up, -Z forward, no leaf bones, baked from NLA strips, textures embedded), and GLB. The .blend working files (27 MB each, textures packed) are not shipped because they exceed the transfer limit; `rig.py` rebuilds them exactly. `sheet.py` re-imports the FBX and renders the check sheets and GIFs.

## Known limits (for art review)

- **Cloth follows the body.** Sparkstep's cape is weighted to the legs and spine, so in Idle it stretches between the legs. Iris's scarf rides the right arm. Cloth bones or a cloth sim would fix this later.
- The Idle stance is the Keraunos Meshy idle: feet apart, knees slightly bent.
- No fingers; hands stay open as in the A-pose.
- Walk is in place, and both feet clear the base for a few frames at mid-stride (max 0.084 m).

## Unity check

`run-unity-check.ps1` creates a throwaway project in `unity-check\` and imports these two rigs plus the Keraunos pilot FBX as Generic with looping clips. It samples every frame, flags sinking below -0.01 m or floating above 0.02 m, measures the loop seam, and renders front and side frames to `unity-check-renders\`. The report goes to `unity-check-report.json`. It does not touch UnityProof.

## Files
| File | Bytes | SHA-256 |
|---|---|---|
| zeus_iris_signal_runner_fbx_verify.json | 326 | 657a56fc5aad7baa719301e4561faf1f2ef41b1aee4e0a35265b29d78b0b3a24 |
| zeus_iris_signal_runner_idle.gif | 237351 | 5cbdd6e123893600d24e7c912d1093d41dcb604ece4874cef543110615ff1007 |
| zeus_iris_signal_runner_joints.json | 2487 | 4f9c7f66225275283a2bc1f71f5c99e895e9fe36dcbf82dddae73f03af30daaa |
| zeus_iris_signal_runner_joints_front.png | 193550 | b016bd3566730737d12b353b3b5f660387ad4e461f7bde18cc7b7628cb78ded0 |
| zeus_iris_signal_runner_rig_report.json | 419 | 156774d29e5017858fd3e4593e140d6a0e2fc4a04dfd8bd3489bedb7eddb80dc |
| zeus_iris_signal_runner_rigged.fbx | 13293804 | 141ec9a6776f67d4b96a6b2fb6054d566b69c65b476a9070f0329fb7f031ca6b |
| zeus_iris_signal_runner_rigged.glb | 15214872 | d3e2309e4d9b3eb2ed29903322aca1cf79536838f86b1777cff33713fc34f31b |
| zeus_iris_signal_runner_sheet.png | 1048556 | 9cc220001a72b211ea7e44cf696930fe39350ab971dba2c5a0e379a4299b81cc |
| zeus_iris_signal_runner_walk.gif | 226523 | cb7d8580eb41458e8b3688dc960dcc1c6bc3e8a5deff901b8c54e699e6775e3e |
| zeus_keyword_sparkstep_runner_fbx_verify.json | 349 | b22f6f02de07f8f8106dc2b930564df9fd0e8fdec051c92cb99122eb21b6e4fc |
| zeus_keyword_sparkstep_runner_idle.gif | 226588 | af4cb738c43d6f105b9ae001aa65af4e930832ca2b448a9b7f95fcfa77c3e5ec |
| zeus_keyword_sparkstep_runner_joints.json | 2501 | 7563cf7eba09e86603117d182ca9d97d52b1cb26095193cdc6e12bc44a8717ab |
| zeus_keyword_sparkstep_runner_joints_front.png | 183117 | ac59ea05ef92bca15cf4d3ca266b9d041fbf389ad22bcd3ece166f7502a3dd70 |
| zeus_keyword_sparkstep_runner_rig_report.json | 421 | ce8da703d8ec1f8bf4e76bb203093ba9da7e8decf7718cd7494d2d5ff4992799 |
| zeus_keyword_sparkstep_runner_rigged.fbx | 12258668 | 380a9909b6bf40d09179f6b1aca811112dd4009ef5b7f11110eb6fea40870403 |
| zeus_keyword_sparkstep_runner_rigged.glb | 13974836 | f292bf5ae39601469b023f14885b2ba02f33858b47f44ea1e361ac2057b95ed6 |
| zeus_keyword_sparkstep_runner_sheet.png | 983328 | a45820bc1f1fff7b5e1e2c49686690a0c79144534d67b4e24fcda99f41118284 |
| zeus_keyword_sparkstep_runner_walk.gif | 208419 | 8148fab602e7f74a1163639fbeecd800735a804369afcb3da395e3bfb173e57e |

Sources (read only): `batch-04c-zeus-humans-desolate-tuba/zeus_keyword_sparkstep_runner.glb` 5dd5965f1f505d714d6c2e446f8022693c3fe275468de7e39b7127ba8f487b1a, `zeus_iris_signal_runner.glb` 1b8abe6e36cb38b849f164cecdf55792b1b545df2ac90e5d2ed5598d8fc7e328, clips from `batch-04-alpha-img2-3d/rigged/zeus_keraunos_prime.glb` 89a003ba74a6c5752e8bd8628d6d44ffb4180aa1b68ff3144a4139d03c74630c.

Rebuild: `python3 -I scripts/fit.py -- <mesh.glb> <out-prefix> ['{"_mirror":"Left"}']`, then `python3 -I scripts/rig.py -- <mesh.glb> <out-prefix>.json <keraunos rigged.glb> <outdir> <card_id>`, then `python3 -I scripts/sheet.py <card>_rigged.fbx <framesdir> <card_id>`, with the `bpy==5.2.2` module.
