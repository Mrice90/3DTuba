// IC.RefereeHost: serves the native C# rules core over ic-engine-1
// (prototypes/lobby-lab/engine/README.md) for the ranked referee and for
// headless lockstep test clients.
//
// It starts matches exactly the way the Unity client's online mode does
// (OnlineMatch.cs / BridgeClient.New with multiplayer:true): the same `new`
// request and the same relay-seed mapping, so a replay here reaches the
// same state hash the two players' clients reported.
//
// Usage: dotnet IC.RefereeHost.dll <NativeRules data folder>

using System;
using System.Text.Json.Nodes;
using InfiniteConquest.Native;

namespace InfiniteConquest.Referee {
    public static class Program {
        static NativeRulesSession session;
        static JsonNode state;
        static readonly string dataDir = Environment.GetEnvironmentVariable("IC_NATIVE_RULES_DATA") ?? "native/data";
        static int bridgeId;

        public static int Main(string[] args) {
            var data = args.Length > 0 ? args[0] : dataDir;
            string line;
            while ((line = Console.In.ReadLine()) != null) {
                if (line.Trim().Length == 0) continue;
                JsonNode id = null;
                JsonObject reply;
                try {
                    var req = JsonNode.Parse(line).AsObject();
                    id = req["id"]?.DeepClone();
                    reply = Handle(req, data);
                } catch (Exception ex) {
                    reply = Error("BAD_REQUEST", ex.Message);
                }
                reply["id"] = id;
                Console.Out.WriteLine(reply.ToJsonString());
                Console.Out.Flush();
            }
            return 0;
        }

        /// The Unity client's mapping from the relay's 64-hex seed to the engine seed.
        public static int EngineSeed(string relaySeed) => unchecked((int)Convert.ToUInt32(relaySeed.Substring(0, 8), 16));

        static JsonObject Handle(JsonObject req, string data) {
            var op = (string)req["op"];
            if (op == "new") {
                var seed = (string)req["seed"];
                if (seed == null || seed.Length != 64) return Error("BAD_REQUEST", "seed must be the relay's 64-hex seed");
                session = new NativeRulesSession(data);
                // Identical to BridgeClient.New(seed, humanSeat: 0, "HERO", multiplayer: true). The state
                // hash is the same for either human seat (checked), so seat 0 stands for both clients.
                var r = Bridge("{\"op\":\"new\",\"seed\":" + EngineSeed(seed) + ",\"human_player\":0,\"human_faction\":\"ZEUS\","
                    + "\"bot_faction\":\"POSEIDON\",\"difficulty\":\"HERO\",\"interactive_reactions\":true,\"multiplayer\":true}");
                if (!(bool)r["ok"]) return Forward(r);
                state = r["state"];
                return Status();
            }
            if (session == null) return Error("NO_MATCH", "send new first");
            switch (op) {
                case "status": return Status();
                case "legal": {
                    var player = (int)req["player"];
                    var legal = new JsonArray();
                    if (!Over() && player == Acting()) {
                        var r = Bridge("{\"op\":\"legal\"}");
                        if (!(bool)r["ok"]) return Forward(r);
                        foreach (var a in r["legal"].AsArray()) legal.Add(a.DeepClone());
                    }
                    return new JsonObject { ["ok"] = true, ["legal"] = legal };
                }
                case "act": {
                    var player = (int)req["player"];
                    var actionId = (string)req["action_id"] ?? "";
                    if (Over()) return Error("INVALID_ACTION", "the game is over");
                    var r = Bridge(new JsonObject { ["op"] = "act", ["action_id"] = actionId, ["seat"] = player }.ToJsonString());
                    if (!(bool)r["ok"]) return Forward(r);
                    state = r["state"];
                    return new JsonObject { ["ok"] = true };
                }
                case "hash": {
                    var r = Bridge("{\"op\":\"hash\"}");
                    if (!(bool)r["ok"]) return Forward(r);
                    return new JsonObject { ["ok"] = true, ["state_hash"] = (string)r["state_hash"] };
                }
                default: return Error("BAD_REQUEST", "unknown op " + op);
            }
        }

        static JsonObject Bridge(string json) {
            var req = JsonNode.Parse(json).AsObject();
            req["id"] = "b" + (++bridgeId);
            return JsonNode.Parse(session.Handle(req.ToJsonString())).AsObject();
        }

        static int Acting() => (int)state["acting_player"];
        static bool Over() => state["winner"] != null || (string)state["phase"] == "GAME_OVER";

        static JsonObject Status() => new JsonObject {
            ["ok"] = true,
            ["turn"] = (int)state["turn"],
            ["active"] = Over() ? null : Acting(),
            ["winner"] = state["winner"]?.DeepClone(),
            ["over"] = Over(),
        };

        static JsonObject Forward(JsonObject r) => Error((string)r["error_code"] ?? "ERROR", (string)r["error"] ?? "");
        static JsonObject Error(string code, string message) => new JsonObject { ["ok"] = false, ["error_code"] = code, ["error"] = message };
    }
}
