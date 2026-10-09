# Port notes — per-module deviations and open questions for Astra

Draft status: 47 files ported 2026-10-08; cross-file reconcile pass applied (Guid→long unification, ActivePlayer/Board properties, ActionHints/CommandProcessor ported). NOT compile-checked (no .NET SDK in this sandbox).
Astra owns implementation, integration, and testing (AI-083/AI-084).

## RNG parity (pre-existing, from CONVENTIONS.md §4)
`SeededRng` wraps `System.Random`; Java's `java.util.Random` produces a
different stream for the same seed. Seed-for-seed golden parity needs either a
ported 48-bit Java LCG or a re-seeded golden corpus. The Bot module is the
first consumer: MORTAL's exploration draws flow through the GameState-owned
`SeededRng` (never `new Random()`), in a fixed order per decision — but the
draw *values* will differ from Java until the RNG itself is ported.

## Modules ported (2026-10-08, Thalia)
BoardPosition, BoardGeometry, BoardState, TerrainRules, LineOfSightRules,
MovementRules — one Java file → one C# file, semantics mirrored including
check order. Java source: Desolate-Tuba @ e0e565b.

### Verified against the Java tree (not just transcribed)
- `KeywordValue(int range, int amount)` — **range first**. `defaultValue`
  preserves the Java arg order (TURRET = range 2/amount 1, etc.).
- `activateAbility` check order: ownership → top-of-stack → has-ability →
  already-used → line-of-sight → GP check → `spendGp` → `markAbilityUsed` →
  resolve (relevant to TerrainRules-adjacent S4 reasoning; engine itself not
  ported yet).
- `CardDefinition.isPermanent()` is an instance method (delegates to a private
  static) → ported as `IsPermanent()` method.
- `GameState.useTerrainTrigger/recordTerrain/destroy` are package-private →
  assumed `internal` on the C# `GameState`.

### Deliberate deviations from a literal transcription
1. **`Math.Round` → `Math.Floor(x + 0.5)`** (BoardGeometry.HexTrace). Java's
   `Math.round(double)` is specified as floor(x + 0.5); C#'s `Math.Round`
   defaults to banker's rounding. The literal form is load-bearing for trace
   parity — do not "simplify".
2. **BoardGeometry enum behavior → extension methods.** C# enums cannot carry
   methods; `Distance/Adjacent/Neighbors/HexTrace` are extensions so call
   sites keep their Java shape (`geometry.Distance(a, b)`).
3. **BoardState storage: `List<BoardPosition>` order + `Dictionary` lookup.**
   Mirrors Java `LinkedHashMap` insertion order (row-major) for iteration;
   the dictionary is lookup-only — game logic never iterates it, per the
   contract. `Positions()` returns `IReadOnlyList<BoardPosition>` (ordered);
   Java returned a `Set`, but only membership/iteration were used.
