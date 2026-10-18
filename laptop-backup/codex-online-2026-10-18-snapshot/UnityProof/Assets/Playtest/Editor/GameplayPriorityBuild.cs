using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using InfiniteConquest.Playtest;

public static class GameplayPriorityBuild {
    static readonly List<string> checks = new List<string>();
    static void Check(bool value, string label) {
        if (!value) throw new Exception("GAMEPLAY_VALIDATE FAIL " + label);
        checks.Add(label); Debug.Log("GAMEPLAY_VALIDATE PASS " + label);
    }
    static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
    static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, Private).GetValue(obj);
    static void Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Private).Invoke(obj, args);
    [MenuItem("Infinite Conquest/Validate gameplay priorities")]
    public static void Validate() {
        checks.Clear();
        var cast = new BridgeAction { id = "r7-a1", type = "cast", instance_id = "spell", card_id = "zeus_skybreaker_bolt", target_instance_id = "structure", target = new WireHex { x = 2, y = 3 } };
        var move = new BridgeAction { id = "r7-a2", type = "move", instance_id = "unit", to = new WireHex { x = 2, y = 3 } };
        var activate = new BridgeAction { id = "r7-a3", type = "activate", instance_id = "unit", at = new WireHex { x = 2, y = 3 } };
        var withoutHex = new BridgeAction { id = "r7-a4", type = "activate", instance_id = "unit" };
        var actions = new[] { cast, move, activate, withoutHex };
        Check(LiveActionSelection.AtTarget(LiveActionSelection.For(actions, "spell"), 2, 3).Single() == cast, "structure spell target survives action grouping");
        Check(LiveActionSelection.For(actions, "unit").Contains(withoutHex), "non-spatial ability remains selectable");
        Check(LiveActionSelection.AtTarget(LiveActionSelection.For(actions, "unit"), 2, 3).Length == 2, "ambiguous hex retains both actions");
        Check(LiveActionSelection.For(actions, "other").Length == 0, "actions do not leak between sources");
        Check(LiveActionSelection.AtTarget(actions, 0, 0).Length == 0, "invalid target does not invent an action");
        var decoded = BridgeClient.DecodeResponse("{\"ok\":true,\"state\":{\"slots_enabled\":true,\"players\":[{\"slots_total\":4,\"slots_used\":3,\"slots_available\":1,\"hand\":[{\"card_id\":\"huge\",\"slot_cost\":2}]}]},\"legal\":[{\"id\":\"a\",\"type\":\"cast\",\"instance_id\":\"spell\",\"target_instance_id\":\"structure\",\"target\":{\"x\":2,\"y\":3}}]}");
        Check(decoded.legal[0].target_instance_id == "structure" && decoded.state.players[0].hand[0].slot_cost == 2 && decoded.state.slots_enabled, "bridge preserves target identity and capacity fields");

        var wirePlay = BridgeClient.DecodeResponse("{\"ok\":true,\"legal\":[{\"id\":\"wire-land\",\"type\":\"play\",\"instance_id\":\"land\",\"to\":{\"x\":2,\"y\":1}}]}").legal[0];
        Debug.Log("WIRE_COORD_DIAGNOSTIC target=" + (wirePlay.target == null ? "null" : wirePlay.target.x + "," + wirePlay.target.y) + " destination=" + (wirePlay.destination == null ? "null" : wirePlay.destination.x + "," + wirePlay.destination.y));
        Check(LiveActionSelection.At(LiveActionSelection.Target(wirePlay), 2, 1), "decoded land highlights actual bridge placement instead of phantom origin");
        Check(wirePlay.destination == null, "decoded ordinary land has no relocation destination");
        var wireCast = decoded.legal[0];
        Check(wireCast.destination == null && LiveActionSelection.At(LiveActionSelection.Target(wireCast), 2, 3), "decoded ordinary spell targets chosen card without phantom confirmation");
        var wireRecall = BridgeClient.DecodeResponse("{\"ok\":true,\"legal\":[{\"type\":\"cast\",\"has_destination\":true,\"target\":{\"x\":2,\"y\":3},\"destination\":{\"x\":0,\"y\":0}}]}").legal[0];
        Check(wireRecall.destination != null && LiveActionSelection.At(wireRecall.destination, 0, 0), "real relocation to origin remains a separate destination choice");
        // Exercise the same handlers used by mouse input with an in-memory command writer.
        var host = new GameObject("Gameplay validation host"); host.SetActive(false);
        var game = host.AddComponent<PlaytestGame>();
        var boardObject = new GameObject("Gameplay validation board");
        var board = boardObject.AddComponent<BoardView>();
        var factory = new TokenFactory(Shader.Find("Universal Render Pipeline/Lit"), Shader.Find("Universal Render Pipeline/Unlit"), Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        var soundObject = new GameObject("Gameplay validation sound"); var sound = soundObject.AddComponent<SfxBank>();
        var writer = new StringWriter();
        GameObject structureObject = null;
        using (var bridge = new BridgeClient(null, writer, new StringReader(""))) {
            try {
                board.Build(factory);
                Set(game, "board", board); Set(game, "sfx", sound); Set(game, "bridge", bridge);
                var mode = typeof(PlaytestGame).GetField("mode", Private);
                mode.SetValue(game, Enum.Parse(mode.FieldType, "Live"));
                Set(game, "liveActions", actions);
                Set(game, "liveActions", new[] { wirePlay });
                Call(game, "SelectLive", "land");
                Check(board.IsLegal(new Vector2Int(2, 1)) && !board.IsLegal(new Vector2Int(0, 0)), "decoded land marks intended placement only");
                Call(game, "Click", new Vector2Int(2, 1), null);
                Check(writer.ToString().Contains("wire-land") && Get<bool>(game, "waiting"), "decoded land placement sends immediately from clicked hex");
                writer.GetStringBuilder().Clear(); Set(game, "waiting", false); Set(game, "liveActions", new[] { wireCast });
                Call(game, "SelectLive", wireCast.instance_id);
                Call(game, "Click", new Vector2Int(2, 3), null);
                Check(writer.ToString().Contains("\"a\"") && Get<bool>(game, "waiting"), "decoded ordinary spell sends on target click without extra tile");
                writer.GetStringBuilder().Clear(); Set(game, "waiting", false); Set(game, "liveActions", actions);
                Call(game, "SelectLive", "spell");
                Check(board.IsLegal(new Vector2Int(2, 3)), "Skybreaker target visibly marked legal");
                Call(game, "Click", new Vector2Int(0, 0), null);
                Check(writer.ToString().Length == 0 && Get<string>(game, "liveSelected") == "spell", "illegal click preserves selection and sends no command");
                Call(game, "Click", new Vector2Int(2, 3), null);
                Check(writer.ToString().Contains("r7-a1") && Get<bool>(game, "waiting"), "legal structure target sends exact bridge action");

                writer.GetStringBuilder().Clear(); Set(game, "waiting", false); Set(game, "liveActions", actions);
                Call(game, "SelectLive", "unit"); Call(game, "Click", new Vector2Int(2, 3), null);
                Check(writer.ToString().Length == 0 && Get<BridgeAction[]>(game, "actionChoices").Length == 2, "ambiguous target waits for explicit action choice");
                Call(game, "ActLive", activate);
                Check(writer.ToString().Contains("r7-a3"), "explicit activation uses its own action id");

                var recallA = new BridgeAction { id = "r8-a1", type = "cast", instance_id = "recall", target = new WireHex { x = 2, y = 3 }, destination = new WireHex { x = 3, y = 0 } };
                var recallB = new BridgeAction { id = "r8-a2", type = "cast", instance_id = "recall", target = new WireHex { x = 2, y = 3 }, destination = new WireHex { x = 2, y = 1 } };
                writer.GetStringBuilder().Clear(); Set(game, "waiting", false); Set(game, "liveActions", new[] { recallA, recallB });
                Call(game, "SelectLive", "recall"); Call(game, "Click", new Vector2Int(2, 3), null);
                Check(writer.ToString().Length == 0 && board.IsLegal(new Vector2Int(2, 1)) && board.IsLegal(new Vector2Int(3, 0)), "relocation target selection offers all legal destinations without casting");
                Call(game, "Click", new Vector2Int(2, 1), null);
                Check(writer.ToString().Contains("r8-a2") && !writer.ToString().Contains("r8-a1"), "chosen relocation destination sends corresponding action only");
                writer.GetStringBuilder().Clear();
                Call(game, "Click", new Vector2Int(3, 0), null);
                Check(writer.ToString().Length == 0, "waiting input cannot submit a second action");
                structureObject = factory.Create(PlaytestCatalog.Get("zeus_ability_oracle_spire"), 0, out _);
                var structure = structureObject.GetComponent<Piece>(); structure.InstanceId = "oracle-spire";
                board.Add(structure, 1, 1, true);
                Get<Dictionary<string, Piece>>(game, "pieces").Add(structure.InstanceId, structure);
                var structureAbility = new BridgeAction { id = "r9-a1", type = "activate", instance_id = structure.InstanceId, card_id = structure.Card.id, at = new WireHex { x = 1, y = 1 } };
                Set(game, "waiting", false); Set(game, "liveActions", new[] { structureAbility });
                Call(game, "SelectPiece", structure); Call(game, "SelectLive", structure.InstanceId);
                Check(Get<string>(game, "liveSelected") == structure.InstanceId && board.IsLegal(new Vector2Int(1, 1)), "actual Oracle Spire token selects its engine ability");
                var abilityLabel = (string)game.GetType().GetMethod("ActionLabel", Private).Invoke(game, new object[] { structureAbility });
                Check(abilityLabel == "Activate " + structure.Card.name, "structure action has explicit activation label");
                Call(game, "OpenAbilities", new Vector2Int(1, 1));
                Check(Get<BridgeAction[]>(game, "actionChoices").Single() == structureAbility && writer.ToString().Length == 0, "right-click ability handler offers choice before execution");
                Call(game, "ActLive", structureAbility);
                Check(writer.ToString().Contains("r9-a1"), "structure activation submits its own revision-scoped action");
            } finally {
                if (structureObject != null) UnityEngine.Object.DestroyImmediate(structureObject);
                UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(boardObject); UnityEngine.Object.DestroyImmediate(soundObject);
            }
        }
        Directory.CreateDirectory("Build");
        File.WriteAllText("Build/gameplay-priority-validation.json", "{\"passed\":true,\"checks\":" + checks.Count + "}");
    }
    public static void Build() { Validate(); Sp1AudioBuild.Build(); }
}
