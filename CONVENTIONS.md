# Porting conventions (the contract)

Every `.cs` file in `rules-core/` follows these rules. They exist so the
later AI-084 differential conformance has a fighting chance.

## 1. File mapping
One Java file → one C# file, same base name. Header comment on every file:
`// Port of <path-to-java-file> @ Desolate-Tuba e0e565b`. Method names:
Java `camelCase` → C# `PascalCase`, same semantics, same order of checks.

## 2. Types
- Java `record` → C# `record`. Java `enum` → C# `enum` (same member names,
  UPPER_SNAKE kept for wire parity).
- Java `Optional<T>` → C# nullable `T?`.
- Java `List<T>` → `IReadOnlyList<T>` on public surface, `List<T>` internally.
- **No `Dictionary` iteration in game logic.** Where the Java code iterates a
  map, use `SortedDictionary` or sort keys explicitly. Never depend on
  `string.GetHashCode()` for logic (it is randomized per process in .NET).
- Java `sealed interface` hierarchies (e.g. `GameAction`) → C# abstract
  record + derived records.

## 3. Determinism (hard rules — violations fail conformance later)
- All randomness flows through `SeededRng`, owned by `GameState`, seeded at
  match creation. No `new Random()` anywhere else.
- No wall-clock: `DateTime.UtcNow`/`Now` are forbidden in `rules-core/`.
- No `Guid.NewGuid()` in game logic (instance IDs come from a counter seeded
  through the RNG stream).
- No async, no threads, no static mutable state in the core.

## 4. RNG parity note (for Astra / AI-084)
Java's `java.util.Random` and .NET's `System.Random` produce **different
streams** for the same seed. `SeededRng` currently wraps `System.Random` and
is API-complete, but exact seed-for-seed parity with the Java engine will
need either (a) a ported Java-compatible RNG (48-bit LCG, ~20 lines), or
(b) re-seeding the golden corpus against the C# stream. Flagged in
PORT_NOTES.md — do not "fix" silently; it changes every golden hash.

## 5. Events
`GameEvent` mirrors are C# records with the same field names (camelCase kept
on the wire-facing record properties to match AI-062 event JSON). These are
the conformance surface — when in doubt, match the Java field exactly.

## 6. What NOT to port
- GUI/CLI scaffolding, logging frameworks, Jackson annotations (use
  System.Text.Json attributes where serialization is needed).
- Bot *presentation* pacing; only decision logic (BotPlayer/BotDifficulty).

## 7. Project files
`rules-core/rules-core.csproj`: netstandard2.1, `<Nullable>enable</Nullable>`,
`<LangVersion>latest</LangVersion>`, no PackageReferences in the core.
