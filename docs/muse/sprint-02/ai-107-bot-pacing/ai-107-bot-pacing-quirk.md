# AI-107 — Bot pacing quirk: Tidepool Surveyor oscillates 3,5 ↔ 3,4

**Status:** root cause identified in the pinned alpha bot (read-only); fix designed for the future engine. This doc + `test_pacing.py` are the deliverable.

## Observed behavior

In the seed-42 replay (`docs/muse/sprint-02/timeline/fixtures/dump-seed-42.jsonl`), units
pace back and forth between two adjacent hexes, burning movement on
zero-net-progress moves — up to 3 oscillations per turn. The report cited
Tidepool Surveyor, but the dump shows it is general bot behavior: **25
oscillations across 4 units on both sides** (both `poseidon_tidepool_surveyor`
instances account for 22; `zeus_ability_skyline_seer` and
`zeus_keyword_stormgate_sentinel` account for the other 3):

| seq | turn | instance | card | move |
|-----|------|----------|------|------|
| 22 | 1 | `2a5eec6d` | tidepool_surveyor | 3,5 → 3,4 |
| 23–25 | 1 | `04e583b3` | tidepool_surveyor | 3,5 → 3,4 → 3,5 → 3,4 |
| 44–46 | 3 | `04e583b3` | tidepool_surveyor | 3,4 → 3,5 → 3,4 → 3,5 |
| 47–49 | 3 | `2a5eec6d` | tidepool_surveyor | 3,4 → 3,5 → 3,4 → 3,5 |
| 76–79 | 5 | both surveyors | — | 3,5 → 3,4 → 3,5 → 3,4 |
| 103–108 | 7 | both surveyors | — | 3,4 → 3,5 → 3,4 → 3,5 (×3) |
| 134–138 | 9 | both surveyors + sonar adept | — | same pattern |
| 158–164 | 10 | `6f143f78` | skyline_seer (Zeus) | 3,3 → 3,2 → 3,3 → 3,2 |
| 181 | 11 | `2a5eec6d` | tidepool_surveyor | 3,4 → 3,4 (cost 0 — a literal no-op move) |
| 204–205 | 12 | `08c686ca` | stormgate_sentinel (Zeus) | 3,3 → 3,4 → 3,3 |

All moves are rules-legal. The quirk is cosmetic/strategic, not a rules violation.

## Root cause (pinned alpha, read-only)

All citations are `Mrice90/TubaExperiment @ 992bc95`, inspected read-only via the GitHub API.

**1. Move scoring is destination-agnostic.** `BotPlayer.score()` gives every `move`
command a flat 35, ignoring where the unit is going:

- `game-cli/src/main/java/com/infiniteconquest/cli/BotPlayer.java:322`
  `case "move" -> 35;`

Compare with the neighboring cases, which all evaluate their target:
`attack` scores lethality and target type (lines 285–301), `play` scores card
type plus a line-of-sight `screenBonus` (302–313), `activate` scores ability
effects (353+). `move` is the only action whose score ignores its arguments.

**2. The tie-break is tactically blind.** `ranked()` sorts by score, then by
reverse lexicographic command string ("the largest command string wins score
ties" — the author's own comment at lines 270–274):

- `BotPlayer.java:275-278`

Move commands are built as `move <from.x> <from.y> <to.x> <to.y>`
(`ActionHints.java:47`). With every move tied at 35, the winner is simply the
lexicographically largest string — a deterministic coin flip with no tactical
content. On the board edge (x=3 is the max), this deterministically prefers the
highest-numbered destination, so the bot bounces between (3,5) and (3,4).

**3. No position memory.** `score()` is a pure function of `(state, command)`.
The bot never records where a unit just came from, so it cannot detect that it
is undoing its previous move.

**4. Moving always beats ending.** `case "end" -> 0` (`BotPlayer.java:324`) vs
move's 35, and `capitalSynergy()` never bonuses `move` (`BotPlayer.java:463` —
only `cast`/`react`/`play`/`burrow`/`blink`). As long as any move is legal, the
bot moves rather than passing, so a unit with nothing better to do paces until
its movement is spent.

**Why these units:** with no attack targets in range, moves (35) outscore everything
else available, so any idle unit spends its turn oscillating. The Surveyors are
simply idle the most — cheap units played on turn 1 with nothing to do.

## Fix design (for the future engine — not the pinned alpha)

The alpha is pinned read-only and the rules bridge must stay a faithful engine,
so this is **not** fixed here. When the bot is rewritten (C# port / post-pin
engine), any one of these removes the quirk:

1. **Destination-aware move scoring** — score `move` by what the destination
   achieves (approach an objective, threaten an enemy, break a sight line),
   mirroring how `play` uses `screenBonus`. Even a small distance-to-nearest-
   objective term breaks the tie meaningfully.
2. **Anti-oscillation memory** — track each instance's previous position and
   heavily penalize (or forbid) a move that returns it there. Two integers of
   state per unit.
3. **Wasted-move budget** — if the best-scoring move's destination equals the
   unit's position two actions ago, prefer `end`.

Option 2 is the smallest correct fix; option 1 is the strategically best one.

## Test

`test_pacing.py` characterizes the quirk against the committed seed-42 dump: it
detects A→B→A oscillations by the same instance and asserts the exact observed
pattern. It is a characterization test — it documents the bug precisely so the
future fix can be verified by inverting it. Run: `python test_pacing.py`.
