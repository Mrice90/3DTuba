# Asset library gap list — 2026-10-08 (Claude Code)

The full per-card table is in `2026-10-08-asset-library-gaps.csv` next to this file: 367 rows, made up of the 139 runtime cards plus 228 cards from the other four factions. Its columns are: priority, id, name, faction, type, model, texture, rig, vfx, tris, over_60k, source, meshy_task_ids, source_art, gap, next_step and credit_est.

**Scope:** models, textures, rigs and VFX only. Sound, animation code, abilities and rules belong to other lanes. `playtest/unity-build-2026-10-02/` was not touched.

## Meshy credits: nothing generated

- **Balance: 9 credits, before and after.** I checked meshy.ai in Chrome on 2026-10-08 at about 02:20 EDT.
- **The cheapest job costs more than that.** In the web UI, image-to-3D costs 10 credits on Meshy 6 Lite and 25 on Meshy 7.1 Flagship.
- **So I generated and downloaded nothing.** There is no batch-08 folder, no check_glb run and no review sheet. No purchase or top-up was made.
- **No unknown recent jobs.** The newest task in the Meshy history is dated 10/01/2026. Nothing from the last 24 hours exists outside our manifests.

## Sources used

- **Runtime catalog:** `3DTuba-unity-playable/UnityProof/Assets/Playtest/Resources/Playtest/cards.json` (139 cards: Zeus and Poseidon, including capitals and tutors). Each card's `modelSource` is the GLB the Oct 2 build imported.
- **Other four factions:** `docs/muse/sprint-01/manifest.json`. These are Desolate-Tuba original card rows at `dde98f8`, 57 each for Ares, Athena, Hades and Hephaestus. I left out the 24 DEMO/UNASSIGNED prototype rows.
- **Model metadata:**
  - The batch manifests 01–07, AI-050/052 `tracking.json`, `RIG_PILOT_VALIDATION_2026-09-30.md` and `zeus-white-blue-gold-pilot/README.md`.
  - Triangle counts, skins and clips come from reading each source GLB's accessors (2026-10-08).
- **Earlier reviews:** the Thalia-corrected SP1-INVENTORY, the sprint plan and `playtest/sp1-models-candidates-2026-10-08/`.
- **Rig check:** none of the 90 imported Unity token FBXs contains animation clips. All 90 use `animationType: 2` (Generic) with no takes. Today, motion comes from `AnimHost` tweens and `Vfx` generic bursts.
- **Palette check:** I measured the mean colour and dark-texel share of the imported `Token_BaseColor.png` for the questioned Zeus pieces.

## Totals

### Runtime catalog (Zeus + Poseidon, 139)

| Type | Final | Candidate (fix already downloaded) | Interim (imported, off-style/held) | Stand-in (procedural) | None (by design) |
|---|---|---|---|---|---|
| Character (48) | 37 | 4 | 7 | 0 | – |
| Structure (34) | 0 | 0 | 1 (Abyss Gate) | **33** | – |
| Capital (6) | 3 (Zeus) | 0 | 3 (Poseidon) | 0 | – |
| Land (35) | 35 | 0 | 0 | 0 | – |
| Spell (16) | – | – | – | – | 16 (effect only) |
| **Total** | **75** | **4** | **11** | **33** | **16** |

### Rigs, textures, triangle counts and VFX

- **Rigs:** all 48 characters lack rigs and clips in Unity, and 11 of them are non-humanoid.
  - Three source GLBs do carry Meshy rigs, but none was imported with its clips. The Storm Titan and Keraunos Prime rigs failed the preview check; the Trident Core rig is provisional.
- **Textures:** 11 imported models still have held or off-style textures, and 4 more are dark or provisional imports whose compliant replacement is already downloaded.
- **Triangle counts:** 12 models are over 60K, all of them batch-07 Poseidon humans that were never remeshed (116K–568K tris).
  - The worst is `poseidon_delphic_sonar_adept` at 567,766 tris.
  - `poseidon_keyword_reef_tunneler` (50,516) is under the flag but still above the ~30K standard.
- **VFX:** all 16 spells need a unique cast + impact effect. All 123 board pieces still use the generic faction burst for summon and death.

