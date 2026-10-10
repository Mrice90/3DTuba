# Meshy batch 05 / 06 / 07 provenance (AI-061-PROVENANCE)

Date: 2026-10-10 (file named for the 2026-10-09 review cycle). Author: Claude Code. Read-only consolidation: no asset files changed, no Meshy jobs submitted. Updated the same day with model hashes read from the local staging folders on Matt's PC, at Matt's request.

Machine-readable copy: `docs/reviews/2026-10-09-batch-provenance.csv` (one row per model, full card-art hashes).

## Counts

| | Count |
|---|---|
| Models in batches 05/06/07 | **48** (05: 5 lands, 06: 30 lands, 07: 13 Poseidon characters) |
| Models traced to batch + source card + Unity catalog entry | **48 of 48** |
| Model file SHA-256 recorded | **48 of 48** (the exact GLB the Unity playtest imports) |
| Meshy task IDs on record | **101** |
| Task IDs traced in full (36-char UUID) | **74** (batch 06: 60 of 60; batch 07: 14 of 26) |
| Task IDs that could not be traced in full (prefix only) | **27** (batch 05: 15 of 15; batch 07: 12 of 13 texture IDs) |
| Source card art present in repo with SHA-256 | **38 of 48** (the 10 tutor lands have no card art in repo) |

## Sources

In repo:
- Task IDs, staged path, source art: `docs/reviews/2026-10-08-asset-library-gaps.csv` on `muse/sprint-01-content-audit` @ `c41eff5`.
- Unity catalog cross-check: `UnityProof/Assets/Playtest/Resources/Playtest/cards.json` on `claude/unity-live-match` @ `fc30be3`. All 48 `modelSource` values name the same batch folder and file; no mismatches.
- Card art hashes: SHA-256 of `UnityProof/Assets/Playtest/Resources/CardArt/<card_id>.jpg` on `claude/unity-live-match` @ `fc30be3`.

On Matt's PC (`Infinite Conquest/3DTuba/assets/staging/meshy/`, read only, 2026-10-10):
- Batch 05: `MANIFEST.md` (task IDs and roles) and `SHA256SUMS.txt`. That checksum file covers only the five raw Meshy downloads (column `raw_meshy_sha256`). The `hybrid/` GLBs the game imports had no checksum, so `model_sha256` was computed here from those files (sizes matched the PC listing).
- Batch 06: `MANIFEST.md`, `tasks.json`, `SHA256SUMS.txt` (covers `hybrid/` and `meshy-raw/`).
- Batch 07: `MANIFEST.md`, `tasks.json`, `SHA256SUMS.txt` (covers `textured/`). These were not remeshed, so there is no separate raw hash.
- Every batch 06/07 task ID in the gaps CSV matched the local `tasks.json`; batch 05 IDs match its `MANIFEST.md`.

The model GLBs themselves are still not in the repo (Unity `Resources/Tokens/` is gitignored), so the hashes here are what to check a future upload against.

## Still open

- **27 prefix-only task IDs.** The local manifests hold prefixes too; batch 07's manifest says the full IDs are visible in the Meshy workspace. No jobs were resubmitted to find them.
- **10 tutor lands** have no card art in the repo to hash.

## Notes

