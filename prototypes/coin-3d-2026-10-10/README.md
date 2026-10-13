# Coin prototype — AI-080-COIN-3D-PRESENTATION (2026-10-10, Claude Code)

Isolated prototype of the 3D end-over-end starting-player coin with swappable skins. Build, launch steps and test results: `playtest/unity-build-coin-prototype-2026-10-10/README.md`.

## Layout

- `overlay/`: every new or changed file, at its path relative to `3DTuba-restoration/UnityProof/`. Copy it over a copy of that project to reproduce the build:
  `Unity -batchmode -quit -projectPath <copy> -executeMethod CoinPrototypeBuild.Build -buildPath <dir>\InfiniteConquestPlaytest.exe`, then copy `Bridge/` next to the exe.
- `original/MatchRestoration.cs`: the restoration file before my change, for diffing. The live restoration source is untouched (same hash).
- `tools/make_coin_textures.py`: regenerates the placeholder skin PNGs (PIL; faction colours from 3DTuba `docs/production/FACTION_COLOR_GUIDE.md`; the CapitalArtTest skin crops the existing capital card art). No Meshy, no paid generation.
- `evidence-build1/`: test run on the first build. The coin edge dipped into the round pedestal on landing; build 2 fixed that and switched to a hex pedestal. The final evidence is in the build folder.
- `texture-preview.png`: both skins' faces side by side.
- `SHA256SUMS.txt`: hashes of `overlay/`, `original/` and `tools/`.

The scratch Unity project copy lives in my local temp folder, not in the shared tree.

## Files

| Path (under UnityProof/) | Status | Role |
|---|---|---|
| `Assets/Playtest/Scripts/CoinSkin.cs` | new | ScriptableObject: front/back face textures, face tint/metallic/smoothness, rim texture + colour or full rim material override, reveal emission. `CoinSkin.Load(name)` resolves `Resources/CoinSkins/<name>` and falls back to `Default`. |
| `Assets/Playtest/Scripts/CoinMesh.cs` | new | Procedural placeholder coin: 3 submeshes (front face, back face, rim+bevels). Also builds the hex pedestal (6 segments). |
| `Assets/Playtest/Scripts/CoinFlip3D.cs` | new | Self-contained stage (own camera and lights, 500 units below the board), animation, frame capture, `-coinTest` proof runner. |
| `Assets/Playtest/Scripts/MatchRestoration.cs` | changed | Only `PresentCoinFlip` and `DrawCoinFlip` (plus two fields). They now start `CoinFlip3D` with the engine winner and draw the result label. The call site in `PlaytestGame.LiveLoop` is unchanged. |
| `Assets/Playtest/Editor/CoinPrototypeBuild.cs` | new | Texture import settings, `CoinLitTemplate.mat` (URP Lit with `_EMISSION`, so the variant ships), skin assets, 16 checks, then the unchanged `RestorationBuild.Build()`. |
| `Assets/Playtest/Resources/CoinSkins/` | new | `Default.asset` (placeholder emblems) and `CapitalArtTest.asset` (skin-swap test), each with `front.png`, `back.png`, `rim.png`; `CoinLitTemplate.mat`; `.meta` files. |

No edits to the restoration HUD (`PlaytestGame.cs`), CameraRig/startup/help, mulligan, Storm Relay, command center, interruption visuals or turn sounds. It reuses the existing `card`/`click`/`turn` UI cues as they are.

**Swapping a skin without code:** edit `Default.asset` in the Inspector, or create one (Assets → Create → Infinite Conquest → Coin Skin) under `Resources/CoinSkins/` and launch with `-coinSkin <assetName>`. A future AI-109 equip system would just pass that name. No ownership, entitlement or store is modelled.

**Integration (needs a separate claim/handoff):** this prototype touches `MatchRestoration.cs`, which sits in the restoration HUD lane. Before merging, the restoration owner should take the five new files and the `CoinSkins` folder as they are, and apply the `MatchRestoration.cs` diff (about 40 lines, coin functions only). Then run `CoinPrototypeBuild.Build` or add `CoinPrototypeBuild.Validate`/`Prepare` to their build chain.

## What the real coin model needs before art

Unity units are metres. The prototype coin is 1.0 across and 0.08 thick, with 0.018 bevels.

- **Size/proportion:** 1.0 diameter, 0.06–0.10 thick. The camera framing assumes 1.0, and the code can scale it if needed. Keep the silhouette round: the flip reads from the disc turning edge-on.
- **Pivot and orientation:** pivot at the geometric centre. +Y is the front-face normal (front = seat 0 / Zeus), −Y the back face (seat 1 / Poseidon). The flip axis is local X, so the coin must be symmetric about X–Z. The top of the front design points to +Z. The back design must read upright after a 180° turn about X, so its top points to −Z. From Blender (Z-up), export FBX with Forward −Z / Up Y, Apply Transform, scale 1.0, and check in Unity that +Y is the front.
- **Material slots, in this order:** 0 `Face_Front`, 1 `Face_Back`, 2 `Rim` (edge band, both bevels, any raised lip). `CoinSkin` fills these three slots, so the skins keep working on a new mesh with this slot order.
- **UV0, faces:** planar projection of each face onto the full 0–1 square, centre (0.5, 0.5), disc edge touching the square's edges. Each face has its own texture, so both may overlap 0–1. Front: U = +X, V = +Z. Back: U = +X, V = −Z (the mirror that keeps it upright after the flip).
- **UV0, rim:** one strip. U = 0→1 around the circumference (seam at +X, increasing towards +Z), V = 0 at the bottom (−Y) → 1 at the top (+Y), including the bevels. Wrap U (Repeat), clamp V. Reeding/knurling goes in the texture or normal map, not geometry.
- **Textures per skin:** faces 1024×1024 sRGB PNG (2048 for collectible close-ups; the import cap is 1024 and can be raised). Rim 1024×64 (or 2048×128). The emblem and the faction name must read at about 300 px on screen. Optional per-skin maps need small `CoinSkin` additions when real art arrives: face/rim normal maps, a URP metallic-smoothness map (metal R, smoothness A), an emission mask.
- **Relief:** put the emblem relief in per-skin normal maps rather than mesh geometry. Mesh relief would lock one design into the model and break skin swaps. The mesh itself can carry a neutral raised lip ring and the bevels.
- **Poly budget:** the placeholder is 576 triangles (72 segments). The real coin should stay at or under 2,500 triangles, with a single LOD; it is a full-screen interstitial. Hard edges at the face–bevel and bevel–rim boundaries, smooth around the circumference.
- **Look:** lit by a warm key spot (upper left front) and a cyan point fill (right), on a dark navy background. Default metallic is 0.35 on faces and 0.85 on the rim. Avoid pure-mirror faces, because there is no reflection probe on the stage and they go dark.
- **No Meshy:** model locally (Blender) or keep the procedural mesh with real textures.
