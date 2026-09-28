using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// AI-052-ASSET: place an imported Meshy token on the proof board and render review stills.
// Batch: unity -batchmode -executeMethod TokenPreview.Render -tokenPath <asset> -tokenName <name>
public static class TokenPreview {
    static string Arg(string name, string fallback) {
        var a = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return fallback;
    }
    static Vector3 Cell(int x, int y) { return new Vector3((x - 1.5f) * 1.3f, 0, (y - 2.5f) * 1.3f); }
    static void Shot(Camera cam, string path, int w, int h) {
        var rt = new RenderTexture(w, h, 24) { antiAliasing = 8 };
        cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null;
        Debug.Log("TOKEN_PREVIEW wrote " + path);
    }
    // Builds a URP Lit material from Token_*.png written by the Blender normalizer.
    // glTF packs roughness in G and metal in B; URP wants metal in R and smoothness in A.
    static Material BuildMaterial(string dir) {
        string Tex(string tag) => $"{dir}/Token_{tag}.png";
        var normalImp = (TextureImporter)AssetImporter.GetAtPath(Tex("Normal"));
        if (normalImp != null && normalImp.textureType != TextureImporterType.NormalMap) { normalImp.textureType = TextureImporterType.NormalMap; normalImp.SaveAndReimport(); }
        string mrPath = Tex("MetallicRoughness"), msPath = Tex("MetallicSmoothness");
        var mrImp = (TextureImporter)AssetImporter.GetAtPath(mrPath);
        if (mrImp != null && !File.Exists(msPath)) {
            mrImp.isReadable = true; mrImp.sRGBTexture = false; mrImp.SaveAndReimport();
            var mr = AssetDatabase.LoadAssetAtPath<Texture2D>(mrPath);
            var px = mr.GetPixels();
            for (int i = 0; i < px.Length; i++) px[i] = new Color(px[i].b, 0, 0, 1f - px[i].g);
            var ms = new Texture2D(mr.width, mr.height, TextureFormat.RGBA32, true, true);
            ms.SetPixels(px); ms.Apply();
            File.WriteAllBytes(msPath, ms.EncodeToPNG()); AssetDatabase.ImportAsset(msPath);
            var msImp = (TextureImporter)AssetImporter.GetAtPath(msPath); msImp.sRGBTexture = false; msImp.SaveAndReimport();
        }
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex("BaseColor")));
        var n = AssetDatabase.LoadAssetAtPath<Texture2D>(Tex("Normal"));
        if (n) { mat.SetTexture("_BumpMap", n); mat.EnableKeyword("_NORMALMAP"); }
        var m = AssetDatabase.LoadAssetAtPath<Texture2D>(msPath);
        if (m) { mat.SetTexture("_MetallicGlossMap", m); mat.SetFloat("_Smoothness", 1f); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); }
        AssetDatabase.CreateAsset(mat, $"{dir}/Token.mat");
        Debug.Log($"TOKEN_MATERIAL base={mat.GetTexture("_BaseMap") != null} normal={n != null} metal={m != null}");
        return mat;
    }
    [MenuItem("Infinite Conquest/Render token preview")]
    public static void Render() {
        string tokenPath = Arg("-tokenPath", "Assets/Art/Tokens/ThunderRam/ThunderRam.fbx");
        string tokenName = Arg("-tokenName", "thunder-ram");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var floor = new Material(lit) { color = new Color(.09f, .15f, .22f) };
        var home = new Material(lit) { color = new Color(.12f, .55f, .49f) };
        for (int x = 0; x < 4; x++) for (int y = 0; y < 6; y++) {
            var t = GameObject.CreatePrimitive(PrimitiveType.Cube);
            t.transform.position = Cell(x, y); t.transform.localScale = new Vector3(1.18f, .18f, 1.18f);
            t.GetComponent<Renderer>().sharedMaterial = (x == 1 && y == 2) ? home : floor;
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(tokenPath);
        if (prefab == null) throw new System.Exception("token asset not found: " + tokenPath);
        var token = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var tokenMat = BuildMaterial(Path.GetDirectoryName(tokenPath).Replace('\\', '/'));
        foreach (var r in token.GetComponentsInChildren<Renderer>()) r.sharedMaterial = tokenMat;
        token.transform.position = Cell(1, 2) + Vector3.up * .09f;
        token.transform.rotation = Quaternion.Euler(0, 200, 0);
        var b = new Bounds(token.transform.position, Vector3.zero);
        foreach (var r in token.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        Debug.Log($"TOKEN_BOUNDS size={b.size} min={b.min} renderers={token.GetComponentsInChildren<Renderer>().Length}");
        var sun = new GameObject("Key light").AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 2.0f; sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(48, -35, 0);
        var fill = new GameObject("Rim light").AddComponent<Light>();
        fill.type = LightType.Directional; fill.intensity = .7f; fill.color = new Color(.6f, .75f, 1f);
        fill.transform.rotation = Quaternion.Euler(25, 150, 0);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.42f, .46f, .55f);
        var cam = new GameObject("Review camera").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.025f, .04f, .065f);
        Directory.CreateDirectory("Build");
        cam.fieldOfView = 30; cam.transform.position = token.transform.position + new Vector3(3.2f, 4.6f, -5.8f); cam.transform.LookAt(token.transform.position);
        Shot(cam, $"Build/token-preview-{tokenName}-board.png", 1600, 1000);
        cam.fieldOfView = 28; cam.transform.position = b.center + new Vector3(1.6f, 1.3f, -2.4f); cam.transform.LookAt(b.center);
        Shot(cam, $"Build/token-preview-{tokenName}-closeup.png", 1600, 1000);
    }
}
