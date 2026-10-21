# Rig handoff to Codex: PR #27 fixed rigs (AI-216, 2026-10-21)

These are the verified inputs for Codex's integration copy `worktrees/release-consolidated-2026-10-21/`. I did not edit that folder, any earlier source or package, or any Codex or ChatGPT folder. My Unity editor and player use has stopped and is released (see the SPRINT_LOG AI-216 row).

Verified reference copy: `3DTuba-rig-fixes-2026-10-21/UnityProof` (the AI-215 source plus the changes below). Build and smoke evidence: `DELIVERY.md`.

## 1. FBX files (16), drop-in over the same paths

Source: `reviews/rig-fixes-import-2026-10-21/models/<card>_rigged.fbx`. This is a copy of project files `rig-combat-2026-10-18-fixes/models/`, with the 10 split files rejoined. All 16 match the published `SHA256SUMS.txt` (copied here as `models/SHA256SUMS.txt`).

Destination: `UnityProof/Assets/Playtest/Resources/Tokens/<card>/<card>.fbx`. Overwrite the file only and keep the existing `.meta`, so the GUID and import settings stay the same.

| Card | SHA-256 of the FBX | Token .meta GUID (unchanged) |
|---|---|---|
| poseidon_keyword_reef_tunneler | a8b56b589be5ea562744133a3c10755dacc39e04abb614629bbc157b4ead8092 | bff18f64d94bbbe459c4d0840358c108 |
| poseidon_naiad_flowshaper | b257270110a015dae0cc10a1fa90ff1cc2dd10c43d975d1eebdf6af9e3250d39 | 3f1ec2399ea4dbf45bf281f46dba6918 |
| poseidon_tidepool_surveyor | ef824e5ac4af62b5b19f6135a15468053f90840728cd9f443651fba191a0fb2c | f97d596f34a85d940b5279805fdfa335 |
| poseidon_undertow_stalker | 39680d0d8caf2f62565f98f8b68a5c1362201acb584f9568b1307c9c83f501ac | 99974f73daa600142a1b58722e8f0726 |
| zeus_aegis_airguard | 7eb6b1208169e19c747f1c9ebdfd3c9364e6a7cbdc7b9af7ffcd710fdbca1713 | cd37058be9cad6b4ca4bb0a085c255ea |
| zeus_apex_keraunos_seraph | 319bc373d63640749e64ceed56402ce389b6354dce756a448d6e7a03da6eb61b | de2bf1c0b9788a94c885491ac23c4e73 |
| zeus_arc_relay_scout | 3479147f107ea625f907ab0514bf786c458e39fee29d89a64bdec779feaf0477 | b0015108b21025d4ba0f0f225eadf7ef |
| zeus_eagle_of_the_high_grid | 853ffa8ae29f56a59d3d09cd3386aecc205952821e0b16f3d253050aa1ffb51b | 2565b780de9781141908da536505316d |
| zeus_hera_protocol_warden | 3db9fc499f619f3d4abe99929cd0ceeca69167600d2913be2588712036c2d0c0 | 4567e8f338e553043a4cca7de905e425 |
| zeus_iris_signal_runner | 9c3c4e7847da46a97d5be3f17e417b6a3c7736b317073e11d175783ec123e119 | fd14d8f42a87b0541a17f16b812b17ec |
| zeus_keraunos_prime | 45fd8d17174701f559ebd1d55396c90d27d6fc91145164dc9bbbc8975ccc365f | d26635c4371fa2845ab1ffe89c323ded |
| zeus_keyword_cloudline_raider | 302bafb6deb569d13218ac081ea4d37987c4ee6222dfdbe3a8cf8772327d4612 | 8406979d53ed74f4aa6ded8ff50eb107 |
| zeus_keyword_sparkstep_runner | 4a53498f5502b7e84461ab8b29328b0b0f627aa5ac2323c8cb0f25c499d00822 | b4a6293fcc16aae46bb668d218acdb37 |
| zeus_keyword_stormgate_sentinel | 3cbfc5860ca661bce025529d2739df43b99935d7c5c47ef5804cac848bdde28d | ea722df554ff5b945944e0f3f0ecef51 |
| zeus_siege_thunder_ram | 39027203ad3f0ab83749a23dc2458b778cbb517af13b15648415a104a4b9e732 | f0a3220bf1430474b865c3f42f8e6a2e |
| zeus_tempest_oracle | 347c6c1dc83e76f8ab0fa87ebd3a4ba5ab1a607a30a215bc10492b0f4f19c5c2 | a68020fab74e4ab4db8be03ceebb53eb |

## 2. Rig reports (16), required

