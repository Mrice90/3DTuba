// AI-060-RIG-COMBAT: wires rigged token FBXs (Idle/Walk/Attack/Hit/Death/Summon takes) into the game.
// For every Resources/Tokens/<card>/<card>.fbx that carries those takes it sets a Generic import with clips,
// loops Idle/Walk, adds the impact/down/land animation events from <card>_rig_report.json if present, and
// builds Resources/Tokens/<card>/<card>_Rig.controller whose triggers match PresentationMotion's MotionKind names
// (Move->Walk, Attack->Attack, Ability->Attack, Hit->Hit, Summon->Summon, Destroy->Death).
// TokenFactory assigns the controller at spawn; PresentationMotion.PlayRig then fires the trigger.
// Menu: Infinite Conquest/Rig Combat Hookup. Batchmode: -executeMethod RigCombatHookup.Run
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class RigCombatHookup
{
    const string TokensRoot = "Assets/Playtest/Resources/Tokens";
    static readonly string[] Takes = { "Idle", "Walk", "Attack", "Hit", "Death", "Summon" };

    [MenuItem("Infinite Conquest/Rig Combat Hookup")]
    public static void Run()
    {
        int wired = 0;
        foreach (var dir in Directory.GetDirectories(TokensRoot))
        {
            string card = Path.GetFileName(dir);
            string fbx = $"{TokensRoot}/{card}/{card}.fbx";
            var imp = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (imp == null) continue;
            var takes = imp.importedTakeInfos.Select(t => t.name.Split('|').Last()).ToArray();
            if (!Takes.All(takes.Contains)) continue;
            Wire(card, fbx, imp);
            wired++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"RIGHOOKUP wired {wired} rigged tokens");
    }

    static void Wire(string card, string fbx, ModelImporter imp)
    {
        imp.animationType = ModelImporterAnimationType.Generic;
        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.importAnimation = true;
        imp.skinWeights = ModelImporterSkinWeights.Standard;
        var report = LoadEvents(Path.Combine(Path.GetDirectoryName(fbx), card + "_rig_report.json"));
        var clips = imp.defaultClipAnimations;
        foreach (var c in clips)
        {
            c.name = c.name.Split('|').Last();
            c.loopTime = c.name == "Idle" || c.name == "Walk";
            if (report.TryGetValue(c.name, out var ev))
            {
                float len = Mathf.Max(1, c.lastFrame - c.firstFrame);
                c.events = ev.Select(e => new AnimationEvent { functionName = "OnRigEvent", stringParameter = e.Key,
                    time = Mathf.Clamp01((e.Value - 1) / len) }).ToArray();
            }
        }
        imp.clipAnimations = clips;
        imp.SaveAndReimport();

        var byName = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
        string ctrlPath = $"{TokensRoot}/{card}/{card}_Rig.controller";
        AssetDatabase.DeleteAsset(ctrlPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        var sm = ctrl.layers[0].stateMachine;
        var states = Takes.ToDictionary(t => t, t => { var s = sm.AddState(t); s.motion = byName[t]; return s; });
        sm.defaultState = states["Idle"];
        void Trigger(string trigger, string take)
        {
            if (!ctrl.parameters.Any(p => p.name == trigger)) ctrl.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
            var t = sm.AddAnyStateTransition(states[take]);
            t.AddCondition(AnimatorConditionMode.If, 0, trigger);
            t.duration = .08f; t.canTransitionToSelf = true; t.hasExitTime = false;
        }
        Trigger("Move", "Walk"); Trigger("Attack", "Attack"); Trigger("Ability", "Attack");
        Trigger("Hit", "Hit"); Trigger("Summon", "Summon"); Trigger("Destroy", "Death");
        foreach (var take in new[] { "Walk", "Attack", "Hit", "Summon" })
        {
            var back = states[take].AddTransition(states["Idle"]);
            back.hasExitTime = true; back.exitTime = 1f; back.duration = .12f;
        }
        // Death holds its last pose; the piece is despawned by the game.
        EditorUtility.SetDirty(ctrl);
    }

    // Reads {"clips": {"Attack": {"events": {"impact": 15}}, ...}} without a JSON dependency.
    static System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, int>> LoadEvents(string path)
    {
        var result = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, int>>();
        if (!File.Exists(path)) return result;
        string json = File.ReadAllText(path);
        foreach (var take in Takes)
        {
            int i = json.IndexOf("\"" + take + "\"");
            if (i < 0) continue;
            int e = json.IndexOf("\"events\"", i), close = json.IndexOf('}', e);
            if (e < 0 || close < 0) continue;
            var body = json.Substring(json.IndexOf('{', e) + 1, close - json.IndexOf('{', e) - 1);
            var map = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var kv in body.Split(','))
            {
                var parts = kv.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out int f)) map[parts[0].Trim().Trim('"')] = f;
            }
            if (map.Count > 0) result[take] = map;
        }
        return result;
    }
}
