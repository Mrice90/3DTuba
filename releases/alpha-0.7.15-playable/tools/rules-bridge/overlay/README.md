# 3DTuba rules overlay

Rule changes for Infinite Conquest 3D, layered over the pinned alpha engine
(TubaExperiment `992bc95`, read-only). The files under `src/` are copies of the
alpha classes with small, commented changes, plus two new classes. They are
compiled together with `RulesBridge.java` and put **before** the alpha jar on
the classpath, so they replace the jar's copies of those classes. The alpha
repository is never modified.

A match picks its rules in the bridge `new` request:

| `rules` | Behaviour |
|---|---|
| `alpha` (default) | Exactly the pinned alpha rules. Verified identical to the unmodified engine: same events, legal actions and state hashes over 30 seeded matches at all three bot levels. CI and the AI-097 lockstep test use this. |
| `ic3d` | The 3D game's rules below. The Unity client sends this (override with `-rules alpha`). |

## What `ic3d` changes

**Summon slots** (backlog AI-108). Every friendly Structure and Capital has summon
slots. A Character can only be summoned (or burrowed) on or next to one that has
enough free slots, and it holds those slots while it stays on the battlefield.
Slots free up when the Character is destroyed or returned to hand; losing the
Structure does not remove the Characters it summoned. All numbers are in
`core/SummonSlots.java`:

| Anchor | Slots |
|---|---|
| Capital | 3 |
| Structure cost 0-1 | 1 |
| Structure cost 2-3 | 2 |
| Structure cost 4-5 | 3 |
| Structure cost 6+ | 4 |

| Character cost | Slots it takes |
|---|---|
| 0-3 | 1 |
| 4-6 | 2 |
| 7+ | 3 |

When several anchors qualify, the one on the destination hex pays first, then the
one with the most free slots (deterministic, for lockstep play).

**Covered Structures.** In the alpha only the top card of a stack can be targeted
or activate. With `ic3d`:

- Spells that damage or repair a Permanent (Skybreaker Bolt, Erode Foundation,
  Restorative Tide) can also target a Structure or Capital under other cards. A
  lethal hit destroys it and the cards above settle down onto the stack.
- A Land, Structure or Capital covered only by its owner's cards can still use
  its activated ability (once per turn, GP cost as printed).
- A Permanent at 0 HP that becomes the top card because its cover moved away
  is destroyed. The alpha only checked this when the cover was destroyed, so a
  wrecked Structure could survive a Blink or move off it (found by the rules
  audit).

Covered targets use one extra token in the engine command (`cast <hand> <x> <y>
<card-id>`, `activate <x> <y> <card-id>`). The bridge reports them with
`"covered": true` and, for casts, `target_card_id`.

## Files

| File | Change |
|---|---|
| `core/HouseRules.java` | New: the two switches. |
| `core/SummonSlots.java` | New: slot capacity, cost, usage and anchor choice. |
| `core/GameState.java` | Summon-slot ledger (copied with the state for bot lookahead). |
| `core/GameEngine.java` | Slot check on summon/burrow; covered spell targets and activations; wreck check after each action. |
| `core/BoardState.java` | A covered card may leave the middle of its stack. |
| `cli/ActionHints.java` | Legal-action list follows the same rules. |
| `cli/CommandProcessor.java` | Parses the optional card id. |
| `cli/BotPlayer.java` | Scores a covered activation by the card that fires. |

## Build and check

    python build_classes.py --jar <alpha jar> --out <build>/Bridge/classes
    ./audit/run.sh <alpha jar>      # rules audit, both rule sets

`test_bridge.py` (AI-079) compiles the overlay too.
