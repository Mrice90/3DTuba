# SP2 Slot QA — AI-108 Card-Balance Review + AI-080 Covered-Activation QA

**Date:** 2026-10-08
**Lane:** Thalia (QA/docs/evidence review only — no game code, packaging, backlog, or package edits)
**Authority:** Mathew via relay, 2026-10-08. Scope: review the provisional pooled slot policy and write six concrete boundary scenarios for covered land/structure activation. Deliverable: this report (published as `docs/muse/sprint-03/2026-10-08-slot-qa-report.md`).
**Hard limits honored:** no edits to game/shared backlog/packages; no provider spend; no deploy; completed work not restarted; no binary or human acceptance claimed.

## 1. Governing decisions (Mathew, recorded)

1. **Slot system is NEW, not a pinned rule** (2026-10-07 ~22:06). Provisional pooled policy: structures supply 2; ordinary characters cost 1; Olympian Storm Titan and Poseidon Trident Core cost 2; ordinary Capitals 0 (boons may grant later); death/removal frees use; structure loss preserves units and blocks new summons; every summon/burrow path validates before costs/state mutation. **No final balance approval implied.**
2. **Covered activation ruling** (2026-10-07 ~22:24): land abilities stay usable under structures; structure abilities stay usable under characters — "no matter what the stack gets up to." This **supersedes the top-only activation boundary** in the reconciled backlog for the 3D build. Codex owns AI-080-COVERED-ACTIVATION (next P0), including a visible activation affordance. **Does not change spell targeting rules** (top-only targeting stands).
3. **Development philosophy** (2026-10-07): the 3D Unity build is the living game; the 2D engine is a read-only reference. Playtest-driven divergence is expected. QA records each divergence without relitigating it.

## 2. Independently verified reference facts

All below read directly from the read-only 2D reference (`~/workspace/Desolate-Tuba`). The 2D engine has **no slot concept** (AI-108 is design-phase); it is the baseline the 3D build diverges from.

**Card population** (`game-core/src/main/resources/cards/*.json`, deduped by ID): **143 unique cards** — LAND 31, STRUCTURE 29, CHARACTER 61, SPELL 16, CAPITAL 6. (Unity build reports 139; the 4-card delta is unexplained but immaterial to this review.)

**Activated abilities — 7 total** (`trigger == "ACTIVATED"`):
| Card | Type | gpCost | Effect |
|---|---|---|---|
| `zeus_ability_oracle_spire` | STRUCTURE | 2 | DRAW_CARD 1 |
| `poseidon_ability_tidewell` | STRUCTURE | 2 | HEAL_CAPITAL 2 |
| `zeus_eagles_perch_array` | LAND | 3 | DAMAGE_ENEMY_CAPITAL 2 |
| `zeus_storm_relay_pylon` | STRUCTURE | 2 | DAMAGE_ENEMY_CAPITAL 1 |
| `poseidon_palace_of_tides_approach` | LAND | 1 | HEAL_SELF 2 |
| `poseidon_sonar_beacon` | STRUCTURE | 2 | DRAW_CHARACTER 1 |
| `zeus_ion_storm_lattice` | STRUCTURE | 2 | DRAW_CARD 2 |

Note: 2 of the 7 are **lands** — directly in scope of Mathew's "land abilities under structures" ruling.

**Activation reference implementation** (`game-core/src/main/java/com/infiniteconquest/core/GameEngine.java:29-49`):
- `:31-33` — owner + battlefield-zone check → reject "Ability source must be your battlefield card."
- `:34-36` — top-of-stack check → reject "Only the top card of a stack can activate an ability." **This is the exact boundary Mathew's ruling supersedes for LAND and STRUCTURE sources in the 3D build.** (Characters are not covered by the ruling.)
- `:37` — must have an ACTIVATED ability.
- `:38` — `abilityUsedThisTurn()` → reject "Ability already used this turn." Once-per-turn, per card.
- `:39-42` — DAMAGE_ENEMY_CAPITAL effects additionally require line of sight to the enemy capital.
- `:44-47` — GP sufficiency checked **before** `spendGp`, `markAbilityUsed`, and effect resolution. Rejection is atomic: no GP spent, ability not marked used, no effect.
- `:43` — total cost sums `gpCost` across all ACTIVATED abilities on the card.

**Once-per-turn reset** (`CardInstance.java:93-98`): `markAbilityUsed()` sets the flag; `resetTurnActions()` clears it each turn.

