# Shared repository work plan

Updated 2026-09-27 19:15 EDT. Product Owner directs workers to maintain sprint/backlog context in this repository. This file and SPRINT_LOG.md are shared engineering records; local automation records must reconcile them at each checkpoint. They do not override direct Product Owner instructions or grant access by themselves.

Standing authorization: Product Owner directly authorized Muse in its own UI to work with Astra in the designated repos and maintain sprint/backlog records. HA-010 is resolved by that direct message and Muse acceptance. Both TubaExperiment and Desolate-Tuba remain read-only; all changes go to 3DTuba. No paid jobs, purchases or live publication implied.

## Current executable queue (supersedes older item statuses below)
- AI-047 / Astra / REVIEW: Windows and browser lobby fixes at 70d3fac, PR #1. Muse ported behavior, and Astra merged Muse history through 702f602 without conflict. Windows combined validation passed 43/43 and both demos exit 0. Integrated via PR #1 merge 0940062; no release claim.
- AI-045 / Muse; Astra acceptance / REVIEW: local-only binding, Host and browser-origin guards at b3e17a6 with 10 negative tests at 7f85e47; Muse reports 43/43 Linux checks. Astra independently passed 43/43 Windows checks and both demos; merged in PR #1. Guarded browser lifecycle follow-up remains in acceptance queue. Output prototypes/lobby-lab and docs/muse/sprint-01. No production identity claim.
- AI-046 / Muse / REVIEW (Muse delivered, Windows acceptance pending): reproducible Windows/Linux packaging handoff delivered, commits through b514ebf. fetch-source.sh/.bat (pinned read-only fetch @ 992bc95, pin-verified); build-release.sh/.bat (JDK17+/javac/jar/pin diagnostics fail-closed; Jackson 2.18.2 from Maven Central with pinned SHA-256 — not tracked upstream); smoke.sh/.bat (5 checks incl. headless 36-match seeded engine run); play.sh/.bat (honest missing-jar message, checksum auto-verified before launch, tampered jar refused); CHECKSUMS.sha256 (reference 728c3fc1...5149029523), PROVENANCE.md, .gitignore; runbook docs/muse/sprint-01/alpha-build-handoff.md. Linux verified end-to-end from clean fetch (smoke 5/5; all negative diagnostics); rebuild content-equivalent, not byte-identical (fresh 20696b45...f14af). Windows .bat (CRLF) mirror recipe, NOT executed on Windows. No binary published, no formal release, source repos untouched. Evidence: SPRINT_LOG.md 2026-09-27 19:10-19:35 entry.
- AI-048 / Muse / DONE 2026-09-27 evening EDT: bounded AI-004 packaging regression coverage delivered. regress.sh/.bat re-runs fetch->build->smoke in an isolated temp copy and reports REGRESSION: PASS / FAIL at <stage>; verify stage checks the fresh jar against its OWN freshly generated CHECKSUMS.sha256 (never an unrelated reference artifact — rebuilds are content-equivalent, not byte-identical). Intentional-break modes: --break=pin (off-pin checkout must fail the build) and --break=checksum (corrupted jar must fail verification). Commits: c4ae900/c1c61dc (harness), a85d87f (README), 25961dd (exec-bit fix, see below). Evidence: `--break=pin` -> correctly detected at stage 'build' (exit 0); `--break=checksum` -> correctly detected at stage 'verify' (exit 0); clean run -> REGRESSION: PASS, 118 sources, smoke 5/5, jar 2b7c65a6...f017 self-verified (exit 0). Real defect found and fixed by the harness: the four .sh scripts were 100644 in the repo, so a pristine clone could not run ./fetch-source.sh (Permission denied); fixed to 100755 in 25961dd. regress.bat written (CRLF) but NOT executed on Windows — first Windows run is a shakedown. README documents usage. No source writes, no binary publication, no paid jobs. Evidence: SPRINT_LOG.md AI-048 completion entry.
- AI-030 / Astra / IN_PROGRESS / P0: UnityProof isolated 3D movement slice from pinned AI-036 cases; legal adjacent move spends one and enemy structure rejects move without state change. Output UnityProof source, Windows build, runnable scene, fixture checks and runtime smoke evidence. Windows build, eleven assertions and two runtime outcomes pass; rendered board screenshot inspected. Mouse-event acceptance and clean-checkout rebuild remain REVIEW. Only two-case parity, not full engine migration. Do not edit Muse paths concurrently.
- AI-031 / Astra / IN_PROGRESS / P0: integrate reviewed increments, provenance/security/bug checks and reproducible commands; Windows/Linux supporting-tools workflow authored; remote CI evidence pending. Review new failures at each checkpoint.
- AI-037/038 DONE: real 391-row manifest + 29 Python tests and one Node offline smoke independently passed; these are supporting tools.
- AI-027/028/036 REVIEW: published audit, handoff and movement fixture; latest Java execution evidence is in alpha-core-foundation.md.
- AI-043 REVIEW: Muse reports 169/169 Java tests; Astra independent full rerun pending.
- AI-044 PARTIAL: 2D alpha recipe/screenshots and chat JAR attachment; GitHub has no binary release. AI-046 closes clean Windows handoff gaps.
- AI-032/033/034 setup VERIFIED 2026-09-27 evening EDT (Mathew direct): Claude worker, Meshy runner/session, and ElevenLabs access all confirmed working via successful test connections; all cleared for tasking from the 00:00 meeting onward. Mathew will verify generated assets by the 06:00 meeting. Zero remaining human-side setup blockers. Do useful technical prep; no paid connection tests or invented available workers.
- AI-049 DONE 2026-09-27 ~22:15 EDT (Rune): asset prompt directory v1 + final acceptance — 139 cards with Meshy prompts, 21 faction/archetype SFX groups, 32 unique apex-tier SFX briefs, docs/muse/sprint-01/asset-prompts/. Three 21:00 QA nits fixed and directory regenerated from pinned inputs (commits 2bf8c16/a073f91/0ee5803); first generation batches queued in batch-01-queue.md (c752ea5), not submitted.

