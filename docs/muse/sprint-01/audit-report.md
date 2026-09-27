# Sprint IC-2026-09-27-DAY-01 — AI-027 Content Audit Report

**Date:** 2026-09-27 · **Checkpoint:** 18:00 America/New_York
**Scope:** read-only inspection of the current alpha and the original; asset manifest for 3D migration.
No source repository was modified. No media generated. No credentials used. No deployment performed.

## 1. Pinned source commits (verified by `git rev-parse HEAD` on fresh clones)

| Repo | Branch inspected | Pinned SHA | Commit date / subject |
|---|---|---|---|
| TubaExperiment (alpha) | `strip/zeus-poseidon-desktop` (remote default; `origin/HEAD` points here) | `992bc95c7164416ea0a25a4ce120f6ec0a0a167a` | 2026-09-26 — "0.7.15: version bump" |
| Desolate-Tuba (original) | `main` (default) | `dde98f8c71ec80ba9046271d1c84e160735c8fcb` | 2026-09-23 — "Rebuild six faction starters with reliable curves and safe deck reset" |

- TubaExperiment remote also has `main` at `0d59e55c0547ed0084e81a5d0623abbcc21e4c77` (not inspected; the strip branch is the live alpha line). Tags `v0.7.2-alpha`…`v0.7.15-alpha` exist; `GameVersion.VERSION = "0.7.15"` (`game-gui/src/main/java/com/infiniteconquest/gui/GameVersion.java:9`).
- Desolate-Tuba remote has dozens of `codex/*` branches (earlier AI-assisted prototype era) plus `feature/capital-income-board-fit`. Default-branch `main` was used as "the original" per the brief; codex branches were not individually surveyed (see §6 unresolved issues).
- Both clones verified clean (`git status --porcelain` empty) before and after inspection.

## 2. Alpha runtime card / Capital inventory (TubaExperiment @ `992bc95`)

### 2.1 How cards load at runtime
- `CardCatalog` (`game-core/.../data/CardCatalog.java`) loads one JSON catalog (schema v1), enforcing unique snake_case IDs per file.
- `PrototypeCardPool` (`game-cli/.../cli/PrototypeCardPool.java:14-23`) merges **10** catalogs: `prototype-characters, development-cards, faction-cards, faction-spells, faction-apex-cards, faction-keyword-cards, faction-development-expansion, faction-development-expansion-2, tactical-keyword-cards, faction-ability-cards`.
- `CapitalRoster` (`game-cli/.../cli/CapitalRoster.java:14`) separately loads `faction-capitals.json` and requires every entry to be `CardType.CAPITAL` and ≥3 Capitals per faction.
- The GUI (`game-gui/.../gui/GameContext.java`) consumes `PrototypeCardPool` + `CapitalRoster` + `FactionDecks` — this is the runtime path audited.
- `CardType` enum (`game-core/.../core/CardType.java`): `CHARACTER, LAND, STRUCTURE, SPELL, CAPITAL`.

### 2.2 JSON inventory — 143 cards, 143 unique IDs, zero duplicates (all files, verified by script)

| File | Cards | Factions |
|---|---|---|
| `faction-cards.json` | 45 | ZEUS 22, POSEIDON 23 |
| `faction-spells.json` | 10 | ZEUS 5, POSEIDON 5 |
| `faction-apex-cards.json` | 20 | ZEUS 10, POSEIDON 10 |
| `faction-keyword-cards.json` | 10 | ZEUS 5, POSEIDON 5 |
| `faction-ability-cards.json` | 6 | ZEUS 3, POSEIDON 3 |
| `faction-development-expansion.json` | 8 | ZEUS 4, POSEIDON 4 |
| `faction-development-expansion-2.json` | 8 | ZEUS 4, POSEIDON 4 |
| `tactical-keyword-cards.json` | 6 | ZEUS 3, POSEIDON 3 |
| `development-cards.json` | 19 | DEMO 19 (6 LAND, 5 STRUCTURE, 8 CHARACTER) |
| `prototype-characters.json` | 5 | UNASSIGNED 5 (CHARACTER) |
| `faction-capitals.json` | 6 | ZEUS 3, POSEIDON 3 (all type CAPITAL) |

### 2.3 Generated tutor IDs
`FactionTutorExpansion` (`game-cli/.../cli/FactionTutorExpansion.java`) **generates 20 cards at runtime** — 5 Lands + 5 Structures per faction in `FactionDecks.FACTIONS` (sorted). ID scheme (line 44-45):