**Large-unit card facts:** `zeus_apex_olympian_storm_titan` is CHARACTER, cost 10 GP; `poseidon_poseidons_trident_core` is CHARACTER, cost 7 GP.

## 3. Relayed evidence (labeled — not independently verifiable)

- 70 engine/bridge assertions PASS; baseline protocol 11/11 PASS with capacity disabled (golden preserved).
- 2 slot-enabled bridge matches reached GAME_OVER (seed42: 81 actions/140 summon offers; seed713: 58 actions/136 checks).
- Unity: 14 handler checks PASS; Oracle Spire editor validation 17/17 PASS; player smoke 35 checks PASS; 2 live runs GAME_OVER with 3 mouse-targeted spells total.
- SP2 package: `Infinite Conquest/playtest/unity-build-SP2-gameplay` (`PLAY-INFINITE-CONQUEST.cmd`); SHA256SUMS covers runtime DLL, resources.assets, 3 overlay classes, JAR, bridge, policy, executable.
- Codex drafted **88 card rows** locally for the slot policy (contents not accessible to Thalia).
- Structures-first opening retained per Mathew (HUD/help communication, no Capital slots); slots-0 delayed units on turns 1–3 in observed runs; no deadlock observed, **not guaranteed for all hands** (accepted risk).

## 4. AI-108 card-balance review — numbered findings

**F1. Population the policy applies to (corrected 2026-10-08).** Per Codex: the 88-row draft is **48 Characters + 34 Structures + 6 Capitals** — the two named heavy Characters (Titan, Trident Core) are **included** among the 48, not subtracted from a 90-row total. (An earlier inference that 88 = 90 − 2 was wrong and is withdrawn.) Note the 3D configuration's population differs from the 2D reference (61 Characters, 29 Structures) — this is a **draft of current configuration, not evidence of balanced values**.

**F2. Capacity arithmetic.** Player-wide pooled capacity = 2 × (living controlled structures). A 3-structure board supports 6 ordinary characters or, e.g., 1 Titan (2) + 4 ordinaries (4). The single-structure opening supports exactly 2 ordinaries or 1 heavy — the structures-first rule and the provisional numbers interact to make the **first structure the game's most important early play**.

**F3. Heavy-unit tension is real and intended.** Titan (10 GP) and Trident Core (7 GP) each consume a full structure's supply. At 2 structures (4 slots), fielding both heavies leaves zero room for anything else. This is the "large characters cost more" design working as stated; whether 2 (vs 3, vs scaling with GP cost) is right is **balance judgment reserved for Mathew after human play — explicitly not approved here**.

**F4. No baseline exists to balance against.** The 2D reference has no slot concept, so there is no prior art for "correct" capacity pressure. The provisional 2/1/2/0 is the first stake in the ground. Balance review at this stage can only check **coherence** (findings F1–F3: coherent) — not correctness.

**F5. Structures-first opening risk (accepted).** With capitals at 0 and lands supplying nothing, turns 1–3 field no units until the first structure lands; a structure-less opener fields nothing. Observed runs showed delay but no deadlock; **no-deadlock is not proven for all opening hands**. Mitigation is communication (HUD/help), not mechanics, per Mathew. This is the highest-risk provisional call and should be re-examined after human SP2 matches.

**F6. Burrow consumes a slot.** Burrow (Mole) is a summon path: it validates against capacity **before** costs/mutation like every other path (Mathew-confirmed). A burrow at full capacity is rejected atomically — GP unspent, Mole stays in hand.

**F7. Control change follows the current owner.** Supply/usage is computed from living **controlled** board cards: a stolen/borrowed character counts against its current controller's pool, and stops counting against the previous owner's (Mathew-confirmed). Recommend this be stated verbatim in the design doc.

**F8. Structure loss is graceful by design.** used > total preserves survivors and blocks new summons — no forced sacrifices, no retroactive illegality. Recovery path (play a new structure) is always available through the normal development allowance.

## 5. AI-080 covered-activation QA — six boundary scenarios

Each scenario: setup → action → expected outcome → what it validates. Citations are to the 2D reference behavior being **diverged from** (the ruling changes `:34-36` for LAND/STRUCTURE sources) or **preserved** (all other checks).