Next checkpoint 00:00 EDT Sep 28; sprint ends 06:00 EDT. Workers may proceed through their accepted bounded stages after green tests, preserving the last working increment. Record delivery, acknowledgement, execution, acceptance, integration and release separately. Evidence and exact commands belong in SPRINT_LOG.md. No claim of uninterrupted background execution.

## Open decisions
HA-003 target platforms/release order/cross-play; HA-004 currency/login campaign rules; HA-006 expansion roster remain unspecified. Proceed with reversible prototype and inventory work; do not invent commercial choices. HA-005 is agent-side live-service/version verification, no hosting purchase pending. Unity Personal license verified; package metadata warned about sign-in during initialization, but project creation completed. Record a human blocker only if a required operation actually fails.

---

# Infinite Conquest product backlog

Version 1 — 2026-09-27. Product Owner direction is recorded in PRODUCT_LOG.md; evidence and limits are in REPOSITORY_BASELINE.md. This backlog supports the four independent daily meeting runs. Priority is execution order, not a promised delivery date. Split large items into bounded child action IDs before implementation; preserve parent IDs and never recycle IDs.

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

## Recurring checkpoint procedure

At 00:00, 06:00, 12:00 and 18:00 read the compact state/logs and relevant backlog entries. Inspect changes and check results since the last checkpoint, triage AI-005/AI-006 findings, select the highest-priority unblocked work, and record the exact next action. Once CI is configured it checks each covered change. Scheduled meetings do not imply continuous execution or media jobs running in the background.

Keep generation jobs resumable: record provider job ID, input brief/version, expected outputs, current state and review result before polling or retrying. Do not resubmit an uncertain paid job. Keep specialist assignments bounded and tied to action IDs; only assign verified available workers.

## Definition of done

An item is DONE only when its acceptance criteria are met, relevant tests/review pass, integration is verified, and the records include branch/commit/artifact evidence. A draft, generated asset, passing unit test or repository file alone is not a finished feature. RELEASED additionally requires a verified deployed/store/distribution destination. Scope or policy decisions are preserved in PRODUCT_LOG.md; unresolved dependencies stay visible here and in HUMAN_ACTIONS.md.



