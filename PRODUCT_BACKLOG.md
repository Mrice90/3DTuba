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
P1 | IN_PROGRESS 2026-09-28 ~19:20 EDT | Owner: Muse (Rune) | Reviewer: Claude | Dependencies: none (reads the pinned Java rules, read-only).
Why: the 3D board, animations and sounds must be driven by authoritative rule events, but no event vocabulary exists. UnityProof only knows two hard-coded movement cases, and the rules live in TubaExperiment `game-core` (Java). A contract with golden transcripts lets presentation work start now, whatever AI-003 decides about how Unity gets the rules.
Work (all under `docs/muse/sprint-02/board-events/`): read TubaExperiment `992bc95` `game-core` (GameEngine, MovementRules, board/stack, combat, capital and ability code) and list every state change a player must see: card deployed onto a stack, land stacked, structure raised, capital placed, character moved (one event per step), attack declared, opportunity attack, damage dealt, card destroyed, ability activated, spell cast and resolved, capital hit, GP change, turn start/end, match end. Deliver:
- `board-events.md`: one row per event with its payload fields (event, seq, turn, player, card_id, instance_id, from/to `{x,y}`, stack_index, amount), the Java `file:line` at the pin that causes it, and the presentation hooks it drives (animation key from the AI-049 `animation_events`, SFX cue key).
- `board-events.schema.json` (JSON Schema 2020-12), one event per line (JSONL).
- `golden/zeus-vs-poseidon-short.jsonl`: a hand-derived transcript of a short scripted match (both capitals placed, 2 lands, 1 structure, 1 character that moves 2 steps and attacks, 1 destroy), using card IDs from `manifest.json`.
- `validate_board_events.py` and `test_validate_board_events.py` (standard library only): schema check plus invariants (seq strictly increasing, coordinates inside 4×6, moves only for CHARACTER instances, a destroyed instance emits nothing later, stack_index consistent per cell). Add both to `.github/workflows/verify.yml`.
Acceptance: verify.yml green on Linux and Windows with the new step; every event cites a Java source line at the pin; Claude checks the citations against the Java source. Out of scope: Unity or Java code changes.

### AI-063 — Fix the asset prompt directory for the real board (child of AI-049/AI-017)
P1 | READY 2026-09-28 | Owner: Muse (Rune) | Reviewer: Claude (meetings thread, as Meshy owner) | Dependencies: none.
Why: all 139 `meshy_prompt` strings say "hex-based tactics board game", and every LAND asks for a "hexagonal terrain tile". The rules board is a 4×6 square grid (`BoardPosition` WIDTH 4, HEIGHT 6, Chebyshev distance) where lands stack under structures and characters, and UnityProof tiles are 1.18-unit squares on a 1.3 spacing. There is also no size contract beyond characters (`check_glb.py` defaults to 1.8 units), and Keraunos Spire came out about 2.5 tiles tall, hiding its neighbours. The 35 lands have not been generated yet, so fixing this now avoids wasted credits.
Work: add `docs/muse/sprint-02/board-scale.md` proposing a footprint and height budget per type (starting point: LAND = full 1×1 square tile, top surface ≤ 0.25 units, flat enough for a token to stand on; STRUCTURE ≤ 0.9 tile, ≤ 1.6 units; CHARACTER ≤ 0.8 tile, 1.8 units per `check_glb.py`; CAPITAL = 1 tile, ≤ 2.2 units; SPELL none). Update `build_asset_directory.py` to use square-tile wording, add `board_footprint` and `height_budget` fields per card, and state the stack role in each prompt (lands must carry a unit on top). Regenerate the `.json` and `.md`. Add a test that no prompt contains "hex" and every non-spell card has both fields, and run it in verify.yml.
Acceptance: regenerated directory plus a changed-card summary in SPRINT_LOG.md; CI green; the meetings thread confirms the scale numbers before the next Meshy batch uses them. Batches 01 and 02 are not regenerated because of this item; that is the Meshy lane's call.

### AI-064 — Per-card presentation manifest and coverage report (child of AI-060, AI-018–020)
P2 | READY after AI-062 | Owner: Muse (Rune) | Reviewer: Claude | Dependencies: AI-062 event vocabulary.
Why: the vision needs every token to have its own animations and sounds, but 107 of 139 cards currently share group SFX, and the cue names (DEPLOY, DESTROY, CLICK, MELEE/RANGED…) differ from the cue set the ElevenLabs lane now produces (summon, move, attack, hit, death, ability, idle). Nothing shows what each card still lacks.
Work (under `docs/muse/sprint-02/presentation/`): `presentation-manifest.json` mapping every card to the AI-062 events it can emit, each with an animation clip key and a unique SFX key (`<card_id>_<cue>`), plus its model path. `coverage.py` (standard library) reads the manifest and a staging root and writes a Markdown table per card showing whether the model, textures, each animation and each SFX are present or missing. Test it against a small fixture tree and run it in verify.yml.
Acceptance: CI green; Claude runs `coverage.py` on Mathew's PC against `assets/staging/` and posts the first real coverage report; the ElevenLabs thread confirms the cue names match what it produces.

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
| AI-065 | P1 | Muse | READY | Fix regress.bat for real Windows PCs: (1) the xcopy at line 85 fails with 'File creation error' (likely MAX_PATH; use robocopy or exclude build\); (2) the WORK dir `%RANDOM%%RANDOM%` collides across back-to-back runs, and an early exit skips cleanup. | All 6 break modes report 'intentional break correctly detected' back-to-back from a deep path on a local Windows PC. Claude re-runs it. |
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
| AI-062 | P1 | Muse (Rune) | IN_PROGRESS 2026-09-28 ~19:20 EDT | Board event contract v1 from the pinned Java rules: vocabulary with source citations, JSON schema, golden Zeus-vs-Poseidon transcript, validator in verify.yml. | CI green on both OSes; Claude checks the citations. |
| AI-063 | P1 | Muse (Rune) | READY 2026-09-28 | Replace the hex wording in all 139 Meshy prompts with the real 4×6 square stacked board; add a per-type footprint and height budget; regenerate; test in verify.yml. | CI green; the meetings thread confirms the scale before the land batch. |
| AI-064 | P2 | Muse (Rune) | READY after AI-062 | Per-card presentation manifest (events → animation key + unique SFX key) and a coverage report script. | CI green; Claude posts the first real coverage report. |

### Accepted or done (for reference; evidence in SPRINT_LOG.md)
AI-037/038 manifest + offline smoke DONE · AI-045 lobby guards merged (PR #1) · AI-047 Windows/browser lobby fixes merged (PR #1) · AI-049 asset prompt directory DONE (139 cards, 21 SFX groups, 32 apex briefs) · AI-052-WIN Windows packaging repair ACCEPTED (run 36386714758) · AI-054 GLB staging checker verified 55/55 (local, not yet committed) · AI-029/032/033/034 setup verified.

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

## Child action catalog (IDs 027–064)
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
| AI-060 | AI-017 | Build-as-you-play 3D battlefield (north star) | Claude → Astra | BACKLOG |
| AI-061 | AI-018/020 | Continuous media production lane | Claude | IN_PROGRESS |
| AI-062 | AI-060 | Board event contract v1 | Muse | READY |
| AI-063 | AI-049/017 | Asset prompts fixed for the square stacked board | Muse | READY |
| AI-064 | AI-060/018–020 | Presentation manifest + coverage report | Muse | READY after AI-062 |
| AI-059 | AI-031 | Reconcile unmerged astra/* branches | Claude | DELIVERED |
