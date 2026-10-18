using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using InfiniteConquest.Playtest;
// AI-060-RIG-COMBAT: the 48 Zeus/Poseidon character tokens were swapped in place (same Resources/Tokens path, name
// and .meta GUID) for the rigged FBXs from playtest/rig-combat-2026-10-15/models-48 (Idle/Walk/Attack/Hit/Death/Summon).
// The previous tokens are kept in Assets/RigCompare/<card>.fbx (not in Resources, so not in the build) for comparison.
// Check: wires the clips and controllers (RigCombatHookup), then for every card checks the import (six clips, loops,
// events, controller triggers), that TokenFactory still builds it at the same in-game size as before, and samples
// every clip to measure ground contact. Build: Check, then the inherited structures + full build chain.
// Render: old token next to the rigged one in each clip pose, through TokenFactory as in a match.
public static class RigCombatBuild {
    const string Tokens = "Assets/Playtest/Resources/Tokens";
    static readonly string[] Takes = { "Idle", "Walk", "Attack", "Hit", "Death", "Summon" };
    static readonly string[] Triggers = { "Move", "Attack", "Summon", "Destroy", "Ability", "Hit" };

    public static string[] Ids() => Directory.GetFiles("Assets/RigCompare", "*.fbx").Select(Path.GetFileNameWithoutExtension).OrderBy(s => s).ToArray();

