// AI-060-RIG-ROSTER Unity import check. Batchmode:
//   Unity.exe -batchmode -projectPath <unity-check> -executeMethod RigCheck.Run -logFile rigcheck.log -quit
// Imports every FBX in Assets/RigCheck as Generic with looping clips, samples each clip frame by frame,
// measures the lowest skinned vertex (feet on the ground), the loop seam, and renders front/side frames.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class RigCheck
{
    static readonly List<string> Log = new List<string>();

    public static void Run()
    {
        Application.logMessageReceived += (msg, st, type) =>
        {
            if (type != LogType.Log) Log.Add(type + ": " + msg.Split('\n')[0]);
        };
        string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../"));
        string renderDir = Path.Combine(outDir, "unity-check-renders");
        Directory.CreateDirectory(renderDir);
        var sb = new StringBuilder("{\n  \"unity\": \"" + Application.unityVersion + "\",\n  \"models\": [\n");
        var fbxs = Directory.GetFiles(Path.Combine(Application.dataPath, "RigCheck"), "*.fbx");
        bool allPass = true;
        for (int i = 0; i < fbxs.Length; i++)
        {
            string assetPath = "Assets/RigCheck/" + Path.GetFileName(fbxs[i]);
            bool pass;
            sb.Append(CheckModel(assetPath, renderDir, out pass));
            allPass &= pass;
            sb.Append(i < fbxs.Length - 1 ? ",\n" : "\n");
        }
        sb.Append("  ],\n  \"warnings\": [" + string.Join(",", Log.Distinct().Select(Q)) + "],\n");
        sb.Append("  \"result\": \"" + (allPass ? "PASS" : "FAIL") + "\"\n}\n");
        File.WriteAllText(Path.Combine(outDir, "unity-check-report.json"), sb.ToString());
        Debug.Log("RIGCHECK " + (allPass ? "PASS" : "FAIL") + " -> " + Path.Combine(outDir, "unity-check-report.json"));
    }

    static string Q(string s) { return "\"" + s.Replace("\\", "\\\\").Replace("\"", "'") + "\""; }
    static string F(float f) { return f.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture); }

    static string CheckModel(string assetPath, string renderDir, out bool pass)
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(assetPath);
        imp.animationType = ModelImporterAnimationType.Generic;
        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.importAnimation = true;
        imp.skinWeights = ModelImporterSkinWeights.Standard; // 4 bones, the default the game will use
        imp.SaveAndReimport();
        var clipsSetup = imp.defaultClipAnimations;
        foreach (var c in clipsSetup) { c.loopTime = true; c.name = c.name.Split('|').Last(); }
        imp.clipAnimations = clipsSetup;
        imp.SaveAndReimport();

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        var clips = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__")).ToArray();
        var go = (GameObject)Object.Instantiate(prefab);
        go.transform.position = Vector3.zero;
        var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
        string name = Path.GetFileNameWithoutExtension(assetPath);
        var sb = new StringBuilder();
        sb.Append("    {\"model\": " + Q(name) + ", \"bones\": " + smr.bones.Length +
                  ", \"vertices\": " + smr.sharedMesh.vertexCount + ", \"clips\": [\n");
        pass = clips.Length >= 2;
        var baked = new Mesh();
        for (int ci = 0; ci < clips.Length; ci++)
        {
            var clip = clips[ci];
            int frames = Mathf.RoundToInt(clip.length * clip.frameRate);
            float lo = float.MaxValue, hi = float.MinValue;
            Vector3[] first = null; float seam = 0;
            for (int f = 0; f <= frames; f++)
            {
                clip.SampleAnimation(go, f / clip.frameRate);
                var v = Bake(smr, baked);
                float m = v.Min(p => p.y);
                lo = Mathf.Min(lo, m); hi = Mathf.Max(hi, m);
                if (f == 0) first = v;
                if (f == frames) seam = v.Zip(first, (a, b) => (a - b).magnitude).Max();
            }
            bool sink = lo < -0.01f, floating = lo > 0.02f;
            bool ok = clip.isLooping && !sink && !floating;
            pass &= ok;
            sb.Append("      {\"clip\": " + Q(clip.name) + ", \"length\": " + F(clip.length) + ", \"frameRate\": " + F(clip.frameRate) +
                      ", \"isLooping\": " + clip.isLooping.ToString().ToLower() + ", \"minY_lowest\": " + F(lo) +
                      ", \"minY_highest\": " + F(hi) + ", \"loopSeamMaxVertexDelta\": " + F(seam) +
                      ", \"result\": \"" + (ok ? "PASS" : "FAIL") + "\"}" + (ci < clips.Length - 1 ? ",\n" : "\n"));
            foreach (var t in new[] { 0f, 0.5f })
            {
                clip.SampleAnimation(go, t * clip.length);
                Render(go, Path.Combine(renderDir, name + "_" + clip.name + "_" + (t == 0 ? "f0" : "mid")));
            }
        }
        sb.Append("    ], \"result\": \"" + (pass ? "PASS" : "FAIL") + "\"}");
        Object.DestroyImmediate(go);
        return sb.ToString();
    }

    static Vector3[] Bake(SkinnedMeshRenderer smr, Mesh baked)
    {
        smr.BakeMesh(baked, true);
        var m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
        return baked.vertices.Select(p => m.MultiplyPoint3x4(p)).ToArray();
    }

    static void Render(GameObject go, string basePath)
    {
        var b = go.GetComponentInChildren<SkinnedMeshRenderer>().bounds;
        var camGo = new GameObject("cam"); var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = Mathf.Max(b.size.y, 1f) * 0.6f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.12f, 0.14f, 0.2f);
        var lightGo = new GameObject("light"); var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1.2f; lightGo.transform.rotation = Quaternion.Euler(40, -30, 0);
        var rt = new RenderTexture(512, 640, 24); cam.targetTexture = rt;
        var views = new Dictionary<string, Vector3> { { "front", Vector3.forward }, { "side", Vector3.right } };
        foreach (var kv in views)
        {
            cam.transform.position = b.center + kv.Value * 5f;
            cam.transform.LookAt(b.center);
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(512, 640, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 512, 640), 0, 0); tex.Apply();
            File.WriteAllBytes(basePath + "_" + kv.Key + ".png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        RenderTexture.active = null; cam.targetTexture = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(camGo); Object.DestroyImmediate(lightGo);
    }
}