4. **MovementRules BFS: parallel `HashSet<BoardPosition> reached`.** The Java
   code returns `distance.keySet()`; enumerating `Dictionary.Keys` is banned
   by the contract, so membership is tracked separately. `LegalDestinations`
   returns `IReadOnlySet<BoardPosition>`; iteration order is unspecified
   (Java's `Set` contract likewise) — logic only uses `Contains`.
5. **`passability` → `PassabilityOf`.** The nested enum already owns the name
   `Passability`; C# cannot also have a method of that name.
6. **Java `Integer.compare(a, b)` → `Math.Sign(a - b)`** (Bresenham steps).
   Identical for ints.
7. **Exceptions:** `IllegalArgumentException` → `ArgumentException`,
   `IllegalStateException` → `InvalidOperationException`,
   `Optional.orElseThrow()` → `InvalidOperationException`. `netstandard2.1`
   has no `ArgumentNullException.ThrowIfNull`; explicit null checks are used.
8. **`TerrainRules.entered`/`startTurn` keyword lists** are `static readonly`
   arrays instead of per-call `List.of(...)`. No semantic difference (never
   mutated), deterministic order preserved.
9. **StartTurn max-by-damage tie-break** is a manual loop instead of LINQ
   `Max` with a comparer: `damage` desc, then `instanceId.ToString()`
   ordinal-desc. `Guid.ToString()` ("d" format) matches Java
   `UUID.toString()` shape (lowercase hex with dashes); ordinal comparison
   matches Java's char-wise `String.compareTo` for this alphabet.

## Assumed API surface (types not yet ported — reconcile when porting)
These are referenced but do not exist yet in `rules-core/`. Shapes assumed:

- `InfiniteConquest.RulesCore.Data.Keyword` — enum, UPPER_SNAKE members
  (MOLE, VANGUARD, BLINK, FAST_STRIKE, SHARP_SHOT, SIEGE, HIGH_GROUND, COVER,
  WAYSTATION, FERTILE, SANCTUARY, ARCHIVE, TURRET, MEDIC_TENT, WATCHTOWER,
  BULWARK, WORKSHOP, BEACON). Mirrors `data/Keyword.java`.
- `InfiniteConquest.RulesCore.Data.KeywordValue` — record with `Range`,
  `Amount` (int), validation range 0–12 / amount 1–100. (Java file lives in
  the `core` package, but it is card-model data; placed in Data per the
  task brief. Astra: move if the Data group disagrees.)
- `InfiniteConquest.RulesCore.Data.CardType` — enum
  { CHARACTER, LAND, STRUCTURE, SPELL, CAPITAL }.
- `InfiniteConquest.RulesCore.Data.CardDefinition` — record; `Type`,
  `Name` properties; `HasKeyword(Keyword)`, `KeywordValue(Keyword)`,
  `IsPermanent()` methods.
- `InfiniteConquest.RulesCore.Core.CardInstance` — **already ported by a
  sibling agent** (`Core/CardInstance.cs`): `InstanceId` is **`long`**, not
  `Guid`, per CONVENTIONS.md §3 (IDs come from a GameState-owned counter
  seeded through the RNG stream — no `Guid.NewGuid()`). All six modules in
  this batch use `long` for card IDs to match. `Owner`, `Zone`,
  `Definition`, `Damage`, `CombatDamage` are properties;
  `MovementRemaining()`, `EffectiveAttack()`, `EffectiveDefense()`,
  `DefenseRemaining()` are **methods**; `AddCombatDamage`,
  `HealCombatDamage`, `HealDamage`, `RestoreMovement`, `AddAttackBonus`,
  `HasKeyword`/`KeywordValue`/`IsPermanent` live where noted below.
- `InfiniteConquest.RulesCore.Core.Zone` — enum
  { DECK, HAND, BATTLEFIELD, DISCARD, EXILE }. (Not yet ported as a file;
  referenced from CardInstance.cs.)
- `InfiniteConquest.RulesCore.Core.Phase` — enum
  { START, PLAY, END, GAME_OVER }. (Not yet ported as a file.)
- `InfiniteConquest.RulesCore.Core.GameState` — `Board` (BoardState),
  `Rules` (MatchRules), `Phase` properties; `Card(Guid)` returning
  `CardInstance?`; `BattlefieldCards(int)` returning
  `IReadOnlyList<CardInstance>`; `internal` methods
  `UseTerrainTrigger(CardInstance, CardInstance, Keyword)`,
  `RecordTerrain(CardInstance, CardInstance, Keyword, int)`,
  `Destroy(CardInstance)`.
- `InfiniteConquest.RulesCore.Core.MatchRules` — `Geometry`
  (BoardGeometry) property.

## Open questions for Astra
1. AI-083 names the pin as TubaExperiment `992bc95`; this draft ports
   Desolate-Tuba @ `e0e565b` (the only readable 2D tree on this machine).
   Re-pin/diff during AI-084 conformance and reconcile drift — the
   2026-10-08 2D citation check already found ±2-line drift in
   `GameEngine.java` between tree states.
2. `TubaExperiment/` on this machine is empty; if `992bc95` has a fuller
   card set, re-verify the seven-activated-sources count and the S1–S6
   card IDs against it.
3. Confirm the Data/Core split for `KeywordValue` (see above).
4. `LegalDestinations` iteration order: Java's `HashMap.keySet()` order is
   deterministic per JVM (record hashCode) but differs from .NET's
   `HashSet` order. Current port treats it as unspecified; if any consumer
   (UI, bot) iterates it, sort explicitly at the call site.
5. **ID-scheme inconsistency across draft files (needs reconciliation):**
   `CardInstance.cs` (sibling agent) uses `long` IDs per CONVENTIONS.md §3,
   and this batch's six files use `long` to match — but the sibling
   `GameEngine.cs` still takes `Guid` in several signatures
   (`LegalMovementDestinations`, `LegalAttackDestinations`,
   `OpportunityThreats`, `OpportunityThreat.AttackerId`,
   `PlayableFromHand`, `DevelopableFromHand`, plus `HashSet<Guid>` locals).
   Those `Guid` usages cannot compile against `long InstanceId` and must
   be reconciled to `long` (or the convention revisited — do not silently
   reintroduce `Guid`).
6. **StartTurn tie-break vs the ID scheme.** Java breaks damage ties by
   `instanceId.toString()` where IDs are MD5-based UUIDs
   (`MatchFactory`: `UUID.nameUUIDFromBytes(seed:player:index:defId)`).
   This port compares `InstanceId.ToString()` ordinally with `long` IDs,
   which is a literal mirror of the *operation* but cannot reproduce the
   *order* — so on exact damage ties the two engines may pick different
   repair targets. Either engine-local determinism is sufficient (flag it
   in AI-084), or the tie-break needs an order-correlated rule both
   engines share. Do not paper over this in conformance.
7. `MatchFactory.cs` (sibling agent) contains `new Random(` — banned by
   CONVENTIONS.md §3 outside `SeededRng`. Flagged, not fixed (not this
   batch's file).

## Bot module (2026-10-08, Thalia)
`Bot/BotPlayer.cs`, `Bot/BotDifficulty.cs` — decision logic only
(move/attack/summon/ability/spell choices, MORTAL/HERO/DEMIGOD temperaments).
No presentation pacing, no CLI scaffolding (CONVENTIONS.md §6).

### Verified against the Java tree (not just transcribed)
- Evaluation order and all heuristic constants reproduced exactly
  (attack 150/108/110/95, play 90/85+screen/80, spells 140→60, ability
  scales, evaluate weights 100/10/1.5/2/8, screen bonus 15×n capped at 45).
- `Ranked` tie-break: `OrderByDescending(score)` then
  `ThenByDescending(command, StringComparer.Ordinal)` — ordinal matches
  Java's UTF-16 natural ordering for command strings.
- `DemigodChoice` keeps the strict `>` comparison (first best wins ties) and
  the `"OK:"` result-prefix guard, ordinal.
- `MortalChoice` draw order: `NextDouble()` then `Next(bound)` — fixed per
  decision, matching Java's `nextDouble()` / `nextInt()` sequence.
- `Evaluate` uses `1 - playerId` literally for the foe index, as Java does.
- `EnemyCapitalRemaining` returns `int.MaxValue` when no enemy Capital exists
  (mirrors `orElse(Integer.MAX_VALUE)`).
- `IsLethalCapitalStrike` (ability branch) computes ping 0 when no card is on
  the hex, then compares — mirrors the `.orElse(0)` chain exactly.
- `FilterCapitalAttacks` keeps the full list (a copy — nothing mutates it)
  when no capital strikes exist, when nothing else can be hit, or when no
  lethal survives the filter.

### Deliberate deviations from a literal transcription
1. **Constructors take `SeededRng`, never `new Random()`.** The Java
   `BotPlayer()` / `BotPlayer(difficulty)` overloads create an unseeded
   `java.util.Random`; those overloads are dropped. `BotPlayer(SeededRng)`
   still defaults to HERO. This is the first module that *requires* the
   GameState-owned RNG — see the RNG parity note above.
2. **Ghost instance IDs are counter-derived, not random.** Java's
   `UUID.randomUUID()` in `screenBonus` does not touch the game RNG, so the
   port must not either (spending `SeededRng` draws here would shift every
   later MORTAL exploration draw out of parity). `NextGhostId()` builds a
   deterministic `Guid` from a private instance counter; ghosts live only on
   discarded probe copies, so cross-instance counter reuse cannot collide.
3. **`BotDifficulty` titles/descriptions are extension methods.** C# enums
   cannot carry fields; `Title()` / `Description()` preserve the exact Java
   strings. Java's `toString()` returning the title has no enum equivalent —
   use `Title()`.
4. **`Decision` properties are PascalCase** (`Command`, `Result`). It is not
   a wire event, so the camelCase rule does not apply.
5. **Defensive default arms** on the `SpellScore` / `ActivateScore` switch
   expressions throw `ArgumentOutOfRangeException`. The Java switches are
   compile-time exhaustive over the enums (member lists verified against the
   Java tree); the arms are unreachable if the C# enums stay in sync.
6. **`HasKeyword` called as a method** (`card.Definition.HasKeyword(...)`),
   matching 10+ call sites in the sibling's Core files. The ported
   `Data/CardDefinition` record currently exposes only
   `IReadOnlySet<Keyword> Keywords` — reconcile by adding the method to the
   record or refactoring the call sites.

### Assumed API surface (types not yet ported — reconcile when porting)
- `GameState.Winner` as `int?` property (Java `OptionalInt`).
- `GameState.Copy()` and `GameState.CapitalPassiveFor(int)` (as
  `CapitalPassive?`) as methods; `PlayerState.Hand` as
  `IReadOnlyList<Guid>` property.
- `CapitalPassive` enum assumed in `InfiniteConquest.RulesCore.Data` (all
  other enums landed there); members STORM_TITHE, TRIDENT_RESTORATION,
  DEEP_RESERVES, CLOUDWARD used, OLYMPIAN_MUSTER/TIDAL_RENEWAL fall to the
  switch default — verified against the Java enum.
- `CardInstance` ctor assumed positional `(Guid, CardDefinition, int, Zone)`.
- `Bot.ActionHints` (`ForActivePlayer`, `SpellActionsForPlayer`) and
  `Bot.CommandProcessor` (`Execute`) — not yet ported; BotPlayer
  instantiates them exactly as the Java does.

## Game-state batch (2026-10-08, Thalia — subagent)
`Core/ActionResult.cs`, `Core/CapitalPassive.cs`, `Core/CapitalDeployment.cs`,
`Core/CapitalPassiveRules.cs`, `Core/DevelopmentRules.cs`, `Core/CardInstance.cs`,
`Core/PlayerState.cs`, `Core/GameAction.cs`, `Core/GameEvent.cs`,
`Core/GameSnapshot.cs`, `Core/GameState.cs` — one Java file → one C# file,
Desolate-Tuba @ e0e565b. Semantics mirrored including check order; Java
package-private → `internal`, `IllegalStateException`/`IllegalArgumentException`
→ `InvalidOperationException`/`ArgumentException`.

### Verified against the sibling ports already in the tree (not just assumed)
- `BoardState`: `IsEmpty/Push/Remove/PositionOf→BoardPosition?/TopAt→long?`
  all match; internal copy constructor exists and is used by `GameState` copy.
- `BoardPosition`: `X`/`Y` properties, `IsOnPlayerSide(int)` — matches.
- `MatchRules`: `Current()`/`Hex()` statics, `StartingGp`,
  `SecondPlayerStartingGp`, `CardsDrawnAtTurnStart`, `InitialHandSizeFor(bool)` — match.
- `Data.CardDefinition`: `Income()`/`HasKeyword()`/`KeywordValue()`/`IsPermanent()`
  are **methods** (not properties); 9-arg ctor overload
  `(id, name, type, faction, cost, attack, defense, movement, range)` exists
  and is used for `HIDDEN_CARD`.
- `Data.CardAbility`: `Trigger/Effect/Amount/GpCost` properties.
- `Data.KeywordValue`: `Range`/`Amount` properties.
- `Data.Keyword`: BLINK, FERTILE, ARCHIVE members confirmed.
- `CardAbilityRules.Resolve` is public; `TerrainRules.StartTurn` is public
  static; `VictoryEvaluator.WinnerAfterCapitalLoss` is public returning int.
- Enum members all UPPER_SNAKE in Data (Zone, Phase, CardType,
  AbilityTrigger, DevelopmentPassive) — referenced accordingly.

### Deliberate deviations from a literal transcription
1. **Instance IDs are `long`, not `Guid`** (task contract, CONVENTIONS.md §3):
   `GameState` owns `SeededRng Rng` plus a monotonic `_nextInstanceId`
   counter seeded through the stream (`1 + Rng.Next(int.MaxValue - 1)`);
   `NextInstanceId()` issues IDs. `GameState.Copy()` cannot clone
   `System.Random`'s stream, so the copy derives
   `new SeededRng(source.Seed ^ source._nextEventSequence)` — deterministic
   without touching the source's stream (open design point for AI lookahead).
2. **`cards` map: `SortedDictionary<long, CardInstance>`** (ascending ID)
   instead of Java `LinkedHashMap` (insertion order). Identical whenever
   cards register in ID-creation order (true for live matches);
   `BattlefieldCards` additionally orders by ID explicitly.
3. **Hidden placeholder IDs are negative longs**
   (`-(1_000_000·player + 1_000·zoneOrdinal + index + 1)`; live IDs positive).
   Java used `UUID.nameUUIDFromBytes` — deterministic either way, values
   differ by construction.
4. **CapitalPassiveRules tie-breaks** use numeric ID order where Java used
   `UUID.toString()` lexicographic order (forced by the ID-scheme change).
5. **Event detail strings** embed `long` IDs and C# record `ToString()`s
   instead of UUID hex and Java record strings — conformance on `detail`
   needs normalization (same caveat as the board batch's tie-break note).
6. **`GameEvent`/`GameSnapshot`/`CardView` keep camelCase properties**
   (CONVENTIONS.md §5 extended to the snapshot — same wire surface).
7. **`GameAction`**: abstract record + 10 nested derived records; `CastSpell`
   has `long? TargetId` and `BoardPosition? Destination` (the Java engine
   null-checks both); other actions keep non-nullable positions.
8. **`ActionResult`** keeps lowercase positional names (`accepted`,
   `message`) to avoid colliding with the `Accepted`/`Rejected` factories.
9. **`CapitalPassiveRules.MostDamagedPermanent`** is dead code in Java too;
   ported verbatim.
10. **`Mulligan`** checks discard uniqueness explicitly to mirror Java
    `Set.copyOf` duplicate rejection.

### Namespace decisions (for Astra/parent to confirm)
- Per the draft task brief, not-yet-ported types are referenced as
  `InfiniteConquest.RulesCore.Data.<PascalName>` (single `using` per file).
- **`CapitalPassive` lives in `Core`** per the explicit task instruction
  (it was in this batch's file list with `Core` namespace). This conflicts
  with the Bot module's assumption that it is in `Data` ("all other enums
  landed there") — one of the two must move; recommend following the
  all-enums-in-Data convention and moving it, or updating the Bot references.

## Reconcile pass (2026-10-08 ~21:45 ET, Thalia) — cross-file fixes applied
Five workers ported concurrently; these inconsistencies were found and fixed
by hand. Unfixed items remain flagged for Astra.

### Fixed
1. **Instance-ID type unified to `long`.** GameEngine.cs used `Guid` in 10
   signatures/locals and MatchFactory.cs built `Guid` name-UUIDs, while
   CardInstance/GameState/GameAction/BoardState/BotPlayer all use `long`
   (CONVENTIONS.md §3: no Guid). Fixed: GameEngine signatures → `long`,
   `HashSet<Guid>` → `HashSet<long>`, `(System.Guid?)null` → `(long?)null`
   (CardAbilityRules.cs), BotPlayer ghost ids → negative-long counter
   (was counter-derived `Guid`; negatives can never collide with real ids
   and never touch the RNG stream — same rationale, no Guid).
2. **MatchFactory deck ids → deterministic longs.** Replaced
   `NameUuidFromBytes` (returned `Guid`) with `NameSeededId` returning the
   MD5-based value's most-significant 64 bits big-endian — exactly Java's
   `UUID.nameUUIDFromBytes(...).getMostSignificantBits()`. Deterministic,
   no Guid, value-faithful to Java's msb.
3. **GameState.ActivePlayer / .Board: method → property.** GameEngine and
   BotPlayer both use property-style (`state.ActivePlayer`, `state.Board`);
   GameState defined them as methods. Converted the definitions; no
   method-style call sites existed.
4. **ActionHints.cs + CommandProcessor.cs ported** (were missing; BotPlayer
   depends on both). ActionHints is a full logic port. CommandProcessor
   ports Execute + all game commands; `board`/`hand`/`inspect` return
   placeholders — BattlefieldRenderer is presentation, out of scope.
   Also fixed: `result.Accepted/Message` → `result.accepted/message`
   (ActionResult uses lowercase positional names).

### Still open for Astra (not fixed — needs judgment or the compiler)
- `System.Text.Json` packaging: netstandard2.1 has no in-box System.Text.Json
  and CONVENTIONS §7 forbids PackageReferences in the core. CardCatalog.cs
  was written against it anyway — Astra decides: add the PackageReference
  or retarget the framework.
- `CapitalPassive` lives in `Core` (per task instruction); Bot assumed
  `Data` but resolves via `using` — compiles, but Astra may want it moved.
- `GameState.Copy()` RNG cloning is a stub (`new SeededRng(seed ^ nextEventSequence)`);
  System.Random streams can't be cloned — open design point for conformance.
- Re-pin against TubaExperiment `992bc95` and diff (draft used e0e565b).
- RNG parity strategy (ported 48-bit Java LCG vs re-seeded golden corpus).
- Nothing here has ever been compiled — first `dotnet build` belongs to Astra.
