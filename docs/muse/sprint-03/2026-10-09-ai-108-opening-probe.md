# AI-108-OPENING — Structures-first opening measured across 1,000 seeds

**Date:** 2026-10-09
**Lane:** Claude (headless measurement). Answers Muse finding **F5** in `2026-10-08-slot-qa-report.md` ("no-deadlock is not proven for all opening hands").
**Slot numbers unchanged.** Supply 2 per structure, characters 1, Storm Titan / Trident Core 2, capitals and lands 0. Whether to change them is Mathew's call; this report only measures them.

## Bottom line

- **No deadlock in 8,000 matches.** Every slot-enabled match reached GAME_OVER (0 draws, 0 turn-limit stalls). No seed left both players unable to act.
- **The cost is a slower, emptier opening.** With slots on, 95% of players field nothing at the end of their first turn (65% without slots), 69% after turn 2 (51%), 50% after turn 3 (43%). From turn 4 the two rule sets are within 3 points.
- **First summon moves about one turn later:** mean 3.26 vs 2.24 personal turns, 90th percentile turn 5 vs 4, worst case turn 13 vs 9.
- **2% of players never summon at all** (81 of 4,000 at HERO), against 0.3% without slots. All 81 lost; median match length 12 total turns. This is a **soft lock**: a land or structure drought, plus a fast opponent, ends the game before the chain land, structure, summon completes. 65 of the 81 never placed a structure.
- **9% of players spend 4+ consecutive turns with a summon in hand that the slot cap blocks** (365 of 4,000).
- **Going first is unaffected:** the first player wins 49% with and without slots.

## Method

`releases/alpha-0.7.15-playable/tools/opening-probe/OpeningProbe.java` (reproduce with `run.sh` in that folder).

- Engine: pinned alpha `992bc95` built with the release recipe; set up exactly like the rules bridge (4×6 HEX, capitals at (1,0)/(2,5), alpha starter decks, bot RNG seeds `seed ^ 0xC0FFEE1/2`).
- Both seats are bots. The coin flip decides who goes first, so each faction appears as both first and second player.
- Grid: seeds 1–1,000 × {Zeus seat 0 vs Poseidon seat 1, Poseidon seat 0 vs Zeus seat 1} × {slots on, slots off} × {HERO, MORTAL} = 8,000 matches, then 6,000 more for the levers below.
- Slots on: before the bot chooses, any `play`/`burrow` of a character whose slot cost exceeds free capacity is removed from its legal list.

**Limits, stated plainly**

- The slot rule here is a **re-implementation of the documented policy** (slot QA report §1), not the SP2 overlay classes, which only exist in the local Unity package. A run of the same probe against the SP2 bridge on Mathew's PC would confirm it.
- The pinned alpha has no covered-activation change (AI-080). That only affects activations, not summoning.
- Bots do not know about slots. They do not rush structures harder when slots are on (mean first structure turn about 2.5 with slots, 2.6 without). A human who knows the rule should do better than these numbers, so read them as an upper bound on the pain.
- No mulligan in the base runs, same as the bridge.

## Per-turn results (HERO, 4,000 player-matches per column)

| Personal turn | Fields nothing, slots off | Fields nothing, slots on | Turn with only "end", off | Turn with only "end", on | Summon blocked by slots and board empty |
|---|---|---|---|---|---|
| 1 | 65% | 95% | 18% | 36% | 42% |
| 2 | 51% | 69% | 4% | 12% | 41% |
| 3 | 43% | 50% | 3% | 9% | 22% |
| 4 | 36% | 38% | 3% | 7% | 11% |
| 5 | 34% | 35% | 1% | 4% | 6% |
| 6 | 31% | 31% | 2% | 3% | 4% |

"Fields nothing" means no character of that player on the board at the end of their turn. MORTAL results are within 2 points of HERO on every row.

**First summon turn, by faction and order (HERO, slots on; slots off in brackets)**

| Player | Mean first structure | Mean first summon | 90th pct | Worst | Never summoned |
|---|---|---|---|---|---|
| Zeus, going first | 2.80 | 3.59 (2.40) | 6 | 13 | 27 (4) |
| Zeus, going second | 2.37 | 3.20 (2.14) | 5 | 13 | 15 (2) |
| Poseidon, going first | 2.70 | 3.31 (2.27) | 5 | 13 | 22 (3) |
| Poseidon, going second | 2.26 | 2.96 (2.14) | 4 | 12 | 17 (3) |

