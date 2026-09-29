# Infinite Conquest — 3D movement proof

An interactive, deliberately small 24-hex board (4 × 6, odd-row offset) with a gold runner and optional red enemy structure. Click a teal tile to spend the runner's single movement point. Reset either fixture with the buttons above the board. A rejected move preserves position and movement budget.

This implements **only** the two AI-036 fixture scenarios and their immediate boundary checks. It is not a port of the complete Java rules engine, a complete match, multiplayer, or production art. Movement uses the HEX geometry the alpha plays (`MatchRules.hex()`, ported from TubaExperiment `BoardGeometry.HEX.distance`): six neighbours per hex. The AI-036 fixture was written against the engine's SQUARE test default, so its note that diagonals cost one does not apply; both fixture cases give the same result on hex.

## Run the built increment

On the development machine, double-click `Build/Windows/InfiniteConquestProof.exe`. Keep its adjacent `_Data`, UnityPlayer and runtime files together. Builds are local artifacts and are ignored by Git.

## Rebuild from source

Requires Unity **6000.6.3f1**, Windows build support, an active Unity license, and the Unity CLI. From the repository's parent `Infinite Conquest` directory:

```powershell
& .\tools\unity\unity.exe run .\3DTuba\UnityProof --editor-version 6000.6.3f1 --timeout 600 -- -executeMethod ProofBuild.Build -logFile proof-build.log
```

Or run the same command with your installed `unity` CLI and absolute project path. `ProofBuild.Build` validates the pinned fixture, opens `Assets/Scenes/MovementProof.unity` (generating it only when missing, so hand-authored scene changes survive), adds it to the build list without removing other scenes, and builds Windows. It throws on a failed assertion or unsuccessful build. Validation evidence is `Build/validation.json`; this is fourteen assertions (fixture cases, boundaries and hex geometry), not a full game regression suite. The scene contains an explicit shader reference so player stripping preserves the runtime materials.

For a bounded runtime smoke, from this project directory:

```powershell
& .\Build\Windows\InfiniteConquestProof.exe -batchmode -proofSmoke -proofResult Build/smoke.json -logFile Build/player-smoke.log
```

The player checks the legal and blocked outcomes through the same Move/Reset handlers used by the UI, writes the JSON result, and exits 0 only when both pass. A normal launch without `-proofSmoke` is interactive. This smoke does not assert mouse-event delivery or full-match rules.

## Provenance and limits

`Assets/Proof/movement-fixture.json` is copied unchanged from `docs/muse/sprint-01/movement-fixture.json`, pinned to TubaExperiment `992bc95c7164416ea0a25a4ce120f6ec0a0a167a`. Its historical embedded no-JDK note is superseded by the later alpha-core-foundation runbook; the JSON is preserved for traceability. Neither reference repository is modified. Primitive geometry/materials are locally authored placeholders.

Source files: MovementState.cs (restricted model, hex distance), BoardLayout.cs (board size, hex tile mesh and world positions, shared by every script), MovementProof.cs (3D view/input), Editor/ProofBuild.cs (fixture validation and build), Editor/TokenPreview.cs (Meshy token review stills). Unity packages are pinned by Packages/packages-lock.json. The project has no cloud link, purchases, network service, or account system.

