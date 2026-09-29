# Win-split analysis over seeded AI matches (AI-070)

Analysis-only (no rules change; TubaExperiment stays read-only). Follows the
AI-066 06:00 observation: player 0 won all 3 CI event-dump seeds.

## Method

- Tool: `EventDump.java` (AI-066, `releases/alpha-0.7.15-playable/tools/event-dump/`,
  compiled at the `c540163` tree against the pinned-alpha fat jar built from
  TubaExperiment `992bc95c7`). Both seats play HERO-difficulty bots with
  seed-derived RNGs; same seed → byte-identical dump (re-verified: seed 42
  re-run is byte-identical).
- Base run: 21 seeds (1–20, 42). Seat 0 = Zeus starter deck, seat 1 = Poseidon
  starter deck, via `DemoMatchFactory.create(seed, zeusBuild, poseidonBuild, …)`.
- Swap run (local-only harness variant, not committed): same 10 seeds 1–10 with
  the deck arguments swapped, so seat 0 = Poseidon, seat 1 = Zeus. Engine, bots
  and rules untouched — only the harness argument order changes, which
  separates "who goes first / which seat" from "which deck".
- Each dump validated against `docs/muse/sprint-02/board-events/validate.py`
  (samples from both runs: VALID).

## Results

Base run — seat 0 = Zeus, seat 1 = Poseidon (21 seeds):

| seed | winner | first player | events | turns |
|---|---|---|---|---|
| 1 | 0 (Zeus) | 0 | 288 | 14 |
| 2 | 0 (Zeus) | 0 | 695 | 29 |
| 3 | 0 (Zeus) | 0 | 476 | 20 |
| 4 | 0 (Zeus) | 0 | 612 | 27 |
| 5 | 0 (Zeus) | 0 | 150 | 9 |
| 6 | 0 (Zeus) | 0 | 732 | 35 |
| 7 | 0 (Zeus) | 0 | 303 | 16 |
| 8 | 0 (Zeus) | 0 | 1019 | 41 |
| 9 | 0 (Zeus) | 0 | 1580 | 67 |
| 10 | 0 (Zeus) | 0 | 463 | 21 |
| 11 | 0 (Zeus) | 0 | 305 | 16 |
| 12 | 0 (Zeus) | 0 | 504 | 22 |
| 13 | 0 (Zeus) | 0 | 532 | 30 |
| 14 | 0 (Zeus) | 0 | 1367 | 49 |
| 15 | 0 (Zeus) | 0 | 536 | 24 |
| 16 | 0 (Zeus) | 0 | 2035 | 75 |
| 17 | 0 (Zeus) | 0 | 679 | 28 |
| 18 | 0 (Zeus) | 0 | 148 | 9 |
| 19 | 0 (Zeus) | 0 | 609 | 27 |
| 20 | 0 (Zeus) | 0 | 250 | 13 |
| 42 | 0 (Zeus) | 0 | 235 | 14 |

Swap run — seat 0 = Poseidon, seat 1 = Zeus (10 seeds):

| seed | winner | first player | events | turns |
|---|---|---|---|---|
| 1 | 1 (Zeus) | 0 | 972 | 37 |
| 2 | 1 (Zeus) | 0 | 545 | 22 |
| 3 | 1 (Zeus) | 0 | 1014 | 37 |
| 4 | 0 (Poseidon) | 0 | 409 | 23 |
| 5 | 0 (Poseidon) | 0 | 222 | 13 |
| 6 | 1 (Zeus) | 0 | 438 | 22 |
| 7 | 1 (Zeus) | 0 | 633 | 25 |
| 8 | 1 (Zeus) | 0 | 1166 | 46 |
| 9 | 1 (Zeus) | 0 | 1027 | 44 |
| 10 | 0 (Poseidon) | 0 | 513 | 25 |

## Conclusion

**The winner follows the Zeus deck, not the seat: starter-deck asymmetry, not
first-player advantage.**

- Base: Zeus (seat 0) 21/21. Swap: Zeus (seat 1) 7/10. Combined: **Zeus wins
  28/31 ≈ 90%** regardless of which seat holds the deck.
- The seat-0 bot and seat-1 bot are the same HERO code with per-seed,
  per-seat RNG streams, so a 28/31 skew across 31 distinct seeds cannot be
  attributed to one seat's bot RNG constant. The remaining difference between
  the sides is the deck contents (starter lists + default capital).
- Corroborating control: the harness always seats player 0 first (first turn
  belongs to player 0 in all 31 dumps — no coin flip observed in
  `DemoMatchFactory` turn order), yet moving Zeus to the *second* seat barely
  dents its win rate (100% → 70%). First-player advantage is not the driver.

## Implications

- Under HERO bots, the Zeus starter deck beats the Poseidon starter deck ~90%
  of the time. Both factions are meant to be permanently free and viable
  (AI-012), so this is a balance flag for the AI-012 balance work, not an
  emergency: starter decks are expected to be tuned before release, and bot
  mirrors are only one data point (human playtests can differ).
- Suggested follow-ups (AI-012 lane): repeat this protocol with mirrored
  matchups (Zeus-vs-Zeus) to sanity-check the harness has no hidden
  seat bias; add a turn-order coin flip to `DemoMatchFactory` if the product
  wants first-player fairness measured; run human/AI playtests for balance
  confirmation. No rules change is proposed here.

## Limits

- 31 seeds is enough to reject "fair coin" but not to quantify the true win
  probability precisely (90% ± ~10 pp at a rough 95% interval).
- Bot-vs-bot only; says nothing about human-vs-human balance or about the
  non-starter card pool.
- The swap variant of the harness was used locally for analysis and was not
  committed; the base `EventDump.java` in the repo is unchanged.
