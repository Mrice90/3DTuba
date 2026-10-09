using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using InfiniteConquest.Playtest;

// AI-094 coin: builds Assets/Playtest/Coin/Coin.prefab (with its three material assets) and the stand-alone
// test scene Assets/Playtest/Coin/CoinFlip.unity, then checks the model, skins and flip maths. Batch:
//   Unity -batchmode -quit -projectPath UnityProof -executeMethod CoinBuilder.BuildAndValidate
public static class CoinBuilder {
    const string Dir = "Assets/Playtest/Coin";
    const string ModelPath = "Assets/Playtest/Resources/Coin/coin.fbx";
    public const string PrefabPath = Dir + "/Coin.prefab";
    public const string ScenePath = Dir + "/CoinFlip.unity";
    static readonly List<string> results = new List<string>();
    static void Check(bool ok, string name) { results.Add((ok ? "PASS " : "FAIL ") + name); Debug.Log("COIN_VALIDATE " + (ok ? "PASS " : "FAIL ") + name); if (!ok) throw new Exception("FAILED: " + name); }

    [MenuItem("Infinite Conquest/Build coin prefab and test scene")]
    public static void BuildAndValidate() {
        results.Clear();
        AssetDatabase.Refresh();
        Validate();
        BuildPrefab();
        BuildScene();
        Check(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<CoinFlip>() != null, "Coin.prefab saved with CoinFlip");
        File.WriteAllLines(Path.Combine(Application.dataPath, "../coin-validate.txt"), results);
        Debug.Log("COIN_VALIDATE done: " + results.Count + " checks passed");
    }

    public static void Validate() {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        Check(model != null, "coin.fbx imported");
        var mf = model.GetComponentInChildren<MeshFilter>(); var mr = model.GetComponentInChildren<MeshRenderer>();
        Check(mf != null && mr != null, "coin has one mesh renderer");
        var mesh = mf.sharedMesh;
        Check(mesh.subMeshCount == 3, "coin mesh has 3 sub-meshes (rim, heads, tails), got " + mesh.subMeshCount);
        int tris = mesh.triangles.Length / 3;
        Check(tris <= 2000, "coin is low-poly: " + tris + " tris");
        var names = mr.sharedMaterials.Select(m => m != null ? m.name : "").ToArray();
        foreach (var slot in new[] { "Coin_Rim", "Coin_Heads", "Coin_Tails" }) Check(names.Any(n => n.Contains(slot)), "material slot " + slot + " present (" + string.Join(", ", names) + ")");
        var b = mesh.bounds.size;
        Check(Mathf.Abs(b.x - 1) < .02f && Mathf.Abs(b.z - 1) < .02f && b.y < .1f, $"coin lies flat, 1.0 across, thin on Y (size {b})");
        // Heads must face up: the heads sub-mesh normals point +Y in the model's frame.
        int hi = Array.FindIndex(names, n => n.Contains("Coin_Heads"));
        var tr = mf.transform; var nrm = mesh.normals; var idx = mesh.GetTriangles(hi);
        var up = tr.TransformDirection(nrm[idx[0]]);
        Check(up.y > .9f, "heads faces up after import (normal " + up + ")");
        foreach (var id in CoinSkin.BuiltIn) {
            var s = CoinSkin.Load(id);
            Check(s != null && s.Heads.width == s.Heads.height && s.Tails.width == s.Tails.height, "skin '" + id + "' loads with square heads and tails");
        }
        for (int r = 0; r < 2; r++) {
            var face = CoinFlip.Spin(1, r, 37, 5) * Vector3.up;
            Check(r == 0 ? face.y > .999f : face.y < -.999f, $"result {r} lands {(r == 0 ? "heads" : "tails")} up (up·Y {face.y:0.000})");
            Check((CoinFlip.Spin(0, r, 37, 5) * Vector3.up).y > .999f, $"result {r} starts heads up and flat");
            Check(Mathf.Abs((CoinFlip.Spin(.5f, r, 37, 5) * Vector3.up).y) < 1f, $"result {r} is mid-spin at the apex");
        }
        Check(PlaytestGame.StarterFromDetail("Match seed 42; coin flip: Player 2 starts", 0) == 1, "engine detail 'Player 2 starts' maps to seat 1");
        Check(PlaytestGame.StarterFromDetail("Match seed 7; coin flip: Player 1 starts", 1) == 0, "engine detail 'Player 1 starts' maps to seat 0");
        Check(PlaytestGame.StarterFromDetail("Match start", 1) == 1, "missing coin-flip text falls back to the event player");
    }

    static Material Mat(string name) {
        string path = Dir + "/Materials/" + name + ".mat";
        Directory.CreateDirectory(Dir + "/Materials");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) {
            var lit = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.defaultShader : Shader.Find("Standard");
            m = new Material(lit) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }

    public static void BuildPrefab() {
        var root = new GameObject("Coin");
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), root.transform);
        model.name = "Model";
        var mr = model.GetComponentInChildren<MeshRenderer>();
        mr.shadowCastingMode = ShadowCastingMode.On;
        mr.sharedMaterials = mr.sharedMaterials.Select(m => Mat(m.name)).ToArray();   // same slot names, as editable assets
        var flip = root.AddComponent<CoinFlip>();
        flip.Target = mr;
        flip.ApplySkin(CoinSkin.Load("olympus"));
        foreach (var m in mr.sharedMaterials) EditorUtility.SetDirty(m);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
    }

    public static void BuildScene() {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var cam = Camera.main;
        cam.transform.position = new Vector3(0, 4.2f, -6.2f);
        cam.transform.LookAt(new Vector3(0, 1.2f, 0));
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.06f, .07f, .1f);
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ground.name = "Table"; ground.transform.localScale = new Vector3(5, .05f, 5); ground.transform.position = new Vector3(0, -.05f, 0);
        ground.GetComponent<Renderer>().sharedMaterial = Mat("CoinDemo_Table");
        ground.GetComponent<Renderer>().sharedMaterial.color = new Color(.16f, .18f, .22f);
        var coin = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        coin.transform.position = Vector3.zero; coin.transform.localScale = Vector3.one * coin.GetComponent<CoinFlip>().Size;
        var demo = new GameObject("CoinFlipDemo").AddComponent<CoinFlipDemo>();
        demo.Coin = coin.GetComponent<CoinFlip>();
        EditorSceneManager.SaveScene(scene, ScenePath);
    }
}