    static TokenFactory Factory() => new TokenFactory(Shader.Find("Universal Render Pipeline/Lit"), Shader.Find("Universal Render Pipeline/Unlit"), Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

    public static void Build() { Check(); StructureModelsBuild.Build(); }

    public static void Check() {
        int checks = 0; var fails = new List<string>();
        Action<bool, string> check = (ok, m) => { checks++; if (!ok) fails.Add(m); };
        RigCombatHookup.Run();
        PlaytestCatalog.Load();
        var factory = Factory();
        var ids = Ids();
        check(ids.Length == 47, "47 rigged cards (Thunder Ram kept static), found " + ids.Length);
        var rows = new List<string>();
        foreach (var id in ids) {
            var c = PlaytestCatalog.All.FirstOrDefault(x => x.id == id);
            check(c != null && c.type == "CHARACTER", "character card " + id);
            if (c == null) continue;
            check(c.model == "Tokens/" + id + "/" + id, "model ref unchanged " + id + " " + c.model);
            string fbx = Tokens + "/" + id + "/" + id + ".fbx";
            var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
            check(imp.animationType == ModelImporterAnimationType.Generic && imp.importAnimation, "generic import with animation " + id);
            var clips = imp.clipAnimations.ToDictionary(x => x.name);
            foreach (var t in Takes) check(clips.ContainsKey(t), "take " + t + " " + id);
            if (!Takes.All(clips.ContainsKey)) continue;
            check(clips["Idle"].loopTime && clips["Walk"].loopTime && !clips["Attack"].loopTime && !clips["Death"].loopTime, "loops " + id);
            check(clips["Attack"].events.Any(e => e.stringParameter == "impact"), "impact event " + id);
            check(clips["Death"].events.Any(e => e.stringParameter == "down"), "down event " + id);
            check(clips["Summon"].events.Any(e => e.stringParameter == "land"), "land event " + id);
            var ctrl = Resources.Load<RuntimeAnimatorController>(c.model + "_Rig") as AnimatorController;
            check(ctrl != null, "controller resolves " + id);
            if (ctrl != null) foreach (var tr in Triggers) check(ctrl.parameters.Any(p => p.name == tr && p.type == AnimatorControllerParameterType.Trigger), "trigger " + tr + " " + id);

            var go = factory.Create(c, 1, out bool real);
            check(real, "real model via TokenFactory " + id);
            var piece = go.GetComponent<Piece>();
            var model = go.transform.Find("Model");
            var anim = model.GetComponentInChildren<Animator>();
            var smr = model.GetComponentInChildren<SkinnedMeshRenderer>();
            check(anim != null && anim.runtimeAnimatorController != null, "animator has controller " + id);
            check(anim != null && anim.GetComponent<RigEvents>() != null, "rig event receiver " + id);
            check(smr != null && smr.sharedMesh != null && smr.bones.Length > 1, "skinned mesh " + id);
            int tris = smr != null && smr.sharedMesh != null ? smr.sharedMesh.triangles.Length / 3 : 0;
            check(model.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterials.All(m => m != null && m.name == "Token")), "token material " + id);

            // Previous token through the same AI-063 budget, for an in-game size comparison.
            var oldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RigCompare/" + id + ".fbx");
            var old = UnityEngine.Object.Instantiate(oldPrefab);
            var ob = TokenFactory.Bounds(old);
            PlaytestCatalog.Budget(c.type, out float bh, out float bf);
            float os = Mathf.Min(bh / Mathf.Max(.001f, ob.size.y), bf / Mathf.Max(.001f, Mathf.Max(ob.size.x, ob.size.z)));
            Vector3 oldSize = ob.size * os;
            UnityEngine.Object.DestroyImmediate(old);
            // Rigged size from the posed mesh itself (renderer bounds are padded for clip culling).
            Vector3 newSize = Vector3.zero; float restLo = 0;
            if (smr != null) {
                // BakeMesh ignores the parent's budget scale, so map the unscaled bake through the full transform.
                var rest = new Mesh(); smr.BakeMesh(rest, false);
                var rm = smr.transform.localToWorldMatrix;
                var rb = new Bounds(rm.MultiplyPoint3x4(rest.vertices[0]), Vector3.zero);
                foreach (var v in rest.vertices) rb.Encapsulate(rm.MultiplyPoint3x4(v));
                newSize = rb.size; restLo = rb.min.y; UnityEngine.Object.DestroyImmediate(rest);
            }
            // BakeMesh output carries the parent budget scale in a version-dependent way, so clip measurements
            // are taken relative to the baked rest pose and the in-game size comes from piece.Height (tight bounds).
            float bakeRestH = Mathf.Max(.0001f, newSize.y);
            float hRatio = piece.Height / Mathf.Max(.001f, oldSize.y);
            float wRatio = Mathf.Max(newSize.x, newSize.z) / bakeRestH * piece.Height / Mathf.Max(.001f, Mathf.Max(oldSize.x, oldSize.z));

            // Ground contact: bake the skinned mesh at 13 points of every clip and measure its lowest vertex.
            var perClip = new List<string>(); float worstSink = 0, worstFloat = 0; string worstClip = "";
            float rootY = restLo, H = bakeRestH, maxTop = 0;
            var baked = new Mesh();
            if (anim != null && smr != null) foreach (var t in Takes) {
                var clip = anim.runtimeAnimatorController.animationClips.FirstOrDefault(x => x.name == t);
                if (clip == null) { check(false, "clip asset " + t + " " + id); continue; }
                float lo = float.MaxValue, hi = float.MinValue, loStart = 0;
                for (int i = 0; i <= 12; i++) {
                    clip.SampleAnimation(anim.gameObject, clip.length * i / 12f);
                    smr.BakeMesh(baked, false);
                    var m = smr.transform.localToWorldMatrix;
                    float l = float.MaxValue, top = float.MinValue;
                    foreach (var v in baked.vertices) { float y = m.MultiplyPoint3x4(v).y - rootY; if (y < l) l = y; if (y > top) top = y; }
                    if (i == 0) loStart = l;
                    lo = Mathf.Min(lo, l); hi = Mathf.Max(hi, top);
                }
                maxTop = Mathf.Max(maxTop, hi);
                float sink = -lo / H, flo = loStart / H;
                if (sink > worstSink) { worstSink = sink; worstClip = t; }
                if (flo > worstFloat) worstFloat = flo;
                perClip.Add("\"" + t + "\":{\"seconds\":" + clip.length.ToString("0.###") + ",\"minY\":" + (lo / H).ToString("0.####") + ",\"startY\":" + (loStart / H).ToString("0.####") + ",\"top\":" + (hi / H).ToString("0.###") + "}");
            }
            UnityEngine.Object.DestroyImmediate(baked);
            check(worstSink <= .04f, "no clip sinks below the tile " + id + " " + worstClip + " " + worstSink.ToString("0.###"));
            check(worstFloat <= .04f, "every clip starts on the tile " + id + " " + worstFloat.ToString("0.###"));
            check(hRatio > .85f && hRatio < 1.15f, "in-game height matches the previous token " + id + " " + hRatio.ToString("0.##"));
            rows.Add("{\"id\":\"" + id + "\",\"triangles\":" + tris + ",\"bones\":" + (smr != null ? smr.bones.Length : 0) +
                     ",\"height\":" + piece.Height.ToString("0.###") + ",\"oldHeight\":" + oldSize.y.ToString("0.###") +
                     ",\"heightRatio\":" + hRatio.ToString("0.###") + ",\"widthRatio\":" + wRatio.ToString("0.###") +
                     ",\"worstSink\":" + worstSink.ToString("0.####") + ",\"worstSinkClip\":\"" + worstClip + "\",\"startFloat\":" + worstFloat.ToString("0.####") +
                     ",\"clipTop\":" + (maxTop / H).ToString("0.###") + ",\"clips\":{" + string.Join(",", perClip) + "}}");
            UnityEngine.Object.DestroyImmediate(go);
        }
        Directory.CreateDirectory("Build");
        bool passed = fails.Count == 0;
        File.WriteAllText("Build/rig-combat-validation.json", "{\"passed\":" + passed.ToString().ToLowerInvariant() + ",\"checks\":" + checks +
            ",\"failures\":[" + string.Join(",", fails.Select(f => "\"" + f.Replace("\"", "'") + "\"")) + "],\"perToken\":[\n" + string.Join(",\n", rows) + "]}");
        foreach (var f in fails) Debug.LogError("RIGCOMBAT FAIL " + f);
        Debug.Log("RIGCOMBAT " + (passed ? "PASS " : "FAIL ") + checks + " checks, " + fails.Count + " failures");
        if (!passed && Environment.GetCommandLineArgs().Contains("-rigStrict")) throw new Exception("RIGCOMBAT FAIL " + fails.Count);
    }

