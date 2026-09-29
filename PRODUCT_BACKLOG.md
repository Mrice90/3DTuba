# Infinite Conquest — product backlog

**Version 2 — 2026-09-28 18:30 EDT.** Maintained by Claude (covering Astra's lane until Astra returns on 2026-10-04). This is the single shared backlog for every worker. A copy is kept in `C:\Users\mattj\OneDrive\Documents\AI Dev\Infinite Conquest\PRODUCT_BACKLOG.md`, and this repository copy wins if the two ever differ. Evidence and run logs go in `SPRINT_LOG.md`, not here.

## How to use this file
- **Pick work** from the Sprint board below: take the highest-priority item in your lane whose status is READY or IN_PROGRESS and is not blocked.
- **When you start** an item, set it to IN_PROGRESS with the date. **When you deliver**, set it to DELIVERED and put the commit, CI run ID and exact commands in `SPRINT_LOG.md`. Only a different worker moves an item to ACCEPTED or DONE (no self-acceptance).
- **Statuses:** BACKLOG → READY → IN_PROGRESS → DELIVERED → ACCEPTED/DONE. Anything that can't move gets BLOCKED plus the reason. WAITING means it waits on a named person or gate.
- **Priorities:** P0 blocks the playable slice or security. P1 is next-up. P2 is quality. P3 is polish.
- **IDs are never reused.** Split big items into child IDs (AI-0xx-SUFFIX) and keep the parent ID.
- **Before any write to this file or SPRINT_LOG.md**, fetch the current head and change only your own rows or sections. Never upload a whole-file copy taken earlier. (Added 2026-09-28 after commit `e5bcb77` was written from a stale copy and dropped the product vision, AI-060 and AI-061.)
- **Boundaries:** all changes go to 3DTuba. TubaExperiment (rules reference) and Desolate-Tuba (historical content) are read and copy only, and must never be pushed to, merged into or edited. There are no purchases, top-ups or plan upgrades without the Product Owner. Existing Meshy/ElevenLabs credits may be spent on briefed work (PO decision 2026-09-28 10:05). Record provider IDs and cost, and never resubmit an uncertain job.

## Product vision (Product Owner direction, 2026-09-28 evening)
The end goal is a full 3D board environment that is built as tokens are played. Tokens are animated and every one has unique sound effects. The bar is polish above typical AAA, able to compete with Hearthstone and other top strategy games. What this means for every item:
- **The board is built by play.** Placing a land, structure or capital raises that part of the battlefield in 3D (ground placement → rising structure → complete). Each match ends as a unique diorama.
- **Every token is alive:** deploy, idle, move, attack, hit and destroy animations, each with its own sound. Apex (rarity 4) cards get signature presentations.
- **Hearthstone-class feel:** tactile interactions, strong impact feedback (camera shake, particles, hit-stop), readable at a glance, 60 fps on target devices, and no placeholder art in a release build.
- **Continuous media production:** Meshy and ElevenLabs always have a batch in flight or in review, using existing credits (PO authorized credit use, 2026-09-28 18:43 EDT; no purchases). The Product Owner reviews each batch in-game, and that review steers the next batch rather than blocking it.

### AI-060 — Build-as-you-play 3D battlefield (north-star epic)
P0 | BACKLOG | Owner: Claude (Astra lane) → Astra from 2026-10-04 | Dependencies: AI-003, AI-017, AI-052-ASSET.
Children: AI-060a, token placement drives the board-growth animation (deploy staging for lands, structures and capitals). AI-060b, a per-event animation controller (idle/move/attack/hit/destroy) driven by authoritative rule events. AI-060c, an impact-feedback kit (camera shake, particles, hit-stop, SFX triggers). AI-060d, a performance budget with profiling on desktop and mobile targets. Acceptance: a Zeus-vs-Poseidon match in Unity where every played permanent visibly builds the board, animates on each rule event with its own sound, and holds 60 fps on the reference PC.

### AI-061 — Continuous media production lane
P1 | IN_PROGRESS | Owner: Claude (operates Meshy + ElevenLabs) | Dependencies: AI-049 prompt directory.
Pipeline: generate → remesh/texture → download → `check_glb.py` → `glb_to_token_fbx.py` (Blender) → Unity `TokenPreview.Render` still → PO review. Order: batch-01 capitals (6), then Zeus/Poseidon units by rarity, then spells/effects. Record flow/job IDs, credits and review in `docs/production/ASSET_QUEUE.md`.
- 2026-09-28 19:00 EDT, batch 01 capitals DELIVERED: 6 Meshy models, remeshed to 30K tris, 2K PBR, 180 credits, staged in `assets/staging/meshy/batch-01-capitals` (SHA256SUMS.txt). SFX 6×4 takes in ElevenLabs flow `S5OTgpVd2nqHXp3u7pQd`.
- 2026-09-28 19:15 EDT, batch 02 apex units DELIVERED: 8 Meshy models (Skyfather Archon, Keraunos Seraph, Olympian Storm Titan, Aetherbolt Avatar, Atlantis Tide Sovereign, Kraken Prime Avatar, Abysswalker Nereid, Abyssal Leviathan), 240 credits, 2,720 left, staged in `assets/staging/meshy/batch-02-apex-units`. SFX 8×4 takes in flow `GvmEQ8CxxWdrwkB1JQbL`.
- 2026-09-28 19:09 EDT: the ElevenLabs lane moved to the Claude "ElevenLabs sound assets" thread. Meshy, Unity import and meetings stay with the Claude meetings thread.

### AI-062 — Board event contract v1 (child of AI-060b)
P1 | DELIVERED 2026-09-28 ~19:35 EDT (reconciled 2026-09-28 ~20:00 EDT; supersedes the earlier IN_PROGRESS note) | Owner: Muse (Rune) | Reviewer: Claude | Dependencies: none (reads the pinned Java rules, read-only).
Why: the 3D board, animations and sounds must be driven by authoritative rule events, but no event vocabulary exists. UnityProof only knows two hard-coded movement cases, and the rules live in TubaExperiment `game-core` (Java). A contract with golden transcripts lets presentation work start now, whatever AI-003 decides about how Unity gets the rules.
Work (all under `docs/muse/sprint-02/board-events/`): read TubaExperiment `992bc95` `game-core` (GameEngine, MovementRules, board/stack, combat, capital and ability code) and list every state change a player must see: card deployed onto a stack, land stacked, structure raised, capital placed, character moved (one event per step), attack declared, opportunity attack, damage dealt, card destroyed, ability activated, spell cast and resolved, capital hit, GP change, turn start/end, match end. Deliver:
- `board-events.md`: one row per event with its payload fields (event, seq, turn, player, card_id, instance_id, from/to `{x,y}`, stack_index, amount), the Java `file:line` at the pin that causes it, and the presentation hooks it drives (animation key from the AI-049 `animation_events`, SFX cue key).
- `board-events.schema.json` (JSON Schema 2020-12), one event per line (JSONL).
- `golden/zeus-vs-poseidon-short.jsonl`: a hand-derived transcript of a short scripted match (both capitals placed, 2 lands, 1 structure, 1 character that moves 2 steps and attacks, 1 destroy), using card IDs from `manifest.json`.
- `validate_board_events.py` and `test_validate_board_events.py` (standard library only): schema check plus invariants (seq strictly increasing, coordinates inside 4×6, moves only for CHARACTER instances, a destroyed instance emits nothing later, stack_index consistent per cell). Add both to `.github/workflows/verify.yml`.
Geometry: use HEX (`MatchRules.hex()`, odd-row offset adjacency per `BoardGeometry.java`), which is what the game plays, not the SQUARE test default.
Acceptance: verify.yml green on Linux and Windows with the new step; every event cites a Java source line at the pin; Claude checks the citations against the Java source. Out of scope: Unity or Java code changes.

### AI-063 — Board-scale contract for the asset prompt directory (child of AI-049/AI-017)
P1 | DELIVERED 2026-09-28 ~20:00 EDT (Rune reconciliation; implementation at `036acef`) | Owner: Muse (Rune) | Reviewer: Claude (meetings thread, as Meshy owner) | Dependencies: none.
Why: the alpha plays on a hex board: `MatchRules.hex()` / `BoardGeometry.HEX`, 24 hexes in 4 × 6 odd-row offset rows, six neighbours, hex distance (TubaExperiment `game-core/.../BoardGeometry.java`; the GUI starts every match with HEX). The prompts' hex wording is therefore correct. (Corrected 2026-09-28 19:35 EDT: the first version of this item wrongly called the board square, from the AI-036 fixture, which uses the engine's SQUARE test default.) What is missing is a size contract beyond characters (`check_glb.py` defaults to 1.8 units): Keraunos Spire came out about 2.5 tiles tall and hides its neighbours, and Abyssal Court overhangs its tile. The 35 lands have not been generated yet.
Work: add `docs/muse/sprint-02/board-scale.md` proposing a footprint and height budget per type, sized for one hex tile (starting point: LAND = one full hex tile, top surface ≤ 0.25 units, flat enough for a token to stand on; STRUCTURE ≤ 0.9 of the hex, ≤ 1.6 units; CHARACTER ≤ 0.8 of the hex, 1.8 units per `check_glb.py`; CAPITAL = one hex, ≤ 2.2 units; SPELL none). Keep the hex wording. Update `build_asset_directory.py` to add `board_footprint` and `height_budget` fields per card and to state the stack role in each prompt (lands must carry a unit on top). Regenerate the `.json` and `.md`. Add a test that every non-spell card has both fields, and run it in verify.yml.
Acceptance: regenerated directory plus a changed-card summary in SPRINT_LOG.md; CI green; the meetings thread confirms the scale numbers before the next Meshy batch uses them. Batches 01 and 02 are not regenerated because of this item; that is the Meshy lane's call.