**S1. Covered land activates — correct owner, cost paid exactly once.**
Setup: Zeus controls `zeus_eagles_perch_array` (LAND, ACTIVATED, gpCost 3, DAMAGE_ENEMY_CAPITAL 2) with a Zeus structure deployed on top; enemy capital in line of sight; Zeus holds 5 GP.
Action: activate the land's ability.
Expected: **legal** under Mathew's ruling (supersedes GameEngine.java:34-36 for LAND sources). 3 GP spent exactly once (5 → 2); 2 damage to enemy capital; land flagged used this turn (CardInstance.java:93).
Validates: covered lands activatable; cost charged once; effect resolved once.

**S2. Covered structure activates — correct owner.**
Setup: Poseidon controls `poseidon_ability_tidewell` (STRUCTURE, ACTIVATED, gpCost 2, HEAL_CAPITAL 2) with a friendly Poseidon character on top; Poseidon holds 4 GP.
Action: activate Tidewell.
Expected: **legal** under the ruling. 2 GP spent (4 → 2); capital healed 2; structure flagged used.
Validates: covered structures activatable — the exact case Mathew ruled on.

**S3. Enemy's covered structure is not yours — ownership boundary.**
Setup: enemy `zeus_ability_oracle_spire` with an enemy character on top.
Action: you attempt to activate it.
Expected: **rejected** — "Ability source must be your battlefield card" (GameEngine.java:31-33, preserved). No GP spent, no state change, nothing flagged.
Validates: coverage does not leak activation rights across owners; the ruling extends *your* reach, not your opponent's cards.

**S4. Insufficient GP → atomic rejection.**
Setup: your `zeus_ion_storm_lattice` (STRUCTURE, ACTIVATED, gpCost 3, DRAW_CARD 2) under a friendly character; you hold 2 GP.
Action: attempt activation.
Expected: **rejected** — "Not enough GP" (GameEngine.java:44, preserved). GP stays 2; ability **not** flagged used; zero cards drawn. A later attempt at 3 GP succeeds normally.
Validates: validation precedes `spendGp` (:45) and `markAbilityUsed` (:46) — rejection leaves no partial state.

**S5. Once-per-turn — second activation rejected, no double charge.**
Setup: your `zeus_ability_oracle_spire` (covered or not), 10 GP.
Action: activate (2 GP → 8, draw 1, flagged used). Attempt activation again the same turn.
Expected: **rejected** — "Ability already used this turn" (GameEngine.java:38, preserved). GP stays 8; exactly 1 card drawn total. Next turn, `resetTurnActions()` (CardInstance.java:95) clears the flag and activation works again.
Validates: once-per-turn per card; costs and effects happen exactly once per legal activation.

**S6. Controls expose every eligible source — the visible affordance.**
Setup: your board holds (a) a covered land with ACTIVATED (Eagle's Perch under a structure), (b) a covered structure with ACTIVATED (Tidewell under a character), (c) an uncovered structure with ACTIVATED (Sonar Beacon), (d) a structure with no activated ability, (e) a source already used this turn.
Action: invoke the activation affordance (the right-click path Mathew found dead).
Expected: **(a), (b), (c) all offered** — coverage hides nothing; (d) not offered; (e) shown as unavailable with a reason (consistent with Codex's "unavailable-card reasons"). Right-click produces a response on every eligible source.
Validates: closes the exact defect Mathew reported (right-click did nothing); implements the "visible activation affordance" half of AI-080-COVERED-ACTIVATION. Note: this is a **controls requirement**, independent of the engine rule change.

## 6. Access gaps and acceptance limits

- **No access** to 3DTuba-unity-playable source, Codex's 88 drafted rows, any probe/test code, or the SP2 package. All laptop/build assertions in §3 are relayed.
- **2D reference is read-only**: the covered-activation divergence and the slot system cannot be verified against 3D code — only against the 2D rules they diverge from.
- **No binary acceptance** (package not independently inspectable) and **no human acceptance** (Mathew's full SP2 match pending) are claimed in this report.
- The 143-vs-139 card count delta and the 88-row composition (F1) are noted as inferences where marked.

## 7. Handoff

- **Codex (gameplay):** AI-080-COVERED-ACTIVATION implementation — engine rule change for LAND/STRUCTURE sources (S1, S2) + visible affordance exposing all eligible sources (S6). Ownership, atomic rejection, once-per-turn, and line-of-sight checks are preserved behavior (S3–S5).
- **Mathew (design):** provisional slot numbers (F3–F5) await balance judgment after human SP2 play; structures-first no-deadlock risk (F5) is the item to watch.
- **Thalia:** evidence-only verdict retained; will review implementation evidence against scenarios S1–S6 when concrete changes arrive.
