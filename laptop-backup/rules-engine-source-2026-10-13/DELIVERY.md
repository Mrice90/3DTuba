# Infinite Conquest 0.4 — playable hexagon checkpoint

**Preserved checkpoint:** active Unity source has advanced to 0.4.1. The exact 0.4 source is archived in `source-0.4.zip` and verified against all entries in this report's source manifest. The 0.4 package and installer remain unchanged. See `ground-integration/DELIVERY.md` for the newer portable candidate.

October 13, 2026. Owner: Codex, working alone under Mathew's direct release-push authorization. This is a tested development candidate, not commercial release approval.

## Run

Open `../../playtest/unity-build-release-candidate/PLAY-INFINITE-CONQUEST.cmd`, then **Enter the battlefield**, choose Zeus or Poseidon, **Begin conquest**, select a glowing home hex and confirm. Buy from your deck, play cards from your hand, and end your turn. Esc opens the pause menu.

This portable package requires no Java. Previous packages and the installed application were preserved. The source is `../../3DTuba-release-candidate/UnityProof`, copied from the released audio-library baseline. Native rules source, adapter and offline build/test runner are in this review's `source/` and `runner/`. The separately claimed ground-skins lane was not modified or merged.

Optional installer: `../../playtest/installer-release-candidate-0.4.0/InfiniteConquest-Setup.exe` (446,119,936 bytes). Its embedded payload was extracted as data and all 218 runtime files matched the tested portable build. Installer SHA256: `4e4ac42242a140f5a6989bf2b83b894b9d771fb9d99a98841c372d58bd6ab28a`. Installation was not executed; the inherited installer replaces the existing per-user 3D playtest installation if run. Use the portable launcher to preserve that installation.

## Delivered

- Authoritative radius-three hexagon: 37 playable cells, 15 home cells per faction and seven neutral cells. The 7×7 coordinate envelope has 12 invalid corner cells; those corners cannot host pieces, capitals or line traces. This is not the old rectangular engine underneath a visual preview.
- Client rendering, capital selection, board refresh, camera framing and bundled playback follow the same geometry. Capital placement is validated on both client and native rules sides. Default capitals are (3,0) and (3,6). Original playback coordinates are translated into the new mask.
- All 139 playable catalogue cards and all 163 card faces have 2D art. Twenty new faction land/structure illustrations complete the playable set; 24 legacy/demo faces explicitly reuse related art. Existing art is preserved. `art-coverage.json` records source hashes and reuse; `generated-art.json` lists the generated originals. There is no claim of 163 newly generated or unique images.
- Original citadel menu backdrop; redesigned main menu, allegiance/difficulty selection, tutorial and settings. Responsive layout inspected at 1280×720 and 960×640. Tutorial no longer truncates camera controls; selected settings no longer depend on clipped “selected” suffixes.
- Shop rows allow the complete status line. Printed hand hit testing uses actual active card rectangles, leaving blank margins/gaps clickable and removing exited cards from hit testing.
- Existing approved audio is retained. No sound generation, remote publication, new Meshy jobs or purchases.

## Verification of this exact package

Assembly-CSharp SHA256: `be5ec849bbe246b31342ee9c0edcfeed318163dc883c238ebbb1bb54e5516ab9`.

| Evidence | Result |
|---|---|
| Native offline build | Zero warnings/errors |
| `geometry-results.txt` | 11,073 assertions: mask, adjacency, distances, contained/unbroken/symmetric line traces |
| `hex-setup-results.json` | 357 checks; all 30 legal capital starts; invalid/neutral choices rejected atomically |
| `protocol-results.json` | 18,153 checks across eight complete deterministic native games, both factions, seeds 42/713/7/101 |
| `deck-results.json` | 26 deck validation checks |
| `editor-checks/` | Release/art/hit-area 170, board 38, camera 50, shop 39, reaction cues 42, R3 35, restoration 741, gameplay 25, coin 16, existing audio 1,336 plus audio followup 18, native both-seat integration |
| `packaged-final-results.json` | Playback plus eight complete Unity matches, exit 0; 347 actions, 355 HP snapshots, 24 command-center activations, 10 capital-damage checks and three Storm Relay checks |
| `visual/capture-results.json` | Actual purchases at 1280×720 and 960×640; card preview; two additional rendered matches, both seats, exit 0 |
| `visual-1280/result.json`, `visual-960/result.json` | All 163 card images load; no missing art; 37 board cells |
| `source-manifest.json`, `payload-manifest.json`, `checkpoint.json` | Source and runtime hashes; 218 runtime files, 634,207,422 bytes, no Java payload |
| `native-source-manifest.json` | 60 native source/project files pinned separately from Unity assets |
| `installer-verification.json` | Installer version 0.4.0 and all 218 embedded runtime files verified against the portable package; no install executed |

Packaged checks explicitly clear JAVA_HOME and remove Java from PATH. They exercise real game handlers under automation, not a human mouse acceptance session. These particular live matches encountered no reaction windows or healing actions; focused inherited tests cover those paths, but this does not establish new human acceptance. Geometry is a new `purchase-v3-hex37` profile; no Java parity or old network-peer compatibility is claimed.

Rendered screenshots were inspected. Initial hidden-window captures were black; those were replaced with valid visible, non-activating window captures. Earlier pre-art builds and their results are historical, not this final package's acceptance evidence. The final build log is `unity-build-final.log`; build entry point is `ReleaseReadinessBuild.Build`.

## Remaining release gates and next work

1. Battlefield environment, lighting, tile materials and match HUD still look like a development build. Main-menu improvements do not establish the requested AAA quality. Continue a cohesive in-match art pass and inspect populated boards.
2. Recheck four-plus-card hands and board click access at both target sizes under actual user interaction. The invisible margin bug is fixed and regression-tested, but real overlapping card faces still legitimately consume clicks.
3. Balance and pacing on the new geometry: deterministic games ranged from 15 to 58 turns. These are diagnostic runs, not a statistically sufficient balance study or starter-deck approval.
4. Integrate/review the staged structure and rig roster; current candidate retains inherited stand-ins and animation limitations. Do not take over the separate ground-skins claim without an explicit handoff.
5. Online menus, native multiplayer compatibility, capital negotiation, reconnect, hosted service and two-person/two-PC acceptance remain open. This package is the solo candidate.
6. Installer upgrade/uninstall safety, clean-machine installation, performance on target hardware and full human match acceptance remain open. Do not replace a user's working installation merely to run the candidate.
7. Muse resumes sound-generation work on Thursday per Mathew. Codex account reset was observed as October 20 at 5:17 PM Eastern with one reset credit; applying or synchronizing account resets is outside the available controls.

Reproduce packaged checks: run `run_packaged.py 42 713 7 101`. `capture.py 1280`, `capture.py 960`, and `capture_gameplay.py` open temporary visible game windows and exit. `checkpoint.py` refreshes manifests after validation; never present old hashes as current after source changes.