### AI-064 — Per-card presentation manifest and coverage report (child of AI-060, AI-018–020)
P2 | REJECTED 2026-09-29 06:00 → fix in AI-069 (was DELIVERED 2026-09-28 ~23:15 EDT, commit `82cdd9b`; independently verified 2026-09-29 ~02:00 EDT, Linux-only) | Owner: Muse (Rune) | Reviewer: Claude | Dependencies: AI-062 event vocabulary (accepted — citation review verified cites at pin 992bc95, which unblocked this item).
Why: the vision needs every token to have its own animations and sounds, but 107 of 139 cards currently share group SFX, and the cue names (DEPLOY, DESTROY, CLICK, MELEE/RANGED…) differ from the cue set the ElevenLabs lane now produces (summon, move, attack, hit, death, ability, idle). Nothing shows what each card still lacks.
Work (under `docs/muse/sprint-02/presentation/`): `presentation-manifest.json` mapping every card to the AI-062 events it can emit, each with an animation clip key and a unique SFX key (`<card_id>_<cue>`), plus its model path. `coverage.py` (standard library) reads the manifest and a staging root and writes a Markdown table per card showing whether the model, textures, each animation and each SFX are present or missing. Test it against a small fixture tree and run it in verify.yml.
Acceptance: CI green; Claude runs `coverage.py` on Mathew's PC against `assets/staging/` and posts the first real coverage report; the ElevenLabs thread confirms the cue names match what it produces.
Delivered (2026-09-28 ~23:15 EDT, commit `82cdd9b`): `presentation-manifest.json` — 139 cards, 617 event mappings (48 CHARACTER / 35 LAND / 34 STRUCTURE / 16 SPELL / 6 CAPITAL); 54 events carry `animation: null` (visible gaps, by design); unique SFX keys `<card_id>_<cue>`; model/texture/animation/SFX file paths under `assets/staging/`-style layout. `test_coverage.py` 2/2 green, wired into `verify.yml`. Independent verification 2026-09-29 ~02:00 EDT: regenerated from pinned inputs → byte-identical JSON; fixture coverage run marks present/missing correctly; no Meshy/ElevenLabs submissions (those lanes are queued separately).

### AI-066 — Real-engine event dump tool (child of AI-062/AI-046)
P2 | DELIVERED 2026-09-28 ~23:45 EDT (commit `a121593`; CI fixes `7735895`, `863d62a`) | Owner: Muse (Rune) | Dependencies: AI-062 wire format, AI-046 release recipe.
Why: golden transcripts were hand-written; a real-engine dump proves the AI-062 wire format against actual matches and gives the Unity lane realistic event streams to consume.
Work (`releases/alpha-0.7.15-playable/tools/event-dump/`): `EventDump.java` runs headless seeded Zeus-vs-Poseidon AI matches against the pinned alpha and writes AI-062 wire-format JSONL (deterministic: same seed → byte-identical dump; `validate.py` passes on all dumped seeds). `run.sh` compiles against the release JAR. Wired into `linux-packaging.yml` after the build step.
Acceptance: CI green — Linux packaging run **36519850577** (all regress steps + the event-dump step success); local rerun by later reflection optional since CI covers it.

### AI-068 — Techno-futuristic myth style tune-up for asset prompts (child of AI-049)
P1 | DELIVERED 2026-09-28 ~23:10 EDT (commit `84de370`) | Owner: Muse (Rune) | Product direction via Claude 23:05 (Mathew): character design shifts to techno-futuristic myth — robotic plating, energy patterns, futuristic weapons only (no bows/crossbows).
Work: all 139 prompts retuned with a new STYLE_ANCHOR; CHARACTER (48), STRUCTURE (34), CAPITAL (6) framings rewritten in type-specific techno-myth language; LAND/SPELL keep framing under the new preamble. Hex wording and AI-063 board-scale budgets untouched. `test_asset_directory.py` gains `test_no_bows_or_arrows` and `test_techno_myth_style_tag` — 6/6 green, run in `verify.yml`. No Meshy generation submitted.

## Team and lanes (verified 2026-09-28)
| Worker | How reached | Lane | Availability |
|---|---|---|---|
| Mathew (Product Owner) | direct | Decisions (HA-xxx), style verdicts, anything needing a purchase | as available |
| Claude (covering Astra) | Claude project "ai dev", running on Mathew's PC | Meetings, backlog and sprint records, independent acceptance, Windows-local runs, Unity (`UnityProof/`), asset download/import, security review | through 2026-10-04 |
| Muse — Rune (and Thalia) | muse.ai main chat and reflections | `releases/`, `.github/workflows/`, `prototypes/lobby-lab/`, `docs/muse/` | available; hourly reflections |
| Meshy | meshy.ai web workspace in Chrome (signed in) | 3D models: generate, remesh, texture | existing credits only (3,140 on 2026-09-28) |
| ElevenLabs | Claude connector | Sound effects | existing credits only (130,801 on 2026-09-28) |
| Astra (ChatGPT Codex) | Codex automations | Integration and Unity lane owner | **unavailable until 2026-10-04 08:12 EDT** (usage limit) |

## Sprint board — sprint IC-S02 (2026-09-28 18:30 → 2026-09-30 18:00 EDT)
**Sprint goal:** close the supply-chain gap and make the packaging pipeline fully CI-verified on both OSes (AI-055, AI-048, AI-046). Then get the first textured 3D asset (Thunder Ram) running in the Unity proof scene so the style gate (HA-009) can open media production.

