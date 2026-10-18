using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using InfiniteConquest.Playtest;
// Renders each swapped Poseidon token through TokenFactory exactly as in a match, next to the same token with the
// original 60K mesh put back (from a temporary Assets/PoseidonCompare/<id>.fbx copy that is not part of the build).
// Left = original, right = 30K. Writes one overview and one close-up per card plus triangle totals.
public static class PoseidonTokensRender {
    public static void Render() {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        PlaytestCatalog.Load();
        var factory = new TokenFactory(Shader.Find("Universal Render Pipeline/Lit"), Shader.Find("Universal Render Pipeline/Unlit"), Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        string outDir = Environment.GetEnvironmentVariable("IC_RENDER_OUT") ?? "Build";
        Directory.CreateDirectory(outDir);
        var ids = PoseidonTokensBuild.Ids; int cols = 4; float gap = 6f, pair = 0.75f;
        int before = 0, after = 0, real = 0; var rows = new System.Collections.Generic.List<string>();
        var centres = new Vector3[ids.Length];
        for (int i = 0; i < ids.Length; i++) {
            var c = PlaytestCatalog.All.First(x => x.id == ids[i]);
            var centre = new Vector3((i % cols - (cols - 1) / 2f) * gap, 0, (i / cols) * gap); centres[i] = centre;
            var orig = factory.Create(c, 1, out bool r1); var nu = factory.Create(c, 1, out bool r2);
            if (r1 && r2) real++;
            var origMesh = AssetDatabase.LoadAllAssetsAtPath("Assets/PoseidonCompare/" + ids[i] + ".fbx").OfType<Mesh>().FirstOrDefault();
            if (origMesh == null) throw new Exception("POSEIDON RENDER missing original " + ids[i]);
            var mf = orig.GetComponentInChildren<MeshFilter>(); int tNew = (int)(mf.sharedMesh.triangles.Length / 3);
            mf.sharedMesh = origMesh; int tOld = (int)(origMesh.triangles.Length / 3);
            before += tOld; after += tNew;
            rows.Add("{\"id\":\"" + ids[i] + "\",\"before\":" + tOld + ",\"after\":" + tNew + "}");
            orig.transform.position = centre + Vector3.left * pair; nu.transform.position = centre + Vector3.right * pair;
        }
        var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.6f;
        sun.transform.rotation = Quaternion.Euler(45, -30, 0);
        RenderSettings.ambientLight = new Color(.45f, .47f, .52f);
        var cam = new GameObject("Cam").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.13f, .14f, .17f); cam.fieldOfView = 30;
        cam.transform.position = new Vector3(0, 17, -14); cam.transform.LookAt(new Vector3(0, .4f, gap));
        Shot(cam, 2400, 1350, Path.Combine(outDir, "poseidon30k-overview.png"));
        cam.fieldOfView = 26;
        for (int i = 0; i < ids.Length; i++) {
            cam.transform.position = centres[i] + new Vector3(0, 1.6f, -3.6f); cam.transform.LookAt(centres[i] + new Vector3(0, .4f, 0));
            Shot(cam, 1200, 700, Path.Combine(outDir, ids[i] + "_orig-vs-30k.png"));
        }
        File.WriteAllText(Path.Combine(outDir, "poseidon30k-triangles.json"), "{\"before\":" + before + ",\"after\":" + after + ",\"perToken\":[" + string.Join(",", rows) + "]}");
        Debug.Log("POSEIDON RENDER real=" + real + "/" + ids.Length + " triangles " + before + " -> " + after);
        if (real != ids.Length) throw new Exception("POSEIDON RENDER FAIL real=" + real);
    }

    static void Shot(Camera cam, int w, int h, string path) {
        var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 }; cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt; var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = null; RenderTexture.active = null; UnityEngine.Object.DestroyImmediate(rt);
    }
}
