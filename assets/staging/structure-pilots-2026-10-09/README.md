# AI-081 structures: all 34 Zeus and Poseidon structures, built locally (2026-10-09)

Status: **STAGED for art review.** Every Zeus and Poseidon structure in `docs/reviews/2026-10-08-asset-library-gaps.csv` now has a model, built headless in Blender 5.2.2 (`bpy`). No Meshy jobs were run and no credits were spent. Nothing was imported into a Unity project. The game keeps its procedural stand-ins until these are integrated.

History: AI-081-STRUCTURES-LOCAL built three pilots (Storm Relay Pylon, Keraunos Charging Spire, Tidal Pump Station). Mathew approved the pilot style on 2026-10-09 ("the style your going with is fine"), and AI-081-STRUCTURES-FULL built the other 31 with the same kit and rules.

## The 34 structures

| Card id | Title | Faction | Tris | Height | Widest |
|---|---|---|---|---|---|
| `zeus_storm_relay_pylon` | Storm Relay Pylon (pilot) | Zeus | 6,038 | 1.51 | 1.72 |
| `zeus_keraunos_charging_spire` | Keraunos Charging Spire (pilot) | Zeus | 7,790 | 1.57 | 1.68 |
| `poseidon_tidal_pump_station` | Tidal Pump Station (pilot) | Poseidon | 7,774 | 1.32 | 1.74 |
| `zeus_ability_oracle_spire` | Oracle Spire | Zeus | 7,918 | 1.57 | 1.72 |
| `zeus_apex_worldstorm_spire` | Worldstorm Spire | Zeus | 7,792 | 1.6 | 1.72 |
| `zeus_cloudwall_bastion` | Cloudwall Bastion | Zeus | 6,882 | 1.53 | 1.72 |
| `zeus_ion_storm_lattice` | Ion Storm Lattice | Zeus | 6,286 | 1.55 | 1.72 |
| `zeus_structure_aegis_conductor` | Aegis Conductor | Zeus | 8,954 | 1.46 | 1.72 |
| `zeus_structure_cloud_archive` | Cloud Archive | Zeus | 9,714 | 1.58 | 1.68 |
| `zeus_structure_oracle_of_storms` | Oracle of Storms | Zeus | 8,392 | 1.56 | 1.72 |
| `zeus_structure_stormglass_relay` | Stormglass Relay | Zeus | 8,642 | 1.58 | 1.68 |
| `zeus_tutor_structure_1` | Sparkstep Beacon | Zeus | 9,450 | 1.59 | 1.72 |
| `zeus_tutor_structure_2` | Cloudline Dispatch | Zeus | 8,478 | 1.51 | 1.72 |
| `zeus_tutor_structure_3` | Sharp-Shot Observatory | Zeus | 10,308 | 1.59 | 1.72 |
| `zeus_tutor_structure_4` | Seraphic Relay | Zeus | 10,846 | 1.55 | 1.72 |
| `zeus_tutor_structure_5` | Skyfather's Summons | Zeus | 8,640 | 1.59 | 1.72 |
| `zeus_zephyr_mooring_mast` | Zephyr Mooring Mast | Zeus | 7,750 | 1.59 | 1.72 |
| `zeus_zeus_command_nexus` | Zeus Command Nexus | Zeus | 9,254 | 1.44 | 1.72 |
| `poseidon_ability_tidewell` | Tidewell Bastion | Poseidon | 9,346 | 1.55 | 1.72 |
| `poseidon_abyss_gate` | Abyss Gate | Poseidon | 6,318 | 1.59 | 1.72 |
| `poseidon_apex_leviathan_gate` | Leviathan Gate | Poseidon | 9,446 | 1.6 | 1.72 |
| `poseidon_coral_bulwark` | Coral Bulwark | Poseidon | 5,248 | 1.6 | 1.72 |
| `poseidon_driftwood_breakwater` | Driftwood Breakwater | Poseidon | 9,594 | 0.79 | 1.72 |
| `poseidon_moonwell_tidegate` | Moonwell Tidegate | Poseidon | 7,810 | 1.38 | 1.72 |
| `poseidon_sonar_beacon` | Sonar Beacon | Poseidon | 7,556 | 1.56 | 1.72 |
| `poseidon_structure_ambrosial_spring` | Ambrosial Spring | Poseidon | 8,662 | 1.53 | 1.72 |
| `poseidon_structure_current_exchange` | Current Exchange | Poseidon | 10,812 | 1.58 | 1.74 |
| `poseidon_structure_pearl_infirmary` | Pearl Infirmary | Poseidon | 8,628 | 1.11 | 1.74 |
| `poseidon_structure_tidevault` | Tidevault | Poseidon | 10,240 | 1.31 | 1.74 |
| `poseidon_tutor_structure_1` | Undertow Burrow Gate | Poseidon | 9,976 | 1.44 | 1.74 |
| `poseidon_tutor_structure_2` | Tideguard Barracks | Poseidon | 6,962 | 1.44 | 1.74 |
| `poseidon_tutor_structure_3` | Nereid Calling Conch | Poseidon | 8,780 | 1.38 | 1.74 |
| `poseidon_tutor_structure_4` | Leviathan Muster Dock | Poseidon | 7,940 | 1.56 | 1.74 |
| `poseidon_tutor_structure_5` | Thalassic Hero Hall | Poseidon | 6,992 | 1.54 | 1.74 |

