# AI-081-STRUCTURES-LOCAL: three local structure pilots (2026-10-09)

Status: **STAGED for art review.** Built locally in Blender 5.2.2 (headless `bpy`) with no Meshy jobs and no credits spent. Nothing was imported into any Unity project, and no shared Unity file was touched. The procedural stand-ins in the game are unchanged until these are reviewed and integrated.

## The three cards

All three are P0 rows in `docs/reviews/2026-10-08-asset-library-gaps.csv` ("stand-in (procedural)", no model). They were picked for three different silhouettes, so the review covers a tall tower, a framed machine and a wide low complex:

| Card | Faction | Silhouette | Tris | Height | Widest | Source art (Desolate-Tuba `acda76f`) SHA-256 |
|---|---|---|---|---|---|---|
| `zeus_storm_relay_pylon` Storm Relay Pylon | Zeus | single tower with crescent horns and orb | 6,038 | 1.51 | 1.72 | `5346641846d31fb249afc2b0c3e5a9d481b35166ed885afa0c5afebf91544b18` |
| `zeus_keraunos_charging_spire` Keraunos Charging Spire | Zeus | caged lightning coil between twin pylons, gold spire | 7,790 | 1.57 | 1.68 | `15a1f62e7d1cae7e799108ddf2c822a8e4a278ef08f94908e5d4ea104dd8c326` |
| `poseidon_tidal_pump_station` Tidal Pump Station | Poseidon | wide pump hall, two side turbines, aqueduct deck | 7,774 | 1.32 | 1.74 | `c39854667559df0cf495998a7c92668b8ed50426559ca0d7627e4b51b041b3d2` |

Source art: `3DTuba/assets/source-art/desolate-tuba-acda76f/structures/<card>.jpg` (read only).

## Budgets met

- **Board scale (AI-063):** units are contract units, with a hex 2.0 across the flats, the same convention as `PlaytestCatalog.ContractUnit`. Every pilot is at most 1.6 tall and at most 1.8 (0.9 hex) across. `TokenFactory` rescales to the same budget anyway, so they keep these proportions in game.
- **Hex orientation:** the base hexes are pointy-top (corners toward front and back), matching `BoardLayout.TileMesh`, so plinth edges line up with the tile.
- **Origin and facing:** origin at the centre of the base, lowest point at 0, front facing +Z in Unity (−Y in the .blend). Verified by re-importing both the GLB and the FBX (`verify.json`).
- **Polycount:** 6–8k tris each, a quarter of the 30K Meshy remesh target.
- **One material:** each model is one mesh with one material named `Token`. `TokenFactory` replaces every material slot with the folder's `Token` material, so a multi-material model would lose its colours in game. The colours come from a 4×4 palette atlas instead: each part's UVs sit on one swatch.
- **Palette (FACTION_COLOR_GUIDE):** Zeus is white marble first, strong gold second, royal blue third, and blue-white lightning glow. Poseidon is abyssal navy and teal with a cyan glow, pearl white and restrained gold.

## Files

| File | What it is |
|---|---|
| `<card>.glb` | glTF with the atlas embedded (base colour, metallic-roughness, emission) |
| `<card>.fbx` | FBX for Unity (Y-up, −Z forward, base colour and emission embedded) |
| `<card>.blend` | Source scene |
| `Token_BaseColor.png`, `Token_Emission.png`, `Token_MetallicSmoothness.png` | The shared 128×128 atlas, the same for all three. For a Unity URP Lit `Token` material: Base Map, Emission Map (HDR intensity about 2), and Metallic Map with smoothness in alpha. Use point filtering. |
| `<card>_sheet.jpg`, `structure_pilots_review_sheet.jpg` | Source art beside front 3/4, front, side and top renders on a hex tile |
| `renders/` (local copy only) | The individual 640×640 renders |
| `stats.json`, `verify.json` | Measured size, tris and hashes; re-import check |
| `scripts/` | `kit.py` (helpers and palette), `structures.py` (the three builders), `build.py`, `verify.py`, `render.py`, `sheet.py` |

Rebuild: `pip install bpy==5.2.2`, then from `scripts/` run `python3 -I build.py ../out`, then `verify.py`, `render.py` and `sheet.py` with the same folder (`sheet.py` also takes the source-art folder). Cycles renders need `libegl1 libegl-mesa0 libgl1-mesa-dri` on Linux.

| File | Bytes | SHA-256 |
|---|---|---|
| zeus_storm_relay_pylon.glb | 251,888 | `c210d38d63c89802d9cb88f9b687d0626e0301a494201413f682d4b3426f43d0` |
| zeus_storm_relay_pylon.fbx | 150,300 | `d635a183d5b7ffc48fd3edbe5e139c95983ec1f0cc04a2ebd4c7fabf8e49da71` |
| zeus_keraunos_charging_spire.glb | 317,036 | `2e3937b87315b89a1b50e7e348d901039b979678802a33f9cd971a592c55fa71` |
| zeus_keraunos_charging_spire.fbx | 155,948 | `d28bcaf5c644099841f751266b5dcd3eb5ba0dd4584a16642ee739fdff3eb607` |
| poseidon_tidal_pump_station.glb | 328,072 | `4b09404ecc513b8002fb953e7ec4838d2aaa6a566344df0c7e72dfc0f5a75612` |
| poseidon_tidal_pump_station.fbx | 156,780 | `56a2fecbe7fa913b00e68ddbb07b6af87fd5facfdaaab6d48986971af0b39f6d` |

## Known limits

- **Kitbash, not sculpt.** These are clean hard-surface blockouts with flat palette colours: no painted texture, carving or filigree detail. They read clearly at board distance, but up close they look simpler than the Meshy models. The review question is whether this look is good enough for structures or whether it should only be a base for later detail passes.
- **The floating-island scenery is left out.** The art places each building on a rock island with waterfalls. On the board each one sits on a land tile, so only a short rock plinth is kept.
- **No glass transparency.** The single-material rule rules out a see-through chamber, so the Charging Spire's coil sits in an open gold cage and the Pump Station's water column is an opaque glowing cylinder.
- **Static.** The gaps list also asks for a build-up summon and a collapse death. Those are not in this pilot. Because the parts are generated separately, a later pass can export them in groups (base, body, crown) so Unity can stack them up on summon.
- **Not checked in Unity.** Integration stays with the AI-081-CANDIDATE-PROMOTE integration lane (Codex): drop the FBX and a `Token` material into the card's model folder and check it on the board.

## Next gate

Art review by Mathew or Thalia. If the look is accepted, the same kit can build the other 30 structures (AI-081-STRUCTURES-FULL). Abyss Gate stays tracked separately as an interim.
