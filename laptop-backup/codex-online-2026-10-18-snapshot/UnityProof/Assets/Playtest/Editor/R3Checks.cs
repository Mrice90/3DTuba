using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using InfiniteConquest.Playtest;

// AI-080-RESTORATION-R3 editor checks: capital HP refresh (Storm Relay damage, capital heal), visible
// -N/+N numbers, visible engine rejection and the ability command center. They drive
// the same PlaytestGame handlers the mouse uses, with canned bridge lines and an in-memory command writer.
// Full chain: Unity -batchmode -quit -projectPath <r3>/UnityProof -executeMethod R3Build.Build -buildPath <exe>
public static class R3Build {
    static readonly List<string> checks = new List<string>();
    static void Check(bool ok, string label) {
        if (!ok) throw new Exception("R3_VALIDATE FAIL " + label);
        checks.Add(label); Debug.Log("R3_VALIDATE PASS " + label);
    }
    static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
    static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, Private).GetValue(obj);
    static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Private).Invoke(obj, args);
    // Runs a presentation coroutine to completion (speed 200 makes waits instant; motion handles are skipped).
    static void Drain(IEnumerator e, int depth = 0) {
        int guard = 0;
        while (guard++ < 10000 && e.MoveNext()) {
            var c = e.Current;
            if (c is CustomYieldInstruction) continue;
            if (c is IEnumerator inner && depth < 8) Drain(inner, depth + 1);
        }
    }
    static int Floats(string text) => UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None).Count(t => t.gameObject.name == "Float " + text);

    const string Cap0 = "aaaaaaaa-0000-0000-0000-000000000000", Cap1 = "bbbbbbbb-0000-0000-0000-000000000000";
    static string Capitals(int hp0, int hp1) =>
        "\"capitals\":[{\"seat\":0,\"instance_id\":\"" + Cap0 + "\",\"card_id\":\"zeus_capital_olympus_citadel\",\"max_hp\":20,\"damage\":" + (20 - hp0) + ",\"hp\":" + hp0 + ",\"x\":3,\"y\":0},"
        + "{\"seat\":1,\"instance_id\":\"" + Cap1 + "\",\"card_id\":\"poseidon_capital_atlantis_nexus\",\"max_hp\":20,\"damage\":" + (20 - hp1) + ",\"hp\":" + hp1 + ",\"x\":2,\"y\":5}]";
    static string Abilities(bool relayUsed) =>
        "\"abilities\":["
        + "{\"instance_id\":\"zland\",\"card_id\":\"zeus_tutor_land_1\",\"name\":\"Tutor Land\",\"type\":\"LAND\",\"x\":2,\"y\":1,\"buried\":true,\"effect\":\"Draw 1 Structure\",\"effect_type\":\"DRAW_STRUCTURE\",\"gp_cost\":2,\"status\":\"READY\",\"reason\":\"\",\"action_id\":\"r5-a2\"},"
        + "{\"instance_id\":\"zspire\",\"card_id\":\"zeus_ability_oracle_spire\",\"name\":\"Oracle Spire\",\"type\":\"STRUCTURE\",\"x\":2,\"y\":1,\"buried\":false,\"effect\":\"Draw 1 card\",\"effect_type\":\"DRAW_CARD\",\"gp_cost\":2,\"status\":\"USED\",\"reason\":\"Ability already used this turn\"},"
        + "{\"instance_id\":\"relay\",\"card_id\":\"zeus_storm_relay_pylon\",\"name\":\"Storm Relay Pylon\",\"type\":\"STRUCTURE\",\"x\":1,\"y\":1,\"buried\":false,\"effect\":\"Deal 1 damage to the enemy Capital (needs line of sight)\",\"effect_type\":\"DAMAGE_ENEMY_CAPITAL\",\"gp_cost\":2,"
        + (relayUsed ? "\"status\":\"USED\",\"reason\":\"Ability already used this turn\"}," : "\"status\":\"READY\",\"reason\":\"\",\"action_id\":\"r5-a1\"},")
        + "{\"instance_id\":\"eagle\",\"card_id\":\"zeus_eagles_perch_array\",\"name\":\"Eagle's Perch Array\",\"type\":\"LAND\",\"x\":3,\"y\":2,\"buried\":false,\"effect\":\"Deal 2 damage to the enemy Capital (needs line of sight)\",\"effect_type\":\"DAMAGE_ENEMY_CAPITAL\",\"gp_cost\":3,\"status\":\"BLOCKED\",\"reason\":\"No line of sight to the enemy Capital - blocked by cover\"}]";
    static string Stack(int x, int y, params string[] cards) => "{\"x\":" + x + ",\"y\":" + y + ",\"stack\":[" + string.Join(",", cards) + "]}";
    static string Card(string id, string cardId, int owner) => "{\"instance_id\":\"" + id + "\",\"card_id\":\"" + cardId + "\",\"owner\":" + owner + "}";
    static string Board => "\"board\":[" + string.Join(",", Stack(1, 0, Card(Cap0, "zeus_capital_olympus_citadel", 0)), Stack(1, 1, Card("relay", "zeus_storm_relay_pylon", 0)),
        Stack(0, 1, Card("zland", "zeus_tutor_land_1", 0), Card("zspire", "zeus_ability_oracle_spire", 0)), Stack(3, 2, Card("eagle", "zeus_eagles_perch_array", 0)),
        Stack(2, 5, Card(Cap1, "poseidon_capital_atlantis_nexus", 1))) + "]";
    static string Line(string extraState, string legal, string events = "[]", string extra = "") =>
        "{\"id\":\"x\",\"ok\":true,\"revision\":5,\"events\":" + events + ",\"state\":{\"turn\":3,\"phase\":\"MAIN\",\"active_player\":0,\"acting_player\":0,\"starting_player\":0,\"slots_enabled\":true,"
        + "\"players\":[{\"seat\":0,\"gp\":6,\"hand_count\":0,\"hand\":[]},{\"seat\":1,\"gp\":3,\"hand_count\":5,\"hand\":[]}]," + Board + "," + extraState + "},\"legal\":" + legal + extra + "}";

    [MenuItem("Infinite Conquest/Validate R3 (capital HP, command center)")]
    public static void Validate() {
        checks.Clear();
        // ---- decoder
        var legal = "[{\"id\":\"r5-a1\",\"type\":\"activate\",\"instance_id\":\"relay\",\"card_id\":\"zeus_storm_relay_pylon\",\"at\":{\"x\":1,\"y\":1}},{\"id\":\"r5-a2\",\"type\":\"activate\",\"instance_id\":\"zland\",\"card_id\":\"zeus_tutor_land_1\",\"at\":{\"x\":2,\"y\":1}},{\"id\":\"r5-a9\",\"type\":\"end_turn\"}]";
        var r1 = BridgeClient.DecodeResponse(Line(Capitals(20, 20) + "," + Abilities(false), legal));
        Check(r1.state.HasCapitals && r1.state.capitals[1].hp == 20 && r1.state.capitals[1].max_hp == 20 && r1.state.capitals[1].instance_id == Cap1, "decoder reads capital hp/max_hp/instance");
        Check(r1.state.abilities.Length == 4 && r1.state.abilities[0].buried && r1.state.abilities.Count(a => a.Ready) == 2, "decoder reads ability list (buried flag, two ready)");
        Check(!new BridgeAbility { status = "READY" }.Ready, "an entry without a legal action id is never treated as ready");
        var legacy = BridgeClient.DecodeResponse("{\"ok\":true,\"state\":{\"players\":[{\"hand\":[]}]},\"legal\":[]}");
        Check(!legacy.state.HasCapitals, "legacy bridge lines keep the old HP behaviour");

        // ---- requests
        var reqWriter = new StringWriter();
        using (var reqBridge = new BridgeClient(null, reqWriter, new StringReader(""))) {
            reqBridge.New(42, 0, "HERO", 1, 0, false, clientMetadata: true);
            Check(reqWriter.ToString().Contains("\"client_metadata\":true") && !reqWriter.ToString().Contains("interactive_mulligan"), "solo new requests client metadata and no mulligan (setup as before)");
            reqWriter.GetStringBuilder().Clear();
            reqBridge.New(42, 0, "HERO", -1, -1, true, clientMetadata: true);
            Check(reqWriter.ToString().Contains("\"multiplayer\":true") && reqWriter.ToString().Contains("\"client_metadata\":true") && !reqWriter.ToString().Contains("interactive_mulligan"), "online (multiplayer) new also requests capital HP metadata");
        }

        // ---- R3b: line-of-sight wording on card faces (display only)
        Check(CardFaces.Get("zeus_storm_relay_pylon").rulesText.EndsWith(CardFaces.LineOfSightNote), "Storm Relay card face says it needs line of sight to the enemy Capital");
        Check(CardFaces.Get("zeus_eagles_perch_array").rulesText.EndsWith(CardFaces.LineOfSightNote), "Eagle's Perch Array card face says it needs line of sight");
        Check(!CardFaces.Get("zeus_keraunos_charging_spire").rulesText.Contains("line of sight"), "Keraunos death trigger (no sight check in the engine) is unchanged");
        Check(CardFaces.WithLineOfSight(CardFaces.Get("zeus_storm_relay_pylon").rulesText) == CardFaces.Get("zeus_storm_relay_pylon").rulesText, "line-of-sight note is added once");
        { var t = CardFaces.Get("zeus_storm_relay_pylon").rulesText; Check(t.StartsWith("Activate once per turn") && t.Contains("2 GP: deal 1 damage to the enemy Capital. " + CardFaces.LineOfSightNote), "Storm Relay rules text otherwise unchanged"); }

        // ---- live handlers
        var host = new GameObject("R3 validation host"); host.SetActive(false);
        var game = host.AddComponent<PlaytestGame>();
        var boardObject = new GameObject("R3 validation board"); var board = boardObject.AddComponent<BoardView>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var factory = new TokenFactory(Shader.Find("Universal Render Pipeline/Lit"), Shader.Find("Universal Render Pipeline/Unlit"), font);
        var soundObject = new GameObject("R3 validation sound"); var sound = soundObject.AddComponent<SfxBank>(); sound.Muted = true;
        var rigObject = new GameObject("R3 validation camera"); var rig = rigObject.AddComponent<CameraRig>(); rig.InputEnabled = false;
        var writer = new StringWriter();
        var spawned = new List<GameObject>();
        using (var bridge = new BridgeClient(null, writer, new StringReader(""))) {
            try {
                board.Build(factory);
                Set(game, "board", board); Set(game, "sfx", sound); Set(game, "bridge", bridge); Set(game, "rig", rig);
                Set(game, "factory", factory); Set(game, "vfx", new Vfx(factory.Glow(Color.white), factory, font)); Set(game, "speed", 200f);
                var mode = typeof(PlaytestGame).GetField("mode", Private); mode.SetValue(game, Enum.Parse(mode.FieldType, "Live"));
                var pieces = Get<Dictionary<string, Piece>>(game, "pieces");
                Piece Add(string id, string cardId, int owner, int x, int y) {
                    var go = factory.Create(PlaytestCatalog.Get(cardId), owner, out _); spawned.Add(go);
                    var p = go.GetComponent<Piece>(); p.InstanceId = id; board.Add(p, x, y, true); pieces[id] = p; return p;
                }
                Add(Cap0, "zeus_capital_olympus_citadel", 0, 3, 0); var cap1 = Add(Cap1, "poseidon_capital_atlantis_nexus", 1, 2, 5);
                Add("relay", "zeus_storm_relay_pylon", 0, 1, 1); Add("zland", "zeus_tutor_land_1", 0, 2, 1); Add("zspire", "zeus_ability_oracle_spire", 0, 2, 1);
                var eagle = Add("eagle", "zeus_eagles_perch_array", 0, 3, 2);
                void Receive(BridgeResponse r) {   // LiveLoop's handling of one response, minus the frame waits
                    if (r.events != null) foreach (var e in r.events) { e.hasAmount = true; Drain((IEnumerator)Call(game, "Apply", e)); }
                    Set(game, "liveRevision", r.revision);
                    Set(game, "liveState", r.state); Call(game, "SyncState", r.state);
                    Set(game, "liveActions", r.legal ?? new BridgeAction[0]);
                    Set(game, "waiting", false);
                }
                Receive(r1);
                var model = Get<MatchModel>(game, "model");
                Check(model.CapitalHp[0] == 20 && model.CapitalHp[1] == 20 && model.CapitalMax[1] == 20 && Floats("-0") == 0, "capital HP and max come from the snapshot");

                // Command center: exact instance, highlight, confirm sends that action only.
                var relay = r1.state.abilities.First(a => a.instance_id == "relay");
                Call(game, "CommandCenterPick", relay);
                Check(Get<string>(game, "liveSelected") == "relay" && Get<string>(game, "commandSelected") == "relay", "command center selects the Storm Relay instance");
                Check(board.IsLegal(new Vector2Int(1, 1)) && board.IsLegal(new Vector2Int(2, 5)), "source hex and enemy capital target are highlighted");
                Check(writer.ToString().Length == 0, "picking an entry sends nothing before confirmation");
                Call(game, "CommandCenterConfirm");
                Check(writer.ToString().Contains("\"action_id\":\"r5-a1\"") && Get<bool>(game, "waiting"), "confirm sends exactly the Storm Relay action id");
                writer.GetStringBuilder().Clear(); Set(game, "waiting", false); Set(game, "liveActions", r1.legal);
                Call(game, "CommandCenterPick", r1.state.abilities.First(a => a.instance_id == "zland"));
                Check(Get<string>(game, "liveSelected") == "zland" && board.Selected == new Vector2Int(2, 1), "buried land under a structure is selected as its own instance");
                Call(game, "CommandCenterConfirm");
                Check(writer.ToString().Contains("\"action_id\":\"r5-a2\"") && !writer.ToString().Contains("r5-a1"), "buried source confirm sends its own action, not the top card's");
                writer.GetStringBuilder().Clear(); Set(game, "waiting", false); Set(game, "liveActions", r1.legal);
                Call(game, "CommandCenterPick", r1.state.abilities.First(a => a.instance_id == "eagle"));
                Check(Get<string>(game, "liveSelected") == null && Get<string>(game, "liveStatus").Contains("line of sight") && board.IsLegal(new Vector2Int(2, 5)), "LOS-blocked source shows its reason and the target it cannot see");
                Call(game, "CommandCenterConfirm");
                Check(writer.ToString().Length == 0, "a blocked source cannot be confirmed");
                Call(game, "CommandCenterPick", r1.state.abilities.First(a => a.instance_id == "zspire"));
                Check(Get<string>(game, "liveStatus").Contains("already used"), "used source says it was used this turn");
                Call(game, "ClearSelection");
                Check((bool)Call(game, "ExplainBlockedSource", eagle) && Get<string>(game, "liveStatus").Contains("No line of sight"), "clicking a blocked piece explains why");

                // Storm Relay resolves: ability event, CAPITAL_HIT, snapshot 19.
                var hitEvents = "[{\"turn\":3,\"player\":0,\"event\":\"CARD_ABILITY_TRIGGERED\",\"instance_id\":\"relay\",\"amount\":1,\"detail\":\"relay ACTIVATED DAMAGE_ENEMY_CAPITAL 1 cost 2\"},"
                    + "{\"turn\":3,\"player\":1,\"event\":\"CAPITAL_HIT\",\"instance_id\":\"" + Cap1 + "\",\"amount\":1,\"detail\":\"1 capital damage to poseidon_capital_atlantis_nexus\"}]";
                int floatsBefore = Floats("-1");
                Receive(BridgeClient.DecodeResponse(Line(Capitals(20, 19) + "," + Abilities(true), "[{\"id\":\"r6-a0\",\"type\":\"end_turn\"}]", hitEvents)));
                Check(model.CapitalHp[1] == 19, "Storm Relay activation lowers the displayed enemy capital HP (20 -> 19)");
                Check(Floats("-1") == floatsBefore + 1, "a visible -1 number appears over the enemy capital");
                Check(Get<BridgeState>(game, "liveState").abilities.First(a => a.instance_id == "relay").status == "USED", "Storm Relay shows used after activation");
                // A change only the snapshot reports (no event) still reaches the HUD, with its number.
                floatsBefore = Floats("-1");
                Receive(BridgeClient.DecodeResponse(Line(Capitals(20, 18) + "," + Abilities(true), "[]")));
                Check(model.CapitalHp[1] == 18 && Floats("-1") == floatsBefore + 1, "snapshot-only capital change refreshes HP and shows the number");

                // Heal on a damaged own capital.
                Receive(BridgeClient.DecodeResponse(Line(Capitals(14, 18) + "," + Abilities(true), "[]")));
                Check(model.CapitalHp[0] == 14, "own capital damage shows (20 -> 14)");
                int healFloats = Floats("+4");
                var healEvents = "[{\"turn\":4,\"player\":0,\"event\":\"GP_SPENT\",\"amount\":2,\"detail\":\"2 for Restorative Tide\"},"
                    + "{\"turn\":4,\"player\":0,\"event\":\"CAPITAL_HEALED\",\"instance_id\":\"" + Cap0 + "\",\"amount\":4,\"detail\":\"4 capital healing to zeus_capital_olympus_citadel\"}]";
                Receive(BridgeClient.DecodeResponse(Line(Capitals(18, 18) + "," + Abilities(true), "[]", healEvents)));
                Check(model.CapitalHp[0] == 18, "a heal on the damaged capital raises the displayed HP (14 -> 18)");
                Check(Floats("+4") == healFloats + 1, "a visible +4 number appears over the healed capital");
                Check(model.Log.Any(l => l.Contains("Capital heals 4")), "heal is written to the match log");

                // Online (two-human) seat 1: both capitals follow the multiplayer snapshot and events.
                Set(game, "humanSeat", 1); Call(game, "ResetR3");
                Receive(BridgeClient.DecodeResponse(Line(Capitals(18, 18) + ",\"multiplayer\":true,\"abilities\":[]", "[]")));
                int f2 = Floats("-2"), f3 = Floats("+3");
                var onlineEvents = "[{\"turn\":6,\"player\":1,\"event\":\"CAPITAL_HIT\",\"instance_id\":\"" + Cap1 + "\",\"amount\":2,\"detail\":\"2 capital damage to poseidon_capital_atlantis_nexus\"},"
                    + "{\"turn\":6,\"player\":0,\"event\":\"CAPITAL_HEALED\",\"instance_id\":\"" + Cap0 + "\",\"amount\":2,\"detail\":\"2 capital healing to zeus_capital_olympus_citadel\"}]";
                Receive(BridgeClient.DecodeResponse(Line(Capitals(20, 16) + ",\"multiplayer\":true,\"abilities\":[]", "[]", onlineEvents)));
                Check(model.CapitalHp[1] == 16 && model.CapitalHp[0] == 20, "online seat-1 client applies damage to its own capital and healing to the opponent's");
                Check(Floats("-2") == f2 + 1 && Floats("+2") >= 1, "online client shows -2 and +2 numbers on the right capitals");
                Receive(BridgeClient.DecodeResponse(Line(Capitals(17, 16) + ",\"multiplayer\":true,\"abilities\":[]", "[]")));
                Check(model.CapitalHp[0] == 17 && Floats("-3") >= 1, "online snapshot-only change on the opponent capital reaches the HUD with its number");
                Set(game, "humanSeat", 0);

                // Engine rejection is visible.
                Call(game, "ShowRejection", "engine rejected action: No line of sight to the enemy Capital — blocked by cover");
                Check(Get<string>(game, "liveStatus").StartsWith("Not allowed: No line of sight") && Get<string>(game, "banner").Contains("No line of sight"), "engine rejection reason shows in the status line and banner");
                Check(model.Log.Last().StartsWith("Rejected:"), "rejection is logged");

            } finally {
                foreach (var go in spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
                foreach (var t in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None)) if (t.gameObject.name.StartsWith("Float ")) UnityEngine.Object.DestroyImmediate(t.gameObject);
                foreach (var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) if (ps.gameObject.name == "Burst") UnityEngine.Object.DestroyImmediate(ps.gameObject);
                UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(boardObject);
                UnityEngine.Object.DestroyImmediate(soundObject); UnityEngine.Object.DestroyImmediate(rigObject);
            }
        }
        Directory.CreateDirectory("Build");
        File.WriteAllText("Build/r3-validation.json", "{\"passed\":true,\"checks\":" + checks.Count + ",\"names\":[" + string.Join(",", checks.Select(c => "\"" + c.Replace("\"", "'") + "\"")) + "]}");
    }

    public static void Build() { Validate(); CameraSnapChecks.Run(); CoinPrototypeBuild.Build(); }
}