    // One sheet per card: previous token, then the rigged token in Idle, Walk, Attack (impact), Hit, Death (end)
    // and Summon (land), plus a roster overview of previous vs rigged Idle.
    public static void Render() {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        PlaytestCatalog.Load();
        var factory = Factory();
        string outDir = Environment.GetEnvironmentVariable("IC_RENDER_OUT") ?? "Build/rig-render";
        Directory.CreateDirectory(outDir);
        var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.6f;
        sun.transform.rotation = Quaternion.Euler(45, -30, 0);
        RenderSettings.ambientLight = new Color(.45f, .47f, .52f);
        var cam = new GameObject("Cam").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.13f, .14f, .17f); cam.fieldOfView = 24;
        string[] poses = { "Old", "Idle", "Walk", "Attack", "Hit", "Death", "Summon" };
        var ids = Ids(); float gap = .9f;
        foreach (var id in ids) {
            var c = PlaytestCatalog.All.First(x => x.id == id);
            var made = new List<GameObject>();
            for (int i = 0; i < poses.Length; i++) {
                var go = factory.Create(c, 1, out _); made.Add(go);
                go.transform.position = new Vector3((i - 3) * gap, 0, 0);
                go.transform.rotation = Quaternion.Euler(0, 195, 0);
                var model = go.transform.Find("Model");
                if (poses[i] == "Old") {
                    // put the previous mesh back in place of the skinned one, at the old budget scale
                    var oldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RigCompare/" + id + ".fbx");
                    var old = UnityEngine.Object.Instantiate(oldPrefab, go.transform, false);
                    foreach (var r in old.GetComponentsInChildren<Renderer>()) r.sharedMaterials = r.sharedMaterials.Select(_ => model.GetComponentInChildren<Renderer>().sharedMaterial).ToArray();
                    UnityEngine.Object.DestroyImmediate(model.gameObject);
                    old.transform.localScale = Vector3.one;
                    var ob = TokenFactory.Bounds(old);
                    PlaytestCatalog.Budget(c.type, out float bh, out float bf);
                    old.transform.localScale = Vector3.one * Mathf.Min(bh / Mathf.Max(.001f, ob.size.y), bf / Mathf.Max(.001f, Mathf.Max(ob.size.x, ob.size.z)));
                    ob = TokenFactory.Bounds(old);
                    old.transform.position += new Vector3(go.transform.position.x - ob.center.x, -ob.min.y, go.transform.position.z - ob.center.z);
                    continue;
                }
                var anim = model.GetComponentInChildren<Animator>();
                var clip = anim.runtimeAnimatorController.animationClips.First(x => x.name == poses[i]);
                float t = clip.length * .5f;
                string ev = poses[i] == "Attack" ? "impact" : poses[i] == "Summon" ? "land" : null;
                if (ev != null) t = clip.events.Where(e => e.stringParameter == ev).Select(e => e.time).DefaultIfEmpty(t).First();
                if (poses[i] == "Death") t = clip.length;
                clip.SampleAnimation(anim.gameObject, t);
            }
            cam.transform.position = new Vector3(0, 1.5f, -5.6f); cam.transform.LookAt(new Vector3(0, .35f, 0));
            Shot(cam, 1800, 520, Path.Combine(outDir, id + "_poses.png"));
            foreach (var g in made) UnityEngine.Object.DestroyImmediate(g);
        }
        Debug.Log("RIGCOMBAT RENDER " + ids.Length + " sheets in " + outDir);
    }

    static void Shot(Camera cam, int w, int h, string path) {
        var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 }; cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt; var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = null; RenderTexture.active = null; UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex);
    }
}
