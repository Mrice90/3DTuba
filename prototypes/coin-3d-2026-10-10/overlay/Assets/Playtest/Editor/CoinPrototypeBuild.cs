using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using InfiniteConquest.Playtest;

// AI-080-COIN-3D-PRESENTATION prototype build: prepares the coin skin assets, checks the placeholder
// mesh, then runs the unchanged restoration validation/build chain.
//   Unity -batchmode -quit -projectPath <copy> -executeMethod CoinPrototypeBuild.Build -buildPath <dir>\InfiniteConquestPlaytest.exe
public static class CoinPrototypeBuild {
    const string Root = "Assets/Playtest/Resources/CoinSkins";
    static int checks;
    static void Check(bool ok, string name) { if (!ok) throw new Exception("COIN FAIL " + name); checks++; Debug.Log("COIN PASS " + name); }

    public static void Build() {
        Prepare();
        Validate();
        Directory.CreateDirectory("Build");
        File.WriteAllText("Build/coin-validation.json", "{\"passed\":true,\"checks\":" + checks + "}");
        RestorationBuild.Build();
    }

    [MenuItem("Infinite Conquest/Coin/Prepare coin skins")]
    public static void Prepare() {
        AssetDatabase.Refresh();
        foreach (var skin in new[] { CoinSkin.DefaultName, "CapitalArtTest" })
            foreach (var part in new[] { "front", "back", "rim" }) {
                var imp = (TextureImporter)AssetImporter.GetAtPath($"{Root}/{skin}/{part}.png");
                imp.textureType = TextureImporterType.Default; imp.sRGBTexture = true; imp.mipmapEnabled = true;
                imp.wrapModeU = part == "rim" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                imp.wrapModeV = TextureWrapMode.Clamp; imp.anisoLevel = 8; imp.maxTextureSize = 1024;
                imp.alphaSource = TextureImporterAlphaSource.None;
                imp.SaveAndReimport();
            }

        var template = AssetDatabase.LoadAssetAtPath<Material>($"{Root}/CoinLitTemplate.mat");
        if (template == null) {
            template = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(template, $"{Root}/CoinLitTemplate.mat");
        }
        template.EnableKeyword("_EMISSION");                 // keeps the emissive Lit variant in the player
        template.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        template.SetColor("_EmissionColor", Color.black);
        EditorUtility.SetDirty(template);

        Skin(CoinSkin.DefaultName, "Default (placeholder emblems)", new Color(.85f, .72f, .32f), new Color(.35f, .28f, .06f));
        Skin("CapitalArtTest", "Capital art (skin swap test)", new Color(.75f, .9f, 1f), new Color(.05f, .3f, .4f));
        AssetDatabase.SaveAssets();
    }

    static void Skin(string name, string display, Color rim, Color glow) {
        string path = $"{Root}/{name}.asset";
        var skin = AssetDatabase.LoadAssetAtPath<CoinSkin>(path);
        if (skin == null) { skin = ScriptableObject.CreateInstance<CoinSkin>(); AssetDatabase.CreateAsset(skin, path); }
        skin.displayName = display;
        skin.front = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/{name}/front.png");
        skin.back = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/{name}/back.png");
        skin.rimTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/{name}/rim.png");
        skin.rimColor = rim; skin.emission = glow;
        EditorUtility.SetDirty(skin);
    }

    static void Validate() {
        var def = CoinSkin.Load(null); var alt = CoinSkin.Load("CapitalArtTest");
        Check(def != null && def.name == CoinSkin.DefaultName, "default skin resolves with no -coinSkin");
        Check(CoinSkin.Load("NoSuchSkin") == def, "unknown skin falls back to default");
        Check(alt != null && alt != def, "second skin resolves by name");
        foreach (var s in new[] { def, alt }) Check(s.front != null && s.back != null && s.rimTexture != null && s.front != s.back, s.name + " has distinct faces and a rim");
        Check(def.FaceFor(0) == def.front && def.FaceFor(1) == def.back, "seat 0 = front, seat 1 = back");
        Check(alt.front != def.front && alt.back != def.back, "skins carry different face textures");
        Check(Resources.Load<Material>("CoinSkins/CoinLitTemplate") != null, "lit template material ships in Resources");

        var mesh = CoinMesh.Build();
        Check(mesh.subMeshCount == 3, "coin has front/back/rim submeshes");
        var v = mesh.vertices; var n = mesh.normals; var uv = mesh.uv;
        foreach (var t in uv) if (t.x < -1e-4f || t.x > 1.0001f || t.y < -1e-4f || t.y > 1.0001f) Check(false, "UVs inside 0..1");
        Check(true, "UVs inside 0..1");
        for (int sm = 0; sm < 3; sm++) {
            var tri = mesh.GetTriangles(sm); bool facing = tri.Length > 0;
            for (int i = 0; i < tri.Length; i += 3) {
                var face = Vector3.Cross(v[tri[i + 1]] - v[tri[i]], v[tri[i + 2]] - v[tri[i]]);
                if (Vector3.Dot(face, n[tri[i]]) <= 0) facing = false;
            }
            Check(facing, "submesh " + sm + " triangles face their normals");
        }
        Check(Vector3.Dot(n[mesh.GetTriangles(CoinMesh.Front)[0]], Vector3.up) > .99f, "front face points +Y");
        Check(Vector3.Dot(n[mesh.GetTriangles(CoinMesh.Back)[0]], Vector3.down) > .99f, "back face points -Y");
        Check(mesh.bounds.size.y > .079f && mesh.bounds.size.y < .081f && Mathf.Abs(mesh.bounds.size.x - 1) < .001f, "1 unit across, 0.08 thick");
        UnityEngine.Object.DestroyImmediate(mesh);
    }
}
