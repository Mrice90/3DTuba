# Engine adapters for lockstep clients and the ranked referee

Both the headless lockstep client (`../lockstep-client.js`) and the ranked
referee (`../referee-engine.js`) drive a rules engine through one small async
interface. Seat 0 is the room's `seatA`, seat 1 is `seatB`; the seed is the
relay's 64-hex commit-reveal seed.

```
start({seed, seats})        -> {turn, activeSeat, winnerSeat, over}
legal(seat)                 -> [{id, type, ...}]   // ids are revision-scoped
act(seat, actionId)         -> {ok: true} | {ok: false, error}
status()                    -> {turn, activeSeat, winnerSeat, over}
hash()                      -> 64-hex canonical state hash
close()
```

## `ic-engine-1`: the same interface as a subprocess

`jsonl-engine.js` spawns any program that speaks this over stdin/stdout, one
JSON object per line, so the native C# core (or the Java rules bridge) can
serve both the referee and test clients without linking into Node:

| request | reply (`ok: true`) |
|---|---|
| `{"id":1,"op":"new","seed":"<64 hex>","players":2}` | `{"id":1,"ok":true,"turn":1,"active":0,"winner":null,"over":false}` |
| `{"id":2,"op":"legal","player":0}` | `{"id":2,"ok":true,"legal":[{"id":"r3-a1","type":"move"}]}` |
| `{"id":3,"op":"act","player":0,"action_id":"r3-a1"}` | `{"id":3,"ok":true}` or `{"id":3,"ok":false,"error_code":"INVALID_ACTION","error":"..."}` |
| `{"id":4,"op":"status"}` | `{"id":4,"ok":true,"turn":2,"active":1,"winner":null,"over":false}` |
| `{"id":5,"op":"hash"}` | `{"id":5,"ok":true,"state_hash":"<64 hex>"}` |

Rules for a conforming host:

- `act` for a player who is not allowed to act, or with an id that is not in
  that player's current `legal` list, fails with `INVALID_ACTION` and changes
  nothing. The referee treats that as an illegal intent by that seat.
- Same seed and same accepted actions give the same `state_hash` on every
  machine (the relay compares it every turn).
- Setup choices (capital tile, deck, mulligan) must either be derived from the
  seed or travel as ordinary actions, because the relay transcript holds only
  the seed and the ordered intents.
- Stdout carries replies only; diagnostics go to stderr.

`duel-engine.js` is a tiny deterministic reference engine that implements the
interface in-process. It exists to test the network and ranked pipeline end
to end; it is not the game's rules. The real referee plugs the native C# core
in through `ic-engine-1` (see `../docs/ranked-design.md`, section 4).
