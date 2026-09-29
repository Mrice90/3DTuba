# AI-076 — AI-012 balance options memo

**Docs only. No rules change. TubaExperiment stays read-only.**
For Mathew's decision as HA-015.

## The numbers (AI-072)

From `docs/muse/sprint-02/win-split-analysis.md` (31 seeded HERO-vs-HERO matches):

- **Base** (seat 0 = Zeus starter, seat 1 = Poseidon starter): Zeus wins **21/21** (100%)
- **Swap** (seat 0 = Poseidon, seat 1 = Zeus): Zeus wins **7/10** (70%)
- **Combined: Zeus wins 28/31 ≈ 90%**

Key finding: **the winner follows the Zeus deck, not the seat.** Moving Zeus
to the second seat drops its win rate from 100% to 70% — a real but secondary
effect. The primary driver is starter-deck asymmetry, not turn order.

The harness always seats player 0 first (no coin flip in `DemoMatchFactory`).
First-player advantage exists but is not the main problem.

## Option A: Coin flip for turn order

**What:** Randomize which seat takes the first turn each match (instead of
always seat 0).

**Expected effect:** Small. The swap data shows turn order accounts for roughly
a 30-point swing (100% → 70%) when Zeus moves from first to second seat. A coin
flip would give Poseidon the first turn 50% of the time, but Poseidon still
lost 70% of its games even when going second with the Zeus deck on the other
side. Estimated: Zeus win rate drops from ~90% to ~75-80%. Does not fix the
underlying deck gap.

**Cost:** Tiny. One RNG call in the match factory. No card changes.

**Verdict:** Worth doing for fairness perception, but insufficient alone.

## Option B: Poseidon starter tweaks

**What:** Strengthen the Poseidon starter deck list (specific card swaps to be
proposed after deeper analysis; candidates: early-game tempo, removal, or
capital defense).

**Expected effect:** Large, but uncertain. The 90% Zeus win rate suggests a
~20-25 point deck-strength gap. Closing it requires meaningful changes, not
tweaks. Risk: overcorrection flips the skew.

**Cost:** Medium. Requires design iteration and re-running the balance suite
(60+ seeds) to validate. TubaExperiment is read-only, so changes would land
in the 3D C# core (AI-083) or as a variant ruleset, not the alpha.

**Verdict:** The real fix, but needs careful iteration with data.

## Option C: Both (recommended)

Coin flip now (cheap, fair), Poseidon starter rebalance as a follow-up with
balance-suite validation. The coin flip also gives cleaner data for measuring
the deck tweak's effect — without it, turn order confounds the results.

## Recommendation

**Do Option C.** The coin flip is a one-line change with no downside. The
Poseidon tweak needs a dedicated balance pass (AI-012) with the 60-seed suite
re-run to confirm the gap closes without overshooting.

---
*Prepared 2026-09-29. Awaiting Mathew's decision (HA-015).*
