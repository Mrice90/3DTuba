# rules-soak

A headless soak, determinism and regression harness for the native C# rules core. It drives `NativeRulesSession` (the same JSON protocol Unity and the ranked referee use) and checks invariants after every action. It is read-only against the engine: no rules live here.

Findings and the handoff for the engine owner are in [docs/rules-soak/2026-10-16-HANDOFF.md](../../docs/rules-soak/2026-10-16-HANDOFF.md).

## Requirements

You need the .NET 8 SDK. On Linux, `apt install dotnet-sdk-8.0` works.

## Commands

```
./test.sh                                   # regression suite against engine/0.4.5 (about 1 min)
./soak.sh engine/0.4.5 1 100 out 4          # seeds 1-100, 4 workers, two passes + determinism diff
dotnet build -c Release -o out              # then:
out/RulesSoak soak   --data engine/0.4.5/data --mode mp|bots --seeds 1-50 [--difficulty all] [--save-clean]
out/RulesSoak replay --data engine/0.4.5/data --case cases/0.4.5/clean/<file>.json
out/RulesSoak shrink --data engine/0.4.5/data --case <failing case> --out <min.json>
out/RulesSoak trace  --data engine/0.4.5/data --case <case> --from N
```

## Modes

- `mp`: multiplayer protocol. Scripted agents (aggro or random) pick one offered action id per step for whichever seat must act. This is the path the referee replays.
- `bots`: the shipping `PlaytestBot` plays both seats. Its transcript is then replayed as multiplayer intents, and the state hash must match after every step.

## Case files

A case file holds:
- the `op:new` request,
- the intents (`seat`, `action_id`, `command`),
- the state hash after `op:new` and after each intent,
- for a failing case, `expect_kind` and `expect_step`.

To test a new engine version, put it in `engine/<version>/{bin,data}` with a `SHA256SUMS.txt`, and record cases under `cases/<version>/`.
