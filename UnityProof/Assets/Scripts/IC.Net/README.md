# IC.Net — relay-1 client library (Unity lane)

Pure C# client for the AI-097 lockstep match relay. **No UnityEngine
references**, `netstandard2.1`, C# 9, **zero dependencies** (the tiny
`JsonLite` parser is included — no NuGet restore needed inside Unity).

## Files

- `RelayClient.cs` — the WebSocket client (`System.Net.WebSockets.ClientWebSocket`).
- `RelayMessages.cs` — relay-1 message DTOs + outbound builders.
- `JsonLite.cs` — minimal JSON reader/writer (internal).

## Usage from Unity

```csharp
// Build the upgrade URI for a v2 room (roomId + seat from POST /v2/rooms/pair).
var uri = new Uri($"ws://{host}:{port}/rooms/{roomId}/ws?dataVersion=lab-2&seat={seat}");
var client = await RelayClient.ConnectAsync(uri, seat, roomId);

// Events fire on a thread-pool thread — marshal to the main thread
// before touching Unity objects.
client.IntentReceived += i => mainThread.Queue(() => bridge.Act(i.ActionId));
client.HashRequested += turn => mainThread.Queue(async () => {
    var hash = await bridge.HashAsync();          // rules-bridge {"op":"hash"}
    await client.SendHashAsync(turn, hash);
});
client.SeedReceived += seed => mainThread.Queue(() => StartMatch(seed));

await client.SendHelloAsync();                    // or SendHelloAsync(lastSeq) to resume
await client.CommitSeedAsync(seed, salt);         // SHA-256(seed|salt) computed inside
await client.RevealSeedAsync(seed, salt);         // SeedReceived fires with the final seed
await client.SendIntentAsync(actionId);           // sequenced + broadcast by the relay
await client.AnnounceTurnAsync(turn, activeSeat); // arms the relay turn timer
await client.DisposeAsync();                      // close
```

Reconnect resume: keep `client.LastSeq` (highest intent seq seen). On a
dropped socket, connect a fresh `RelayClient` for the same seat and call
`SendHelloAsync(lastSeq)` — the relay replies with `welcome` carrying the
missed intents in `RelayWelcome.Log`.

## Protocol

relay-1, specified in `prototypes/lobby-lab/docs/relay-design.md`. The
lab relay (`prototypes/lobby-lab/server.js`) speaks it over loopback for
local testing; production runs the same session in the `MatchRoom` Durable
Object.

## Wiring it into PlaytestGame (Unity lane)

`PlaytestGame` already drives a live human-vs-bot match through its own
`BridgeClient` (`Assets/Playtest/Scripts/BridgeClient.cs`: `New` / `Legal` /
`Act` over stdio). A relayed two-human match reuses that loop with the
relay as the sequencer. Do NOT edit anything under `Assets/Playtest/` from
this library — the sketch below is for the Unity lane to apply.

```csharp
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using IC.Net;
using InfiniteConquest.Playtest;

public sealed class RelayMatchDriver {
    readonly PlaytestGame game;          // your MonoBehaviour
    readonly BridgeClient bridge;        // the existing stdio bridge client
    readonly RelayClient relay;
    readonly ConcurrentQueue<Action> mainThread = new ConcurrentQueue<Action>();
    string seat, roomId;
    int humanSeat;

    public static async Task<RelayMatchDriver> Connect(
            PlaytestGame game, BridgeClient bridge,
            string relayHost, int relayPort, string roomId, string seat, int humanSeat) {
        var uri = new Uri($"ws://{relayHost}:{relayPort}/rooms/{roomId}/ws?dataVersion=lab-2&seat={seat}");
        var relay = await RelayClient.ConnectAsync(uri, seat, roomId);
        return new RelayMatchDriver(game, bridge, relay, roomId, seat, humanSeat);
    }

    void Hook() {
        // Events fire on a thread-pool thread: enqueue, drain in Update()/LiveLoop().
        relay.SeedReceived += seed => mainThread.Enqueue(() => {
            // Relay seed is 64 hex chars; bridge.New wants an int. Both clients
            // must map it identically: first 8 hex chars, unchecked to int.
            int bridgeSeed = unchecked((int)Convert.ToUInt64(seed.Substring(0, 8), 16));
            bridge.New(bridgeSeed, humanSeat);
        });
        relay.IntentReceived += intent => mainThread.Enqueue(() => {
            bridge.Act(intent.ActionId);   // sequenced by the relay; both sides apply the same order
        });
        relay.HashRequested += turn => mainThread.Enqueue(() => {
            // Needs a Hash() op on the Unity BridgeClient (see below): send
            // {"op":"hash"}, read state_hash from the response, then:
            // await relay.SendHashAsync(turn, stateHash);
        });
        relay.HashMismatched += mm => mainThread.Enqueue(() =>
            Debug.LogError($"HASH MISMATCH turn {mm.Turn}"));
    }

    public async Task StartAsync() {
        Hook();
        await relay.SendHelloAsync();                       // or SendHelloAsync(lastSeq) to resume
        var salt = Guid.NewGuid().ToString("N");
        await relay.CommitSeedAsync(Guid.NewGuid().ToString("N"), salt);
        await relay.RevealSeedAsync(/* same seed */ "", salt);
    }

    // Call from the human's action picker INSTEAD of bridge.Act directly:
    public Task PlayHumanAction(string actionId) => relay.SendIntentAsync(actionId);

    // After the bridge reports a new turn (r.state.turn increased):
    public Task OnNewTurn(long turn, string activeSeat) =>
        relay.AnnounceTurnAsync(turn, activeSeat);

    // Drain in MonoBehaviour.Update or inside LiveLoop():
    public void DrainMainThread() {
        while (mainThread.TryDequeue(out var a)) a();
    }
}
```

Two additions the Unity lane needs in its own `BridgeClient`
(`Assets/Playtest/Scripts/BridgeClient.cs` — do not edit from this library):

1. A hash query for protocol v1.1.0. The current Unity client only speaks
   v1.0.0 ops (`new`/`legal`/`act`). Add e.g.:
   ```csharp
   public void Hash() => Send("{\"id\":\"" + NextId() + "\",\"op\":\"hash\"}");
   // then TryReceive until the matching id arrives; read response.raw's "state_hash".
   ```
   The response is `{"id":…, "ok":true, "revision":N, "turn":T, "state_hash":"…"}` —
   read-only, no state mutation.
2. Keep `relay.LastSeq` (highest intent seq seen). On a dropped socket,
   connect a fresh `RelayClient` for the same seat and call
   `SendHelloAsync(lastSeq)` — the relay replays missed intents in
   `welcome.log`; apply each via `bridge.Act` in order before resuming.

## Notes

- The bridge `{"op":"hash"}` (rules-bridge protocol v1.1.0) produces the
  `stateHash` the relay compares each turn.
- `RelayClient.CommitFor(seed, salt)` is public for tests.
- Unknown inbound message types are ignored (forward compatibility).
