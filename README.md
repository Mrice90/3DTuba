# C# rules core — DRAFT for Astra (AI-083 prep)

**Status: DRAFT — not reviewed, not tested, not for the shared repo.**
Started 2026-10-08 by Thalia (Muse) at Mathew's direction: "start the bulk of
the work in logic… your work can be taken by Astra later and implemented and
tested."

This is a working draft of the AI-083 C# port. Astra owns AI-083/AI-084 and
takes this over for implementation, integration, and testing. Do not push
this to 3DTuba as-is.

## What this is
A pure .NET Standard 2.1 C# library (`rules-core/`, no UnityEngine references)
porting the pinned 2D alpha rules to C#, module by module, mirroring the Java
source file-for-file. Target acceptance (Astra/AI-084): event-for-event parity
with the Java engine on the golden seed corpus.

## Source tree ported
`~/workspace/Desolate-Tuba` @ `e0e565b` (the readable 2D Java tree on this
machine; the TubaExperiment checkout here is empty). AI-083 names the pin as
TubaExperiment `992bc95` — Astra should re-pin/diff against `992bc95` during
conformance and reconcile any drift.

## What's here
- `rules-core/Core/` — board, state, engine, rules (mirrors `core/` package)
- `rules-core/Data/` — card data model (mirrors `data/` package)
- `rules-core/Bot/` — HERO bot (mirrors `game-cli` BotPlayer/BotDifficulty)
- `CONVENTIONS.md` — the porting contract every file follows
- `PORT_NOTES.md` — per-module deviations, open questions for Astra

## Java pin
Ported from `Desolate-Tuba` @ `e0e565b` (the readable 2D Java tree available
during the draft). AI-083 names the pin as TubaExperiment `992bc95` — re-pin
and diff against `992bc95` during AI-084 conformance before trusting any
line-level detail.

## Build & test (for Astra — not run during the draft)
Prerequisites: .NET SDK 8+ (the library targets `netstandard2.1`).
```bash
cd rules-core
dotnet build
dotnet test   # once AI-084's conformance tests land; see below
```
Known environment note: `dotnet test`'s test-host socket is blocked in the
sandbox this draft was written in — CI (Linux + Windows) is the supported
test route until that's resolved.

Conformance path (AI-084, not included here): run the same seed + action
script through the Java engine and this library, compare the AI-062 event
stream, and report the first divergence (sequence, field, both values).

## Known gaps (see PORT_NOTES.md for the full list)
- Nothing here has ever been compiled or tested.
- `System.Text.Json` packaging: netstandard2.1 has no in-box System.Text.Json
  and the core forbids PackageReferences — needs a decision (add the
  PackageReference or retarget the framework).
- RNG parity: `SeededRng` wraps `System.Random`; Java's `java.util.Random`
  produces a different stream for the same seed. Seed-for-seed golden parity
  needs a ported 48-bit Java LCG or a re-seeded golden corpus — decide in
  AI-084 before cutting golden hashes.
- `GameState.Copy()` RNG cloning is a stub.
- `CapitalPassive` namespace placement (Core vs Data) undecided.

## Explicitly out of scope for this draft
- `dotnet build`/`dotnet test` were not runnable in this sandbox (no .NET SDK;
  vstest socket blocked) — **nothing here is compile-checked.** Astra builds it.
- AI-084 conformance harness, golden corpus, CI wiring.
- Unity integration (AI-087), server (AI-085), mobile (AI-086).
- Balance numbers and card data: ported as data reads, not re-decided.
