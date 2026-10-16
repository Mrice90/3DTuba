# IC.RefereeHost: the native rules core for the ranked referee

A small console program that serves the native C# rules core
(`InfiniteConquest.Native.NativeRulesSession`, the same engine the Unity
client runs) over `ic-engine-1`, the JSONL protocol in
`prototypes/lobby-lab/engine/README.md`. The ranked referee replays relay
transcripts through it, and the headless lockstep test client can play real
matches with it.

```
./fetch-native-rules.sh            # pinned DLLs + card data into ./native (checksummed)
dotnet build -c Release -o out     # .NET 8 SDK
LAB_REFEREE_SECRET=... node ../../prototypes/lobby-lab/referee-runner.js \
  --base https://<ranked worker> --engine-cmd "dotnet out/IC.RefereeHost.dll native/data"
```

## How it matches the Unity client

It starts each match exactly as `OnlineMatch.cs` / `BridgeClient.New` do in
online mode: `multiplayer: true`, `interactive_reactions: true`, seat 0 Zeus,
seat 1 Poseidon, HERO, default starter decks, and the engine seed taken from
the first 8 hex digits of the relay seed (`unchecked((int)uint)`). The state
hash does not depend on which seat is "human" (checked), so one replay
serves both players. The native core itself refuses an intent from a seat
that is not the acting seat (`INVALID_SEAT`) and any stale or invented
action id (`INVALID_ACTION`); the referee turns either into a loss for the
seat that sent it.

**Setup choices travel as intents in this build.** In online mode the client
sends no deck, capital tile or faction choice in `new`; everything after the
seed (buys from the market, plays, moves, reactions) is an ordinary action
id on the relay. So the transcript (seed plus ordered intents) is enough to
replay a match, and no separate setup record is needed. If a later build adds
deck or capital choice to online `new`, those choices must move into intents
(or into the transcript) before ranked can verify such matches.

## Which build it referees

`fetch-native-rules.sh` pins the newest native core committed to this repo
(2026-10-11, the deck-shop/board overlay). The 0.4.5 release candidate on the
PC may be newer. To referee 0.4.5 matches, copy that build's
`InfiniteConquest.Native.dll` and `InfiniteConquest.RulesCore.dll` into
`native/lib` and its `StreamingAssets/NativeRules` into `native/data`, then
rebuild. The referee and every client in a ranked match must run the same
rules build, or replays will not match: the relay's `dataVersion` gate is
where that belongs once versions are pinned per release.

## Checked

2026-10-16, cloud container, .NET 8.0.131: two lockstep clients in separate
processes played a full match on the native core over the lab relay (100
turns, 622 intents, 99 per-turn hash checks, all matched). The referee
reached the clients' final state hash and rated the winner. One forged intent
spliced into the real transcript made the referee rule against its sender.