### Other four factions (P4, 228 cards)

- Nothing exists in the runtime yet: 98 characters, 46 structures, 12 capitals, 42 lands and 30 spells.
- Only the 12 capitals have Desolate-Tuba art. The other 216 cards have no art at all, so card art has to be made before image-to-3D.

## Estimated Meshy credit cost by priority

Unit costs, as recorded in the manifests:
- **New piece: 35 credits.** Meshy 7.1 image-to-3D (25) + remesh (0 on the web) + texture (10), as in batches 04b/04c/04d.
- **Retexture: 10 credits.**
- **New land: 20 credits.** Meshy 6 Lite (10) + texture (10), then local `hybrid_land.py`, as in batch 06.
- **Rig: about 20 credits.** Auto-rig (5) + 5 clips × 3. This uses the Meshy API price list (docs.meshy.ai/en/api/pricing); I did not verify the web UI price, and no rig cost is recorded in any manifest.

| Priority | What | Rows | Est. credits |
|---|---|---|---|
| **P0** | Zeus/Poseidon structures on stand-ins (33), plus a style review of the Abyss Gate interim model | 34 | 1,190 |
| **P1** | Zeus/Poseidon characters: rig + 5 clips for all 48 | 48 | 1,180 |
| | of which: remaking 7 interim characters (Skyfather Archon and 5 off-style Poseidon models regenerated at 35 each; the Eagle retextured at 10) | | 220 (incl.) |
| | of which: promoting 4 already-downloaded candidates | | 0 (incl.) |
| **P2** | Remesh the 12 batch-07 characters over budget (shared with P1 rows) | 12 | 0 via local Blender decimate, or 15 each via Meshy (remesh 5 + retexture 10) |
| **P2** | Remake the 3 off-style Poseidon capitals (batch-01 text-to-3D) | 3 | 105 |
| **P3** | Unique cast/impact VFX for 16 spells (Unity work) | 16 | 0 Meshy |
| **P4** | Ares, Athena, Hades and Hephaestus: models + character rigs (216 cards need card art first) | 228 | 8,260 |
| | **Zeus + Poseidon subtotal (P0–P3)** | | **~2,475** |
| | **Full library** | | **~10,735** |

At 9 credits, even one P0 structure (35) can't be funded. Funding all of P0 needs about 1,190 credits.

## Top-priority gaps

### Zero-credit wins

These are candidates that are already downloaded and only need promoting after the baseline verdict:

1. **`zeus_apex_olympian_storm_titan`:** batch-04d with the spear trim. The token is in `playtest/sp1-models-candidates-2026-10-08/`. Palette PASS (Thalia); the trim is under review.
2. **`zeus_keraunos_prime`:** batch-04d, with the token in the same folder. Palette PASS.
3. **`poseidon_poseidons_trident_core`:** batch-04b static. This replaces the off-style batch-03 provisional rig.
4. **`zeus_siege_thunder_ram`** (new finding):
   - **Baseline:** imports the dark AI-052 text-to-3D ram (52% dark texels).
   - **Compliant replacement already staged:** `batch-04b-alpha-img2-3d/zeus_siege_thunder_ram.cleaned.glb`, the white/gold retexture `01a0f429-9f62-713c-8bd3-bc19249e37f1` with debris removed.

### P0: structures (33 stand-ins, 35 credits each, 1,155 total)

- **Zeus (17):**
  - `zeus_ability_oracle_spire`, `zeus_apex_worldstorm_spire`
  - `zeus_storm_relay_pylon`, `zeus_cloudwall_bastion`, `zeus_keraunos_charging_spire`, `zeus_zeus_command_nexus`
  - `zeus_ion_storm_lattice`†, `zeus_zephyr_mooring_mast`†
  - `zeus_structure_{aegis_conductor, oracle_of_storms, stormglass_relay, cloud_archive}`
  - `zeus_tutor_structure_1..5`
- **Poseidon (16):**
  - `poseidon_ability_tidewell`, `poseidon_apex_leviathan_gate`
  - `poseidon_tidal_pump_station`, `poseidon_coral_bulwark`, `poseidon_sonar_beacon`
  - `poseidon_moonwell_tidegate`†, `poseidon_driftwood_breakwater`†
  - `poseidon_structure_{current_exchange, ambrosial_spring, tidevault, pearl_infirmary}`
  - `poseidon_tutor_structure_1..5`
