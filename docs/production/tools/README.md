# AI-054 — GLB staging checker

Supporting tooling only. This is not a playable milestone and not an acceptance verdict.

`check_glb.py` is a read-only, standard-library-only Python 3 check for Meshy `.glb` outputs in `assets/staging/meshy/`. It runs before the Unity import step in `ASSET_QUEUE.md`. The contract comes from the AI-050 brief and the AI-049 `manifest-entry.json` constraints: filename = `card_id`, about 1.8 units tall, feet at the origin.

## Checks

| Check | Rule |
|---|---|
| `glb_container` | Magic `glTF`, container version 2, and a declared length equal to the file size. The JSON chunk comes first, followed by an optional BIN chunk. Every chunk must be in bounds and 4-byte aligned. JSON must be strict (no `NaN`/`Infinity`). |
| `filename_card_id` | The stem is snake_case. It must equal `--card-id` when given and must exist in the manifest `card_id` column (default `docs/muse/sprint-01/manifest.csv`). |
| `height` | World-space Y extent is `--expected-height` (1.8) ± `--height-tolerance` (0.10 = 10%). |
| `feet_at_origin` | World-space min Y is within ± `--feet-tolerance` (0.05 units) of 0. |
| `materials_assigned` | **Opt-in, only with `--require-materials`.** Every primitive rendered by the default scene references a material. Primitives in meshes the scene never draws are ignored. The failure detail names each offending `meshes[i].primitives[j]`. A pass only means a material is assigned; it says nothing about material separation or style. |

Default behaviour is unchanged: without `--require-materials` there is no material check, and a missing material is only counted in the report. An out-of-range, negative or non-integer `material` index is malformed glTF. It is **rejected (exit 2) with or without the flag**, as before this change.

The height, feet and filename thresholds are per-card command-line options. The defaults are the AI-050 character contract. This tool defines no polygon budget; triangle counts are reported, never judged.

World-space bounds read every POSITION vertex drawn by the default scene. Node `matrix`/TRS transforms are applied down the node tree. The report also lists:
- bounds (min, max, size), XZ center and tallest axis
- topology: mesh instances, primitives by mode, triangles, vertices and degenerate indexed triangles
- material names, used materials, primitives without a material, and texture/image counts
- the manifest `type`

**Rejected (exit 2)** rather than guessed at:
- truncated or oversized files, or bad headers/chunks
- non-finite positions or transforms
- POSITION data outside its declared `min`/`max`, or `min > max`
- accessors, bufferViews or buffers out of range
- out-of-range indices
- node cycles
- POINTS/LINES primitives
- Draco, meshopt or quantized positions
- sparse accessors
- external or data-URI buffers (nothing outside the GLB is opened)
- scenes with no geometry

**Not proven by this tool:**
- semantic +Y-up / +Z-forward orientation. glTF is +Y-up by definition, so Y extent is treated as height, but whether the model actually stands upright and faces forward needs visual review.
- material separation, style, silhouette and likeness
- Unity import and in-game scale/performance

## Usage

```
python docs/production/tools/check_glb.py <file.glb> [more.glb ...]
    [--card-id ID] [--manifest CSV | --no-manifest]
    [--expected-height 1.8] [--height-tolerance 0.10] [--feet-tolerance 0.05]
    [--require-materials] [--json]
python docs/production/tools/test_check_glb.py -v
```

Exit codes: `0` all checks pass; `1` a check failed; `2` rejected file or usage error. With several files, the highest code wins. Input files are opened read-only and never written.

## Tests

`test_check_glb.py` builds synthetic GLBs in a temp directory; no fixture files are committed. It has 55 tests:
- **Valid:** a 1.8-unit box on y = 0. Node scale and a matrix translation are applied. A 90° rotation is detected. Unindexed geometry works, degenerate triangles are counted, and the file is unchanged afterwards.
- **Wrong filename:** a bad stem, a mismatched `card_id`, and an id missing from the manifest.
- **Wrong scale:** centimetre scale, plus boundary heights at ±10%.
- **Wrong origin:** a centred mesh, and a node translation.
- **Malformed:** NaN/Inf positions, data outside the declared bounds, NaN declared bounds, `min > max`, accessors/views/buffers out of range, an out-of-range index, Inf and overflowing transforms, a node cycle, a truncated file, bad magic, container version 1, a tiny file, an overlong chunk, bad JSON, and a missing BIN chunk.
- **Unsupported:** POINTS, required Draco, quantized positions, sparse accessors, an external buffer, and no geometry.
- **Materials (`--require-materials`), 10 tests:**
  - Default mode adds no check.
  - Valid named materials pass, and an unnamed material still counts as assigned.
  - A missing material fails, including when there is no `materials` array at all.
  - When one of two primitives lacks a material, the check fails and names only that primitive.
  - A mesh the scene never draws is ignored.
  - An out-of-range index is rejected with and without the flag.
  - Negative and non-integer indices are rejected.
  - A material reference with no `materials` array is rejected.
