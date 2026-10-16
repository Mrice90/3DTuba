# IC.RefereeHost: the native rules core for the ranked referee

A small console program that serves the native C# rules core
(`InfiniteConquest.Native.NativeRulesSession`, the same engine the Unity
client runs) over `ic-engine-1`, the JSONL protocol in
`prototypes/lobby-lab/engine/README.md`. The ranked referee replays relay
transcripts through it, and the headless lockstep test client can play real
matches with it.

```
(cd rules/0.4.5 && sha256sum -c SHA256SUMS)   # the pinned rules build
dotnet build -c Release -o out                # .NET 8 SDK
LAB_REFEREE_SECRET=... node ../../prototypes/lobby-lab/referee-runner.js \
  --base https://<ranked worker> --engine-cmd "dotnet out/IC.RefereeHost.dll rules/0.4.5/data"
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

`rules/0.4.5/` holds the rules core exactly as shipped in the 0.4.5 release
candidate (`playtest/unity-build-release-candidate-0.4.5` on the PC):
`InfiniteConquest.Native.dll` (7f72db84...), `InfiniteConquest.RulesCore.dll`
(cb527524...) and `StreamingAssets/NativeRules`, rules version
`purchase-v3-hex37`. All 16 files match that build's `SHA256SUMS.txt`. The
0.4.5 client's online `new` request and seed mapping were read from its
compiled `Assembly-CSharp.dll` and are identical to what this host sends.

The referee and both players must run the same rules build, or replays will
not match. For a new release, add `rules/<version>/` from that build, check it
against the build's checksums, and build with `-p:NativeRulesLib=rules/<version>/lib`.
The relay's `dataVersion` gate is where that version pinning belongs for
matchmaking.

Notes from the rules soak thread (1,600 hex37 matches on these DLLs, zero
hash differences in-process and across processes): `op:hash` leaves out
`revision` and `reaction_pending`, so this host reports both in `status` and
the referee records the final revision in its verdict; `op:new` without a
seed uses the wall clock, so this host refuses a `new` without the relay seed.

## Checked

2026-10-16, cloud container, .NET 8.0.131, 0.4.5 rules: two lockstep clients
in separate processes played a full match on the native core over the lab
relay (179 turns, 955 intents, 178 per-turn hash checks, all matched). The referee
reached the clients' final state hash and rated the winner. One forged intent
spliced into the real transcript made the referee rule against its sender.