Source: `reviews/rig-fixes-import-2026-10-21/reports/<card>_rig_report.json`, with checksums in `reports/SHA256SUMS.txt`. Destination: `UnityProof/Assets/Playtest/Editor/RigReports/`. Overwrite the 15 existing reports and add `zeus_siege_thunder_ram_rig_report.json`, which is new.

Thunder Ram needs this report. `PlaytestTokenImport.IsRigged` (`Editor/PlaytestBuild.cs`) turns animation import on only for tokens that have a rig report. Without it, the new Thunder Ram FBX imports with no clips.

## 3. Import settings and controllers (no manual steps)

- These are set by existing code, unchanged. `PlaytestTokenImport` handles tokens under `Resources/Tokens/`: it sets materials to None, sets importAnimation from the rig report, and turns off cameras and lights. `RigCombatHookup.Run()` (`Editor/RigCombatHookup.cs`) sets Generic `animationType`, the six takes (Idle, Walk, Attack, Hit, Death, Summon), loopTime on Idle and Walk only, and the `OnRigEvent` events from the report (impact, down and land). It also generates `Resources/Tokens/<card>/<card>_Rig.controller` with triggers Move, Attack, Summon, Destroy, Ability and Hit.
- `RigCombatBuild.Check()` calls `RigCombatHookup.Run()`, so an existing build chain that runs it picks this up. Otherwise, run `-executeMethod RigCombatHookup.Run` once after the files are in place.
- At runtime, `TokenFactory.cs` (around line 71) assigns `Resources.Load<RuntimeAnimatorController>(card.model + "_Rig")` and adds a `RigEvents` component. There is no per-card code. Thunder Ram's controller was generated this way; after the hookup its `.meta` showed `animationType: 2` (Generic).

## 4. Thunder Ram and the 47-to-48 roster checks (the only source edits)

- Thunder Ram was never a code exception. It was static only because it had no rig report and no `Assets/RigCompare` entry.
- Copy the old static token to `Assets/RigCompare/zeus_siege_thunder_ram.fbx` (SHA-256 095477032c2107a48e1bcbf4d42dbee837732fb3f9041cf9c5575c811b3a44a5, the token FBX from before the swap). Unity generates its meta. `RigCombatBuild.Ids()` lists the rigged roster from that folder and the side-by-side render uses it.
- Change three count checks from 47 to 48 (diff: `source/count-48.diff`; full files in `source/`):
  - `Editor/RigCombatBuild.cs:35` `ids.Length == 48`
  - `Editor/PerfBuild.cs:41` `riggedChecked == 48` (only if the AI-209 PerfBuild check is in your line)
  - `Scripts/PresentationValidation.cs:90` `rigged == 48`. Also update the comment on line 89, which still says Thunder Ram keeps the static token.
- Look for any other "47" roster assertion in your line, such as battlefield or equal-opening checks or docs that call Thunder Ram static. I only checked the files in my copy.

## 5. Verified in my copy

- Build Success.
- RIGCOMBAT passed 1,345/1,345, down from 5 failures, so all five clip-start notes are cleared.
- PERF_CHECK passed 241 checks for 48 characters.
- Packaged smoke passed, with 48/48 clip controllers, clips and events.
- All 16 fixed rigs match their token's height (ratio 0.986-1.02) and width (1.00).
- Thunder Ram's texture maps correctly on the new rig. Pose sheet: `render/zeus_siege_thunder_ram_poses.png`.

Build gotcha: if a copied Library gives "No script asset for CoinSkin" and a NullReferenceException at `CosmeticsBuild.cs:18`, delete `Library/ScriptAssemblies`, `Library/Bee`, `Library/ScriptMapper` and `Library/MonoManager.asset`, then rebuild.

## 6. Art concerns still open (Matt to judge in game)

From the AI-214 review (`reviews/claude-release-audit-2026-10-21/pr27-pr28-review/REVIEW.md`), each checked against my Unity pose sheets in `render/`:

1. Thunder Ram Death reads as a flip with its legs in the air, not a collapse. My sheet confirms this. Its Walk, Attack and Hit are small motions, and the attack barely reads.
2. Tidepool Surveyor Death ends with its legs raised and splayed upward, an upturned-bug look. My sheet confirms this. Undertow Stalker's death flips onto its back too.
3. Reef Tunneler's Attack is barely visible, and its Death tips up onto its nose.
4. These are new from my sheets. Keraunos Prime's rock base stretches into tall shards in Death. Aegis Airguard's shield and cape bend sharply in Hit and Summon.
5. Provenance: Thunder Ram's rig report `source` names only `zeus_siege_thunder_ram.glb`. The AI-052 textured GLB path and hash should be recorded.

None of these block the import. They are look-and-feel calls for Matt before he accepts the rigs.