Zeus going first is the slowest opener; Poseidon going second the quickest. The second player's extra opening card is what helps.

## Why a player has no structure after three turns

About 20% of players have no structure on the board after personal turn 3, with or without slots. HERO, slots on, out of 4,000:

| Cause | Players |
|---|---|
| No land on the board to place a structure on (land drought or no GP) | 305 (7.6%) |
| No structure drawn | 293 (7.3%) |
| Structures in hand all cost more than the turn number allows | 144 (3.6%) |
| Structure was legal but the bot played something else | 51 (1.3%) |

Without slots this delay only costs tempo. With slots it also means no units, which is why the "never summoned" count goes from 12 to 81.

## Worst cases (HERO, slots on)

Each of these went 10 personal turns with no structure on the board. Re-run any one with `java -cp <jar>:build/classes OpeningProbe on HERO <faction0> <faction1> <seed> <seed>`.

| Seed | Player | What happened | Result |
|---|---|---|---|
| 958 | Poseidon, seat 1, first | No structure drawn in 10 turns, while 6 lands came down. Characters in hand blocked every turn. | Summoned on turn 11, lost on total turn 22 |
| 144 | Zeus, seat 0, first | No structure drawn in 10 turns; GP climbed to 25 unused | Summoned on turn 11, still won on turn 49 |
| 80 | Poseidon, seat 1, first | 2–4 structures in hand from turn 1, but no land drawn in 10 turns, so nowhere to place them | Never summoned, lost on turn 20 |
| 222 | Zeus, seat 1, first | Same pattern: structures in hand, zero lands for 10 turns | Never summoned, lost on turn 22 |
| 641 | Zeus, seat 0, first | Structures in hand, zero lands for 10 turns | Summoned on turn 13, still won on turn 59 |
| 414 | Zeus, seat 0, first | Structures in hand, zero lands for 10 turns | Summoned on turn 12, still won on turn 45 |

Three of the six recovered and won long games, so a late start is survivable when the opponent is slow too. The worst hands come from two kinds of draw: **no structures at all**, or **structures with no land to place them on**. Under the structures-first rule either one means many turns with no units.

## Levers that leave the slot numbers alone

Same 2,000 HERO matches (seeds 1–1,000, both orders), slots on:

| Variant | Empty board T1 | T2 | T3 | First summon mean | 90th pct | Worst | Never summoned | Blocked streak of 4+ turns |
|---|---|---|---|---|---|---|---|---|
| Slots off (reference) | 65% | 51% | 43% | 2.24 | 4 | 9 | 12 (0.3%) | 0 |
| **Slots on, as today** | 95% | 69% | 50% | 3.26 | 5 | 13 | 81 (2.0%) | 365 (9.1%) |
| Mulligan advice | 93% | 63% | 46% | 3.00 | 5 | 15 | 31 (0.8%) | 183 (4.6%) |
| 16-structure starter | 91% | 65% | 49% | 3.12 | 5 | 13 | 70 (1.8%) | 262 (6.6%) |
| Both | 89% | 61% | 46% | 2.93 | 5 | 12 | 35 (0.9%) | 144 (3.6%) |

- **Mulligan advice** uses the engine's existing mulligan (up to 3 cards). If the opening hand lacks a land or a structure, throw back the most expensive non-development cards. This is HUD/tutorial guidance, not a rule change, and it fits the "communication, not mechanics" decision on F5. It cuts never-summoned by 62% and long blocked streaks by half.
- **16-structure starter** swaps the 4 most expensive characters (Zeus: Thunderhead Guardian ×2, Stormgate Adept ×2; Poseidon: Trench Stalker ×2, Reefwarden ×2) for 2 more copies each of the two cheapest structures. It helps less on its own because the bots don't play structures more eagerly.
- Neither lever gets the first-turn empty board below about 90%. That number comes from the rule itself: a structure has to be on the board before the first unit.

## Recommendation for the design call

1. Keep the provisional numbers through human SP2 play. No deadlock was found.
2. Ship the mulligan guidance in the HUD/help text: "Keep a Land and a Structure; you can't summon until a Structure is down." It is the cheapest lever and the strongest one measured.
3. If the human matches still feel slow, try the 16-structure starter, or a cost-0 structure in the opening hand, before touching slot numbers.
4. Confirm with one run of this probe against the SP2 bridge and overlay classes on Mathew's PC.
