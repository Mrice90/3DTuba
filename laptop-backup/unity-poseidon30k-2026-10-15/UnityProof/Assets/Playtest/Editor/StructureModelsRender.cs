using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using InfiniteConquest.Playtest;
// Renders all 34 structure pieces exactly as TokenFactory builds them in a match (model, Token material, budget scale).
public static class StructureModelsRender {
    public static void Render() {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        PlaytestCatalog.Load();
        var factory = new TokenFactory(Shader.Find("Universal Render Pipeline/Lit"), Shader.Find("Universal Render Pipeline/Unlit"), Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        var cards = PlaytestCatalog.All.Where(c => c.type == "STRUCTURE").OrderBy(c => c.faction).ThenBy(c => c.id).ToArray();
        int cols = 9, real = 0; float gap = 2.4f;
        for (int i = 0; i < cards.Length; i++) {
            var go = factory.Create(cards[i], cards[i].faction == "ZEUS" ? 0 : 1, out bool isReal);
            if (isReal) real++; else Debug.LogError("STRUCTURE RENDER stand-in used for " + cards[i].id);
            go.transform.position = new Vector3((i % cols - (cols - 1) / 2f) * gap, 0, -(i / cols) * gap * 1.1f);
        }
        var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.6f;
        sun.transform.rotation = Quaternion.Euler(45, -30, 0);
        RenderSettings.ambientLight = new Color(.45f, .47f, .52f);
        var cam = new GameObject("Cam").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.13f, .14f, .17f);
        cam.fieldOfView = 30; cam.transform.position = new Vector3(0, 11, 9); cam.transform.LookAt(new Vector3(0, .4f, -3.4f));
        var rt = new RenderTexture(2400, 1350, 24); cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt; var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
        string outDir = Environment.GetEnvironmentVariable("IC_RENDER_OUT") ?? "Build";
        Directory.CreateDirectory(outDir);
        File.WriteAllBytes(Path.Combine(outDir, "structures-in-game.png"), tex.EncodeToPNG());
        Debug.Log("STRUCTURE RENDER real=" + real + "/" + cards.Length);
        if (real != cards.Length) throw new Exception("STRUCTURE RENDER FAIL real=" + real);
    }
}