```
<faction-lower>_tutor_<type-lower>_<1..5>
e.g. zeus_tutor_land_1 … zeus_tutor_land_5, zeus_tutor_structure_1 … 5,
     poseidon_tutor_land_1 … 5, poseidon_tutor_structure_1 … 5
```

- Lands activate to draw Structures; Structures activate to draw Characters (2 GP per activation). Cost curve 2/4/6/8/10; HP Land 8→22, Structure 10→26; tiers unlock on personal turns 2/4/6/8/10 (per `docs/faction-tutor-expansion.md`).
- **Known coupling:** `game-cli/src/main/resources/cards/faction-starters.json` references `zeus_tutor_land_1`, `zeus_tutor_structure_1`, `poseidon_tutor_land_1`, `poseidon_tutor_structure_1` — IDs that exist **only at runtime** (confirmed in file; also flagged at `review/FINDINGS.md:119`). The generator must be kept or tutors moved into JSON.
- The alpha's `LAND_NAMES`/`STRUCTURE_NAMES` maps contain **only ZEUS and POSEIDON** entries; adding a faction to `FACTIONS` without names would throw NPE.

### 2.4 Capitals
6 Capital entities (board entities, not deck cards): `zeus_capital_olympus_citadel`, `zeus_capital_keraunos_spire`, `zeus_capital_cloud_throne`, `poseidon_capital_atlantis_nexus`, `poseidon_capital_trident_bastion`, `poseidon_capital_abyssal_court`. Each faction has exactly 3 (the enforced minimum). `CapitalPassive` enum has 6 entries (3 per faction). All 6 have bespoke art.

### 2.5 Runtime totals
**143 JSON cards + 20 generated tutors + 6 Capitals (already counted in the 143) = 163 runtime entities.**
`FactionDecks.FACTIONS = {"ZEUS", "POSEIDON"}` (`FactionDecks.java:11-12`) — **the alpha is verified Zeus/Poseidon-only.** (The `PRIMARY/SECONDARY_TYPES` and `PRIMARY/SECONDARY_KEYWORDS` maps still carry Hades/Ares/Athena/Hephaestus design entries — placeholders, not loadable factions.)

### 2.6 Alpha-vs-original card delta
- **138 card IDs shared** with the original; **228 original IDs absent** from the alpha (= exactly the four future factions, §4).
- **5 alpha-only cards** (new since the strip-down): `zeus_ion_storm_lattice`, `zeus_zephyr_mooring_mast`, `poseidon_driftwood_breakwater`, `poseidon_drowned_archive`, `poseidon_moonwell_tidegate` — each with new bespoke art (see §3).

## 3. Art / audio / animation evidence (alpha)

- **Card art:** `CardArtFactory` (`game-gui/.../gui/CardArtFactory.java:96-114`) resolves `/art/<type-folder>/<card.id>.jpg` (folders: `capitals, characters, spells, lands, structures`); missing files fall back to a **deterministic procedural renderer**. Inventory: 142 files (`capitals` 6, `characters` 48, `lands` 35, `structures` 34, `spells` 16, `coins` 2, `faction-environments.png` 1).
- **Coverage:** all 119 real-faction JSON cards + all 20 tutors have bespoke art. The 24 cards without art are exactly the 19 DEMO + 5 UNASSIGNED cards (procedural fallback). **Zero art gaps for playable Zeus/Poseidon content.**
- **Audio:** 21 WAV cues in `game-gui/src/main/resources/audio/`, all CC0 Kenney-derived with full source attribution (`ATTRIBUTION.md`). `SoundEffects.Cue` enum (`SoundEffects.java:11-14`): MOVE, DEPLOY, MELEE, RANGED, SPELL, DAMAGE, PENALTY, DESTROY, VICTORY, DEFEAT, CLICK, HOVER, YOUR_TURN, ENEMY_TURN, KEEP, SHUFFLE, COIN_GAIN, COIN_SPEND, REACTION, NOTIFY, CAPITAL_HIT — generic gameplay events, not per-card.
- **Animation:** `Fx.java` timings — deploy 300ms, move 320ms, melee 360ms, destroy 320ms, damage-float 1200ms, hit-flash 350ms, shake 450ms, land-pop 120ms, play-flight 400ms, turn-banner 1100ms. Particle sprites from the Kenney particle pack (`vfx/kenney-particle-pack/`, third-party).