All 34 report `within_budget: true` (`stats.json`). Re-importing each GLB and FBX gives one mesh object with one material named `Token`, its base at 0 and its origin centred (`verify.json`). Tris range from 5.2k to 10.8k, with an average of 8.4k.

Source art is in `3DTuba/assets/source-art/desolate-tuba-acda76f/structures/` (read only). Four cards have no Desolate-Tuba art, so they use `alpha-0.7.15/structures/`: Driftwood Breakwater, Moonwell Tidegate, Ion Storm Lattice and Zephyr Mooring Mast.

## Rules every model follows

- **Board scale (AI-063):** contract units with a hex 2.0 across the flats, the same as `PlaytestCatalog.ContractUnit`. Each model is at most 1.6 tall and at most 1.8 (0.9 hex) across. `TokenFactory` rescales to the same budget, so proportions hold in game.
- **Hex orientation:** base hexes are pointy-top, matching `BoardLayout.TileMesh`.
- **Origin and facing:** origin at the centre of the base, lowest point at 0, front facing +Z in Unity (−Y in the .blend).
- **One mesh, one material:** `TokenFactory` replaces every material slot with the folder's `Token` material, so colour comes from a shared 4×4 palette atlas. Each part's UVs sit on one swatch.
- **Palette (FACTION_COLOR_GUIDE):** Zeus is white marble first, strong gold second, royal blue third, with blue-white lightning glow. Poseidon is abyssal navy and teal, a cyan glow, pearl and restrained gold.

## Files

| File | What it is |
|---|---|
| `<card>.glb` | glTF with the atlas embedded (base colour, metallic-roughness, emission) |
| `<card>.fbx` | FBX for Unity (Y-up, −Z forward, base colour and emission embedded) |
| `<card>.blend` | Source scene |
| `Token_BaseColor.png`, `Token_Emission.png`, `Token_MetallicSmoothness.png` | The shared 128×128 atlas, the same for all 34. For a URP Lit `Token` material: Base Map, Emission Map (HDR intensity about 2), and Metallic Map with smoothness in alpha. Use point filtering. |
| `structures_zeus_sheet.jpg`, `structures_poseidon_sheet.jpg` | Contact sheets: each card's art above its front 3/4 render |
| `<card>_sheet.jpg` | Card art beside front 3/4, front, side and top renders on a hex tile |
| `renders/` (project files copy only, not in the repository or the PC folder) | The individual 640×640 renders |
| `stats.json`, `verify.json` | Size, tris and SHA-256 of every GLB, FBX and blend; re-import check |
| `scripts/` | `kit.py` (helpers and palette), the builder modules `structures.py` (pilots), `zeus_a.py`, `zeus_b.py`, `poseidon_c.py` and `poseidon_d.py`, plus `build.py`, `verify.py`, `render.py` and `sheet.py` |

Rebuild: run `pip install bpy==5.2.2`. Then, from `scripts/`, run `python3 -I build.py ../out structures zeus_a zeus_b poseidon_c poseidon_d`. Follow it with `verify.py ../out`, `render.py ../out` and `sheet.py ../out <source-art root>`. `build.py --only id1,id2` rebuilds a few cards. Cycles renders on Linux need `libegl1 libegl-mesa0 libgl1-mesa-dri`.

## Known limits

- **Kitbash, not sculpt.** These are clean hard-surface models with flat palette colours: no painted texture, carving or filigree. They read clearly at board distance but look simpler up close than the Meshy models.
- **Scenery left out.** The floating-island, cliff and waterfall backgrounds in the art are dropped. Each building sits on a short rock plinth on its land tile.
- **No glass transparency.** Single-material tokens can't be see-through, so glass chambers are open gold cages around glowing cores.
- **Palette gaps.** The atlas has no brown, pink or red. Driftwood Breakwater's wood is grey stone with dark lashings. Poseidon coral is teal with cyan tips.
- **Art that conflicts with the colour guide.** The alpha art for Ion Storm Lattice and Zephyr Mooring Mast is dark iron or black. Black can't be dominant for Zeus, so the lattice is grey girders with gold edges, and the mast is marble with royal-blue panels; both keep their shape.
- **Weakest likenesses.** Nereid Calling Conch: its flared lip is a fan of ribs, not a smooth flare. Cloudwall Bastion: its flat-panel sail turbines look busy from the front 3/4 view.
- **Tri counts.** Eight cards are over 9k, up to 10.8k. That is still far below the 30K Meshy remesh target.
- **Abyss Gate.** This is a local alternative to the held AI-052 Meshy interim, which stays tracked separately. Pick one at integration.
- **Static.** There is no build-up summon or collapse death yet. The parts are generated separately, so a later pass can export grouped parts for Unity to stack on summon.
- **Not checked in Unity.** Integration belongs to the AI-081-CANDIDATE-PROMOTE integration lane (Codex): put each FBX and a `Token` material in the card's model folder and check it on the board.

## Next gate

Art review of the full set by Mathew or Thalia, then Unity integration.