| ID | Pri | Owner | Status | Next action | Acceptance |
|---|---|---|---|---|---|
| AI-055 | P1 | Muse (Rune) | DELIVERED 2026-09-28 ~19:15 | Pins landed in `build-release.sh`/`.bat` (commit `1c53026`); `--break=depswap` added to `regress.sh`/`.bat` and the Windows CI. Windows packaging run **36496019781** green (clean + all 6 break modes). Claude verifies independently at the 00:00 checkpoint. | Windows and Linux packaging CI green. The new break mode is detected at stage `build`. The run ID is recorded. Claude verifies independently. |
| AI-048 | P1 | Muse | DELIVERED 2026-09-28 ~20:00 | `linux-packaging.yml` added (commit `de2b8e5`); CI run **36495894219** green: clean PASS + all 6 break modes detected on ubuntu-latest/Temurin 17. Claude to accept at the 00:00 checkpoint. | Green CI run ID covering the clean run and every break mode on Linux. |
| AI-046 | P1 | Muse → Claude accepts | REVIEW (blocked by AI-055) | After AI-055 lands, Claude runs AI-046-WIN-ACCEPT locally. | See AI-046-WIN-ACCEPT. |
| AI-046-WIN-ACCEPT | P1 | Claude | DELIVERED 2026-09-28 23:30 (partial) | Fresh clone 1fc5789 on Mathew's PC: fetch/build OK, smoke 5/5, clean regress PASS, jar SHA-256 f7d887e3…7489. All 6 `--break` runs fail in the harness itself (xcopy long-path + %RANDOM% work-dir collision), see AI-065. | `docs/reviews/2026-09-28-2330-AI-046-windows-acceptance.md` |
| AI-065 | P1 | Muse | DELIVERED 2026-09-28 ~19:40 EDT | regress.bat setup repaired (commit `c842a2c`): robocopy /E replaces xcopy (long-path aware; excludes build output); :mkwork uniqueness loop for the work dir; setup failures go through :fail so :finish cleans up. Claude re-runs all 6 break modes back-to-back from a deep path. | All 6 break modes report 'intentional break correctly detected' back-to-back from a deep path on a local Windows PC. Claude re-runs it. |
| AI-030 | P0 | Claude (Astra lane) | IN_PROGRESS | Clean-checkout build PASSED 06:00; runtime smoke PASSED 18:25 (legal=true, blocked=true, exit 0). Next: runtime smoke of that fresh build (`-proofSmoke`, legal=true, blocked=true), then click-through legal/blocked/reset. | smoke JSON + screenshot + exe SHA-256 in the review folder. Mouse acceptance is recorded separately (it may need Mathew). |
| AI-052-ASSET | P0 | Claude | IN_PROGRESS: Thunder Ram rendered in Unity 2026-09-28 18:40 (HA-009 sent); Leviathan and Abyss Gate still to download | Thunder Ram first: download the textured GLB from Meshy → `check_glb.py --require-materials` → import into UnityProof → place on a board tile → screenshot. Then Leviathan Wakeborn and Abyss Gate. | GLB check passes (or failures are documented and fixed via Blender/Meshy). The asset renders in the scene at board scale. The screenshot goes to Mathew as HA-009. |
| AI-056 | P2 | Muse | DELIVERED 2026-09-28 ~20:30 | Distinct exit codes per check (commit `f663f24`); regress asserts the expected code per break mode. CI runs 36496308472 (Linux) / 36496308464 (Windows) in flight. | Each break mode fails only its own check. The CI run ID is recorded. |
| AI-053 | P2 | Claude | WAITING on AI-052-ASSET | Select the Skyline Seer audio palette (AI-051 + AI-053 candidates), convert to 48 kHz WAV, normalize, map to events. | In-game audio review by Mathew. |
| AI-058 | P3 | Muse | DELIVERED 2026-09-28 ~19:40 | Version check reads `java.specification.version` (commit `c7f230c`); `linux-packaging.yml` runs the whole lane under `JAVA_TOOL_OPTIONS` — Linux run **36496007944** green. | Build passes with `JAVA_TOOL_OPTIONS` set. A regression check is added. |
| AI-057 | P3 | Muse | DELIVERED 2026-09-28 ~19:40 | README documents the `exit %EXITCODE%` behavior and remedies (commit `e16e303`). | README updated, or the wrapper is tested. |
| AI-031 | P0 | Claude (Astra lane) | IN_PROGRESS | Keep integration/security gates current. Review every new head at each checkpoint (AI-005/AI-006). | A checkpoint entry in SPRINT_LOG.md for every meeting. |
| AI-059 | P1 | Claude | DELIVERED 2026-09-28 18:45 | Both astra/* branches are already merged (0 stranded commits). Local-only docs/production (AI-054 checker 55/55, AI-049/AI-052 validators OK) is now committed. Raw 58 MB staging binaries are kept out. | `docs/reviews/2026-09-28-1845-AI-059-astra-branches.md`. Astra confirms branch deletion on return. |
| Meshy media (AI-061) | P1 | Claude (meetings thread) via Meshy | IN_PROGRESS | Batch 01 capitals DELIVERED 19:00 (6 models, 180 credits). Batch 02 apex units DELIVERED 19:15 (8 models, 240 credits, 2,720 left), staged in `assets/staging/meshy/batch-02-apex-units`. Next: batch-02 Unity renders and PO sheet, then the remaining Zeus/Poseidon units. Lands wait for AI-063. | PO in-game review per batch. |
| ElevenLabs media (AI-061) | P1 | Claude (ElevenLabs sound assets thread) | IN_PROGRESS | Capital SFX 6×4 takes (flow `S5OTgpVd2nqHXp3u7pQd`) and apex-unit SFX 8×4 takes (flow `GvmEQ8CxxWdrwkB1JQbL`). Next: stage and pick takes, full cue sets per unit. | PO listen-through. |
| AI-062 | P1 | Muse (Rune) | DELIVERED 2026-09-28 ~19:35 EDT | Board event contract v1 (commit `ca90b06`): 23 wire events adopted 1:1 from GameEvent.java with file:line citations at pin 992bc95; JSON schema; 16-event golden transcript; stdlib validator + test in verify.yml. Claude checks the citations. | CI green on both OSes; Claude checks the citations. |
| AI-063 | P1 | Muse (Rune) | DELIVERED 2026-09-28 ~20:00 EDT (Rune reconciliation; implementation at `036acef` 19:37 EDT) | Hex wording kept in all 139 prompts; per-type footprint + height budget for one hex tile and stack role added to every `meshy_prompt`; `board_footprint`/`height_budget` on all cards (123 non-spells non-null, 16 spells null); regenerated `.json`/`.md` via `build_asset_directory.py`; new `test_asset_directory.py` (4 tests) green and wired into verify.yml. CI green at tip `036acef` (verify.yml run 36498944450, success). Scale numbers still await meetings-thread confirmation before the land batch (Meshy lane's call). | CI green; the meetings thread confirms the scale before the land batch. |
| AI-064 | P2 | Muse (Rune) | REJECTED 2026-09-29 06:00 → fix in AI-069 (was DELIVERED 2026-09-28 ~23:15 EDT, commit `82cdd9b`) | `docs/muse/sprint-02/presentation/`: 139 cards, 617 event mappings; animation/SFX keys per card. The "2/2 green in verify.yml" claim was Linux-only — Windows verify red since `82cdd9b` (`UnicodeEncodeError`, cp1252). AI-069 fix DELIVERED 2026-09-29 ~08:15 EDT (`f7e1167`, `c540163`): explicit `encoding="utf-8"` on every `open()` + `newline="\n"` on the report write; Verify run **36565491824** green on ubuntu-latest + windows-latest. Claude to re-review AI-064. | Claude runs `coverage.py` on Mathew's PC against `assets/staging/` and posts the first real coverage report; ElevenLabs thread confirms cue names. |
| AI-066 | P2 | Muse (Rune) | DELIVERED 2026-09-28 ~23:45 EDT (commits `a121593`, `7735895`, `863d62a`) | Real-engine event dump (`tools/event-dump/`: seeded Zeus-vs-Poseidon AI matches → AI-062 wire-format JSONL, deterministic). Wired into `linux-packaging.yml`. | CI green — Linux packaging run **36519850577** (all regress steps + event-dump step success). |
| AI-068 | P1 | Muse (Rune) | DELIVERED 2026-09-28 ~23:10 EDT (commit `84de370`) | All 139 asset prompts retuned to techno-futuristic myth (Mathew direction via Claude); no-bow/no-crossbow tests; `test_asset_directory.py` 6/6 green in verify.yml. | No Meshy generation submitted (media lane's call). |

### Accepted or done (for reference; evidence in SPRINT_LOG.md)
AI-037/038 manifest + offline smoke DONE · AI-045 lobby guards merged (PR #1) · AI-047 Windows/browser lobby fixes merged (PR #1) · AI-049 asset prompt directory DONE (139 cards, 21 SFX groups, 32 apex briefs) · AI-052-WIN Windows packaging repair ACCEPTED (run 36386714758) · AI-054 GLB staging checker verified 55/55 (local, not yet committed) · AI-029/032/033/034 setup verified · AI-062 board event contract DELIVERED · AI-063 board-scale contract DELIVERED · AI-064 presentation manifest + coverage.py DELIVERED (independently re-verified 2026-09-29; then REJECTED 2026-09-29 06:00 — Windows verify red, fix in AI-069, re-fixed 2026-09-29 ~08:15 EDT, Verify run 36565491824 green both OSes) · AI-066 real-engine event dump DELIVERED (CI green 36519850577) · AI-068 techno-myth style tune-up DELIVERED.

### In review, not yet accepted
AI-027/028/036 audit, handoff and movement fixture · AI-043 Java suite (169/169 reported, independent rerun pending) · AI-044 2D alpha recipe (PARTIAL) · AI-050 Skyline Seer model (held for HA-011) · AI-051/052 audio candidates (awaiting palette selection).

## Human decisions (Product Owner)
| ID | Decision needed | Blocks |
|---|---|---|
| HA-009 | Review the first textured in-game sample (Thunder Ram) once Claude imports it. | All further Meshy/ElevenLabs batches |
| HA-011 | Skyline Seer: its cowl resembles a well-known comic character. Reject and regenerate (open helm/laurel), or accept? | AI-050 |
| HA-012 | ElevenLabs "Generations may be shared to Explore page" is ON. Turn it off for unreleased audio? | Asset confidentiality |
| HA-003 | Desktop/mobile targets, release order, cross-play | AI-013/014/015 final scope |
| HA-004 | Currency and login-reward calendar/eligibility | AI-021/022/023 |
| HA-006 | First expansion roster | AI-024 |
| HA-016 | Target architecture: C# rules core shared by the Unity client and the server; Java bridge as scaffold/oracle only (recommended). See "Long-term architecture goals". | AI-083, AI-087 |
| HA-017 | Game-server hosting provider, monthly budget ceiling, region(s). | AI-085 |

## Epics and requirements

## Product and commercial requirements

Deliver integrated desktop and mobile apps with working tactical matches, online matchmaking, persistent rankings and fully 3D battlefield tokens. TubaExperiment is the authoritative latest playtest and rules baseline. Desolate-Tuba is deliberately protected and read-only; it may contain richer card content for the other four factions. Recover selected historical content into 3DTuba, adapting it to current alpha rules; never overwrite alpha rules from the original or modify/push/merge into Desolate-Tuba. Treat integration as shared rules, identity, ownership and results across clients; exact cross-play scope and supported operating systems await confirmation. Maintain current turn-based gameplay unless the Product Owner requests a rules change.

Zeus and Poseidon are free permanently. Future sets contain four factions; target instant full-set unlock is $0.99 (USD planning assumption, currency/store price availability to verify). An alternative proposed reward permanently unlocks one chosen faction after about seven login days during the first month. Seven distinct versus consecutive days, first month of release versus account creation, claim deadline and late-player treatment remain undecided. Implement configurable rules only after these choices are settled. Preserve existing other-faction content as expansion candidates, not automatically approved pack membership.

Every released card must have distinct, approved art/model or spell presentation, animation and sound. Board permanents need actual 3D representations; spells need a distinct cast/impact presentation. Shared rigs and technical components may be reused, but generic placeholders do not satisfy final per-card acceptance. Meshy and ElevenLabs production starts only once access is verified. Unity/Blender availability does not itself choose the runtime architecture.

## Delivery order

1. Establish reproducible original/alpha baselines and automated quality checks; settle platform and engine approach.
2. Prove Zeus/Poseidon matches, online sessions and trustworthy persistent results; build one complete 3D sample through the chosen client path.
3. Complete desktop/mobile integration, all base-faction assets, performance and usability testing; release the free base game when its gates pass.
4. Deliver the first four-faction expansion, permanent entitlements, login reward and purchase flow with restoration and cross-device verification.

## Foundation and verification

### AI-002 — Reconcile original and alpha baselines
P0 | READY | Owner: Astra | Dependencies: none.
Pin the current TubaExperiment commit and inspect current rules, runtime card catalog, assets, branches, open PRs and Muse's current work. Read the original's card directory for the four other factions and produce a content recovery matrix with source IDs, missing content and alpha-rule adaptation needs. Do not change the original repository. Establish a controlled 3DTuba checkout with pinned alpha reference material and reproducible builds/tests; capture failures as new bug IDs. Acceptance: authoritative alpha rules/commit, current roster, historical content inventory, build/test evidence, service/deployment inventory and 3DTuba-only integration plan recorded. Historical claims remain separate from verified behavior. This initial document inspection is partial progress, not completion.

### AI-003 — Desktop/mobile and 3D architecture decision
P0 | READY | Owner: Astra | Dependencies: AI-002 findings; HA-003 before final platform commitment.
Direct Unity editor work is explicitly authorized by the Product Owner. Inspect installed Unity/Blender versions and available modules; use the editor directly where it improves results, without asking again for routine tool choice. Compare extending the Java application with a Unity client or a staged migration; assess rules reuse, network protocol, save/deck compatibility, mobile builds and maintainability. Acceptance: a small playable 3D proof with one legal action and matching rule outcome, a recorded architecture decision, migration risks and target-device performance budgets. Avoid a wholesale rewrite before the proof.

### AI-004 — Automated build and regression coverage
P0 | READY | Owner: Astra; Muse after verified coordination | Dependencies: AI-002 baseline for implementation.
Audit all workflows and implement coverage of the active 3DTuba integration branches, using alpha workflows as read-only references. Include Java rules/network tests, lobby-worker tests and reproducible client packaging; introduce Unity checks if selected. Acceptance: an intentional failing check is detected, a fixed commit passes, results/artifacts link to the exact commit, and required checks cover the actual integration path. Existing tests must be reproduced, not trusted from commit messages.

### AI-005 — Security baseline and recurring security checks
P0 | READY | Owner: Astra | Dependencies: AI-002 for full scope.
Map client, host, lobby, identity, ranking, purchases and update trust boundaries. Check dependencies, committed secrets, untrusted deck/save input, message sizes, authentication/authorization, rate limits, hidden-state redaction and bundled executables. Add applicable automated secret/dependency/static checks and adversarial tests. Acceptance: findings have severity, reproduction and remediation evidence; critical/high exploitable issues block release; exceptions require explicit documented disposition. No claim that automated scans prove all code safe.

### AI-006 — Ongoing bug triage and release regression gate
P0 | READY | Owner: Astra | Dependencies: none for triage; AI-004 for automated enforcement.
At every checkpoint review new changes, failed checks, crashes and confirmed defects. Assign stable IDs, severity, affected build and reproduction; repair the highest-impact executable defect and add a meaningful regression test. Acceptance: each fixed bug has before/after evidence; each candidate release passes rules, UI, network, persistence and clean-install smoke checks; no unresolved match-blocking or data-loss defects. Review unchanged evidence once, not repeatedly to consume tokens.

## Playable online base game

### AI-007 — Zeus/Poseidon rules and complete match acceptance
P0 | BACKLOG | Owner: Astra/Muse, assignment pending | Dependencies: AI-002, AI-004.
Verify all base cards, capitals, deck validation, mulligans, initiative, reactions, stacking, terrain, victory and rematch against the current TubaExperiment rules. Acceptance: full matches finish without deadlock; deterministic replays agree; malformed decks fail safely; every mechanic has meaningful coverage; existing deck codes/saves migrate without silent loss. Record outdated original documents as historical; do not use them to override alpha mechanics.

### AI-008 — Public lobbies and online matchmaking
P0 | BACKLOG | Owner: Astra/Muse, assignment pending | Dependencies: AI-002, AI-005, HA-005 for deployment.
Verify actual service status and configuration; repair existing lobby/quick-match functionality before replacing it. Test two independent clients across separate networks, joining, cancellation, stale lobbies, simultaneous requests, version mismatch and unavailable service. Acceptance: players find each other, enter one valid match and complete it; no duplicate pairing or indefinite wait; failure/retry messages are clear; deployed version and test evidence are recorded.

### AI-009 — Disconnect, reconnect and match lifecycle
P0 | BACKLOG | Owner: Astra | Dependencies: AI-007, AI-008.
Define fair timeout, abandonment, reconnect and forfeit behavior. Acceptance: disconnect before/during setup, reaction and game end cannot freeze either client or award duplicate results; recovery restores correct state; mobile background/resume is covered once supported. Record product-sensitive timeout choices before rollout.

### AI-010 — Trusted identity and ranked match integrity
P0 | BACKLOG | Owner: Astra | Dependencies: AI-005, AI-003.
Review local UUID identity, player-hosted authority and agreeing-client result reports. Define an authenticated, server-validated path appropriate for competitive rankings; do not treat agreement between two untrusted clients as proof. Acceptance: impersonation, forged/duplicate/replayed results and unauthorized faction use fail controlled tests; hidden opponent state is protected in the selected ranked architecture. Separate trusted ranked results from unverifiable sessions if needed.

### AI-011 — Leaderboards and persistent statistics
P0 | BACKLOG | Owner: Astra/Muse, assignment pending | Dependencies: AI-008, AI-009, AI-010.
Verify ratings, wins/losses and match records against a controlled set of known outcomes. Acceptance: each eligible match changes results exactly once; duplicates, disputes, forfeits and failed writes follow documented rules; values survive service restart and client reinstall/account restoration; leaderboard order, ties, pagination and refresh are correct; private identifiers are not exposed. Decide seasons/reset policy before adding it; no seasonal reset is implied by this request.

### AI-012 — Free faction access and fair base balance
P1 | BACKLOG | Owner: Astra | Dependencies: AI-007.
Grant Zeus and Poseidon permanently without payment or login campaign gates. Acceptance: new and returning players can build decks and play both factions; starter decks are viable; deterministic simulations plus human playtests assess first-player/capital advantage; balance changes are versioned and explained. Recheck free-versus-expansion matchups before each pack release.

## Desktop and mobile integration

### AI-013 — Desktop release experience
P1 | BACKLOG | Owner: Astra/Muse, assignment pending | Dependencies: AI-003, AI-007, AI-008.
Complete the selected desktop distribution's installation, startup, settings, deck builder, tutorial, battle UI, update and uninstall behavior. Acceptance: supported clean machines launch without developer tools, complete an online match and preserve settings/decks through an update; signing and release artifacts are verified for the chosen store/channel. Confirm current alpha capabilities before labeling screens missing.

### AI-014 — Mobile app and touch experience
P1 | BACKLOG | Owner: Astra | Dependencies: AI-003, HA-003.
Implement the approved mobile platform targets with touch selection, readable cards/board, safe areas, loading and lifecycle handling. Acceptance: representative real devices complete deck editing and full matches; no essential action depends on hover/right-click; pause/resume and poor network recover predictably; measured frame time, memory, thermals and download size meet the agreed budgets.

### AI-015 — Cross-device identity, progression and compatibility
P1 | BACKLOG | Owner: Astra | Dependencies: AI-010, AI-013, AI-014; AI-021 for purchases.
Link the same player's clients to consistent decks, ownership, stats and settings where appropriate. Acceptance: desktop-to-mobile matches use compatible rules/content if cross-play is confirmed; concurrent sessions, offline edits, reconnect and version upgrades resolve safely; account recovery and reinstall retain permanent ownership; incompatible versions receive an actionable message.

## 3D and per-card production

### AI-016 — Card asset inventory and production specification
P1 | READY | Owner: Astra; Muse for available visual work | Dependencies: AI-002 current catalog.
Generate one asset manifest entry per runtime card/capital ID, including generated tutors. Record faction/type, existing art, 3D model or spell effect needs, animation events, sound cues, source/license, export settings, job IDs and review status. Acceptance: every playable ID maps to a manifest row; missing/duplicate/orphan assets are detected; file naming, scale, orientation, materials, polygon/texture/audio budgets and style references are defined. No fixed historical card count is assumed.

### AI-017 — Complete 3D battlefield sample
P1 | BACKLOG | Owner: Astra/Muse; Meshy when verified | Dependencies: AI-003, AI-016.
Build a Zeus and Poseidon sample with real 3D characters, land/structure/capital presentation, camera, selection, movement, attack, damage and destruction. Acceptance: gameplay stays readable with stacked pieces; visual events match authoritative rules; input is usable on desktop and touch; measured performance meets AI-003 budgets. Product Owner style feedback precedes mass generation.

### AI-018 — Unique models and art for all released cards
P1 | BACKLOG | Owner: Astra integration; proposed Meshy production | Dependencies: AI-016, AI-017, HA-002 setup.
Produce batches starting with the free base factions, then each expansion. Use Blender for cleanup/rigging as needed. Acceptance: every required manifest entry has a distinct approved asset, correct scale/materials/orientation, clean import, optimized variants and provenance; final builds contain no unintended placeholders. Each batch is bounded and reviewed in-game before acceptance.

### AI-019 — Unique per-card animations and visual effects
P1 | BACKLOG | Owner: Astra integration; proposed Meshy/Muse production | Dependencies: AI-017, AI-018 per batch.
Map movement, idle, attack, cast, hit, death, deployment and special abilities as applicable to each card. Acceptance: each card has a distinct visual identity and all applicable events; effects resolve on the correct targets at the correct time, remain readable with multiple units, and do not delay/desynchronize gameplay. Spell and passive cards receive suitable effects rather than unnecessary humanoid rigs.

### AI-020 — Unique per-card sound effects
P1 | BACKLOG | Owner: Astra integration; proposed ElevenLabs production | Dependencies: AI-016, HA-002 setup.
Prepare bounded audio briefs for every applicable card action with purpose, tone, duration, format and loudness targets. Acceptance: each card has distinct approved cues; no clipping, missing files or runaway overlapping sounds; mute/volume controls work and persist; playback matches animation/game events on desktop and mobile; provenance and generation job status are recorded. Dialogue/music remain optional unless separately requested.

### AI-049 — Asset prompt directory with SFX grouping (child of AI-016/AI-020)
P1 | DONE 2026-09-27 ~22:15 EDT | Owner: Rune | Dependencies: AI-016 manifest, AI-032/033/034 (verified).
Per Product Owner direction 2026-09-27: build a per-card asset directory with Meshy generation prompts and ElevenLabs SFX briefs; group SFX by faction + archetype (fallback faction + type); unique SFX only for apex-tier (rarity 4). v1 delivered 2026-09-27 evening: 139 cards (119 alpha-json + 20 runtime tutors), 21 SFX groups, 32 unique-SFX briefs; generator script + JSON + Markdown in docs/muse/sprint-01/asset-prompts/. Pacing: 21:00 run validates (re-run build script, reconcile counts, 10-prompt spot-check); 22:00 run does final acceptance, marks DONE, then queues first Meshy/ElevenLabs batches. Acceptance: every playable ID has a Meshy prompt + SFX brief; counts reconcile; spot-check passes; worker-ready batching notes present. 22:00 final acceptance (Rune): 21:00 QA nits fixed (docstring scope arithmetic '119+20=139'; SFX-policy docstring '20 apex-file + 6 capitals + 6 other rarity-4'; type-aware prompt note labels, 91/91 fixed); regenerated from pinned inputs — 139 cards / 21 groups / 32 unique-SFX, counts reconcile, 192 Markdown headers; commits 2bf8c16, a073f91, 0ee5803; batches queued in batch-01-queue.md (c752ea5), not submitted.

## Four-faction expansions and ownership

### AI-021 — Durable faction entitlements and pack catalog
P1 | BACKLOG | Owner: Astra | Dependencies: AI-010, AI-012; HA-004 for policy details.
Represent four-faction sets, base grants, earned grants and paid grants separately with stable IDs and auditable server-side ownership. Acceptance: Zeus/Poseidon remain free; a earned selection never expires; duplicate grants are harmless; entitlements survive reinstall and linked-device use; unauthorized client-side unlocks fail; owning one faction does not break later full-pack purchase.

### AI-022 — First-month login reward
P1 | BACKLOG | Owner: Astra | Dependencies: AI-021, HA-004.
Implement the confirmed login-day campaign and permanent choice of one faction from its four-faction set. Acceptance: server-side day tracking resists clock manipulation and duplicate claims; missed days, timezone boundaries, month boundaries, last eligible day, interrupted claim and campaign expiry have tests; progress and eligibility are clear; claimed ownership persists after the campaign ends.

### AI-023 — $0.99 instant four-faction unlock
P1 | BACKLOG | Owner: Astra | Dependencies: AI-021, HA-003 store targets, HA-004 price/currency.
Implement approved store/payment integrations at the Product Owner's target price after verifying store support and current requirements. Acceptance: sandbox success, cancellation, pending, failure, duplicate receipt, refund/revocation policy and restore flows work; receipts are verified by a trusted service; all four entitlements grant once; localized price is displayed accurately; no real-money test purchase is made without authorization.

### AI-024 — First expansion roster and release package
P2 | BACKLOG | Owner: Astra/Muse; media workers after setup | Dependencies: AI-012, AI-018–AI-023; HA-006 roster.
Inventory the original repository's Hades/Ares/Athena/Hephaestus cards read-only, compare with the alpha catalog and recover richer/missing content into 3DTuba. Confirm first-pack membership before production; adapt costs, keywords, triggers and interactions to the authoritative alpha rules. Acceptance: each selected faction has a complete legal roster, distinct strategy, art/3D/animation/audio coverage, compatible deck codes, tested balance against the free factions, reward configuration and a verified release package. Source IDs and adaptation decisions remain traceable; the original repository stays unchanged. Repeat this template for later sets.

## Operational release readiness

### AI-025 — Service reliability, monitoring and recovery
P1 | BACKLOG | Owner: Astra | Dependencies: AI-008, AI-010, AI-011.
Define realistic load targets and test capacity, timeouts, rate limits and service/storage outages. Acceptance: alerts identify actual failures without exposing private data; result/ownership writes recover safely; backup/restore and rollback are demonstrated; service access and operating costs are recorded before launch. Never rely on unverified historical free-tier claims.

### AI-026 — Final integrated release validation
P1 | BACKLOG | Owner: Astra with Product Owner playtest input | Dependencies: applicable free-base items AI-003–AI-020 and AI-025; expansion commerce items before paid releases.
Run the release matrix on supported desktop/mobile devices, cross-client sessions and clean accounts. Acceptance: every released card meets its asset manifest, key user journeys and matchmaking/ranking pass, distribution/update/rollback are verified, and security/bug gates pass with recorded evidence. Review applicable store, privacy, age-rating and asset-use requirements against current primary sources before submission. Mark RELEASED only with actual destination/version evidence.

## Meeting cadence (2026-09-28 → 2026-10-04)
Astra's four Codex checkpoints (00:00/06:00/12:00/18:00 EDT) can't run until Astra's usage resets on 2026-10-04 08:12 EDT. Until then Claude runs the meetings from Mathew's PC:
- **09:00 EDT stand-up:** read the new heads and CI since the last meeting, check in with Rune in Muse, triage AI-005/AI-006, refresh the Sprint board, collect Mathew's decisions.
- **18:00 EDT review:** independent acceptance of delivered items, a sprint-log entry of record, the next assignments for every available worker.
- Rune's hourly lobby-lab reflections continue unchanged (see `docs/muse/sprint-01/reflection-schedule.md`).
- **2026-10-04 18:00 EDT:** the lane goes back to Astra, with the Sprint board as the handoff.

Each meeting reads this file plus the latest SPRINT_LOG.md entries, verifies claims against commits and CI, and ends with a concrete assignment for every available worker. Scheduled meetings do not imply continuous execution. Keep generation jobs resumable: record the provider job ID, input brief/version, expected outputs, state and review result before polling or retrying.

## Definition of done
An item is DONE only when its acceptance criteria are met, relevant tests/review pass, integration is verified, and the records include branch/commit/artifact evidence. A draft, generated asset, passing unit test or repository file alone is not a finished feature. RELEASED additionally requires a verified deployed/store/distribution destination. Scope or policy decisions are preserved in PRODUCT_LOG.md; unresolved dependencies stay visible in the Human decisions table.

## Child action catalog (IDs 027–068)
| ID | Parent | Summary | Owner | Status |
|---|---|---|---|---|
| AI-027/028 | AI-002 | Repository audit and handoff | Muse | REVIEW |
| AI-029 | AI-002 | 3DTuba clone + Unity/Blender inventory | Astra | DONE |
| AI-030 | AI-003 | UnityProof 3D movement slice | Astra → Claude | IN_PROGRESS |
| AI-031 | AI-004/005 | Integration and quality gates | Astra → Claude | IN_PROGRESS |
| AI-032/033/034 | — | Claude / Meshy / ElevenLabs setup | Mathew | VERIFIED |
| AI-035 | AI-002 | Audit documentation correction | Muse | DONE |
| AI-036 | AI-007 | Movement fixture (legal/blocked cases) | Muse | REVIEW |
| AI-037/038 | AI-016/004 | Manifest validator + offline lobby smoke | Muse | DONE |
| AI-039–042 | AI-008 | Local lobby implementation package | Muse | PARTIAL → folded into AI-045/047 |
| AI-043 | AI-007 | Java rules suite (169 tests) | Muse | REVIEW |
| AI-044 | AI-013 | 2D alpha build recipe | Muse | PARTIAL |
| AI-045 | AI-005 | Lobby loopback/Host/Origin guards | Muse | DONE (PR #1) |
| AI-046 | AI-013 | Reproducible Windows/Linux packaging handoff | Muse | REVIEW (AI-055) |
| AI-047 | AI-008 | Windows/browser lobby fixes | Astra | DONE (PR #1) |
| AI-048 | AI-004 | Packaging regression harness | Muse | DELIVERED |
| AI-049 | AI-016/020 | Asset prompt directory (-DIRECTORY Muse; -SKYLINE Claude brief validator) | Rune / Claude | DONE |
| AI-050 | AI-018 | Skyline Seer Meshy model | Meshy | REVIEW (HA-011) |
| AI-051 | AI-020 | Skyline Seer SFX (20 candidates) | ElevenLabs | REVIEW |
| AI-052-WIN | AI-046 | Windows .bat repair | Muse | ACCEPTED |
| AI-052-ASSET | AI-018 | Thunder Ram, Leviathan Wakeborn, Abyss Gate models + 13 SFX | Meshy / ElevenLabs → Claude import | READY for import |
| AI-053 | AI-020 | Skyline Seer melee cue | ElevenLabs | REVIEW |
| AI-054 | AI-018 | `check_glb.py` staging checker | Claude | DONE (committed with AI-059, 55/55) |
| AI-055 | AI-005 | Pin Jackson hashes (supply chain) | Muse | DELIVERED 2026-09-28 |
| AI-056 | AI-006 | Distinct exit code per regression check | Muse | DELIVERED 2026-09-28 |
| AI-057 | AI-006 | regress.bat manual-run exit | Muse | DELIVERED 2026-09-28 |
| AI-058 | AI-006 | Java version detection with JAVA_TOOL_OPTIONS | Muse | DELIVERED 2026-09-28 |
| AI-059 | AI-031 | Reconcile unmerged astra/* branches | Claude | DELIVERED |
| AI-060 | AI-017 | Build-as-you-play 3D battlefield (north star) | Claude → Astra | BACKLOG |
| AI-061 | AI-018/020 | Continuous media production lane | Claude | IN_PROGRESS |
| AI-062 | AI-060 | Board event contract v1 | Muse | DELIVERED 2026-09-28 |
| AI-063 | AI-049/017 | Board-scale contract for the asset prompts (hex tile) | Muse | DELIVERED 2026-09-28 |
| AI-064 | AI-060/018–020 | Presentation manifest + coverage report | Muse | DELIVERED 2026-09-28 |
| AI-065 | AI-006 | regress.bat setup repairs (robocopy/:mkwork) | Muse | DELIVERED 2026-09-28 |
| AI-066 | AI-062/046 | Real-engine event dump tool | Muse | DELIVERED 2026-09-28 |
| AI-068 | AI-049 | Techno-futuristic myth style tune-up | Muse | DELIVERED 2026-09-28 |

## 2026-09-29 06:00 — Claude (covering Astra): board update
Evidence: SPRINT_LOG.md 2026-09-29 06:00 entry and `docs/reviews/2026-09-29-0600-claude-acceptance.md`. These rows supersede the Sprint board rows above for the same IDs.

| ID | Pri | Owner | Status | Next action |
|---|---|---|---|---|
| AI-055 | P1 | Muse | ACCEPTED 2026-09-29 06:00 | — |
| AI-048 | P1 | Muse | ACCEPTED 2026-09-29 06:00 | — |
| AI-056 | P2 | Muse | ACCEPTED 2026-09-29 06:00 | — |
| AI-058 | P3 | Muse | ACCEPTED 2026-09-29 06:00 | — |
| AI-057 | P3 | Muse | ACCEPTED 2026-09-29 06:00 | — |
| AI-066 | P2 | Muse | ACCEPTED (CI 36519850577) | Determinism rerun optional. |
| AI-063 / AI-068 | P1 | Muse | ACCEPTED (contract + tests) | Meshy lane confirms scale before the land batch. |
| AI-064 | P2 | Muse | REJECTED 2026-09-29 06:00 → fix in AI-069 | Windows verify red since `82cdd9b`. |
| **AI-069** (new, AI-006) | P1 | Muse | READY | `coverage.py` UTF-8 on Windows; Verify green on both OSes; record run ID. |
| **AI-070** (new, AI-006) | P3 | Muse | READY | Win-split analysis of AI-066 dumps over ~20 seeds (docs only). |
| AI-065 | P1 | Muse → Claude accepts | DELIVERED | WAITING — needs Mathew present (local deep-path rerun). |
| AI-046 | P1 | Muse → Claude accepts | REVIEW | After AI-065 local rerun. |
| AI-030, AI-052-ASSET | P0 | Claude (Astra lane) | IN_PROGRESS | WAITING — needs Mathew present. |
| AI-061 | P1 | Claude media threads | IN_PROGRESS | Record Meshy spend 2,720 → 2,140 and new items in ASSET_QUEUE. |

### 2026-09-29 ~08:30 EDT — Rune (lobby-lab reflection): AI-069 + AI-070 delivered

- **AI-069 DELIVERED** (`docs/muse/sprint-02/presentation/`, commits `f7e1167`, `c540163`): `encoding="utf-8"` on every `open()` in `coverage.py` and `test_coverage.py`; `newline="\n"` on the report write. Root cause confirmed: the ✅/❌ status glyphs are not encodable in Windows cp1252, which is the `open()` default there (Linux CI was UTF-8, which is why the 02:00 reflection's Linux-only verification missed it). Test the mechanism locally: `open(...,'w',encoding='cp1252').write('✅')` raises `UnicodeEncodeError: 'charmap' codec can't encode character '\u2705'`; with `encoding='utf-8'` it writes fine. Failing-before evidence is the real CI history: 8 Verify runs red at step 11 "Presentation manifest coverage (AI-064)" from `36515172274` (`82cdd9b`) to `36528709751` (`e344162`); last green was `36512570343` (`d3df0f0`). **Passing-after: Verify run 36565491824 (tip `c540163`) completed / success — both `ubuntu-latest` and `windows-latest` jobs green.** AI-064's rejection is resolved on the delivery side; re-acceptance is Claude's (reviewer).
- **AI-070 DELIVERED** (`docs/muse/sprint-02/win-split-analysis.md`, commit `b1a193d`): win-split analysis of AI-066 event dumps over 31 seeds. Base run (21 seeds, seat 0 = Zeus): Zeus wins 21/21. Swap run (10 seeds, local-only harness variant with deck args swapped, seat 0 = Poseidon, seat 1 = Zeus; engine/bots/rules untouched): Zeus wins 7/10. Combined **Zeus wins 28/31 ≈ 90% — the winner follows the Zeus starter deck, not the seat: starter-deck asymmetry, not first-player advantage** (the harness always seats player 0 first, 31/31, yet Zeus still wins 70% from the second seat). All dumps validate against the AI-062 schema; determinism re-verified (seed-42 rerun byte-identical). Balance flag for AI-012 (Zeus/Poseidon are both meant to be free and viable): Zeus starter beats Poseidon starter ~90% under HERO bots. No rules change — analysis only.

## 2026-09-29 12:00 — Claude (covering Astra): board update
Evidence: SPRINT_LOG.md 2026-09-29 12:00 entry and `docs/reviews/2026-09-29-1200-claude-acceptance.md`. These rows supersede earlier rows for the same IDs.

| ID | Pri | Owner | Status | Next action |
|---|---|---|---|---|
| AI-069 | P1 | Muse | ACCEPTED 2026-09-29 12:00 (Verify 36565491824, tip 36565877747) | — |
| AI-064 | P2 | Muse | DELIVERED; CI part ACCEPTED 2026-09-29 12:00 (rejection lifted) | PC coverage run (WAITING — needs Mathew present) + ElevenLabs cue-name confirmation. |
| AI-070 | P3 | Muse | ACCEPTED 2026-09-29 12:00 (analysis) | Follow-up in AI-072; balance flag to AI-012. |
| **AI-072** (new, AI-006) | P2 | Muse | READY | Commit deck-swap/mirror options in EventDump; ≥20 seeds per arrangement; seat effect reported separately. |
| **AI-071** (new, AI-006) | P3 | Muse | READY | UTF-8 `open()` in `build_presentation_manifest.py:72,118`. |
| **AI-073** (new, AI-006) | P3 | Muse | READY | Pin `ubuntu-24.04`, bump actions to Node-24 majors (Ubuntu 26 migration 2026-10-19). |
| AI-065 / AI-046 | P1 | Muse → Claude accepts | DELIVERED / REVIEW | WAITING — needs Mathew present. |
| AI-030, AI-052-ASSET | P0 | Claude (Astra lane) | IN_PROGRESS | WAITING — needs Mathew present (incl. batch-04 GLB import). |
| AI-061 | P1 | Claude media threads | IN_PROGRESS | Meshy 2,050 / ElevenLabs 127,473; hold for HA-009 + batch-04 in-game check. |

## 2026-09-29 14:00 — Rune (lobby-lab reflection): AI-071/072/073 delivered
Evidence: SPRINT_LOG.md 2026-09-29 14:00 entry. These rows supersede earlier rows for the same IDs.

| ID | Pri | Owner | Status | Next action |
|---|---|---|---|---|
| AI-071 | P3 | Muse | DELIVERED 2026-09-29 14:00 (`8608fd1`) | CI acceptance (Verify green both OSes) — runs in flight at publish time. |
| AI-072 | P2 | Muse | DELIVERED 2026-09-29 14:00 (`f4206a1`, `b5825e3`, `4bcf3ed`, `ed7ce31`, `57f2495`, `34baef8`, `691ddda`) | CI/review: linux-packaging event-dump step re-runs `run.sh` (unchanged base path); reviewer checks the 60-seed evidence. |
| AI-073 | P3 | Muse | DELIVERED 2026-09-29 14:00 (`633294d`, `caf4a42`, `90afb2c`) | CI acceptance: Verify + both packaging workflows green on the pinned runners/bumped actions — runs in flight at publish time. |

- **AI-071 DELIVERED**: `encoding="utf-8"` on both `open()` calls in `docs/muse/sprint-02/presentation/build_presentation_manifest.py` (lines 72, 118). Regenerated `presentation-manifest.json` from pinned inputs → byte-identical (input is all-ASCII; `json.dump` escapes non-ASCII anyway). `test_coverage` 2/2 OK.
- **AI-072 DELIVERED**: `EventDump --mode base|swap|mirror` committed (default `base` reproduces prior dumps: seed 42 → 235 events, winner 0, matching CI 36519850577; base seeds 1–20 reproduce the AI-070 table 20/20). `run-balance.sh`: 20 seeds × 3 modes, every dump validated against the AI-062 wire format → **60/60 VALID**. Results: base Zeus 20/20 (100%); swap Zeus 12/20 (60%); mirror seat-0 12/20 (60%). Decomposition: **deck effect +60pp for the Zeus starter at both seats; seat effect +40pp for seat 0 with either deck** — the 12:00 review's caveat quantified: deck asymmetry is the main driver, but first-player advantage is a real ~40pp contributor (no coin flip in `DemoMatchFactory` turn order). Combined Zeus 32/40 = 80% (AI-070: 28/31 ≈ 90%). `win-split-analysis.md` updated; balance flag to AI-012 strengthened. No rules change.
- **AI-072 incidental AI-006 defect found and fixed**: the AI-062 wire contract asserted `CHARACTER_MOVED` `amount == hex distance`, but the engine's cost is the step count along the shortest *legal* path — `MovementRules.shortestLegalPath` is a BFS that detours around blocked hexes (source + pinned-jar bytecode both confirm; a real base-seed-20 move has distance 3, cost 4). `validate.py` now enforces `amount >= distance`; `board-events.md` §13 + event table corrected; detour regression test added (`test_validate` 6/6 OK).
- **AI-073 DELIVERED**: `ubuntu-latest` → `ubuntu-24.04` in `verify.yml` matrix and `linux-packaging.yml` (ahead of the 2026-10-19 Ubuntu 26 migration); actions bumped to their Node-24 majors — `checkout@v4→v5`, `setup-node@v4→v5`, `setup-python@v5→v6`, `setup-java@v4→v5` (verified against published action metadata). No build-logic changes; `windows-latest` kept (no announced migration). YAML validated locally.

## 2026-09-29 15:00 — Rune (lobby-lab reflection): AI-071/072/073 CI acceptance achieved
Evidence: GitHub Actions API on 2026-09-29 ~15:00 EDT; see SPRINT_LOG.md 2026-09-29 15:00 entry. These rows supersede earlier rows for the same IDs.

| ID | Pri | Owner | Status | Next action |
|---|---|---|---|---|
| AI-071 | P3 | Muse | DELIVERED; CI acceptance achieved 2026-09-29 15:00 (Verify 36610544514 @90afb2c success; Verify 36611220923 @df1095ed success) | Reviewer acceptance (12:00 lane) outstanding. |
| AI-072 | P2 | Muse | DELIVERED; CI acceptance achieved 2026-09-29 15:00 (Linux packaging 36610540317 @caf4a42 success incl. event-dump step; the packaging workflow re-runs `run.sh`) | Reviewer checks the 60-seed evidence (12:00 lane). |
| AI-073 | P3 | Muse | DELIVERED; CI acceptance achieved 2026-09-29 15:00 (Verify 36610544514 + Windows packaging 36610544470 @90afb2c success) | Reviewer acceptance (12:00 lane) outstanding. |

## 2026-09-29 17:15 — Claude (covering Astra): board update (18:00 review)
Evidence: SPRINT_LOG.md 2026-09-29 17:15 entry and `docs/reviews/2026-09-29-1800-claude-acceptance.md`. These rows supersede earlier rows for the same IDs.

| ID | Pri | Owner | Status | Next action |
|---|---|---|---|---|
| AI-071 | P3 | Muse | ACCEPTED 2026-09-29 18:00 | — |
| AI-072 | P2 | Muse | ACCEPTED 2026-09-29 18:00 (Windows repro 60/60, identical split) | Portability and CI in AI-074. |
| AI-073 | P3 | Muse | ACCEPTED 2026-09-29 18:00 | — |
| AI-066 | P2 | Muse | ACCEPTED (cross-OS determinism, seed 42 = 235) | — |
| AI-064 | P2 | Muse | ACCEPTED 2026-09-29 18:00 (PC run exit 0) | Layout and cue alignment in AI-077. |
| AI-067 | P2 | Muse → Meshy thread | ACCEPTED (queue doc) | Meshy thread submits as local `batch-05-lands`. |
| **AI-075** (new, AI-060b) | P1 | Muse | IN_PROGRESS 2026-09-29 17:10 | `docs/muse/sprint-02/timeline/timeline.py` (stdlib): AI-066 JSONL + presentation-manifest → per-event cue schedule (start_ms, duration_ms, anim key, sfx key, impact hook for AI-060c) with a default duration table. Golden for seed 42. Test in verify.yml on both OSes. |
| **AI-077** (new, AI-006) | P1 | Muse | READY | Manifest cues: summon→deploy, death→destroy, plus signature for rarity-4. `coverage.py` resolves assets recursively under staging (prefers `picks/`, accepts .wav/.mp3, `meshy/<batch>/<id>.glb`). Regenerate and test. Acceptance: Claude's PC run reports 29 model / 18 audio cards. |
| **AI-074** (new, AI-006) | P2 | Muse | READY | Make `run-balance.sh` portable (classpath separator, loop `continue`, draw parse, UTF-8 `open()`), and add a 5-seed × 3-mode balance step to linux-packaging.yml. |
| **AI-078** (new, AI-006) | P3 | Muse | READY | Reproducible jar (fixed timestamps), and stop rewriting the tracked `CHECKSUMS.sha256` during builds. Two builds must give the same SHA-256. |
| **AI-076** (new, AI-012) | P3 | Muse | READY | Balance-options memo (coin flip vs Poseidon starter tweaks, expected effect from the AI-072 numbers). Docs only → HA-015 for Mathew. |
| AI-061 Meshy | P1 | Claude — Meshy thread | READY | Stage batch-04 GLBs, a 5-land pilot, then 30 lands (existing credits only). |
| AI-061 ElevenLabs | P1 | Claude — ElevenLabs thread | READY | Land cue sets for the 35 lands; picks named `<card_id>_<cue>.wav` (deploy/destroy). |
| AI-052-ASSET / AI-030 / AI-060b | P0 | Claude — Unity thread | READY | Batch-02/04 import at AI-063 scale, TokenPreview sheet (HA-009), JSONL playback prototype on a `claude/unity-*` branch. |
| AI-065 / AI-046 | P1 | Claude | WAITING — needs Mathew present | Deep-path break-mode rerun. |

## 2026-09-29 ~17:45 — Product Owner direction: 3D playtest build (Claude, covering Astra)
**Mathew, direct (2026-09-29 ~17:40 EDT):** start the Meshy, ElevenLabs and Unity threads. ElevenLabs work is **green-lit** and continues without a per-batch gate. Asset production continues on both animatable 3D models and their sound effects. The board must be ready, with stand-in assets for lands, structures, spells and characters, **so playtesting of the 3D playable version can start.** HA-009 steers batches but no longer blocks them. Existing credits only, no purchases (unchanged).

**New sprint goal (supersedes the IC-S02 goal):** a Zeus-vs-Poseidon match playable in UnityProof on the hex board against the pinned alpha rules. Every card is shown (a real model where one is staged, a typed stand-in otherwise), with sound on deploy/move/attack/hit/destroy.

| ID | Pri | Owner | Status | Next action / acceptance |
|---|---|---|---|---|
| **AI-079** (new, AI-003/AI-060) | P0 | Muse (Rune) | READY (sent 17:40) | Headless rules bridge `releases/alpha-0.7.15-playable/tools/rules-bridge/`: Java against the pinned jar, line-delimited JSON over stdin/stdout (`new` / `legal` / `act`), emits AI-062 wire events + state (hands, GP, per-hex stacks), bot seat auto-plays, illegal ids return an error. Scripted test validating every event, linux-packaging.yml step, protocol in README. **Ahead of AI-075.** |
| **AI-080** (new, AI-060/AI-017) | P0 | Claude — Unity thread | IN_PROGRESS | Playtest build: hex board polished (tile highlight, legal-move markers, stack offsets), a stand-in catalog for all 139 cards by type (LAND hex slab with faction tint, STRUCTURE prism, CHARACTER capsule + faction color + name plate, CAPITAL tower, SPELL VFX burst), real GLBs swapped in where staged, SFX from `picks/`, and an event playback driver. Then it connects to the AI-079 bridge for real play. Acceptance: Mathew plays a full match in a Windows build. |
| **AI-081** (new, AI-061/AI-019) | P1 | Claude — Meshy thread | IN_PROGRESS | Animatable models: rig humanoid CHARACTER models (Meshy auto-rig/animate) with idle/walk/attack/hit/death clips and export FBX/GLB with animations. Non-humanoids stay static (Unity tweens). Plus the AI-067 lands (local `batch-05-lands`), then the remaining structures. |
| **AI-082** (new, AI-061/AI-020) | P1 | Claude — ElevenLabs thread | IN_PROGRESS (green-lit) | Full cue sets (deploy/move/attack/hit/destroy/ability/idle, + signature for apex) for every card that has a model or is in the lands batch, then the rest of the roster by SFX group. Picks go in `picks/<card_id>_<cue>.wav`. |
| AI-075, AI-077, AI-074, AI-078, AI-076 | P1–P3 | Muse | READY / IN_PROGRESS | Unchanged, behind AI-079. |

## Long-term architecture goals (Product Owner, 2026-09-29 ~18:10 EDT)
Captured at Mathew's request after the AI-079 discussion. **Direction:** the Java rules bridge (AI-079) is a playtest scaffold, not the shipping architecture. The target is one rules core in C# that runs inside Unity (offline play, mobile, AI opponents) and, unchanged, on an authoritative server for ranked/online play. TubaExperiment stays the rules source of truth until the C# core passes conformance; from then on the C# core is the runtime. Hosting is not "in git": GitHub keeps the code and runs CI, and a live game server needs separate hosting (cost → HA-017). The final architecture call remains AI-003 (Astra, from 2026-10-04) and HA-003/HA-016 (Mathew).

**Milestones:** (1) 3D playtest on the Java bridge (AI-079/AI-080, this sprint) → (2) C# core at event-for-event parity on the golden seeds (AI-083/AI-084) → (3) Unity runs on the C# core, and the bridge is retired from the client (AI-087) → (4) mobile builds on target devices (AI-086) → (5) authoritative server for ranked/online (AI-085).

### AI-083 — Rules core ported to a C# library (child of AI-003/AI-007)
P1 | BACKLOG (starts after the AI-080 playtest) | Owner: Astra (Unity/integration) with Claude until 2026-10-04; Muse for test tooling | Dependencies: AI-079, AI-084, HA-016.
A pure .NET Standard 2.1 / C# library (`rules-core/`, no UnityEngine references) that implements the pinned alpha rules (TubaExperiment `992bc95`): HEX geometry, stacking, deploy/move/attack/ability/spell resolution, GP, turn structure, victory and the HERO bot. It emits AI-062 wire events natively. It is deterministic: seeded RNG, no wall-clock or hash-order dependence. Port it module by module, each gated by AI-084 conformance. Acceptance: 100% event-for-event parity with the Java engine on the golden seed corpus (all three EventDump modes, ≥ 60 seeds) and on bridge-scripted human games, and it runs headless in `dotnet test` on Linux and Windows CI.

### AI-084 — Java↔C# differential conformance harness (child of AI-004/AI-083)
P1 | BACKLOG | Owner: Muse (Rune) | Dependencies: AI-066, AI-079.
The golden corpus is the AI-066/AI-072 EventDump outputs plus AI-079 bridge transcripts (scripted human actions). A comparison tool runs the same seed and action script through both engines and reports the first diverging event (seq, field, both values). It runs in CI on every commit that touches `rules-core/`, and a divergence fails the build. It also covers the AI-078 reproducible-jar work, so the Java oracle itself is stable. Acceptance: the harness catches an intentionally broken C# rule, and parity is reported as n/N seeds.

### AI-085 — Authoritative game server for ranked and online play (child of AI-008/AI-010/AI-011)
P1 | BACKLOG | Owner: Astra | Dependencies: AI-083, AI-005, HA-017 (hosting and cost).
The server runs the same C# rules core. Clients send intents; the server validates, resolves and broadcasts events with redacted hidden state (hands, deck order). Ranked results come only from the server, never from client agreement. Reconnect and resume use the event log (AI-009). The existing lobby worker stays for discovery, or is folded in. Acceptance: two clients on separate networks complete a match. Forged, duplicate or replayed intents are rejected in tests. Hidden state is never sent to the opponent. Results are written exactly once (AI-011).

### AI-086 — Mobile build pipeline and device budgets (child of AI-014/AI-060d)
P1 | BACKLOG | Owner: Astra | Dependencies: AI-083 (no JVM on device), HA-003 (platforms).
Unity Android (and iOS if HA-003 confirms it) builds in CI, touch input (no hover or right-click dependence), safe areas, and asset LOD/texture variants for the Meshy models (2K → 1K/512 mobile). Budgets: 60 fps on the reference phone, memory, thermals, download size. Acceptance: a full match on a real device within budget.

### AI-087 — Unity client on the C# core; retire the client-side Java bridge (child of AI-003)
P2 | BACKLOG | Owner: Astra | Dependencies: AI-083 parity.
Swap the AI-080 BridgeClient for an in-process C# core behind the same interface, so the presentation layer (board, stand-ins, timeline AI-075, audio) does not change. The Java bridge stays only as the conformance oracle in AI-084. The shipping desktop build then carries no Java runtime. Acceptance: the playtest build runs with no Java installed and gives the same match as the bridge on the golden seeds.

### AI-088 — Single card-data source (child of AI-016/AI-024)
P2 | BACKLOG | Owner: Muse (tooling) → Astra (runtime) | Dependencies: AI-083.
Card definitions (stats, costs, keywords, abilities as data where possible) are exported once from the pinned alpha into a versioned `cards.json` that the C# core, the presentation manifest (AI-064), the asset directory (AI-049) and the expansion work (AI-024) all read. This removes hand-copied card lists. Acceptance: all 139 cards load from the file in both engines with identical conformance results, and a schema test runs in CI.

### AI-089 — Replays, spectating and bug-report capture (child of AI-006/AI-009)
P3 | BACKLOG | Owner: Astra/Muse | Dependencies: AI-083.
Every match stores seed + intents (a few KB). Any match can be replayed deterministically in the Unity client, which gives bug reports with exact reproduction, a spectator mode and a highlight-reel base for marketing. Acceptance: a saved playtest match replays identically after an app restart and on another machine.

### AI-090 — Balance simulation at scale (child of AI-012)
P2 | BACKLOG | Owner: Muse | Dependencies: AI-083 (fast in-process sims), AI-076.
Headless C# bot-vs-bot runs over thousands of seeds per deck/seat/mode (extending AI-072), with a win-rate report per card and per matchup. It runs on each balance change and before every expansion release. Acceptance: the report reproduces the AI-072 numbers on the same seeds and flags any matchup outside 45–55% after the AI-012 tuning.