## 4. Faction comparison: original vs alpha (Hades / Ares / Athena / Hephaestus)

The original (`main` @ `dde98f8c`) holds **366 cards / 366 unique IDs**, all `contentStatus: PROTOTYPE`, across **all six factions** (`FactionDecks.FACTIONS` in the original = ZEUS, POSEIDON, HADES, ARES, ATHENA, HEPHAESTUS). **None of the four future factions exists in the alpha** — no JSON rows, no FACTIONS entry, no tutor names, no starter decks (only art for their 12 Capitals survives in the original).

| Faction | In original (57 cards each) | In alpha | Recoverable source IDs | Adaptation needed for current alpha rules |
|---|---|---|---|---|
| **HADES** | 24 CHAR, 10 LAND, 10 STRUCT, 10 SPELL, 3 CAPITAL (`house_of_hades`, `styx_gate`, `tartarus_vault`) | none | `game-core/src/main/resources/cards/*.json` @ `dde98f8c` (IDs listed in manifest, `expansion_gap=true`); tutor names @ `game-cli/.../FactionTutorExpansion.java:13,21` | Re-add 57 JSON rows; add `HADES` to `FactionDecks.FACTIONS`; re-add 10 tutor names; add starter entries; capital art recoverable (3 JPGs in original `art/capitals/`) |
| **ARES** | 26 CHAR, 10 LAND, 10 STRUCT, 8 SPELL, 3 CAPITAL (`iron_war_camp`, `red_citadel`, `spearpoint_keep`) | none | same pattern @ `dde98f8c`; tutor names @ lines 14,22 | same as above |
| **ATHENA** | 26 CHAR, 12 LAND, 10 STRUCT, 6 SPELL, 3 CAPITAL (`acropolis_command`, `aegis_archive`, `owlwatch_fortress`) | none | same pattern @ `dde98f8c`; tutor names @ lines 15,23 | same as above |
| **HEPHAESTUS** | 22 CHAR, 14 LAND, 12 STRUCT, 6 SPELL, 3 CAPITAL (`bronze_heart`, `great_forge`, `volcanic_foundry`) | none | same pattern @ `dde98f8c`; tutor names @ lines 16,24 | same as above |

**Why adaptation is small (evidence-backed):**
- The engine is faction-agnostic at the keyword *name* level: every keyword used by future-faction cards (VANGUARD, SIEGE, MOLE, FAST_STRIKE, BLINK, SHARP_SHOT, ARCHIVE, WORKSHOP, TURRET, BULWARK, BEACON, WAYSTATION, HIGH_GROUND, FERTILE, COVER) already exists in the alpha's `Keyword` enum (`game-core/src/main/java/com/infiniteconquest/data/Keyword.java:4-11`, re-verified 2026-09-27). **Hypothesis — untested:** "no engine changes required." Enum presence alone does not prove behavioral compatibility: keyword *implementations*, `CapitalPassive` extensions, tutor name maps, card-ability wiring, and ally-deck mixing paths are all unverified. See the compatibility risk matrix (§4.5) for what must be tested before any faction expansion.
- `FactionDecks` PRIMARY/SECONDARY_TYPES and KEYWORDS maps already contain all six factions' design entries (Hades SPELL/CHARACTER MOLE/FAST_STRIKE; Ares CHARACTER/SPELL FAST_STRIKE/SIEGE; Athena CHARACTER/STRUCTURE VANGUARD/SHARP_SHOT; Hephaestus STRUCTURE/LAND VANGUARD/SIEGE) — only the `FACTIONS` gate needs the new entries.
- `CardArtFactory` was trimmed to two factions but degrades gracefully (procedural fallback); only the 12 Capital JPGs exist as recoverable art in the original — all other future-faction art is a gap.
- Open item from `review/FINDINGS.md:209`: the ally-deck decision (keep Zeus↔Poseidon-style ally mixing vs pure 1v1) must be re-decided per added faction, and tutor ally-draw paths need balance re-testing.

### 4.5 Compatibility risk matrix — hypotheses, not findings (verify before implementing)

Each row is an *untested hypothesis* about re-adding a faction. Evidence cites the pinned alpha (`992bc95`); nothing below has been executed. Code paths re-verified 2026-09-27 against the clean clone.

