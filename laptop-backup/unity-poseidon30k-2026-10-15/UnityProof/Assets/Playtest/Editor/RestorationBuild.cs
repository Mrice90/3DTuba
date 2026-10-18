using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using InfiniteConquest.Playtest;

public static class RestorationBuild {
    static int checks;
    static void Check(bool ok, string name) { if (!ok) throw new Exception("RESTORATION FAIL " + name); checks++; Debug.Log("RESTORATION PASS " + name); }
    public static void Build() {
        AssetDatabase.Refresh();
        Check(PlaytestGame.ValidCapitalCell(0, new Vector2Int(3,0)), "Zeus capital accepts own plot");
        Check(!PlaytestGame.ValidCapitalCell(0, new Vector2Int(0,4)), "Zeus rejects opponent plot");
        Check(PlaytestGame.ValidCapitalCell(1, new Vector2Int(3,6)), "Poseidon capital accepts own plot");
        Check(!PlaytestGame.ValidCapitalCell(1, new Vector2Int(6,7)), "capital rejects outside board");
        var land = CardFaces.Get("zeus_ability_stormfront");
        Check(CardFaces.StatLine(land).Contains("TURN " + land.cost), "land turn requirement explicitly labelled");
        Check(CardFaces.StatLine(CardFaces.Get("zeus_ion_storm_lattice")).Contains("1/type/turn"), "structure per-turn restriction labelled");
        Check(CardFaces.Get("poseidon_tutor_land_1").cost > 0, "generated tutor face has actual engine turn value");
        var decoded = BridgeClient.DecodeResponse("{\"ok\":true,\"state\":{\"reaction_pending\":true,\"starting_player\":1,\"players\":[{\"personal_turn\":4}]},\"legal\":[{\"type\":\"pass_reaction\"}]}");
        Check(decoded.state.reaction_pending && decoded.state.starting_player == 1 && decoded.state.players[0].personal_turn == 4, "reaction/coin/personal turn survive actual decoder");
        var host = new GameObject("Audio restoration validation"); var sfx = host.AddComponent<SfxBank>();
        int bindings = 0;
        foreach (var card in PlaytestCatalog.All) {
            if (card.HasModel) {
                string dir = "Assets/Playtest/Resources/" + Path.GetDirectoryName(card.model).Replace('\\','/');
                TokenPreview.BuildMaterial(dir);
                Check(Resources.Load<GameObject>(card.model) != null, "model loads " + card.id);
            }
            foreach (var cue in card.sfx) { sfx.Resolve(card.id, cue, out var specific); Check(specific, "clip loads " + card.id + " " + cue); bindings++; }
        }
        sfx.Resolve("zeus_ability_stormfront", "ability", out var ownLandClip);
        Check(ownLandClip, "land ability reuses its own staged sound");
        UnityEngine.Object.DestroyImmediate(host);
        Directory.CreateDirectory("Build");
        File.WriteAllText("Build/restoration-validation.json", "{\"passed\":true,\"checks\":" + checks + ",\"audio_bindings\":" + bindings + "}");
        ModelReferences.Render();
        GameplayPriorityBuild.Build();
    }
}
