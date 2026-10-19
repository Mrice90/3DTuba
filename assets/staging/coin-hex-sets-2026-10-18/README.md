# Coin and hex cosmetic sets (2026-10-18)

Status: **STAGED for art review.** Three ornate sets, each a coin for the turn-order coin flip and a matching hex tile skin. Everything was built headless in Blender 5.2.2 from the scripts here; no Meshy credits were spent. Nothing is wired into a Unity project yet.

| Set id | Name | Fits | Coin heads (Player 1) | Coin tails (Player 2) | Tile |
|---|---|---|---|---|---|
| `celestial` | Celestial Lightning | Zeus | Gold greek-key band, marble ring with gold stars, royal-blue field with lightning tendrils, white-hot bolt | Gold laurel wreath, storm eye with lightning rays and a glowing orb, two stars | Marble and gold inlay, royal field, glowing lightning paths from corner rivets, calm marble centre |
| `abyssal` | Abyssal Pearl and Runes | Poseidon | Pearl-studded teal band, ring of glowing cyan runes, pearl trident with cyan outline over waves | Wave-crest border, rune circle, pearl nautilus with a glowing suture, two pearls | Teal border of glowing runes, pearls at the corners, faint caustics, smooth navy pool in the centre |
| `obsidian` | Obsidian Ember Cracks | Neutral (any god) | Notched iron band, obsidian face split by glowing ember cracks, bronze hex sigil with a molten core | Fractured obsidian terraces with ember seams and radial cracks, molten core, two embers | Obsidian with ember cracks kept out of the centre, bronze trim and corner studs |

Palettes follow `FACTION_COLOR_GUIDE` (Zeus white, gold, royal blue, blue-white lightning; Poseidon abyssal navy and teal, cyan glow, pearl, restrained gold). Obsidian is a neutral third option so any player can pick it.

Previews: `previews/<set>_hero.png` (both coin faces over seven tiles), `previews/<set>_board.png` (a 37-hex board in the skin) and `previews/<set>_flip.mp4` / `.gif` (the flip clip landing on a tile, with the glow pulse and landing flash).

## Files

| Path | What it is |
|---|---|
| `models/coin_<set>.glb`, `.fbx` | Ornate coin, 27.5k tris, clips `Flip` and `Idle` |
| `models/hex_<set>.glb`, `.fbx` | Hex tile, 4.8k tris (37 tiles is about 179k tris) |
| `models/<set>_set.blend` | Source scene with both pieces and their materials |
| `textures/<set>_<heads,tails,tile>_*.png` | 1024 maps: BaseColor, Emission, Normal (OpenGL), Height (16-bit), MetallicSmoothness (Unity, metal R and smoothness A), MetallicRoughness (glTF) |
| `textures/<set>_heads.png`, `<set>_tails.png` | Drop-in face images for the existing flat coin, relief and glow baked in |
| `unity/CosmeticGlow.cs` | Proposed idle glow script (breathing pulse, celestial flicker, landing flash) |
| `scripts/` | `textures.py` (numpy and Pillow), `build.py`, `verify.py`, `render.py` (bpy) |
| `models/stats.json`, `models/verify.json` | Counts and the re-import check (all 12 exports pass) |

## Contracts

- **Coin:** a drop-in for the AI-094 coin (draft PR #10, `UnityProof/Assets/Playtest/Coin/`). 1.0 across, origin at the centre, heads facing Unity +Y, the same three slots in the same order (`Coin_Rim`, `Coin_Heads`, `Coin_Tails`) and the same planar face UVs. The faces are real relief and the edge is reeded. Relief tops out about 4 mm above the lip on the abyssal pearls, which is invisible at flip size.
- **Tile:** pointy-top hex, 2.0 across the flats (contract units), 0.30 thick, centred on the origin like `BoardLayout.TileMesh` (top face at +0.15). One mesh and one material, so it also survives `TokenFactory`-style material overrides if it ever goes through one. Scale by `tileSize / 2.0` (0.59 for the current `BoardLayout.TileSize` of 1.18).
- **Clips:** `Flip` is 60 frames at 24 fps (0 to 30 toss with five turns, 30 to 46 settle, then hold, landing heads). `Idle` is a 96-frame hover and turn loop. CoinFlip.cs animates the coin root in code and stays the driver for the match flip; the clips are for menus, the collection screen or an Animator.
- **Glow:** emission is a texture, so the glow animates by scaling `_EmissionColor`. That is what `CosmeticGlow.cs` does, per material slot that has an Emission map (the plain rim slot stays dark). Recommended HDR intensity: celestial 2.5, abyssal 2.2, obsidian 3.0 (`CosmeticGlow.For(setId)`). Bloom in post makes it sing.

## How to hook them in (Unity lane)

Fastest, no code: copy `textures/<set>_heads.png` and `<set>_tails.png` into `Resources/Coin/Skins/`, add the ids to `CoinSkin.BuiltIn` and give each a rim colour in `CoinSkin.RimFor` (celestial `#D99A33`, abyssal `#9E7A38`, obsidian `#332E2B`). The existing coin then flips with the new faces; it just has no real relief and no live glow.

Full version:

1. Import `coin_<set>.fbx` and use it in place of `Resources/Coin/coin.fbx` for that skin.
2. `CoinFlip.UseShader` currently swaps in fresh materials and `ApplySkin` only sets the base map. Extend `CoinSkin` with Normal, MetallicSmoothness and Emission textures and set `_BumpMap`, `_MetallicGlossMap` and `_EmissionMap` in `Paint` (enable `_NORMALMAP`, `_METALLICGLOSSMAP`, `_EMISSION`).
3. Add `CosmeticGlow` to the coin, set `Emission = CosmeticGlow.For(id)`, `Flicker` on for celestial, and wire `CoinFlip.OnLand` to `CosmeticGlow.Flash`.
4. Tiles: give `BoardLayout.CreateTile` the `hex_<set>` mesh (scaled as above, collider too) and a material with the tile maps; seat A's skin on A's half and B's on B's per the AI-094 loadout. Add `CosmeticGlow` per tile, or animate the shared material once if 37 property blocks cost too much.

## Rebuild

```
python3 -I scripts/textures.py textures 1024            # numpy + Pillow
python3 -I scripts/build.py textures models             # bpy 5.2.2 (pip install bpy==5.2.2)
python3 -I scripts/verify.py models
python3 -I scripts/render.py models previews <set> hero|board|anim [samples]
```