- **CLI:** exit codes, JSON output, usage errors, the default manifest, and the `--require-materials` exit codes 0/1/2 (default unchanged).

## Run on staged Meshy outputs (2026-09-28, read-only)

The SHA-256 of all four GLBs was identical before and after each run. All four parsed cleanly (`meshy-scene` generator, one mesh, one TRIANGLES primitive, 0 degenerate triangles).

**Triangle counts compared with `ASSET_QUEUE.md`:**
- **AI-052 files:** the three staged files (Thunder Ram 882,440; Leviathan 501,202; Abyss Gate 1,250,576) match the counts recorded in the queue.
- **Skyline Seer:** the queue records no count for AI-050. The staged file has 575,174 triangles, but Astra reports that the current Meshy UI shows 621,922 for the staff attempt. Which attempt the staged file comes from is therefore **unresolved**, and this checker cannot settle it.

| File | Manifest type | Y extent | min Y | Tallest axis | Triangles / vertices | Materials | Result |
|---|---|---|---|---|---|---|---|
| `zeus_ability_skyline_seer.glb` | CHARACTER | 1.8989 (pass) | −0.9505 | y | 575,174 / 287,565 | none | FAIL: feet |
| `zeus_siege_thunder_ram.glb` | CHARACTER | 1.2593 | −0.6301 | z (1.8994) | 882,440 / 441,140 | none | FAIL: height, feet |
| `poseidon_leviathan_wakeborn.glb` | CHARACTER | 1.8996 (pass) | −0.9508 | y | 501,202 / 250,601 | none | FAIL: feet |
| `poseidon_abyss_gate.glb` | STRUCTURE | 1.7655 (pass) | −0.8843 | x (1.8992) | 1,250,576 / 625,254 | none | FAIL: feet |

What these results show:
- **Normalization:** Meshy centres each model on the origin and scales its largest dimension to about 1.9 units. The height passes for Skyline Seer and Leviathan are therefore a side effect of that normalization, not proof of the 1.8 m target. For the Thunder Ram and the Abyss Gate, the longest axis is horizontal.
- **Height contract:** the 1.8-unit rule is the AI-050 character contract. It is not a spec for the Abyss Gate (STRUCTURE) or a siege beast, so those need their own size targets before this check is meaningful for them.
- **Feet:** every model needs its origin moved to the feet, or a re-export, during cleanup.
- **Materials:** no GLB contains any materials, textures or images. The AI-049 requirement for "separated logical materials" is unmet as staged; texturing is still pending.

### Opt-in run with `--require-materials` (2026-09-28)

```
python.exe -B docs/production/tools/check_glb.py <the four GLBs above>                      -> exit 1
python.exe -B docs/production/tools/check_glb.py --require-materials <the four GLBs above>  -> exit 1
```

The default results are unchanged from the table above. With the flag, every file keeps its earlier results and also gets:

```
[FAIL] materials_assigned: 1 of 1 rendered primitive(s) have no material: meshes[0].primitives[0]
```

| File | Default result | `--require-materials` result |
|---|---|---|
| `zeus_ability_skyline_seer.glb` | FAIL: feet | FAIL: feet, materials_assigned |
| `zeus_siege_thunder_ram.glb` | FAIL: height, feet | FAIL: height, feet, materials_assigned |
| `poseidon_leviathan_wakeborn.glb` | FAIL: feet | FAIL: feet, materials_assigned |
| `poseidon_abyss_gate.glb` | FAIL: feet | FAIL: feet, materials_assigned |

These failures are expected for untextured staged outputs. They are a precondition check, not a verdict on style or material compliance.