| # | Area | What the code shows (alpha @ `992bc95`) | Risk if wrong | Explicit test needed |
|---|---|---|---|---|
| R1 | Keyword behavior | All 15 future-faction keywords present in `Keyword` enum (`game-core/src/main/java/com/infiniteconquest/data/Keyword.java:4-11`) | LOW–MEDIUM | Keyword-driven behavior tests per keyword; grep keyword implementations for faction-switched branches |
| R2 | Faction gate | `FactionDecks.FACTIONS = {"ZEUS","POSEIDON"}` (`game-cli/src/main/java/com/infiniteconquest/cli/FactionDecks.java:11-12`); unknown faction → `IllegalArgumentException` (line 54) | LOW | Add HADES to `FACTIONS`, run card-set/deck-builder suite; confirm no second gate exists |
| R3 | Tutor name maps | `LAND_NAMES`/`STRUCTURE_NAMES` hold only ZEUS/POSEIDON (`FactionTutorExpansion.java:10-24`); `LAND_NAMES.get(faction)` NPEs for a missing faction | MEDIUM | Null-safety test: adding a faction without names must fail loudly, or make the maps total |
| R4 | CapitalPassive | Enum at `game-core/src/main/java/com/infiniteconquest/core/CapitalPassive.java` (6 entries, 3/faction per §2.4) | MEDIUM | 12 new passives (3 × 4 future capitals) must be implemented + unit-tested |
| R5 | Starter-deck coupling | `faction-starters.json` references runtime-only tutor IDs (§2.3) | MEDIUM | Integration test: starter decks build without the generator; decide JSON-ify tutors vs keep generator |
| R6 | Card ability wiring | Mechanism for the 228 future cards' abilities unverified (data-driven JSON vs code keyed by ID); shared 138 must behave identically | UNKNOWN (treat as HIGH) | Locate ability implementation mechanism; per-card behavior tests; behavioral diff of shared 138 alpha-vs-original |
| R7 | Art fallback | `CardArtFactory` procedural fallback for missing art (§3) | LOW | Visual smoke test of fallback rendering for new cards |
| R8 | Ally-deck mixing | Zeus↔Poseidon ally paths exist; decision open per `review/FINDINGS.md:209` | MEDIUM | Design decision first, then balance re-test of tutor ally-draw per faction |

Bottom line: "no engine changes required" is downgraded from finding to hypothesis. Only the keyword *name* set is verified compatible by inspection; R3–R6 each need code + tests before any faction ships. No expansions were implemented in this audit.

## 5. Manifest

`manifest.csv` + `manifest.json`: **391 rows, 391 unique card IDs, zero duplicates**, keyed by stable card ID. Columns: `card_id, name, faction, type, source, art_path, art_status, needed_3d, animation_events, sound_cues, provenance, status, expansion_gap, notes`.

- 143 `alpha-json` rows (complete Zeus/Poseidon coverage, DEMO/UNASSIGNED included and labeled)
- 20 `alpha-generated` tutor rows (full ID scheme + names parsed from source)
- 228 `original-json` rows with `expansion_gap=true` — recoverable source IDs + file paths + original SHA; art marked `recoverable-from-original` only where the 12 Capital JPGs exist, otherwise `missing — faction not in alpha`
- `needed_3d`, `animation_events`, `sound_cues` are **type-derived from code evidence** (Fx.java timings, SoundEffects cues, CardType), not invented per-card content
- Every row carries `provenance` = exact file path + commit SHA. No counts invented; all counts produced by script from the clones.

## 6. Unresolved issues / limits of this audit

1. **3DTuba push — resolved 2026-09-27.** Initial state: the repo existed but was empty (GitHub API id 1391108087, size 0, no refs; created 2026-09-27), so the sprint fallback (chat artifacts) was used first. After the user provided a token, all five deliverables were pushed via the GitHub Contents API to branch `muse/sprint-01-content-audit` (head `cec6799`), under `docs/muse/sprint-01/`, and verified via API branch + contents listing. No `main` ref exists remotely yet; no Unity Assets/Packages/ProjectSettings/root files were touched.
2. **Tests not executed** — 72 `*Test.java` files counted (see handoff); the Gradle suite was not run in this read-only time-boxed audit.
3. **Desolate-Tuba `codex/*` branches not surveyed** — default-branch `main` treated as "the original" per the brief; richer content could exist on feature branches (only `main` was diffed).
4. **Meshy / ElevenLabs access** — not verified (out of audit scope); manifest `needed_3d` column is type-level pending art direction.
5. **Per-card 3D specs** — intentionally type-level; bespoke model/effect design belongs to Astra's integration pass.