- **Also: `poseidon_abyss_gate`.** It is imported, but as an AI-052 pre-style text-to-3D model that was never style-reviewed. It needs a style review before any spend.

† No Desolate-Tuba art exists for these four; the input would be the `assets/source-art/alpha-0.7.15/structures/` art. The other 29 have Desolate-Tuba art in `assets/source-art/desolate-tuba-acda76f/structures/`.

**Suggested method** (the batch 04b/04c pattern, not run):
1. Meshy 7.1 image-to-3D from the Desolate-Tuba crop, with no pose.
2. Remesh to 30K.
3. Texture with Text Input and the faction palette from `FACTION_COLOR_GUIDE.md`. Batch 04b/05 found text input beats image input for architecture and lands. Zeus: white, then visible gold, then blue, with blue-white lightning. Poseidon: abyssal navy/teal, cyan glow, pearl/gold accents.
4. Run `check_glb.py --require-materials`, then the Blender token, then a review sheet.

### P1: characters

- **All 48 need a rig plus idle/walk/attack/hit/death clips.** Meshy's rig pilot failed on 2 of the 3 models it was tried on (`RIG_PILOT_VALIDATION_2026-09-30.md`).
  - **Recommendation:** re-pilot on 2–3 batch-04c Desolate-Tuba humans in A-pose before rigging in bulk.
  - The 11 non-humanoids (eagle, ram, krakens, leviathans, molecrab, drone, Boltwing Cavalier's mount, Aetherbolt Avatar, Reef Tunneler) need custom rigs or a static treatment by design.
- **Interim models to remake** (none has a replacement staged):
  - `zeus_apex_skyfather_archon`: held black/gold (51% dark texels) with silhouette drift.
  - `zeus_eagle_of_the_high_grid`: held texture; retexture only.
  - `poseidon_apex_kraken_prime_avatar` and `poseidon_keyword_abyssal_leviathan`: batch-02, off-style.
  - `poseidon_keyword_trench_stalker` and `poseidon_siege_kraken_sapper`: batch-03, off-style.
  - `poseidon_leviathan_wakeborn`: AI-052, never reviewed.

### P2

- **12 batch-07 Poseidon humans over 60K tris:**
  - **Local option (0 credits):** a Blender decimate to about 30K that keeps UVs and the texture. Recommended first.
  - **Meshy option (15 each):** remesh, which drops the texture, so a retexture is needed after it.
- **3 off-style Poseidon capitals** from batch-01.

## Corrections to earlier records

1. **SP1-INVENTORY says the baseline's Zeus capitals and Skyline Seer use "held" black/gold batch-04b textures. The imported tokens are not dark:**
   - The imported `Token_BaseColor.png` mean RGB is 150/163/180 for Citadel, 115/134/152 for Spire, 153/189/203 for Cloud Throne and 114/132/145 for Seer, each with 0% dark texels.
   - This matches the batch-04b manifest, which says the black/gold textures were never downloaded and the white/blue retextures were staged instead.
   - The genuinely dark imports are Skyfather Archon (51% dark), Storm Titan (57%) and Thunder Ram (52%, the AI-052 version).
2. **The baseline imports these batch-01/02/03 text-to-3D models, which the 2026-09-29 `STYLE_REVIEW_OFF-STYLE.md` said not to import:**
   - `poseidon_apex_kraken_prime_avatar`, `poseidon_keyword_abyssal_leviathan`, `poseidon_keyword_trench_stalker`, `poseidon_siege_kraken_sapper`
   - the 3 Poseidon capitals
   - the Trident Core rig
3. **Task IDs are still not recorded** for batch 04c (13 Zeus humans), batches 01–03, AI-050/052 and the Seraph pilot (a Meshy Agent chat). Batch 05 and 07 texture IDs are prefixes only.

## Not verified

- Web-UI prices for rigging and animation.
- Whether the Abyss Gate fits the neo-futuristic style; I didn't render it.
- The in-game look of any row: this is a file-level audit.
