# Movement fixture — smallest deterministic legal + illegal moves

**Pinned alpha:** TubaExperiment `992bc95c7164416ea0a25a4ce120f6ec0a0a167a`
(`strip/zeus-poseidon-desktop`). Read-only inspection; nothing was modified.
**Status:** expectations below are *source-derived* (code inspection). No test was
executed — see "Test execution" at the end.

## Board model (source)

- `game-core/src/main/java/com/infiniteconquest/core/BoardPosition.java` — record `(x, y)`,
  `WIDTH = 4`, `HEIGHT = 6`. The constructor **throws `IllegalArgumentException`**
  for out-of-range coordinates, so an out-of-bounds destination cannot be constructed
  as a `BoardPosition` at all.
- Distance is Chebyshev: `distanceTo = max(|dx|, |dy|)` — diagonal and orthogonal
  steps each cost 1 (`adjacentTo` ⇔ distance 1).
- Player 0's side: `y < 3` (`PLOT_HEIGHT = 3`); player 1's side: `y >= 3`.

## Movement rules (source)

- `game-core/src/main/java/com/infiniteconquest/core/MovementRules.java`
  - `legalDestinations(state, character)`: BFS over `neighbors()`, limited by
    `character.movementRemaining()`; allowance halved (min 1) when the origin is on
    the enemy side. A hex is `OPEN` (empty, or all-LAND stack), `ENTER_ONLY`
    (friendly non-land stack: may end move, not pass through), or `BLOCKED`
    (enemy structure, enemy Capital, or enemy Character).
  - `shortestLegalPath(...)`: BFS path excluding origin, including destination.
- `game-core/src/main/java/com/infiniteconquest/core/GameEngine.java:283`
  (`moveCharacter`): rejects with `"Invalid Character"` (wrong owner / not a Character),
  rejects with `"Destination unreachable"` when `shortestLegalPath` is empty;
  otherwise walks the path, spending 1 movement per step (`card.spendMovement(1)`),
  moving the top of the stack (`state.board().moveTop(current, step, ...)`),
  and records the move. Opportunity attacks are evaluated per step (none in these
  fixtures: no enemy characters on the board).

## Case 1 — legal: single orthogonal step (smallest deterministic ordinary move)

Mirrors `MovementRulesTest` helpers (same constructors, same fixed seed).

Initial state:
- `GameState state = new GameState(1L);` — fixed seed; movement resolution uses no RNG.
- Character: `new CardDefinition("runner", "Runner", CardType.CHARACTER, "DEV", 0, 1, 1, 1, 1)`
  → cost 0, attack 1, defense 1, **movement 1**, range 1 (9-arg ctor;
  `CardDefinition.java:96`; hitPoints defaults to 0 for non-permanents).
- `CardInstance runner = new CardInstance(<uuid>, def, 0, Zone.BATTLEFIELD);`
  (owner 0; the UUID is identity only and does not affect the outcome).
- `state.register(runner); state.board().push(new BoardPosition(0, 0), runner.instanceId());`
- Board otherwise empty. Turn/player: acting as player 0 (`GameAction.MoveCharacter(0, …)`).
  Resources involved: only `movementRemaining`/`movementSpent` (no GP, no cards).

Action:
- `GameEngine engine = new GameEngine();`
- `ActionResult r = engine.apply(state, new GameAction.MoveCharacter(0, runner.instanceId(), new BoardPosition(1, 0)));`

Expected state delta (source-derived):
- `r.accepted()` is true.
- `runner.movementSpent()` == 1 (was 0).
- `state.board().positionOf(runner.instanceId())` == `BoardPosition(1, 0)` (was `(0, 0)`).
- Before the move, `(1, 0)` ∈ `engine.legalMovementDestinations(state, runner.instanceId())`:
  Chebyshev distance from `(0, 0)` is 1 ≤ allowance 1; origin `(0, 0)` is on player 0's
  own side (no halving); destination empty → `Passability.OPEN`.

Existing test: `MovementRulesTest.diagonalMovementCostsOneAndCanBeSplitAcrossActions`
(same apply/accepted/movementSpent pattern, diagonal variant).

## Case 2 — illegal: destination blocked by enemy structure

Same initial state as Case 1, plus:
- Blocker: `new CardDefinition("block", "Block", CardType.STRUCTURE, "DEV", 0, 0, 0, 0, 0)`
  → movement 0, range 0, hitPoints defaults to 1 for permanents
  (`CardDefinition.java:96`; permanents require positive HP).
- `CardInstance wall = new CardInstance(<uuid>, blockDef, 1, Zone.BATTLEFIELD);`
  (owner **1** = enemy).
- `state.register(wall); state.board().push(new BoardPosition(1, 0), wall.instanceId());`

Action:
- `engine.apply(state, new GameAction.MoveCharacter(0, runner.instanceId(), new BoardPosition(1, 0)))`

Expected rejection (source-derived):
- `(1, 0)` ∉ `legalMovementDestinations(...)`: the stack at `(1, 0)` is not all-LAND
  and its non-land card is not friendly → `Passability.BLOCKED`
  (`MovementRules.passability`).
- `shortestLegalPath` returns empty → `moveCharacter` returns
  `ActionResult.rejected("Destination unreachable")` (`GameEngine.java:283-288`).
- Runner stays at `(0, 0)`; `movementSpent()` remains 0; no events recorded.

Why this illegal kind (and not out-of-bounds): `BoardPosition`'s constructor throws
`IllegalArgumentException` for `x ∉ [0,4)` / `y ∉ [0,6)`, so an out-of-bounds
destination cannot reach the engine. The engine-level illegal move is a blocked
destination. (Beyond-allowance is also rejected the same way, e.g. distance 2 with
movement 1 — same `"Destination unreachable"` path.)

Existing test: `MovementRulesTest.enemyStructureBlocksMovement`
(destination excluded from legal destinations).

## Test execution

- **Executed: no.** Blocker: this machine has no JDK (`java: command not found`)
  and the repo has no Gradle wrapper (`gradlew` absent; root `build.gradle` present).
- Exact command on a machine with the toolchain (does not modify source):
  `cd <tubaExperiment@992bc95> && gradle :game-core:test --tests "com.infiniteconquest.core.MovementRulesTest"`
  (CI uses `gradle test`; see `.github/workflows/ci.yml`.)
- All expectations above are derived from reading `MovementRules.java`,
  `GameEngine.java:283`, `BoardPosition.java`, `CardDefinition.java:96`,
  and `MovementRulesTest.java` at the pinned commit — not from executed tests.
