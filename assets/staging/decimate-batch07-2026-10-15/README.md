# Batch-07 Poseidon decimation (AI-060-LAPTOP-BUDGET, decimation part)

Claude Code, 2026-10-15. The 12 batch-07 Poseidon character models were 116K to 568K triangles each, against the 60K budget in docs/reviews/2026-10-08-asset-library-gaps.md. Each one is now 30K triangles, with the same UVs and textures. No Meshy credits were used. This is a candidate for review: nothing has been imported into Unity, and no game files were changed.

Total across the 12: 3,821,498 -> 359,994 triangles (10.6x fewer).

## Results

Silhouette IoU compares the alpha mask of the before and after renders from the same camera. 1.000 means the outline is identical. "Detail" is a close-up of the top fifth of the model, where decimation shows first.

| Card | Tris before | Tris after | Min silhouette IoU (5 views) | Detail IoU | GLB size |
|---|---|---|---|---|---|
| poseidon_abyssal_molecrab | 457,248 | 30,000 | 0.989 | 0.997 | 10.7 MB |
| poseidon_delphic_sonar_adept | 567,766 | 29,999 | 0.976 | 0.981 | 8.7 MB |
| poseidon_fast_razorfin_lancer | 189,382 | 30,000 | 0.992 | 0.994 | 8.9 MB |
| poseidon_keyword_breakwater_hoplite | 246,636 | 30,000 | 0.994 | 0.997 | 8.1 MB |
| poseidon_kraken_tendril_drone | 305,236 | 30,000 | 0.993 | 0.993 | 9.2 MB |
| poseidon_naiad_flowshaper | 476,536 | 29,999 | 0.972 | 0.989 | 9.4 MB |
| poseidon_nereid_current_rider | 116,424 | 29,999 | 0.995 | 0.995 | 7.2 MB |
| poseidon_oceanid_pressure_mage | 293,460 | 29,999 | 0.987 | 0.987 | 8.7 MB |
| poseidon_reefline_defender | 499,164 | 29,999 | 0.983 | 0.989 | 10.3 MB |
| poseidon_tidepool_surveyor | 233,314 | 29,999 | 0.997 | 0.998 | 9.0 MB |
| poseidon_triton_waveguard | 289,702 | 30,000 | 0.984 | 0.984 | 8.6 MB |
| poseidon_undertow_stalker | 146,630 | 30,000 | 0.995 | 0.999 | 7.5 MB |

The weakest outlines are the Naiad Flowshaper (0.972, from its thin water ribbons) and the Delphic Sonar Adept (0.976). Side-by-side sheets show no visible loss at board scale. Please confirm on `sheets/poseidon_naiad_flowshaper_before_after.png` and `sheets/poseidon_delphic_sonar_adept_before_after.png`.

## Files

- `sheets/overview_before_after.png`: all 12 models, front three-quarter view, before and after.
- `sheets/<card>_before_after.png`: three-quarter, side and close-up views, before and after.
- `models/<card>/<card>_30k.glb`: decimated model with its three original textures embedded (base colour, metallic-roughness, normal).
- `models/<card>/<card>_30k.fbx`: the same mesh for the Unity token pipeline. Its texture names now have file extensions; see the rig-roster note about Unity skipping textures that have no extension. The FBX carries base colour and normal only. The game's `Token.mat` supplies its own metallic map, and `TokenFactory` overwrites the material in any case.
- `models/<card>/<card>_<Role>.jpg`: the original texture bytes, extracted unchanged from the source GLB.
- `models/<card>/<card>_30k.blend` and `<card>_report.json`: source hash, triangle counts, size, silhouette IoU and output hashes.
- `verify.json`: every GLB and FBX re-imported in Blender. Each has 1 mesh, 1 UV layer, a triangle count of 29,999 or 30,000, and the same height as its source.
- `scripts/`: `dec_all.py` runs `dec_run.py` (Blender collapse decimate, ratio = 30000 / source tris), followed by `dec_verify.py` and `dec_sheet.py`. Run them with `pip install bpy` (5.2.2), `apt install libegl1`, and `python3 -I`.

## Integration notes for the Unity owner (AI-081 integration lane, Codex)

- The sources are `3DTuba/assets/staging/meshy/batch-07-poseidon-humans-lite/textured/<card>.glb` (SHA-256 in each report), which are read-only.
- The UV layout is unchanged, so the existing `Resources/Tokens/<card>/Token_*.png` textures still fit. Swapping the mesh means replacing `<card>.fbx` in that folder with `<card>_30k.fbx`, renamed to `<card>.fbx`. Keep the original on the side as the fallback.
- The meshes use glTF axes as imported by Blender, the same as the earlier batch exports. Before swapping, check the orientation and scale of one token next to the current FBX.
- Open gates: the in-game look at board scale (Mathew or Thalia), and a populated-board frame-time measurement on the laptop (the rest of AI-060-LAPTOP-BUDGET).
- These models are still static. Rigging them is AI-060-RIG-ROSTER. A 30K mesh also makes that bone-heat step much faster.

## Where the models are

The repository copy holds the review sheets, reports and scripts only. The models (GLB, FBX, blend and textures, about 300 MB) are on Mathew's PC at `Infinite Conquest\playtest\decimate-batch07-2026-10-15\models\`. The batch-07 source GLBs are also kept outside git.
