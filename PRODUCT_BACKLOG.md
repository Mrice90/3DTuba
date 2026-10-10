# Infinite Conquest — product backlog

**Version 2 — 2026-09-28 18:30 EDT.** Maintained by Claude (covering Astra's lane until Astra returns on 2026-10-04). This is the single shared backlog for every worker. A copy is kept in `C:\Users\mattj\OneDrive\Documents\AI Dev\Infinite Conquest\PRODUCT_BACKLOG.md`, and this repository copy wins if the two ever differ. Evidence and run logs go in `SPRINT_LOG.md`, not here.

## October 8, 2026 — assigned work (Codex, Claude Code, Muse/Thalia)

Mathew approved public publication of the previously reviewed updates and explicitly requested tasks for all three workers. Backlog reconciliation published in commit `19c55faa`; verification report in `031ddd7e`. The earlier public-write rejection is resolved by this direct approval.

**New ruling found in Mathew's user messages in Muse's main chat:** land abilities remain usable beneath structures and structure abilities beneath characters, regardless of stack height. This supersedes the reconciliation's top-only activation instruction, but does not itself change spell-target legality. Mathew accepts iterative divergence in the owned 3D rules; protected repositories remain read-only. Titan trim review is ACCEPT in Thalia's reachable chat; promotion/in-game review is still separate.

| Task | Pri | Owner | Assignment/status | Deliverable and gate |
|---|---|---|---|---|
| AI-080-COVERED-ACTIVATION | P0 | Codex | ASSIGNED — next gameplay implementation | Enumerate eligible owned Land/Structure activation sources throughout a stack, show clear source/action choices, apply costs/effects once and reject illegal owner/GP/reuse atomically. Focused engine/bridge/UI checks plus separate build and human review. No gameplay change claimed in this assignment turn. |
| AI-108-CARD-BALANCE | P1 | Codex | DRAFT DELIVERED, design review pending | `reviews/2026-10-08-slot-balance-draft.md`: 88 per-card rows recording current provisional supply/cost, no capital boon invented. Follow with opening measurements and design decision. |
| AI-082-EVENT-MATRIX | P1 | Codex | ASSIGNED — queued after gameplay priority | Map actual emitted events to exact clips/fallbacks, applicable ability cues and tutor sharing; use existing audio before generation. |
| AI-081-PALETTE-PROOF | P1 | Claude Code | ACKNOWLEDGED / IN_PROGRESS via signed-in browser | Read-only actual-import palette/candidate audit, precise files/metadata and contradiction resolution. No Unity build, provider job or shared-record edits. |
| AI-081-CANDIDATE-PROMOTE | P1 | Claude Code | QUEUED after palette proof | Candidate review/import order: Keraunos Prime, trimmed Titan, Trident Core and Thunder Ram; approve subset and preserve rollback/fallbacks before a new presentation increment. |
| AI-081-STRUCTURES-LOCAL / AI-060-RIG-PILOT | P1 | Claude Code | QUEUED for later scoped implementation | Three local structure pilots and one repaired existing rig/clip pilot; no Meshy credit-consuming jobs. |
| AI-108-CARD-BALANCE-REVIEW | P1 | Muse/Thalia | DELIVERED, access-limited review | Independent slot review including zero-capacity openings, burrow, removal/control changes and multi-slot atomicity; distinguish accessible source facts from relayed local tests. |
| AI-080-COVERED-ACTIVATION-QA | P0 | Muse/Thalia | DELIVERED, access-limited review | Six exact acceptance cases for buried abilities, correct owner, costs/effects, GP rejection, once-per-turn restriction and visible controls. Dedicated QA artifact only; no binary acceptance without access. |
| AI-080-SP2-HUMAN | P0 | Mathew; Thalia records | WAITING | Full human live match on the next corrected artifact, recording build/seat/seed/result/duration and activation/target/audio/visual feedback. |
| AI-080-CAMERA | P0 | Claude Code | DELIVERED 2026-10-08 — draft PR https://github.com/Mrice90/3DTuba/pull/7 (`claude/camera-restore` → `claude/unity-live-match`), awaiting Mathew | Claimed files: `UnityProof/Assets/Playtest/Scripts/CameraRig.cs`, `PlaytestGame.cs` (camera lines only), `PlaytestMenus.cs` (controls text). Right-drag rotate/tilt restored (a right-click without dragging is left for abilities), Up/Down arrows tilt, camera faces the human's own capital in live matches. Build `playtest/unity-build-2026-10-09-camera`, live smoke PASS as Zeus and Poseidon. |
| AI-060-RIG-ROSTER | P1 | Claude Code | DELIVERED 2026-10-09, claim released — draft PR https://github.com/Mrice90/3DTuba/pull/8, awaiting art review | Sparkstep Runner + Iris Signal Runner rigged with Idle/Walk (no Meshy jobs). Unity 6000.6.3f1 import check PASS for both plus the Keraunos pilot: clips loop, seam 0, feet grounded, textured after re-exporting FBX textures with file extensions and extracting them on import. Next: cloth review (Sparkstep cape, Iris scarf), then more humans in small batches. |
| AI-094-COIN-FLIP | P1 | Claude Code | IN_PROGRESS 2026-10-09 | Branch `claude/project-thread-wfmruz` → draft PR into `claude/unity-live-match`, no merge. Claimed: `UnityProof/Assets/Playtest/Coin/` (new: coin FBX/GLB, skins, `CoinFlip.cs`, `CoinSkin.cs`, editor prefab builder, `CoinFlip.unity` test scene), `docs/production/coin/` (new), `playtest\coinflip-2026-10-09\` (new), and in `PlaytestGame.cs` only the `MATCH_STARTED` case (one call that plays the flip for the engine's coin-flip result before turn 1). Reskinnable low-poly coin (rim, heads, tails material slots) + flip animation, no Meshy jobs. |
| AI-061-PROVENANCE | P2 | Claude Code | DELIVERED 2026-10-10, claim released — draft PR https://github.com/Mrice90/3DTuba/pull/11, awaiting review | `docs/reviews/2026-10-09-batch-provenance.md` + `.csv`: 48 models (05: 5, 06: 30, 07: 13) all traced to batch, source card and Unity catalog, with model SHA-256 for 48 of 48 (read from local SHA256SUMS for 06/07; batch 05 hybrid GLBs hashed from the local files since its checksum file covers raw downloads only). 101 task IDs: 74 full, 27 prefix only (all of batch 05, 12 batch-07 texture IDs; full IDs only in the Meshy workspace). Card art SHA-256 for 38 of 48. No asset files changed, no Meshy jobs. |

Muse delivery verified in the existing chat: thumbs-up acknowledgement plus visible task “Run Card Balance Review + Activation QA,” extracting game-file lines. Evidence: `reviews/2026-10-08-muse-task-assignment.jpg`. No unrelated comic/media scheduling authorized.

Claude dispatch: CLI first attempt failed before API work with DNS ENOTFOUND and zero usage. Signed-in browser fallback in existing “Product backlog and sprint review” session explicitly accepted AI-081-PALETTE-PROOF and began laptop file inspection. Evidence: `reviews/2026-10-08-claude-task-assignment.jpg`. Scoped brief: `reviews/2026-10-08-claude-backlog-assignment.txt`.

Codex owns shared coordination and build integration. Worker reports must state exact files, accessible evidence, current access limits and next gate. New work is assigned, not presumed accepted or continuously running.

Muse delivered `workspace/goals/infinite-conquest-desktop-alpha/files/2026-10-08-slot-qa-report.md` with seven activated reference sources (5 structures, 2 lands), six scenarios and access limits. She corrected the count to 48 Characters +34 Structures +6 Capitals =88, with the two heavy examples included. This is delivered QA evidence, not independent laptop binary acceptance.

Public-write authority: current user approved publication and requested assignments. In the existing Claude session, Mathew also directly stated “You have a clear yes from me Matt owner of that github to work in and push pull write in read and do anymore you see fit in the github repos under my login”; the surrounding exchange specifically concerned public 3DTuba checkpoint reviews and assignments. This historical explicit authorization was read from the user-authored message, not an assistant's inferred permission.

## Current reconciliation — 2026-10-07 22:50 EDT (Codex)

**This section governs current picking and status for the IDs below, superseding every older snapshot, including entries labeled October 8 in the historical records.** Evidence review: `docs/reviews/2026-10-07-product-backlog-verification.md`; laptop evidence: `reviews/2026-10-07-backlog-verification.json`. Preserve all historical records and unrelated worker edits.

**Current goal:** get the delivered SP2 Windows game accepted in a full human match, resolve gameplay gaps, and finish the Zeus/Poseidon presentation library in small reviewable increments. The longer-term direction remains a polished battlefield built by play, distinct token motion/audio, desktop online play on free hosting first, Android and shared cross-play after the funded authority/server milestone, then Apple and additional factions/seasons.

**Operating constraints:** Meshy downloads and local edits only; no new generation, retexture, rig jobs, purchases or top-ups. ElevenLabs existing credits may be used for verified gaps, with Codex owning the lane; no blanket repeated generation. Zeus uses white/blue/gold. TubaExperiment and Desolate-Tuba remain read-only. The October 2 and SP1 packages stay available. Networking and release tasks below are refined future work, outside the current presentation sprint. These rows nominate owners; they do not imply fresh dispatch or continuous execution.

### Verified delivery and status corrections

| Item | Current disposition | Evidence / remaining gate |
|---|---|---|
| AI-080 / SP1-HUMAN | Partial human feedback recorded; full-match acceptance WAITING | Mathew praised October 2 gameplay and working SP1 audio. No human result/seed/duration report; do not infer one from automated matches. Use SP2 for the next acceptance pass. |
| SP1-FIX / SP2 targeting and activation | DELIVERED; human review pending | Explicit target/action choices, relocation destination step, activation controls, preserved selection after illegal clicks. Original structure-spell failure not reproduced. Exposed-only stack legality is unchanged. |
| AI-108 structure summon slots | Playable implementation DELIVERED; final design/balance open | Structures provide capacity; ordinary units cost 1, large units may cost more; death/removal frees use; most capitals provide 0. Current pooled supply 2/structure and two exact 2-slot examples are provisional. No capital boon chosen. |
| SP2-PRESENTATION / AI-060b | Procedural increment DELIVERED | Movement/attack/summon/hit/ability/destruction motion and six structure archetypes. Static FBXs are not finished skeletal animation. |
| AI-082 / SP1-AUDIO | Runtime mapping DELIVERED; full event/listening acceptance open | 139 cards mapped; SP1 adds 230 mappings for 88 cards from 190 unique cues/380 takes. Tutor groups share sounds; this does not prove unique audio for every card/event. Saved player check has 372 card-specific lookups and generic fallbacks. |
| SP1-AUDIO-PLAN | DELIVERED, consumed by approved production | Do not start another plan/production batch from its old ASSIGNED row. |
| AI-082-POOL | Superseded as an integration blocker | Requested pool investigation no longer blocks the already-delivered audio. Any unproduced pool report is historical/deferred, not independently DONE. |
| SP1-INVENTORY / SP1-PROV / SP1-MODELS-PREP | Historical review verdicts retained | Accepted/corrected from Thalia evidence relayed by Mathew; file/palette contradictions require current verification before promotion. |
| SP1-TITAN-TRIM | DELIVERED, review/promotion still open | Local trim and comparison renders; no new acceptance inferred. |
| ASSET-GAPS | DELIVERED; findings need consolidated verification | 367 inventory rows include 139 runtime and 228 future-faction cards. P4 in the report maps to BACKLOG here. |
| AI-104 / AI-105 / AI-106 | Delivered and independently code-reviewed historically | Fresh package hashes agree; saved SP2 smoke checks ghost suppression and live play. Human visuals/full match remain gates. |
| AI-093 | Delivered source/inventory coverage; in-game approval scope to reconcile | 35 land model references present. Historical land approval retained, but no fresh whole-package visual acceptance claimed. |
| AI-096 / AI-103 | Historical ACCEPTED | Remote sprint log supersedes local READY rows. AI-096 implementation acceptance does not establish current production deployment. |
| AI-097 | Components DELIVERED; Unity integration still open | Relay + bridge hash + C# client delivered; historical lockstep CI repaired. No playable two-human Unity match evidence. |
| AI-100 / AI-101 / AI-102 | Historical accepted/repaired evidence retained | Separate CI-era canonical JAR from the SP2 packaged JAR; current reproducible-build proof remains open. |
| AI-055 / AI-056 / AI-057 / AI-048 | Historical delivered/accepted states retained | Do not reopen from the stale September 30 OPEN summary. AI-046 Windows human check and AI-065 deep-path acceptance remain separate. |
| AI-069 / AI-070 / AI-107 | Historical delivered/accepted records retained | October 4 board/log corrections; AI-107 analysis accepted. Actual runtime bot improvement is a separate task. |

**Availability:** Codex performed this reconciliation. Claude completed SP2 work and reported a service session limit; current availability is unverified. Muse completed evidence/rules review but cannot independently execute the laptop binary. Other chats are idle/not loaded, not evidence of ongoing work. Do not call queued work actively running without a new observed start.

### Ready actions and objective breakdown

Every action needs its own evidence and independent review; owner estimates are proposed lane assignments.

| ID / parent | Pri | Owner | State | Next action and acceptance | Dependency |
|---|---|---|---|---|---|
| AI-080-SP2-HUMAN | P0 | Mathew; Thalia records | WAITING | Full live mouse match on SP2; record build hash, faction/seat/seed, result, duration, defects; hear and view targeting, slots, activation and event effects. | Mathew present |
| AI-108-CARD-BALANCE | P1 | Codex; Mathew design | READY | Draft per-structure supply/per-character cost table, capital boon exceptions and tooltip text. Review pooled vs structure-bound reservations, destruction and control changes; approve exact table before calling balance final. | Delivered slot policy |
| AI-108-OPENING | P1 | Codex; Thalia QA | READY | Measure structures-first openings across both starters/seats and multiple seeds; count end-only turns, usable structure draws and first summon turn. Propose deck/tutorial changes with before/after metrics. | AI-108-CARD-BALANCE draft |
| AI-108-FACTION-IDENTITY | P2 | Mathew design; Codex | BACKLOG | Design necromancer wreckage capacity and machine-structure units as future faction mechanics; specify costs, lifetime and edge cases. Keep outside Season 1 implementation. | Core slot balance |
| AI-080-REACTION-WINDOW | P1 | Codex; Thalia QA | READY | Expose legal human responses during bot/enemy turns; pause bot advancement, choose/pass response, resume exactly once. Test timing, illegal response atomicity and target legality. | Bridge reaction support investigation |
| AI-080-TARGET-REPRO | P1 | Codex; Mathew | READY | Human-check exposed and covered targets with Skybreaker Bolt and Erode Foundation; record card, GP, stack and clicks. Explain top-only rejection. File a defect only if engine/UI disagree. | SP2 build |
| AI-080-ABILITY-ACCEPT | P1 | Codex; Thalia | READY | Script an actual Unity match with usable structure activation, including cost, effect and once-per-turn rejection. Saved random player matches had zero structure activations. | Focused handler tests already pass |
| AI-060-EVENT-SYNC | P1 | Codex; Thalia | READY | Per-event checklist for move/attack/summon/hit/ability/death and spell cast/impact; verify ordering, overlap and interrupted destruction visually with audio. | SP2 package |
| AI-082-EVENT-MATRIX | P1 | Codex | READY | Reconcile each runtime card's actual emitted events against clip mappings; list shared tutor sets, fallbacks, six land-place gaps and applicable abilities. Generate only genuinely missing needed cues. | Current audio/catalog |
| AI-082-MIX-ACCEPT | P1 | Mathew; Codex | WAITING | Match listening on laptop speakers/headphones: loudness, repeated cues, simultaneous sounds, spell impact, victory/defeat; log exact keys and selections. Synthetic UI sounds are explicit interim choices. | Mathew present |
| AI-081-PALETTE-PROOF | P1 | Claude candidate; Codex | READY | Compare actual imported texture hashes/renders to approved Zeus sets; resolve stale held labels for capitals/Skyline/Seraph and retain evidence per card. | Conflicting inventory/audit reports |
| AI-081-CANDIDATE-PROMOTE | P1 | Claude candidate; Thalia/Mathew review | READY | Review Keraunos Prime, trimmed Titan, Trident Core and Thunder Ram existing candidates; validate identity/materials/origin/scale, integrate accepted subset into separate increment, compare in game. | Palette proof and candidate verdicts |
| AI-081-STRUCTURES-LOCAL | P1 | Claude candidate; Codex | READY | Choose 3 of 33 procedural structures for bespoke local kitbash pilot, with board-scale/palette/material budgets and source hashes. Review before scaling to all 33. No Meshy jobs. | Reference art and scale contract |
| AI-081-STRUCTURES-FULL | P1 | Claude candidate | BACKLOG | Fill remaining structure presentations after pilot approval; track Abyss Gate interim separately. Queue provider alternatives only if credits become available under latest constraints. | Local pilot |
| AI-105-TUTOR-ART | P2 | Codex; Mathew art review | READY | List 20 tutor IDs with missing runtime art, choose shared family treatment or individual art, stage faces and verify names/stats on each. | Current card-face catalog |
| AI-060-RIG-PILOT | P1 | Claude candidate | READY | Inspect 3 source-rig candidates and repair/import one valid existing rig locally; retain static fallback. Demonstrate walk/attack/summon/hit/death clips at token scale. | Source rig quality audit |
| AI-060-RIG-ROSTER | P1 | Claude candidate | BACKLOG | Build per-card rig/clip plan for 48 characters including 11 non-humanoids; expand accepted pilot in small batches. Verify skinning/clips in Unity, not only source files. | Rig pilot |
| AI-060-SPELL-VFX | P2 | Codex/Claude candidate | READY | Pick 3 of 16 spells for distinct cast/impact VFX, with legal target markers and recognizable faction identity; expand after in-game review. | Event sync |
| AI-060-LAPTOP-BUDGET | P1 | Codex; Thalia | READY | Profile populated-board CPU/GPU/frame time/memory on reference laptop; inspect 12 reported >60K models and test local LOD/decimation with image comparison. Accept against agreed 60fps budget. | Real rendering evidence |
| AI-061-PROVENANCE | P2 | Claude candidate | READY | Consolidate batch 05/06/07 IDs, hashes, imports and local transformations; retain unknown prefix IDs and audit historical spend without resubmitting jobs. | Existing manifests |
| AI-080-BUILD-PROVENANCE | P1 | Codex; Muse review | READY | Pin source commits/overlay config and reproduce SP2 from clean source. Explain packaged JAR 163e2548… vs earlier CI canonical 2db3a12c…; compare expected source/build recipe before concluding tampering. | Current package manifest; remote integration |
| AI-097-UNITY-WIRING | P1 | Codex; Muse reviewer | BACKLOG | Review delivered IC.Net client, wire relay match driver and bridge hash operation, share slot-policy/version handshake and deterministic seed mapping; retain solo mode. | Gameplay acceptance; later networking sprint |
| AI-097-TWO-CLIENT | P1 | Muse candidate; Codex | BACKLOG | Two Unity clients finish a match with identical per-turn hashes, reconnect replay, mismatch refusal and timeout handling; then real separate-network test. | Unity wiring |
| AI-096-DEPLOY-PROOF | P1 | Muse candidate | BACKLOG | Establish current deployed Worker version vs accepted v2 source; verify room pairing/relay bindings and document quota limits. No deployment claimed from unit tests. | Networking sprint |
| AI-107-BOT-IMPROVE | P2 | Codex; Thalia | BACKLOG | Confirm actual packaged engine scoring, design owned overlay anti-oscillation change, compare seeded pacing and legality. Accepted characterization is not a fix. | Source provenance and balance |
| AI-013-WINDOWS-RELEASE | P1 | Codex; Mathew | BACKLOG | Clean-PC install/run/update/rollback, artifact identity, human acceptance and itch.io packaging. Publish only on explicit release instruction. | Gameplay, presentation, network release gates |
| AI-086-ANDROID-PREP | P2 | Codex; Mathew | BACKLOG | Keep touch/safe-area/LOD requirements documented; implement Android after funded authority/server milestone, then full real-device match. | Latest desktop-first funding decision |
| AI-091-CROSSPLAY | P2 | Codex; Muse candidate | BACKLOG | One PC/Android queue, version compatibility, reconnect and identical result/entitlements; Apple later. Split authority migration/protocol/device acceptance. | Funded server and Android |
| AI-024-FACTION-ROADMAP | P2 | Mathew; Codex | BACKLOG | Reconcile 228 future-faction inventory cards, art dependencies, rules and identity briefs; pick first expansion after Season 1 is functional and polished. | Core game acceptance |
| AI-024-STORY-INTEGRATION | P2 | Mathew; Codex | READY | Align Season 1 card/flavor/media briefs with Story Bible + Meta-Lore Annex: mythic perception stays real, meta truth appears through deniable cracks. Draft private double-identity notes; keep computational terms out of mythic text. | Existing ratified lore |
| AI-092-ENTITLEMENTS-PLAN | P2 | Codex; Mathew | BACKLOG | Split account ownership, processor sandbox receipts/webhooks, idempotency, refunds and cross-device restore. Keep cosmetics/achievements later per latest release scope. Verify current store/payment rules when implementation begins. | Release architecture; processor decision |

**Next sequence:** first target/ability/reaction and slot balance evidence; in parallel with available owners, palette/candidate review, local structure and rig pilots, audio event matrix and profiling. Record full human SP2 match. Networking follows in its own sprint; Android and expansion scope stay on the roadmap. No additional worker was dispatched by this reconciliation.

### Human decisions retained without repeating settled questions

HA-003/018: desktop itch.io first; Android after funded server/profitability; Apple later. HA-021 palette settled. HA-011 Skyline silhouette, HA-012 audio sharing, HA-015 balance, HA-019 processor, HA-020 cosmetics and HA-022 historical spend remain unresolved unless newer direct evidence closes them. New slot naming, per-card balance and capital boons need a concrete design draft under AI-108-CARD-BALANCE; ordinary capitals must not silently gain slots.

## How to use this file
- **Pick work** from the Sprint board below: take the highest-priority item in your lane whose status is READY or IN_PROGRESS and is not blocked.
- **When you start** an item, set it to IN_PROGRESS with the date. **When you deliver**, set it to DELIVERED and put the commit, CI run ID and exact commands in `SPRINT_LOG.md`. Only a different worker moves an item to ACCEPTED or DONE (no self-acceptance).
- **Claim before you start (added 2026-10-08 after Codex and Claude both began the same camera fix).** Before editing any code, add or update the item's row with status IN_PROGRESS, the owner, the branch, and the files or folders you will touch, then push that change. Every worker checks this board, and the last 24 hours of `SPRINT_LOG.md`, for an IN_PROGRESS claim on the same files before starting. If someone else holds the claim, ask Mathew or the claim owner instead of editing in parallel. A brand-new ask from Mathew gets a row before work starts, even when it is small. Release the claim (DELIVERED or BLOCKED) as soon as you stop.
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
| HA-003 | PARTLY DECIDED 2026-09-29: PC + Android first, Apple later, cross-play YES (see AI-091). Still open: min Android version/devices, store channels, Apple timing | AI-013/014/015 final scope |
| HA-004 | Currency and login-reward calendar/eligibility | AI-021/022/023 |
| HA-006 | First expansion roster | AI-024 |
| HA-016 | DECIDED 2026-09-29 (PO delegated "fastest path to full release"): release on a server-authoritative JAVA rules server + Unity thin client (PC + Android); C# port deferred to post-release. See "Release path decision". | AI-083, AI-085, AI-087 |
| HA-017 | DECIDED 2026-09-29: no paid server hosting until the game proves profitable; v1.0 stays on Cloudflare (free tier). | AI-085 |
| HA-018 | PARTLY DECIDED 2026-09-29: PC store = itch.io primary (Steam possible later, not ruled out). Google Play: no account yet, Mathew sets it up closer to the Android release. Open: Steam yes/no + timing; Apple account timing. | AI-013, AI-086 release |
| HA-019 | Payment processor(s) for in-app purchases: Stripe, PayPal, a merchant-of-record (Paddle / Lemon Squeezy / Xsolla) or others. Decide closer to release; see "Payments" note (store-billing rules, $0.99 fee math, tax). | AI-023, AI-092 |
| HA-020 | Cosmetics economy: earn-only (achievements + login rewards) or also sold (packs/individual skins, and at what price)? Earned cosmetics are never removed. | AI-094, AI-092 |

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
| **AI-069** (new, AI-006) | P1 | Muse | DELIVERED 2026-09-29 ~08:15 EDT (`f7e1167`, `c540163`) | `coverage.py` UTF-8 on Windows; Verify run 36565491824 green on ubuntu-latest + windows-latest. |
| **AI-070** (new, AI-006) | P3 | Muse | ACCEPTED 2026-09-29 12:00 (analysis; follow-up in AI-072) | Win-split analysis of AI-066 dumps over 31 seeds (`docs/muse/sprint-02/win-split-analysis.md`, `b1a193d`). Zeus starter wins 28/31 ≈ 90% — deck asymmetry, not seat advantage. |
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
| **AI-075** (new, AI-060b) | P1 | Muse | DELIVERED (awaiting Claude acceptance) | `docs/muse/sprint-02/timeline/timeline.py` (stdlib): AI-066 JSONL + presentation-manifest → per-event cue schedule (start_ms, duration_ms, anim key, sfx key, impact hook for AI-060c) with a default duration table. Golden for seed 42 (235 cues, 87.9 s). Test in verify.yml on both OSes. Commit `a5e159c`. Independent QA 2026-09-29 20:00: `test_timeline.py` 3/3 PASS locally (deterministic, golden match, sequential). CI green for the tip commits not yet recorded. |
| **AI-077** (new, AI-006) | P1 | Muse | DELIVERED (awaiting Claude acceptance) | Manifest cues: summon→deploy, death→destroy, plus signature for rarity-4. `coverage.py` resolves assets recursively under staging (prefers `picks/`, accepts .wav/.mp3, `meshy/<batch>/<id>.glb`). Regenerated: 139 cards, 617 event mappings; cue_set deploy/move/attack/hit/destroy/ability/idle/signature. Commit `55557ae`. Independent QA 2026-09-29 20:00: `test_coverage.py` 2/2 OK; manifest structure sane. Acceptance: Claude's PC run reports 29 model / 18 audio cards. |
| **AI-074** (new, AI-006) | P2 | Muse | DELIVERED (awaiting Claude acceptance) | Make `run-balance.sh` portable (classpath separator, loop `continue`, draw parse, UTF-8 `open()`), and add a 5-seed × 3-mode balance step to linux-packaging.yml. Commit `fb4b57c`. Local: 15 dumps (5 seeds × 3 modes), all AI-062 VALID (base 5-0 Zeus, swap 3-2 Zeus, mirror 2-3). Independent QA 2026-09-29 20:00: diff reviewed — PATH_SEP via uname, `continue` outside `$(...)` with explicit seed increment, draw-safe winner regex, explicit UTF-8 opens; all four fixes correct. |
| **AI-078** (new, AI-006) | P3 | Muse | DELIVERED (awaiting Claude acceptance) | Reproducible jar (fixed timestamps), and stop rewriting the tracked `CHECKSUMS.sha256` during builds. Two builds must give the same SHA-256. Commit `0eed0dd`. build-release.sh: Python zipfile with fixed 2026-01-01 timestamps, sorted entries, deterministic metadata; CHECKSUMS.sha256 no longer copied to tracked dir. Independent QA 2026-09-29 20:00: jarring step re-executed twice on a sample tree → byte-identical SHA-256 (MATCH). Full two-build CI evidence pending. |
| **AI-076** (new, AI-012) | P3 | Muse | DELIVERED (awaiting Claude acceptance) | Balance-options memo (coin flip vs Poseidon starter tweaks, expected effect from the AI-072 numbers). Docs only → HA-015 for Mathew. |
| AI-061 Meshy | P1 | Claude — Meshy thread | READY | Stage batch-04 GLBs, a 5-land pilot, then 30 lands (existing credits only). |
| AI-061 ElevenLabs | P1 | Claude — ElevenLabs thread | READY | Land cue sets for the 35 lands; picks named `<card_id>_<cue>.wav` (deploy/destroy). |
| AI-052-ASSET / AI-030 / AI-060b | P0 | Claude — Unity thread | READY | Batch-02/04 import at AI-063 scale, TokenPreview sheet (HA-009), JSONL playback prototype on a `claude/unity-*` branch. |
| AI-065 / AI-046 | P1 | Claude | WAITING — needs Mathew present | Deep-path break-mode rerun. |

## 2026-09-29 ~17:45 — Product Owner direction: 3D playtest build (Claude, covering Astra)
**Mathew, direct (2026-09-29 ~17:40 EDT):** start the Meshy, ElevenLabs and Unity threads. ElevenLabs work is **green-lit** and continues without a per-batch gate. Asset production continues on both animatable 3D models and their sound effects. The board must be ready, with stand-in assets for lands, structures, spells and characters, **so playtesting of the 3D playable version can start.** HA-009 steers batches but no longer blocks them. Existing credits only, no purchases (unchanged).

**New sprint goal (supersedes the IC-S02 goal):** a Zeus-vs-Poseidon match playable in UnityProof on the hex board against the pinned alpha rules. Every card is shown (a real model where one is staged, a typed stand-in otherwise), with sound on deploy/move/attack/hit/destroy.

| ID | Pri | Owner | Status | Next action / acceptance |
|---|---|---|---|---|
| **AI-079** (new, AI-003/AI-060) | P0 | Muse (Rune) | DELIVERED (awaiting Claude acceptance) | Headless rules bridge `releases/alpha-0.7.15-playable/tools/rules-bridge/`: Java against the pinned jar, line-delimited JSON over stdin/stdout (`new` / `legal` / `act`), emits AI-062 wire events + state (hands, GP, per-hex stacks), bot seat auto-plays, illegal ids return an error. Scripted test validating every event, linux-packaging.yml step, protocol in README. **Ahead of AI-075.** Protocol v1.0.0: id/op requests, id/ok/revision responses, revision-scoped action ids (rN-aM), INVALID_ACTION without mutation, redacted state (opponent hand/deck counts only), bot auto-play, JSONL-only stdout. |
| **AI-080** (new, AI-060/AI-017) | P0 | Claude — Unity thread | IN_PROGRESS | Playtest build: hex board polished (tile highlight, legal-move markers, stack offsets), a stand-in catalog for all 139 cards by type (LAND hex slab with faction tint, STRUCTURE prism, CHARACTER capsule + faction color + name plate, CAPITAL tower, SPELL VFX burst), real GLBs swapped in where staged, SFX from `picks/`, and an event playback driver. Then it connects to the AI-079 bridge for real play. Acceptance: Mathew plays a full match in a Windows build. |
| **AI-081** (new, AI-061/AI-019) | P1 | Claude — Meshy thread | IN_PROGRESS | Animatable models: rig humanoid CHARACTER models (Meshy auto-rig/animate) with idle/walk/attack/hit/death clips and export FBX/GLB with animations. Non-humanoids stay static (Unity tweens). Plus the AI-067 lands (local `batch-05-lands`), then the remaining structures. |
| **AI-082** (new, AI-061/AI-020) | P1 | Claude — ElevenLabs thread | IN_PROGRESS (green-lit) | Full cue sets (deploy/move/attack/hit/destroy/ability/idle, + signature for apex) for every card that has a model or is in the lands batch, then the rest of the roster by SFX group. Picks go in `picks/<card_id>_<cue>.wav`. |
| AI-075, AI-077, AI-074, AI-078, AI-076 | P1–P3 | Muse | DELIVERED (awaiting Claude acceptance) | Queue complete 2026-09-29 ~18:40–18:49 (commits `a5e159c`/`55557ae`/`fb4b57c`/`0eed0dd`/`45ea213`). AI-079 CI green on `1ab13f2` (Linux packaging `36640973532`, Verify `36640973498`); CI runs for the later tip commits not yet recorded. |

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


## Long-term goal: PC + Android with seamless cross-play matchmaking (Product Owner, 2026-09-29 ~18:20 EDT)
**Mathew, direct:** a major long-term goal is for Infinite Conquest to be playable on **PC and Android**, with **Apple (iOS, possibly macOS) later**. Every platform must be able to **matchmake seamlessly with every other**: one player pool, and a PC player can be matched against an Android player without noticing any difference.

This partly settles **HA-003**: the platform order is PC (Windows) first with Android alongside, then Apple later, and cross-play is **yes**. Still open under HA-003: minimum Android version and reference devices, the store channels (Steam / itch / direct for PC; Google Play), and the Apple timing.

What it means for existing items:
- **AI-083/AI-085** (C# rules core shared by client and server) are now **required**, not just recommended. A JVM can't run on Android or iOS, and cross-play needs one authoritative rules engine that every client matches exactly.
- **AI-086** mobile targets **Android first**; iOS becomes a later child (AI-086-IOS) once an Apple developer account exists (a purchase → Product Owner).
- **AI-008/AI-015** matchmaking and cross-device identity must be platform-neutral from the start. Nothing in the protocol, identity or deck format may be tied to one OS.

### AI-091 — Cross-platform matchmaking and cross-play (epic; children AI-008, AI-010, AI-015, AI-085, AI-086)
P0 (long-term) | BACKLOG | Owner: Astra (from 2026-10-04) | Dependencies: AI-083 parity, AI-085 server, HA-003 remainder, HA-017 hosting.
- **One pool:** a single matchmaking queue across PC and Android (later Apple), with ranked and casual queues and a skill rating from AI-011. Platform is metadata only, never a queue split. Optional input-based filtering can come later if touch vs mouse turns out to matter competitively.
- **Version gate:** client and server share a protocol version and a content hash (cards.json, AI-088). Mismatched clients get a clear "update required" message rather than a desync. The rules release cadence is identical across platforms.
- **Identity:** one account across devices (AI-015). Guest play is upgradeable to a linked account. Decks, ownership (AI-021) and ratings follow the player, not the device. Platform logins (Google Play Games; Steam; Apple later) link to the same account.
- **Fairness:** all rules run server-side (AI-085), and hidden state is redacted per player. Turn timers suit touch play, and reconnect/resume (AI-009) covers mobile backgrounding and network changes.
- **UX parity:** the same board readability and actions on mouse and touch (no hover-only information; AI-014). Cross-play is on by default.
- **Acceptance:** a Windows PC and an Android phone on different networks find each other through the queue, play a full ranked match on the authoritative server, and both record the same result exactly once. A version-mismatched client is refused cleanly. A mobile player who backgrounds the app for 60 s mid-turn reconnects to the same state. Repeat for Apple when it's added.


### 2026-09-29 ~18:30 — Store channels (Product Owner)
Mathew: **itch.io is the primary PC channel**. Steam is not ruled out for later. **Google Play:** there is no developer account yet; Mathew creates it closer to the Android release (a one-time fee, his purchase). Effects: AI-013 targets an itch.io release (a zip/installer build, uploaded with the butler CLI through a CI job once the account is linked; no Steam SDK dependency for now, and identity must not assume Steam). AI-086 keeps producing Android builds for internal testing (sideload APK/AAB) until the Play account exists. AI-091's platform logins start with our own account + Google Play Games; Steam login is added if Steam is adopted. HA-018 is updated.

### 2026-09-29 ~18:40 — Payments for a free-to-play game (Product Owner consideration)
Mathew: the game is free, so in-app purchases (the AI-023 $0.99 four-faction unlock and later packs) need a payment processor. Candidates: **Stripe, PayPal**, or others chosen closer to release (HA-019). Not a current-sprint item. Captured so the architecture leaves room for it.

Things the decision must account for (verify against current primary sources at decision time; policies change):
- **Store billing rules:** apps distributed through Google Play (and Apple's App Store later) are generally required to use the store's own billing for digital goods. Alternative/external payment options exist only in some regions and under specific programs. So the Android build most likely needs **Google Play Billing** on Play, and Stripe/PayPal covers PC (itch.io/direct) and web. itch.io provides no in-game purchase API of its own.
- **Micro-price fees:** on $0.99, a typical card rate of ~2.9% + $0.30 takes about a third. PayPal's micropayment pricing or a merchant-of-record may net more; compare the net-per-sale at decision time. Bundling (e.g. a full four-faction pack at a higher price) also changes the math (HA-004).
- **Sales tax / VAT:** selling worldwide means collecting and remitting VAT/GST. A **merchant of record** (Paddle, Lemon Squeezy, Xsolla, or similar) takes that on for a higher fee. Direct Stripe/PayPal leaves it with Mathew (Stripe Tax helps but doesn't remit everywhere).
- **Processor-agnostic entitlements:** whatever the processor, purchases are verified **server-side** (webhook or receipt validation) and granted as durable entitlements on the player's account (AI-021). This way a purchase made on PC is owned on Android and vice versa (AI-091 cross-play), refunds/chargebacks revoke cleanly, and no client can unlock factions itself.

### AI-092 — Payment integration layer (child of AI-021/AI-023)
P2 (long-term) | BACKLOG | Owner: Astra | Dependencies: AI-085 server, AI-021 entitlements, HA-019 processor, HA-004 prices, HA-018 stores.
A server-side purchase service with one interface and pluggable providers: Google Play Billing (Android on Play), and Stripe and/or PayPal or a merchant-of-record for PC/web (Apple IAP later). Webhook/receipt verification, idempotent grants, refund/chargeback revocation, restore purchases, and an audit log. Sandbox/test mode only until release, with no real-money transactions without the Product Owner. Acceptance: in each provider's sandbox, a purchase grants the entitlement exactly once across PC and Android, a refund revokes it, a duplicate webhook is harmless, and a forged client unlock fails.

## 2026-09-29 ~19:15 — Product Owner playtest feedback: board tiles + player cosmetics
**Mathew watched the Unity playtest build (`claude/unity-playtest-20260929` @ `f38f5e1`) live:** he loves the assets and the overall look. The stand-in board tiles are too bland, and the game needs real tile textures. This opens a player-cosmetics line:
- **Board tile styles:** the player picks a tile texture style. **Each player's style shows on their own half of the board only** (the 4×6 hex board splits into two 3-row home halves, one per seat). The opponent sees your style on your half.
- **Coin-flip coin:** one 3D coin model with several texture variants. The player's chosen coin is used in the turn-order coin flip. There is no coin flip in the engine yet: AI-072 found that seat 0 always goes first. That's AI-076/HA-015, and it fits here.
- **Card backs:** selectable designs, the same idea.
- **Unlock sources:** achievements, login rewards (AI-022) and possibly purchases (HA-020). They're cosmetic only: no gameplay effect, ever.

### AI-093 — Textured board tiles v1 for the playtest (child of AI-080/AI-060)
P1 | READY → Claude Unity thread | Dependencies: none.
Replace the bland stand-in tiles with 3–4 textured hex tile styles in the techno-futuristic myth look (e.g. Zeus storm-marble with glowing circuit inlays, Poseidon abyssal coral-metal, neutral obsidian grid, bronze-and-energy). Use procedural shaders/materials first (free); Meshy text-to-texture can refine later with existing credits. Add a per-seat tile style selection in the playtest build (each seat's 3-row home half uses that seat's style; the playtest menu lets you pick each side). Tiles must stay readable: hover/legal markers and stacks clear on every style, and land tokens (top ≤ 0.25) must still sit well on them. Acceptance: Mathew picks styles for each half in the Windows build, and screenshots go into the review sheet.

### AI-094 — Player cosmetics system: tile styles, coins, card backs (epic; children AI-021, AI-022, AI-095)
P2 (long-term) | BACKLOG | Owner: Astra (from 2026-10-04) | Dependencies: AI-021 entitlements, AI-085 server, AI-091 cross-play, HA-020.
- **Content model:** cosmetic types TILE_STYLE, COIN, CARD_BACK (extensible later: board edge, capital skin, emotes). Each has an ID, rarity, a texture/material set on a **shared mesh** (one coin model, one hex tile mesh, one card-back mesh), and an unlock source (default / achievement / login reward / purchase / event).
- **Loadout:** the player equips one of each. The loadout is stored on the account (AI-015) and synced across PC and Android (AI-091). At match start the server sends both loadouts; each client renders seat A's tiles on A's half and B's on B's, both coins in the coin flip, and card backs per player.
- **Ownership:** cosmetics are entitlements (AI-021), granted server-side only. A client can't equip what it doesn't own, and it falls back to the default if the server doesn't confirm.
- **Production:** textures from Meshy text-to-texture / Unity shaders on the shared meshes. A few default styles are free for everyone. There's a performance budget for mobile (AI-086): texture sizes and one material per half.
- **Acceptance:** two accounts with different loadouts on PC vs Android each see the correct style on the correct half, the correct coins in the flip and the correct card backs. An unowned cosmetic can't be equipped via a tampered client.

### AI-095 — Achievements (child of AI-094/AI-011)
P2 (long-term) | BACKLOG | Owner: Astra/Muse | Dependencies: AI-085 (server-verified match results), AI-011.
Server-evaluated achievements computed from the authoritative match event log (AI-062 events). Examples: win with each faction, destroy a capital with a spell, 10 matches, first ranked win. Rewards are cosmetics (AI-094). Progress is shown in the client and can't be granted by the client. Acceptance: an achievement unlocks exactly once from a real server-recorded match, and its cosmetic appears in the loadout on both platforms.

Also linked: **AI-022** login rewards now include cosmetics as reward options (alongside the faction unlock), and **AI-076/HA-015** (the turn-order coin flip) becomes the in-game coin-flip moment that uses the COIN cosmetic.

## 2026-09-29 ~19:25 — Release path decision (Product Owner delegated: "choose the path that gets us to full release fastest")
**Decision (Claude, under Mathew's delegation; Astra reviews on 2026-10-04):** ship v1.0 as a **server-authoritative Java rules server + Unity thin client** on PC (itch.io) and Android. **Don't port the rules to C# before release.**

Why this is faster than the "Long-term architecture goals" plan written at ~18:10 (which it supersedes on ordering):
- The rules already exist, tested and pinned in Java (TubaExperiment 992bc95; AI-066/072/079). A C# port plus event-for-event conformance is weeks of work that ships no new player-visible value.
- Cross-play needs an authoritative server anyway (AI-085/AI-091). If the server runs the Java engine, **no client runs rules at all**. The Unity client only sends intents and renders events, so Android needs no JVM and PC and Android are identical by construction.
- The AI-079 bridge protocol (new/legal/act → AI-062 events + state) *is* the thin-client protocol. Moving it from stdin/stdout to a WebSocket with auth and hidden-state redaction is the server, which removes a whole integration layer.
- Bots run on the server too, so practice vs AI works on every platform.

Trade-offs accepted: v1.0 needs a connection for every mode (no offline play) and hosting from launch (HA-017, a small JVM service; turn-based traffic is light). Offline play, on-device AI and the C# core move post-release.

**Re-sequenced critical path to v1.0:**
1. **AI-079** bridge (Muse, in progress) → **AI-080** live human seat in Unity via the bridge (local playtests).
2. **AI-085 (now P0)** game server: the Java engine behind a WebSocket, using the same message shapes as AI-079. It adds auth, hidden-state redaction, turn timers, reconnect (AI-009) and server-side bots. Owner: Muse (server code in its lane under `releases/`/`server/`) with Claude/Astra integration.
3. **AI-091/AI-008** one cross-play queue (casual + ranked) + **AI-010/AI-011** trusted results and leaderboards on that server.
4. **AI-086** Android build (Unity, touch UI, mobile LOD) + **AI-013** PC itch.io build. Sideload testing until the Play account exists.
5. Content: AI-061 models + SFX for all Zeus/Poseidon cards (stand-ins stay only until each card's real asset lands), AI-093 textured tiles, AI-060 impact polish.
6. Monetization for v1.0 kept minimal: AI-021 entitlements + AI-023 unlock via **Google Play Billing (Android)** and **one merchant-of-record for PC** (fastest: it handles global VAT/tax; final pick HA-019). AI-094 cosmetics at launch = a few free default tile styles/coins/card backs plus the loadout. Achievements (AI-095), login-reward cosmetics (AI-022) and cosmetic sales ship in a post-launch update.
7. AI-005/AI-006 security and release gates, AI-026 release validation, then v1.0.

**Post-release (re-prioritized to P3):** AI-083 C# core, AI-084 conformance harness, AI-087 in-process client core (offline mode), AI-090 large-scale sims (the Java EventDump harness covers balance until then), iOS/Apple.

**Other delegated calls made now:** (a) HA-009 first question: **keep batch-04** for Zeus units present in both batches (the newest style, some rigged); batch-02 stays as an alternative. (b) The Unity playtest branch is merged into the working branch once AI-093 lands and CI is green (Claude does it; no PR to main). (c) The playtest build is kept at `Infinite Conquest\playtest\unity-build-<date>\`.

### 2026-09-29 ~19:40 — Reuse the alpha matchmaking service (Product Owner question → release path)
The alpha's online service (`prototypes/lobby-lab/upstream/worker.js`, deployed Cloudflare Worker + KV, free tier; drift probe 200 OK at 15:00) is a **rendezvous + Elo service only**: lobbies (`/lobbies`), a quick-match queue (`/queue`, `/queue/poll`, `/pair`), results (`/report`, applied only when both clients agree) and `/leaderboard`, `/rating/:uuid`. Matches themselves are **player-hosted**: the host's game exposes a `wss://` tunnel URL, and the Worker never sees game traffic.

**Decision: keep the Worker as v1.0's matchmaking front door** (saves building AI-008 from scratch, costs $0, already live), with these changes:
1. **Pairing hands out a game-server room, not a player tunnel.** When two tickets pair, the Worker asks the AI-085 game server for a room (or mints a signed room token) and gives both players that `wssUrl`. No player hosts, so Android-vs-Android works and nobody runs the authority on their own machine.
2. **Results come from the game server, not client agreement.** `/report` accepts only a server-signed result (shared secret/HMAC), and the two-client agreement path is retired for ranked (AI-010).
3. **Identity:** random UUIDs are replaced/linked to real accounts (AI-015) before ranked launches.
4. **Scale watch:** KV is eventually consistent, and the list-based queue scan can double-pair under load. That's fine for launch-scale traffic. Move the queue to a Durable Object (still Cloudflare) if concurrent queue size or double pairings show up in monitoring (AI-025).
5. The Java game server itself can't run on Cloudflare Workers. It needs a small JVM host (HA-017).
New child **AI-096** (P0, Muse lane `prototypes/lobby-lab/` → deploy with Mathew): Worker v2 with room assignment, signed results and a `dataVersion` gate, keeping the existing endpoints backward compatible for the 2D alpha during transition. Tests in lobby-lab (`npm test`).

## 2026-09-29 ~20:00 — Product Owner decision: desktop-first on Cloudflare, no paid server until profitable
**Mathew, direct:** paid server hosting isn't affordable yet. v1.0 **stays online on Cloudflare (free tier), desktop only** (itch.io), until the game proves profitable. **Then** invest in a real game server and **release the Android app after that.** This supersedes the ~19:25 "Release path decision" on hosting and platform order. Its other calls (batch-04 kept, merge the playtest branch after CI, minimal monetization at launch) stand.

**Architecture for v1.0 (zero hosting cost): deterministic lockstep, relayed by Cloudflare.**
- **Each player's PC runs the rules.** The desktop build bundles the pinned Java engine behind the AI-079 rules bridge (a trimmed Java runtime via `jlink`, about 40–60 MB, invisible to the player). The engine is deterministic (AI-066/AI-072: same seed → byte-identical events on Linux and Windows).
- **Only intents travel.** Both clients start from the same match seed and send each other the chosen action ids. Each applies them locally and gets the same state. After every turn the clients exchange a **state hash**; a mismatch flags the match (desync or tampering).
- **Relay, not tunnels.** A Cloudflare Worker + **Durable Object** per match room relays intent messages over WebSockets. There's no player hosting, no port forwarding and no tunnel setup, and it stays inside Cloudflare's free tier at launch scale (verify current free-tier limits for Workers/Durable Objects at build time). This replaces the alpha's player-hosted `wss://` tunnel model.
- **Matchmaking:** the existing alpha Worker (AI-096) pairs players and hands both the relay room. **Results** use the existing two-client agreement plus matching final state hashes. Disagreements are flagged, as today.
- **Known trade-off (accepted for v1.0):** in lockstep every client holds the full match state, so a modified client could reveal the opponent's hand or deck order. Mitigations for launch: hands are dealt from a shared seed commit-reveal so neither side can pick its draws, the state-hash checks catch rule-breaking, flagged matches don't count toward ranked, and ranked is labelled "beta" until the server phase. True hidden information needs the authoritative server (AI-085) in phase 2.
- Bots (practice vs AI) run locally on the player's PC. **Offline practice mode comes free** with this design.

**Phases:**
1. **v1.0 desktop (itch.io), $0 hosting:** Unity client + bundled Java engine, Cloudflare matchmaking (AI-096) + lockstep relay (**AI-097**), casual + beta-ranked, the full Zeus/Poseidon asset set, textured tiles (AI-093), default cosmetics, Stripe/PayPal/merchant-of-record purchases for PC (HA-019; a Cloudflare Worker can receive the payment webhooks and record entitlements in KV/D1, keeping this free).
2. **When revenue justifies it (profitability gate, Mathew's call):** authoritative game server (AI-085) running the same Java engine, which gives real hidden information, trusted ranked and accounts (AI-010/AI-015).
3. **Android app (AI-086)** as a thin client of that server, with cross-play via one pool (AI-091), Google Play account + Play Billing then. Apple later.
4. **C# core (AI-083 ff.):** only if on-device rules for mobile/offline become worth it. It stays post-release.

Re-prioritized: **AI-097 (new) P0**, AI-096 P0, AI-080/AI-093 P0/P1 for v1.0. AI-085 and AI-086 move to phase 2/3 (P2). AI-091 stays the long-term goal (phase 3).

### AI-097 — Lockstep match relay on Cloudflare + client lockstep (child of AI-008/AI-091)
P0 | READY | Owner: Muse (Worker/Durable Object in `prototypes/lobby-lab/`, plus the bridge protocol additions) with the Claude Unity thread (client side) | Dependencies: AI-079 (delivered, pending acceptance), AI-096.
- **Worker:** `GET /rooms/:id/ws` upgrades to a WebSocket held by one Durable Object per room. It relays `{seq, seat, actionId}` messages in order, persists the intent log (enabling reconnect and replays, AI-089), handles turn timers/forfeit on timeout and exchanges per-turn state hashes. Room tokens come from the AI-096 pairing.
- **Bridge:** add `{"cmd":"hash"}` (a canonical state hash) and seeded match setup that both clients share (a seed commit-reveal, so neither player controls the shuffle).
- **Unity:** a match mode where the local seat's actions go to both the local bridge and the relay, and remote actions from the relay are applied to the local bridge.
- **Acceptance:** two Windows PCs on different networks play a full match through the relay with matching hashes every turn. A tampered client is detected at the next hash. A client that disconnects for 60 s reconnects and resumes from the intent log. Everything runs on the Cloudflare free tier.

## 2026-09-30 00:00 — Claude (covering Astra): board update
Review: `docs/reviews/2026-09-30-0000-claude-acceptance.md`. This section supersedes the AI-074..079 rows above.

| ID | Pri | Owner | Status | Next action / acceptance |
|---|---|---|---|---|
| AI-079 | P0 | Muse | **ACCEPTED** 2026-09-30 00:00 (protocol v1.0.0; CI 36640973532 / 36641738516 step 12) | Integration: Claude Unity thread wires BridgeClient (AI-080). |
| AI-075 | P1 | Muse | **ACCEPTED** (Verify 36641390414, both OSes) | — |
| AI-074 | P2 | Muse | **ACCEPTED** (Linux packaging 36641738516 step 13) | — |
| AI-076 | P3 | Muse | **ACCEPTED** (docs) → HA-015 | Optional: fix the swap-mode wording and the C#-core assumption (the shipped engine is the Java jar; a coin flip can be done bridge-side). |
| AI-077 | P1 | Muse | **REJECTED** — Windows Verify red since `55557ae` | Fix via AI-098. |
| AI-078 | P3 | Muse | **REJECTED** — Linux clean regress red (36641800847) | Fix via AI-099. |
| **AI-099** (new, AI-006) | P1 | Muse | **DELIVERED** 2026-09-30 02:00 (commits `9c67fec`..`b2fd139`) — acceptance pending 06:00 review | Shared `tools/make-repro-jar.py` (fixed timestamps/sorted entries/fixed mode bits/LF manifest/forward slashes, deduped manifest); both build scripts use it, both regress scripts verify against this build's staging checksum, tracked `CHECKSUMS.sha256` deliberately updated to proven hash `2db3a12c…bae86b` (two local builds byte-identical; `--break=checksum` fails with own detail). Linux/Windows packaging green pending CI. |
| **AI-098** (new, AI-006) | P1 | Muse | **DELIVERED** 2026-09-30 02:00 (commit `3723fd6`) — acceptance pending 06:00 review | `find_model()`/`find_sfx()` now return `os.path.relpath(...).replace(os.sep, "/")` — POSIX on Windows, no-op on Linux. Local test_coverage 2/2. Verify windows-latest green pending CI. |
| AI-096 | P0 | Muse | READY, after AI-099/098 | Worker v2 per the ~19:40 section. |
| AI-097 | P0 | Muse + Claude Unity thread | READY, after AI-096 | Per the 20:00 PO section. |

Process: record every CI run ID with its conclusion; fetch before each root-record edit (third stale overwrite on 2026-09-29).

## 2026-09-30 06:00 — Claude (covering Astra): board update
Review: `docs/reviews/2026-09-30-0600-claude-acceptance.md`. This section supersedes earlier rows for these IDs.

| ID | P | Owner | State | Next |
|---|---|---|---|---|
| AI-098 | P1 | Muse | **ACCEPTED** | Verify green on both OSes (36681380841, 36681414338, 36687085629). |
| AI-099 | P1 | Muse | **REJECTED (partial)**; Linux half accepted | Windows jar `18998415…` ≠ canonical `2db3a12c…`, so `play.bat` refuses a fresh Windows build. Fix via AI-100. |
| **AI-100** (AI-006) | P1 | Muse | **DELIVERED** 2026-09-30 ~08:30 EDT (acceptance pending 12:00 review — do NOT self-accept) | `make-repro-jar.py` normalises **all** manifest CRLF→LF plus text-resource (`.json`/`.txt`/`.md`/…) CRLF→LF — the second delta Claude flagged beyond the manifest (16 git-checkout text resources). windows-packaging now **asserts** jar hash == tracked `CHECKSUMS.sha256` (was print-only); new `play.bat --check-only` proves the gate accepts a fresh Windows build without launching the GUI; `test_make_repro_jar.py` 5/5 (fails 3/5 on the old code) runs in verify.yml. Evidence: local full build from pin `992bc95` → `2db3a12c…` (canonical, unchanged — fix is a no-op on LF inputs); same tree with all text resources + manifest converted to CRLF (simulated Windows checkout) → **identical `2db3a12c…`** with the new tool, `7557b17c…` with the old tool (reproduces the `18998415…` failure mode). 23 entries differed under the old tool (manifest + 22 text resources incl. Jackson pom files). Acceptance: Windows + Linux packaging green with equal asserted hashes. |
| AI-076 | P3 | Muse | ACCEPTED (corrections `1ec13ee`) | → HA-015 (Mathew). |
| AI-096 | P0 | Muse | READY, after AI-100 | Worker v2. |
| AI-097 | P0 | Muse + Claude Unity thread | READY, after AI-096 | Per the 20:00 PO section. |

## 2026-09-30 12:00 — Claude (covering Astra): board update
Review: `docs/reviews/2026-09-30-1200-claude-acceptance.md`. This section supersedes earlier rows for these IDs. Lane owners follow the local 07:25 EDT working meeting: ChatGPT owns AI-080/AI-082, Claude owns AI-081, and Muse owns QA.

| ID | P | Owner | State | Next |
|---|---|---|---|---|
| AI-100 | P1 | Muse | **DELIVERED** 2026-09-30 ~14:45 EDT (acceptance pending 18:00 review — do NOT self-accept) | Rework complete. Root cause: CPython `ZipInfo.create_system` defaults 0 on Windows / 3 on POSIX (2-byte OS fingerprint per entry header); pinned to 3 in `make-repro-jar.py` (`b1199b6`). Per-entry diff tool `diff-jar-entries.py` proves the residue: old-Windows simulation shows header-only `create_system` diffs on every entry, fixed packer yields byte-identical jars from LF/CRLF sources. Fixed two WinError 32 regressions in test helpers (unclosed `mkstemp` fd; `0912185`, `874ea3a`). Evidence at `874ea3a`: Linux packaging [#38](https://github.com/Mrice90/3DTuba/actions/runs/36758602542) success (asserts canonical `2db3a12c…`), Windows packaging [#71](https://github.com/Mrice90/3DTuba/actions/runs/36758602750) success (asserts canonical; `play.bat --check-only` green), Verify [#213](https://github.com/Mrice90/3DTuba/actions/runs/36758602937) success both OSes. Full run ledger: `docs/muse/sprint-02/ai-100-ci-ledger.md` (AI-101). |
| **AI-101** (new, AI-006) | P2 | Muse | **DELIVERED** 2026-09-30 ~15:00 EDT (process rule; acceptance with Claude's 18:00 review) | Full CI run ledger `docs/muse/sprint-02/ai-100-ci-ledger.md` (`7bae8ac`): every run at every tested head with URL/ID, workflow, head SHA and conclusion — including red (`18178ba` #63, `3105c1a` #64 packaging failures), in-progress, and failure reasons. The rule now lives as standing record in the ledger header: a delivery entry must list every CI run at its head with its conclusion; an item whose own acceptance CI is red stays IN_PROGRESS, not DELIVERED. |
| **AI-102** (new, AI-006) | P3 | Muse | **DELIVERED** 2026-09-30 ~15:00 EDT (acceptance with Claude's 18:00 review) | `actions/setup-python@v5` → `@v6` in `.github/workflows/windows-packaging.yml:36` (with AI-102 comment; the Node-20 deprecation warning is gone). Shipped in the 14:00 rework commits; independently verified at tip `3bf9641`: Verify #214 (run `36759079743`) **success** on ubuntu-24.04 and windows-latest, which exercises the bumped action. |
| AI-080 | P0 | ChatGPT | READY FOR HUMAN TEST (not accepted) | Push `chatgpt/unity-playable-20260930` (`47c4a15`) to GitHub for independent review. Mathew plays one full mouse-driven match. |
| AI-081 | P1 | Claude (Meshy) | HOLD | Waits on the Zeus palette (HA-021) and the concurrent-queue reconciliation. Meshy is at 1,754. |
| AI-096 | P0 | Muse | READY, after AI-100 | Worker v2. |
| AI-097 | P0 | Muse + ChatGPT Unity | READY, after AI-096 | Per the 20:00 PO section. |
| **HA-021** (new) | — | Mathew | OPEN | Choose the replacement Zeus palette. Black-and-gold is reserved for Hades. |

**12:05 correction (Claude covering Astra):** HA-021 is **CLOSED**. The PO's binding Zeus palette is now recorded in `docs/production/ASSET_QUEUE.md` and `docs/production/ZEUS_COLOR_DIRECTION.md` (local, ~12:00 EDT): dominant white, secondary blue, restrained gold accents, with Desolate-Tuba art as the style reference (read-only). The AI-081 Zeus retexture is no longer blocked on a palette choice. Per that record, Meshy provider execution for it is owned by Mathew, so this checkpoint submits nothing.

## 2026-09-30 18:00 — Claude (covering Astra): board update

Review: `docs/reviews/2026-09-30-1800-claude-acceptance.md`.

| ID | Pri | Owner | State | Next / evidence |
|---|---|---|---|---|
| AI-100 (AI-006) | P1 | Muse | **ACCEPTED** 2026-09-30 18:00 | Windows #72 `36776423170` + Linux #39 `36776423099` @ `a04a0a4`. Independent dispatch @ `e6c519e`: Windows #73 `36782436578`, Linux #40 `36782526494`. All green; canonical `2db3a12c…` unchanged. |
| AI-101 (AI-006) | P2 | Muse | **ACCEPTED** (corrections in AI-103) | Ledger `7bae8ac`/`de2a534`. Fix: `36759079743` = Verify #215 (not #214); add Verify #214 `36758717297` @ `7bae8ac`. |
| AI-102 (AI-006) | P3 | Muse | **ACCEPTED** | setup-python@v6 at `windows-packaging.yml:36`, `verify.yml:19`. |
| **AI-103** (new, AI-006) | P3 | Muse | **DELIVERED** 2026-09-30 ~21:45 EDT (acceptance pending Claude's 00:00 review — do NOT self-accept) | `actions/upload-artifact@v4` → **@v6** in both packaging workflows (commits `5dbdeab`/`f406dd8`, corrected `efa3f4d`/`26aed61` — first attempt used @v5, but @v5's action.yml still declares `node20`; verified @v6 declares `node24` before republishing). Ledger corrections at `f5e5c61`: `36759079743` relabeled #214 → #215, Verify #214 `36758717297` @ `7bae8ac` (success) added. CI: Linux packaging #41 `36799609034` @ `efa3f4d` success, Windows packaging #74 `36799617757` @ `26aed61` success — zero Node 20 annotations on either run (annotation scan over all packaging check runs). |
| AI-096 | P0 | Muse | **DELIVERED** 2026-09-30 ~20:45 EDT (acceptance pending Claude's 00:00 review — do NOT self-accept) | Worker v2 in `prototypes/lobby-lab/`: `worker-v2.js` (`e1c3fe2`) routes `/v2/*` — server-side room assignment (`POST /v2/rooms/pair`, atomic over v1 queue tickets), signed results (`POST /v2/results` → HMAC receipt, `POST /v2/results/verify`), dataVersion gate (min `lab-2`); all other paths delegated to the pinned v1 worker verbatim (2D alpha unaffected). `server.js` (`51c17cf`) wires the router + `LAB_V2_SECRET`/`LAB_V2_DATAVERSION_MIN`; 13 new tests (`a73bf3f`) — full suite **56/56 green**; design + trust doc (`82de7e0`), api.md v2 section (`8eb1f88`), README (`657c45e`). Upstream `worker.js` SHA-256 unchanged. Relay/rendezvous stays AI-097's scope. |
| AI-097 | — | Muse | READY after AI-096/AI-103 | — |
| AI-046-WIN-ACCEPT | P1 | Mathew | WAITING — needs Mathew present | Local `play.bat` run on a fresh Windows build. CI now proves the gate. |
| AI-081 | — | Claude Meshy thread | HOLD | Attribute the Meshy 1,734 → 1,369 (−365) spend and the 10 new Zeus groups. The Seraph import proof waits for Mathew. |
| HA-022 (new) | — | Mathew | **CLOSED** 2026-10-01 ~21:55 EDT (evidence: repo SPRINT_LOG.md @ `ad1597e`) | Mathew confirmed directly (main chat, 2026-09-30 ~21:31 EDT) that he green-lit the 365-credit Meshy spend behind the 10 new Zeus model groups — authorized spend, not unattributed. |
| AI-055 / AI-056 / AI-057 / AI-048 | P1/P2/P3/— | — | OPEN | Unchanged. |

## Sprint IC-S03 plan (2026-10-01 22:15 → 2026-10-04 08:12 EDT) — Claude (covering Astra)
Standup and planning run 2026-10-01 ~22:15 EDT after Mathew's PC restart. This section supersedes earlier rows for these IDs. The sprint ends when Astra returns (2026-10-04 08:12 EDT); the sprint review is the first meeting after that.

**Sprint goal:** spend the rest of this month's Meshy credits on finished, board-ready Zeus/Poseidon assets (all 35 lands on exact hex bases, plus the missing Poseidon humans), give every delivered model and land its sound set in ElevenLabs, and start the AI-097 lockstep relay now that AI-096 is accepted.

**Standup (state at 22:15 EDT)**
- Meshy lane ("Meshy asset development 2" thread): all 30 remaining lands generated and textured in Meshy 6 Lite (600 credits); 35 lands mid-way through hybrid processing onto Blender hex bases; 7 of 13 missing Poseidon humans generated from Desolate Tuba art. About 179 credits left at the last report (21:36). The PC shut off mid-turn; the thread has resumed.
- ElevenLabs lane ("ElevenLabs sound effects" thread): restarted with a full audit of existing audio against what the game needs. Last recorded balance 123,239.
- Muse (Rune): AI-096 and AI-103 delivered 2026-09-30 and waiting on review (the 00:00 review never ran). Verified tonight, see below.
- ChatGPT: `chatgpt/unity-playable-20260930` is still not on GitHub (checked `git ls-remote` 22:15).
- Repo tip `ceea269`: Verify #240 `36953407019` success.

**Acceptances tonight**
- **AI-096 ACCEPTED** (Worker v2): independent `npm test` in `prototypes/lobby-lab` at `ceea269` — 56/56 pass, 0 fail. Acceptance is on tests and the design doc; the deployed-worker step still waits for Mathew.
- **AI-103 ACCEPTED**: `actions/upload-artifact@v6` at `linux-packaging.yml:111` and `windows-packaging.yml:64`; Linux packaging #42 `36799609034` and Windows packaging #75 `36799617757` success.

| ID | Pri | Owner | State | Sprint commitment / acceptance |
|---|---|---|---|---|
| AI-081 / AI-061 Meshy lands | P0 | Claude ("Meshy asset development 2") | IN_PROGRESS | Finish hybrid hex-base processing for all 35 lands; render on the hex board; review sheet to Mathew. Acceptance: every land fits one hex at board scale (AI-063 budget) and matches its faction palette. |
| AI-081 Poseidon humans | P0 | Claude ("Meshy asset development 2") | IN_PROGRESS (7/13 generated) | Generate the other 6 from Desolate Tuba art, texture, download, normalize, render, review sheet. Neo-futuristic: energy weapons, no bows. |
| AI-081 credit burn-down | P1 | Claude (Meshy thread) | READY after the humans | Spend whatever remains on the next highest-value gaps in this order: (1) Skyfather Archon and Eagle of the High Grid (last Zeus holds), (2) any Zeus/Poseidon card still without a model, (3) Zeus retextures toward more gold. Then HOLD all Meshy work until next month's refill. No top-ups. |
| AI-020 / AI-061 SFX coverage | P0 | Claude ("ElevenLabs sound effects") | IN_PROGRESS | Audit staged audio vs the AI-064 presentation manifest; generate missing deploy/move/attack/hit/destroy cues for every delivered model, plus land-placement cues for the 35 lands; stage with a manifest. Acceptance: coverage report shows no Zeus/Poseidon card missing a cue; Mathew listen-through. |
| AI-064 coverage report | P1 | Claude | READY | Run `coverage.py` against `assets/staging/` once the new models and cues are staged; post the first real coverage report. |
| AI-097 | P0 | Muse (Rune) | **IN_PROGRESS 2026-10-01 ~23:00 EDT** — first sprint deliverable shipped | Design doc `prototypes/lobby-lab/docs/relay-design.md` (`5f695b3`): relay-1 protocol (seed commit-reveal, server-sequenced intents, per-turn hash exchange, turn timers/forfeit, reconnect resume), trust model (no auth theater — seat UUIDs are bearer tokens; lockstep hidden-info tradeoff accepted for v1.0 with hash-flagging mitigations). `relay.js` (`4d8a805`): Workers-compatible relay core — `createRelaySession` state machine + `MatchRoom` Durable Object class + `createRelayRouter` for `GET /rooms/:id/ws`. Lab shim: `ws-shim.js` (`b0b8bf6`, stdlib server-side WebSocket) wired into `server.js` (`b70d410`) with loopback guards and the dataVersion gate on the upgrade path; `docs/api.md` relay section (`7e20d3b`). Tests `test/relay.test.js` (`b32cabf`): 16 new (13 session unit + 3 real-WebSocket integration incl. two-seat flow and 60 s-style reconnect resume); full suite **72/72 green**. Second deliverable DELIVERED 2026-10-02 ~02:00 EDT (rules-bridge protocol v1.1.0, additive): `{"op":"hash"}` returns SHA-256 over canonical JSON of the full unredacted state (seed/turn/phase/active_player/winner, both players' GP, full hand/deck/discard with card+instance identity and all mutable per-card state, board cells sorted x,y with ordered stacks); stdlib canonicalizer (sorted keys, compact); read-only — no state/revision mutation, NO_MATCH before `new`. Determinism basis verified against pinned 992bc95 sources (read-only): seed-derived shuffle, `nameUUIDFromBytes` instance ids, seeded bot RNGs. test_bridge.py now **11/11 properties** (was 8/8): hash basics + read-only + sensitivity; two independent processes, same seed → identical hash; same seed + same intents → identical hash sequence, divergent intent → divergent hash (shared prefix identical). Golden fixture byte-identical; local run against canonical jar 2db3a12c; covered by linux-packaging.yml AI-079 step on the pinned build. Third deliverable DELIVERED 2026-10-02 ~21:30 EDT: IC.Net relay-1 client library on `claude/unity-live-match` (`f49a1c0`, 19 files) — `UnityProof/Assets/Scripts/IC.Net/` (netstandard2.1/C#9, zero deps: RelayClient + transport seam + DTOs + JsonLite + README with PlaytestGame wiring sketch) and `UnityProof/IC.Net.Tests/` (17 xUnit unit tests + gated two-client lockstep test vs lab relay + 2 bridges). CI: verify.yml runs `dotnet test`; new icnet-lockstep.yml builds the jar and runs the lockstep test. Local verification 47/47 green via console runner (`dotnet test` blocked in sandbox). Next: Unity-lane wiring into PlaytestGame (Claude Unity thread) + independent acceptance. |
| AI-080 | P0 | ChatGPT | READY FOR HUMAN TEST | Push `chatgpt/unity-playable-20260930` to GitHub so it can be reviewed; then Mathew plays one full mouse-driven match. |
| AI-093 | P1 | Claude + ChatGPT Unity | READY after lands | Swap the new land GLBs into the playtest board as textured tiles. |
| AI-055 / AI-056 / AI-057 / AI-048 | — | Muse | OPEN | Carry over; no new commitment this sprint. |

**Land tiles APPROVED by Mathew 2026-10-01 ~22:20 EDT ("the land is fine i aprove").**

**Needs Mathew (when present):** AI-046-WIN-ACCEPT local `play.bat` run; AI-080/AI-030 mouse-driven full match; AI-065 deep-path rerun; Seraph Unity import proof; Poseidon-human review sheet; SFX listen-through. Open decisions: HA-011, HA-012, HA-015, HA-003 remainder, HA-018..020.

**Process:** one owner per in-progress item (no duplicate Muse runs); fetch before every edit to this file; watch for double-spend if another session uses the Meshy account.

### 2026-10-01 ~22:30 EDT — PO playtest of the Unity build (recording: project files `Screen Recording 2026-10-01 222456.mp4`)
Mathew ran `playtest\unity-build-2026-09-30`. It played the recorded seed-42 bot-vs-bot match to "Zeus wins" (turn 14). Findings:

| ID | Pri | Owner | State | Finding / acceptance |
|---|---|---|---|---|
| **AI-104** (new, AI-080) | P0 | Unity lane (ChatGPT; Claude if the branch reaches GitHub) | READY | **Ghost token: a destroyed unit reappears and shares a hex with an enemy.** The rules forbid this (`MovementRules.passability`: an enemy Character is BLOCKED; README "Friendly Characters may share a stack"). Cause is in the replay, not the rules: in `dump-seed-42.jsonl` the engine emits `CARD_DESTROYED` (seq 63) for Arc Relay Scout *before* its `CHARACTER_MOVED 3,1→3,4` (seq 64), because the opportunity attack kills it mid-move. The client removes the token, then the move event puts it back on 3,4 next to Tidepool Surveyor. Same pattern at seq 180–182. Fix: once an instance is destroyed, ignore later move/damage events for it (or play the move first, then the death). Acceptance: seed-42 replay never shows two enemy units on one hex; add a check to `-playtestSmoke`. |
| **AI-105** (new, AI-080) | P0 | Unity lane | READY | **Show the cards.** Without a visible hand and card faces the match is hard to follow. Need: own hand along the bottom with card art/cost/stats, hover or right-click for a full card view (as in the 2D alpha), and a card pop-up when anything is played. |
| **AI-106** (new, AI-080/AI-079) | P0 | Unity lane | READY | **Live human-vs-bot match.** The build only replays a recorded bot match, so the PO mouse-driven acceptance (AI-080) is not possible yet. Wire the human seat to the accepted AI-079 rules bridge. |
| AI-107 (new, AI-007) | P3 | Muse | **DELIVERED 2026-10-02 ~17:45 EDT** — root cause + characterization, no behavior change | Quirk is general bot behavior, not Surveyor-specific: 25 A→B→A oscillations across 4 units on both sides in the seed-42 dump (both Surveyors 22, Skyline Seer 2, Stormgate Sentinel 1). Root cause in pinned alpha (read-only, `TubaExperiment@992bc95`): `BotPlayer.score()` gives every `move` a flat 35 regardless of destination (`BotPlayer.java:322`); ties broken by reverse-lexicographic command string (`ranked()`, lines 275–278); `score()` is pure `(state, command)` with no position memory; `end` scores 0 so any legal move wins. `docs/muse/sprint-02/ai-107-bot-pacing/`: analysis doc with file:line citations + fix design for the future engine (destination-aware scoring and/or anti-oscillation memory), `test_pacing.py` 4/4 green characterizing the exact pattern (wired into verify.yml). Not fixed in the pinned alpha or the bridge (bridge must stay a faithful engine). |
| **AI-108** (new) | P3 | Mathew (design) | READY | **Structure summon slots (core rules balancing).** Proposal: structures gain limited summon capacity — each character summoned through a structure consumes slots from it. Higher-tier structures have more slots; higher-level or larger characters cost multiple slots per summon. Naming convention to be workshopped (candidates: conduit slots, anchor points, tribute slots). **Faction-identity extension:** the slot economy can differentiate future factions — e.g. a necromancer faction that gains slots from battlefield wreckage, a machine faction whose structures are themselves the units. Slot economy as faction identity lever, not just a balance knob. Design-phase only — no alpha rules change (TubaExperiment read-only). Informs AI-012 balance and AI-007 rules architecture for the 3D engine. |

## 2026-10-02 ~20:45 EDT — standup + IC-S03 replan (Claude, covering Astra; Rune via Muse)
Mathew asked for a standup with Muse, sprint planning, and continued work toward launch. Sprint window unchanged (ends 2026-10-04 08:12 EDT). This section supersedes earlier rows for these IDs.

**Standup.** Rune (Muse): AI-097 infra side done (relay core 72/72, bridge `op:hash` 11/11); AI-107 delivered 17:45. Accepted the replan below and is building the C# relay client (written reply still pending at time of writing). Claude: took the Unity lane, which had no active owner because `chatgpt/unity-playable-20260930` never reached GitHub. Root cause of the 10-01 playtest showing only a replay: the exe only went live when `IC_BRIDGE_CMD` was set by the `.bat`; the player log shows no bridge attempt, so the exe was launched directly.

| ID | Pri | Owner | State | Evidence / next |
|---|---|---|---|---|
| **Unity lane branch** | — | Claude | **PUSHED** | ChatGPT's local Unity work (`47c4a15`, `71aa98b`) is now on GitHub as `claude/unity-live-match`, merged with this branch at `4c09913` (`fa92c6a`, clean merge). |
| AI-106 | P0 | Claude | **DELIVERED** 2026-10-02 ~20:40 (acceptance = Mathew's mouse match) | `c86e23b`: exe finds `Bridge/` (jar + RulesBridge classes) and Java 17 (JAVA_HOME, then PATH) and starts live human-vs-bot by default; `-playback` forces the recording; missing Java/Bridge falls back with an on-screen reason. Verified: one live turn played by mouse (Arc Relay Scout to 1,1; bot attacked, destroyed it and hit the Zeus capital 20→19). Bridge classes rebuilt from v1.1.0 source. |
| AI-104 | P0 | Claude | **DELIVERED** (needs review) | `c86e23b`: events for an instance after its `CARD_DESTROYED` are skipped (6 skipped in seed 42). `-playtestSmoke` gained "no hex ever holds Characters of both seats" — PASS, 0 violations. |
| AI-105 | P0 | Claude | **DELIVERED** (needs Mathew's look) | `c86e23b`: `Tools/stage_card_faces.py` stages 143 card faces (cost/stats/keywords/rules text) and art for 119 from the pinned alpha jar (read-only). Hand along the bottom (playable cards bright), full card view on hover (hand or board piece), centre pop-up for every card played. Smoke check added. 20 engine-generated tutor cards have name/type only. |
| AI-080 | P0 | Mathew | READY FOR HUMAN TEST | New build: `playtest\unity-build-2026-10-02\` (local; double-click the exe). Smoke PASS (11 checks). |
| AI-097 client | P0 | Rune (relay client, `UnityProof/Assets/Scripts/IC.Net/` only) → Claude (Unity wiring) | IN_PROGRESS | Rune: pure C# `RelayClient` (relay-1 over ClientWebSocket: join, commit-reveal seed, intents, hash exchange, resume) + headless two-client lockstep tests. Claude wires it into PlaytestGame after it lands. Unity's existing `BridgeClient.cs` stays; Rune does not edit `UnityProof/Assets/Playtest/`. |
| AI-093 | P1 | Claude | NEXT | Swap the approved land GLBs into the board as textured tiles. |
| AI-107 review | P3 | Claude | NEXT | Review Rune's 17:45 delivery. |
| AI-081 / AI-020 asset lanes | P0 | Claude asset threads | UNCHANGED | No asset-thread activity seen this evening; rows from the 22:15 plan stand. |

## 2026-10-02 ~20:10 EDT — IC-S03 task assignments (Claude, covering Astra; Mathew: "assign tasks")
One owner per item. Every lane fetches this file before writing. Credits: existing Meshy/ElevenLabs balances only, no purchases.

| Owner | Item | Pri | Assignment | Acceptance |
|---|---|---|---|---|
| Claude (Unity) | AI-093 | P1 | Put the 35 approved hybrid land GLBs (`assets/staging/meshy/batch-06-lands-hybrid/hybrid/`) on the playtest board as textured tiles under each placed land. | Every land card shows its own tile at board scale; smoke stays green. |
| Claude (Unity) | AI-097 wiring | P0 | Wire Rune's IC.Net relay client into PlaytestGame for a two-human online match once it lands. | Two local clients finish a match with matching per-turn hashes. |
| Claude (review) | AI-107 | P3 | Review Rune's bot-pacing analysis. | ACCEPT/REJECT row on this board. |
| Claude (Meshy) | AI-081 Poseidon humans | P0 | Download the 13 batch-07 Poseidon humans (already generated and textured, already paid), normalize, check_glb, stage into the playtest catalog, review sheet. | 13 models load in the build; sheet to Mathew. |
| Claude (Meshy) | AI-081 burn-down | P1 | Then spend what is left this month per the 22:15 order (Skyfather Archon, Eagle of the High Grid, other model-less Zeus/Poseidon cards). Hold at zero; no top-ups. | Credits reconciled in the sprint log. |
| Claude (ElevenLabs) | AI-020 / AI-061 | P0 | Deploy/move/attack/hit/destroy cues for every model without card-specific SFX (13 Poseidon humans first, then the Zeus pilots), plus a placement cue per land. Stage with a manifest. | AI-064 coverage report shows no Zeus/Poseidon card missing a cue; Mathew listen-through. |
| Rune (Muse) | AI-097 client | P0 | C# relay-1 client in `UnityProof/Assets/Scripts/IC.Net/` + headless two-client lockstep tests (dotnet, CI). README on how PlaytestGame calls it. Do not edit `UnityProof/Assets/Playtest/`. | Tests green in CI; Claude accepts. |
| Rune (Muse) | QA | P1 | **DELIVERED 2026-10-02 ~20:00 EDT** — independent QA of `claude/unity-live-match` `c86e23b` (branch tip `f49a1c0` at review; the 19 IC.Net commits sit on top of the QA target untouched). | QA note `docs/muse/sprint-02/qa-ai-104-105-106/qa-note.md`. **No defects found — no new rows.** AI-104: ghost-token skip guard reviewed against every instance-acting `Apply()` case — complete; smoke checks (`EnemyShareViolations == 0` hex scan + `GhostEventsSkipped > 0` tripwire) sound. AI-105: 143 faces staged, 119 with art, all 119 JPGs verified present against the JSON art paths; `CardFaces.Get` never throws. AI-106: `DiscoverBridge` logic reviewed (Windows-only `java.exe` lookup matches the exe target; honest playback fallback with on-screen reason). Bridge classes rebuilt from v1.1.0 source against canonical jar `2db3a12c…`: `test_bridge.py` **11/11 PASS** (matches "rebuilt from v1.1.0 source" claim). `-playtestSmoke` not executable in this sandbox (needs Unity/Windows) — the 11-check PASS claim is recorded but unverified here. Acceptance stays: AI-104/105 review → Claude's NEXT row; AI-106 → Mathew's mouse match (AI-080). |
| ChatGPT | AI-082 | P1 | Continue unique-card audio mapping from 51/139 using the approved staged pool and any new ElevenLabs cues; deliver as a mapping file on a branch or a doc Claude can commit. | Coverage count up, generic fallback preserved. |
| ChatGPT | Review | P1 | Code review of `claude/unity-live-match` (its own playable work, now on GitHub) and the player-facing playtest README. | Review notes; defects as rows. |
| Mathew | AI-080 / HA-013 | P0 | Play `playtest\unity-build-2026-10-02\InfiniteConquestPlaytest.exe` for one full mouse match; note anything confusing. Open decisions: HA-011, HA-012, HA-015. | Pass/fail recorded. |

**20:15 correction (Mathew: ChatGPT is out of tokens for a while).** ChatGPT gets no IC-S03 work. AI-082 unique-audio mapping moves to Claude (ElevenLabs lane, folded into AI-020/AI-061). The `claude/unity-live-match` code review moves to Rune's QA row. Also noted: Rune's IC.Net relay-1 client already landed on `claude/unity-live-match` (board `a0e3dbf`); Claude reviews and wires it next.

## 2026-10-08 ~00:55 EDT — backlog sweep + assignments (Claude Code, sprint IC-2026-10-07-PLAYTEST-01)
Supplements local `reviews/2026-10-07-sprint-plan.md` (local); does not supersede its rows. No human match has been recorded since 10-02 (Player.log last written 2026-10-02 20:27), so SP1-HUMAN (Mathew) still gates SP1-FIX, SP1-MODELS promotion, SP1-AUDIO production and SP1-REVIEW. Baseline `playtest/unity-build-2026-10-02/` stays frozen.

**Open items (summary):** SP1-HUMAN baseline match (Mathew, P0); SP1-FIX (Codex, waits on match); SP1-MODELS (Claude, promotion waits on verdict); SP1-AUDIO (Codex, waits on verdict); SP1-REVIEW (Thalia + Mathew); batch 05/06/07 provenance gaps; AI-107 review (Claude, P3); AI-097 Unity wiring (out of scope this sprint: no networking expansion); HA-011/012/015/022 decisions; 33 structure stand-ins need new Meshy jobs (credits exhausted, deferred).

| ID | Pri | Owner | State | Assignment / acceptance |
|---|---|---|---|---|
| **SP1-PROV** (new, AI-061) | P1 | Claude Code | IN_PROGRESS 2026-10-08 | Write `MANIFEST.md` + `SHA256SUMS.txt` for Meshy batches 06 (lands hybrid) and 07 (Poseidon humans); record full task IDs where the files hold them, mark the rest unknown. Local files only, no Meshy jobs. Acceptance: Thalia review. |
| **SP1-MODELS-PREP** (child of SP1-MODELS) | P1 | Claude Code | IN_PROGRESS 2026-10-08 | Run `check_glb.py --require-materials`, hashes and renders on the 3 candidates (Keraunos Prime, Olympian Storm Titan, Poseidon's Trident Core) and hash the imported Seraph FBX; evidence in a separate candidate folder. No baseline change; no promotion before the baseline verdict. |
| AI-107 review | P3 | Claude Code | READY | Review Rune's bot-pacing analysis; ACCEPT/REJECT row. |
| **SP1-INV-REVIEW** (new) | P1 | Muse (Thalia) | ASSIGNED 2026-10-08 ~00:50, acknowledged ("Review SP1-INVENTORY report" task started) | Formal independent review of local `reviews/2026-10-07-claude-model-inventory.md` (file attached in the Muse main chat, since it was missing from her workspace). Verdict ACCEPT / ACCEPT-WITH-CORRECTIONS / REJECT with numbered corrections and unverifiable claims. Evidence: local `reviews/2026-10-08-thalia-sp1-inv-review-assignment.jpg`. |
| **SP1-AUDIO-PLAN** (new, child of SP1-AUDIO / AI-082) | P1 | ChatGPT | ASSIGNED 2026-10-08 ~00:53, working (chatgpt.com chat "Create audio cue plan") | Per-card ElevenLabs cue plan for the 88 cards without card-specific SFX (prompt, duration, loop, priority, credit estimate). Plan only: no generation, credits or build changes. Brief: local `reviews/2026-10-08-chatgpt-sp1-audio-cueplan-brief.md`. Reviewer: Thalia. Evidence: local `reviews/2026-10-08-chatgpt-sp1-audio-plan-assignment.jpg`. |
| SP1-HUMAN | P0 | Mathew | WAITING — needs Mathew present | Play `playtest/unity-build-2026-10-02/PLAY-INFINITE-CONQUEST.bat` live to the result screen (checklist: Thalia's `unity-playtest-checklist.md`). |

**2026-10-08 ~01:40 EDT updates (Claude Code):**
- **SP1-PROV DELIVERED** (needs Thalia review): `3DTuba/assets/staging/meshy/batch-06-lands-hybrid/MANIFEST.md` + `SHA256SUMS.txt` (93 files; all 30 generate + texture IDs in full; the hybrid hex-base step is now documented as `docs/production/tools/hybrid_land.py`). `batch-07-poseidon-humans-lite/MANIFEST.md` + `SHA256SUMS.txt` (14 files; 12 of 13 texture IDs are prefixes only). New finding: the 13 batch-07 humans are **not remeshed** (50K–568K tris each vs ~30K elsewhere), a laptop frame-rate risk worth watching in Mathew's match.
- **SP1-MODELS-PREP DELIVERED** (not promoted): local `playtest/sp1-models-candidates-2026-10-08/` holds token FBX + maps for Keraunos Prime, Olympian Storm Titan and Poseidon's Trident Core, with `README.md` and `SHA256SUMS.txt`. check_glb: height and materials PASS; feet_at_origin FAIL by design (fixed by the FBX conversion). Flag: Olympian Storm Titan carries a trident-like spear (Poseidon emblem). Seraph: imported textures are byte-identical to the white/blue/gold pilot set; the FBX bytes differ (re-export).
- **AI-107 ACCEPTED** (Claude Code): citations confirmed at `992bc95`; review local `reviews/2026-10-08-ai-107-review.md`. Note: the later alpha source in `playtest/build-kit/build/alpha-src/` already has a destination-aware `advanceScore` for moves.
- **SP1-AUDIO-PLAN DELIVERED** (ChatGPT, ~01:00 EDT; needs Thalia review): `sp1-audio-cue-plan.md` in the chatgpt.com chat "Create audio cue plan" (https://chatgpt.com/c/6ac6e9fa-dc30-83ea-b734-15ad6b194baf). Covers all 88 cards, 243 cue rows; 4 shared tutor sets. Budget: 178 unique cues / 356 generations required, up to 203 / 406 if every structure has an ability (33 ability rows are conditional, pending a card-data check). No audio generated. Saved locally as `reviews/2026-10-08-sp1-audio-cue-plan.md`.
- SP1-INV-REVIEW (Thalia): in progress at ~01:45 EDT; no verdict yet.
- **AI-082-POOL** (new, child of AI-082/SP1-AUDIO) | P1 | ChatGPT/Codex (local, with file access) | **ASSIGNED, NOT STARTED** | Map the staged ElevenLabs pool (`3DTuba/assets/staging/elevenlabs/`) to the 88 generic-fallback cards, and list which of the 33 structures have abilities. Brief: local `reviews/codex-2026-10-08/BRIEF.md`; outputs go only in that folder. A headless `codex exec` run from Claude failed: the Windows sandbox helper errors outside the app, and the unsandboxed retry was blocked by Claude's permission settings. It needs to be started in the Codex desktop app.
- **SP1-INVENTORY ACCEPTED WITH CORRECTIONS** (Thalia, 2026-10-08 ~01:10 EDT, relayed by Mathew): the per-faction structure labels were swapped (Zeus 17, Poseidon 16; totals unchanged). Fixed in local `reviews/2026-10-07-claude-model-inventory.md`. Thalia could not verify file-level claims from her workspace. The batch 06/07 manifests and candidate evidence are sent to her next.
- **SP1-PROV-REVIEW + SP1-MODELS-PREP-REVIEW** (new) | P1 | Muse (Thalia) | ASSIGNED 2026-10-08 ~01:18 EDT, working | The batch 06/07 manifests + checksums, the candidate README + checksums, and the 04d/04b review sheets were uploaded to the Muse main chat. She was asked for a verdict per item, a palette-gate judgment, and a call on the Titan spear readability.
- **SP1-PROV ACCEPTED** and **SP1-MODELS-PREP ACCEPTED** (Thalia, 2026-10-08 ~01:24 EDT, script-verified; relayed by Mathew). Palette gate PASS for both Zeus candidates. **Defect for the promotion gate (Mathew):** Olympian Storm Titan carries a clearly three-pronged spear, which is the Poseidon emblem and a faction-readability risk. Promotion is still blocked on the baseline match.
- **SP1-TITAN-TRIM** (Claude Code, Mathew chose "trim", 2026-10-08 ~01:40 EDT) **DELIVERED**: the side tines of the Titan spear were removed in Blender (300 faces). The local edit and re-made FBX are in local `playtest/sp1-models-candidates-2026-10-08/zeus_apex_olympian_storm_titan/`, with the original in `untrimmed/`. Before/after renders are in the same folder. No Meshy credits used. **SP1-TITAN-TRIM-REVIEW** assigned to Thalia, working.

**2026-10-08 ~02:30 EDT (Claude Code, asset-library thread):**
- **ASSET-GAPS DELIVERED**: `docs/reviews/2026-10-08-asset-library-gaps.md` + `.csv` cover 367 rows: the 139 runtime cards plus 228 cards from the other four factions. Of the 139: 75 final, 4 candidates already downloaded, 11 interim (off-style/held), 33 structure stand-ins and 16 spells (effect only). All 48 characters lack rigs/clips; 12 batch-07 humans are over 60K tris. Estimated Meshy cost: P0 1,190, P1 1,180, P2 105, P3 0, P4 8,260.
- **Meshy balance is 9 credits** (cheapest job is 10), so nothing was generated or downloaded. No Meshy tasks in the last 24 h.
- **New zero-credit candidate:** Thunder Ram. The baseline imports the dark AI-052 model, while a compliant white/gold `batch-04b .cleaned.glb` is already staged.
- **Correction to SP1-INVENTORY:** the imported Zeus capitals and Skyline Seer textures are white/blue, not held black/gold.
- **Next:** P0 needs about 1,190 credits, which is Mathew's decision.