- Batch 05 roles (generate, remesh, texture) are confirmed by its local `MANIFEST.md`: Meshy 6 text-to-3D, Remesh to 10K, Texture with text palette. Stormfront also had a discarded image-input texture task (`01a0f72a…`), not used and not counted.
- Batch 05 and 06 lands are staged from `hybrid/`, after the local hex-base step `hybrid_land.py` (on Matt's PC, not in this repo), so `model_sha256` differs from `raw_meshy_sha256` by design.
- Batch 07 characters were not remeshed (50K–568K tris), so they carry generate + texture IDs only.
- `poseidon_drowned_archive` uses `alpha-0.7.15/lands` source art; every other land uses `desolate-tuba/lands`.

## Table

Task IDs: full UUID, or `…` (prefix) when only a prefix is on record. Hashes are truncated here; the CSV has full values (plus `raw_meshy_sha256` and `model_hash_source`).

| Batch | Card ID | Name | Type | Generate | Remesh | Texture | Model SHA-256 | Card art SHA-256 |
|---|---|---|---|---|---|---|---|---|
| 05 | `poseidon_ability_healing_shoal` | Healing Shoal | Land | `01a0f71a-e16f…` (prefix) | `01a0f720…` (prefix) | `01a0f72f…` (prefix) | `e3bf108d31d942e5…` | `171748fc56b1…` |
| 05 | `poseidon_apex_atlantis_crown_basin` | Atlantis Crown Basin | Land | `01a0f71b-7889…` (prefix) | `01a0f726…` (prefix) | `01a0f735…` (prefix) | `97545b6f1016cefa…` | `96fad8a7040f…` |
| 05 | `poseidon_apex_oceanus_current_vault` | Oceanus Current Vault | Land | `01a0f71b-bf1a…` (prefix) | `01a0f728…` (prefix) | `01a0f737…` (prefix) | `062cd78a889f6f90…` | `2589a4c9deda…` |
| 05 | `zeus_ability_stormfront` | Stormfront Plateau | Land | `01a0f71a-956e…` (prefix) | `01a0f71e…` (prefix) | `01a0f73c…` (prefix) | `02bb2053cbd8b93a…` | `76bdf8e43463…` |
| 05 | `zeus_apex_celestial_throne_grid` | Celestial Throne Grid | Land | `01a0f71b-4da4…` (prefix) | `01a0f724…` (prefix) | `01a0f733…` (prefix) | `d864b2b900b735f8…` | `35c073d52eab…` |
| 06 | `poseidon_abyssal_pressure_trench` | Abyssal Pressure Trench | Land | `01a0f948-a148-7386-9a01-3b3eecc6541e` | — | `01a0f94f-381c-7566-b784-6f37c1eeb28d` | `40f16908409f29be…` | `942da2b46300…` |
| 06 | `poseidon_apex_leviathan_nursery_trench` | Leviathan Nursery Trench | Land | `01a0f944-f493-760e-bff8-bc33da82ffb5` | — | `01a0f946-1029-7628-a445-c08b79c64877` | `6ae47b570a529dfd…` | `6d1534071dbf…` |
| 06 | `poseidon_apex_trident_confluence` | Trident Confluence | Land | `01a0f947-5e9f-769f-abef-d16e802a9648` | — | `01a0f94d-2c8b-70da-912f-0bdd8e866fb7` | `924d1e3144c8ce1c…` | `fc7116deee8b…` |
| 06 | `poseidon_apex_worldsea_platform` | Worldsea Platform | Land | `01a0f947-ab5f-75b5-9c0b-aac0d53dc535` | — | `01a0f94d-879e-7481-bd21-475118b1c8b6` | `6e7962fc703bf701…` | `f9849949e9ce…` |
| 06 | `poseidon_coral_data_reef` | Coral Data Reef | Land | `01a0f948-8be0-7114-875f-9fa25a313a4a` | — | `01a0f94f-132d-706d-bbaa-8d5533a07950` | `278b8d2528f5586d…` | `8bf63c6da261…` |
| 06 | `poseidon_drowned_archive` | Drowned Archive | Land | `01a0f948-cc14-72ed-8cc0-8fb4d768e4bf` | — | `01a0f94f-da78-7791-be62-730726e942c3` | `0960a3cc6b21e867…` | `c8e4cb3e85b6…` |
| 06 | `poseidon_land_coral_tributary` | Coral Tributary | Land | `01a0f949-fe3d-7487-b68b-8520f90d3817` | — | `01a0f953-cdf2-7110-987f-bc690f2c810a` | `25a6e57e59a9f381…` | `9e0ea31e1119…` |
| 06 | `poseidon_land_leviathan_shelf` | Leviathan Shelf | Land | `01a0f94a-13c0-73f3-b3e8-e88bd997e98e` | — | `01a0f954-2541-7797-917a-323459c7029c` | `cce73d795c6d0a1e…` | `c44512493f8f…` |
| 06 | `poseidon_land_pelagic_kingdom` | Pelagic Kingdom | Land | `01a0f949-70aa-74fb-a3ed-8292dec0f42a` | — | `01a0f950-6dca-7442-9c45-5a8dc2fd36b2` | `42dbefaf36fb8293…` | `c126ef113b04…` |
| 06 | `poseidon_land_saltmarsh_harbor` | Saltmarsh Harbor | Land | `01a0f949-5b41-760d-88c7-7ace951f8a82` | — | `01a0f950-48ea-7519-914a-f0d638a8f77c` | `1f650f2cae395d33…` | `44b9e45d4f2e…` |
| 06 | `poseidon_neon_tidelands` | Neon Tidelands | Land | `01a0f948-7651-7529-ac11-d53e7a49460c` | — | `01a0f94e-ee53-77e0-9da2-086b25dfcc8e` | `c4fb6c4f4ce831c5…` | `f97602d629e0…` |
| 06 | `poseidon_palace_of_tides_approach` | Palace of Tides Approach | Land | `01a0f948-b64c-778c-98eb-5185b88f638a` | — | `01a0f94f-b5bb-756e-afec-0dc15aec7c69` | `f5ab179b30a0110c…` | `8409029c516a…` |
| 06 | `poseidon_tutor_land_1` | Mole-Tide Channel | Land | `01a0f94a-2888-7727-9ec0-2b72b7a8b98a` | — | `01a0f954-4e31-7523-8ef4-23bc85b7f740` | `de63d4ea3a010678…` | not in repo |
| 06 | `poseidon_tutor_land_2` | Vanguard Reef | Land | `01a0f94a-3d7e-7345-a4a8-5253fcc76fb8` | — | `01a0f954-76f5-77f6-b6b0-f0b24793313d` | `0bfcbc96678404ce…` | not in repo |
| 06 | `poseidon_tutor_land_3` | Leviathan Mooring Shelf | Land | `01a0f94a-52ab-7523-a4a4-d05d1e8f244e` | — | `01a0f954-d9ae-76b5-8b8a-464916b4be7d` | `320d0664ac150a0b…` | not in repo |
| 06 | `poseidon_tutor_land_4` | Sunken Muster Basin | Land | `01a0f94a-67f1-7061-8f5d-e6e66c7b1229` | — | `01a0f955-02b6-7295-bbbc-2984239df0e2` | `5ad9d3053670cb54…` | not in repo |
| 06 | `poseidon_tutor_land_5` | Worldsea Anchorage | Land | `01a0f94a-c9e6-7140-befc-266aeda4571a` | — | `01a0f955-2ba4-7689-bb0f-3e45c96f13cb` | `dd39347f37f1d6c3…` | not in repo |
| 06 | `zeus_eagles_perch_array` | Eagle's Perch Array | Land | `01a0f947-edf6-7720-b1ae-7d85ed329186` | — | `01a0f94e-a4d9-71f9-9c02-9f5e6cc2e943` | `1fc037d18d12735b…` | `83db43096b8c…` |
| 06 | `zeus_ionized_skyway` | Ionized Skyway | Land | `01a0f947-d7d9-744a-94ae-beb4c773a66e` | — | `01a0f94e-2bf1-764a-8828-3e95d4f575c2` | `9f940860a7645bb8…` | `1ca108a3ae08…` |
| 06 | `zeus_land_aurora_reach` | Aurora Reach | Land | `01a0f949-9c56-74c7-9acb-708924b4fd9b` | — | `01a0f951-1574-7398-a344-e99d5f8607a5` | `f571e2f0b0d2ccb4…` | `7c9ab95def85…` |
| 06 | `zeus_land_dawncloud_step` | Dawncloud Step | Land | `01a0f949-3054-76d4-80f4-709bcaff09bc` | — | `01a0f94f-ff3a-7157-9cbf-968b6e8c9820` | `4ec024e443a67de2…` | `97ee2e2a08a8…` |
| 06 | `zeus_land_empyrean_current` | Empyrean Current | Land | `01a0f949-463c-75a7-aadf-5a69973398c5` | — | `01a0f950-2413-7667-ad68-cf2e61b46eb6` | `6562a971e1e92ce3…` | `43c1a31ca6c7…` |
| 06 | `zeus_land_thunderstep_plateau` | Thunderstep Plateau | Land | `01a0f949-8676-745c-be2b-1977a1c136c2` | — | `01a0f950-f0c1-71dd-8db7-3ac32ba69874` | `51018e1b7ebaf60b…` | `9f5bf991506c…` |
| 06 | `zeus_olympian_cloudbank` | Olympian Cloudbank | Land | `01a0f947-c1a1-74e5-a5bc-514ea4514f20` | — | `01a0f94e-0748-7688-9a71-1060f80fd144` | `e7d75f69f5909579…` | `521e89da61f3…` |
| 06 | `zeus_throneward_conduit` | Throneward Conduit | Land | `01a0f948-5fb7-739f-be3a-92f7f3049c66` | — | `01a0f94e-c98a-7584-8750-a71d4437e206` | `4e6683556963052c…` | `41252fd6b7c6…` |
| 06 | `zeus_tutor_land_1` | Stormwright's Approach | Land | `01a0f94a-e022-72fe-9caf-9b8860e2828b` | — | `01a0f955-54b6-7347-9d28-21e8618ef8ee` | `5fb7dead771b32b8…` | not in repo |
| 06 | `zeus_tutor_land_2` | Blinkway Plateau | Land | `01a0f94a-f5be-77e5-b7a8-b1de9bc74e8c` | — | `01a0f955-b814-71d8-a1b0-441836974e48` | `d9557793ca45c980…` | not in repo |
| 06 | `zeus_tutor_land_3` | Far-Sight Cloudbank | Land | `01a0f94b-0b86-75c1-8b7d-9d3f6836556b` | — | `01a0f955-e101-7595-b10e-9675f1bdb7ca` | `f42fc35b3bc6ff17…` | not in repo |
| 06 | `zeus_tutor_land_4` | Keraunic Assembly Field | Land | `01a0f94b-20dd-73a4-8270-02ee8b483de8` | — | `01a0f956-09ca-7720-9f25-c09709ae7fc3` | `1991b4bc04b9249d…` | not in repo |
| 06 | `zeus_tutor_land_5` | Olympian Muster Sky | Land | `01a0f94b-3658-77bc-951d-99a50ea7e1d6` | — | `01a0f956-32aa-7251-bb41-f4a5556dc132` | `9e963935a4aca7e3…` | not in repo |
| 07 | `poseidon_abyssal_molecrab` | Abyssal Molecrab | Character | `01a0f966-0903-7292-bf4e-10896a312be2` | — | `01a0fa6b-4b70…` (prefix) | `3d549302195bb50d…` | `e67df07d0766…` |
| 07 | `poseidon_delphic_sonar_adept` | Delphic Sonar Adept | Character | `01a0f965-7a10-7226-85cc-9a52b3c9110d` | — | `01a0fa6d-6be8…` (prefix) | `ed8e78c39173cdef…` | `559c4e29bd12…` |
| 07 | `poseidon_fast_razorfin_lancer` | Razorfin Lancer | Character | `01a0f966-60b9-7509-a611-d02d758dd8a6` | — | `01a0fa67-c6f5…` (prefix) | `a8b562308a878a94…` | `e30633dea4f9…` |
| 07 | `poseidon_keyword_breakwater_hoplite` | Breakwater Hoplite | Character | `01a0f962-b2b8-70e5-ac2d-fb2ce6f4a802` | — | `01a0f963-96ae-72b0-a692-b044b7bba338` | `abcc3ad156d8f9bc…` | `9778c0adc5df…` |
| 07 | `poseidon_keyword_reef_tunneler` | Reef Tunneler | Character | `01a0f966-4374-75bd-bc62-dc9e6e0f23a5` | — | `01a0fa69-9ef7…` (prefix) | `8e8548ef51a92ad2…` | `70010d6bae2c…` |
| 07 | `poseidon_kraken_tendril_drone` | Kraken Tendril Drone | Character | `01a0f965-9730-701b-8de7-8c51f3896816` | — | `01a0fa6c-b6ba…` (prefix) | `0c21930a573c036a…` | `1c4d1b366387…` |
| 07 | `poseidon_naiad_flowshaper` | Naiad Flowshaper | Character | `01a0f965-ebbb-704c-8779-974fc3c14838` | — | `01a0fa6c-00c2…` (prefix) | `c7357bce0023a1c6…` | `366685d83953…` |
| 07 | `poseidon_nereid_current_rider` | Nereid Current-Rider | Character | `01a0f965-225c-772d-b62c-72b503364eed` | — | `01a0fa6f-947a…` (prefix) | `8c3a5b920b26ee79…` | `0349c92d4ce6…` |
| 07 | `poseidon_oceanid_pressure_mage` | Oceanid Pressure Mage | Character | `01a0f966-261d-7247-bfa5-23a3c917fdd7` | — | `01a0fa6a-7f9c…` (prefix) | `2165f3ab1d780b94…` | `2bbc2c8bcec4…` |
| 07 | `poseidon_reefline_defender` | Reefline Defender | Character | `01a0f965-3f99-710d-84f3-79881f02d966` | — | `01a0fa6e-dbe8…` (prefix) | `e9694ee96e454a23…` | `66fcbba8b839…` |
| 07 | `poseidon_tidepool_surveyor` | Tidepool Surveyor | Character | `01a0f965-051a-7792-890b-d81a677de3bd` | — | `01a0fa70-3dce…` (prefix) | `9a72c54d633ff14d…` | `813a5da7bdf6…` |
| 07 | `poseidon_triton_waveguard` | Triton Waveguard | Character | `01a0f964-b1f6-70e2-8579-ad0bcb0aeeff` | — | `01a0fa71-3787…` (prefix) | `fc356a52d2f561b7…` | `efb523fdd0a4…` |
| 07 | `poseidon_undertow_stalker` | Undertow Stalker | Character | `01a0f965-5cb9-713e-91b8-4976c098c287` | — | `01a0fa6e-26fc…` (prefix) | `189cf8ff4e6eba2d…` | `856a114f214a…` |
