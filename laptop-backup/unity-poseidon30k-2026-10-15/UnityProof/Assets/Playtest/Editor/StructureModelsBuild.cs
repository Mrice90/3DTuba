using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using InfiniteConquest.Playtest;
// AI-081 structures: TokenFactory recolours every staged model with its folder's "Token" material, but the
// StructureModels import (reviews/release-push-2026-10-13/structures) shipped only the palette textures, so all
// 33 structures fell back to a flat faction tint. Create the shared palette material, then run the normal chain.
public static class StructureModelsBuild {
    const string Dir = "Assets/Playtest/Resources/StructureModels";
    public static void Build() {
        int checks = 0; Action<bool, string> check = (ok, m) => { if (!ok) throw new Exception("STRUCTURES FAIL " + m); checks++; };
        foreach (var n in new[] { "Token_BaseColor", "Token_Emission", "Token_MetallicSmoothness" }) {
            var imp = (TextureImporter)AssetImporter.GetAtPath(Dir + "/" + n + ".png");
            check(imp != null, "texture " + n);
            // 4x4 palette atlas: point filtering and no mips keep each swatch a single flat colour.
            imp.filterMode = FilterMode.Point; imp.mipmapEnabled = false; imp.sRGBTexture = n != "Token_MetallicSmoothness";
            imp.textureCompression = TextureImporterCompression.Uncompressed; imp.SaveAndReimport();
        }
        var tmpl = Resources.Load<Material>("Tokens/poseidon_abyss_gate/Token");
        check(tmpl != null, "template token material");
        var mat = new Material(tmpl.shader) { name = "Token" };
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/Token_BaseColor.png"));
        mat.SetTexture("_MainTex", mat.GetTexture("_BaseMap"));
        mat.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/Token_MetallicSmoothness.png"));
        mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        mat.SetFloat("_Smoothness", 1); mat.SetFloat("_WorkflowMode", 1);
        mat.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/Token_Emission.png"));
        mat.SetColor("_EmissionColor", Color.white); mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        AssetDatabase.DeleteAsset(Dir + "/Token.mat");
        AssetDatabase.CreateAsset(mat, Dir + "/Token.mat"); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        check(Resources.Load<Material>("StructureModels/Token") != null, "runtime token material resolves");
        PlaytestCatalog.Load();
        var structures = PlaytestCatalog.All.Where(c => c.type == "STRUCTURE").ToArray();
        check(structures.Length == 34, "34 structures, found " + structures.Length);
        foreach (var c in structures) {
            check(!string.IsNullOrEmpty(c.model), "model ref " + c.id);
            var go = Resources.Load<GameObject>(c.model); check(go != null, "model loads " + c.id);
            var rs = go.GetComponentsInChildren<Renderer>(); check(rs.Length > 0, "renderer " + c.id);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            check(b.size.y > .1f && b.size.y < 5f && Mathf.Max(b.size.x, b.size.z) < 5f, "sane size " + c.id + " " + b.size);
        }
        Directory.CreateDirectory("Build");
        File.WriteAllText("Build/structure-models-validation.json", "{\"passed\":true,\"checks\":" + checks + ",\"structures\":" + structures.Length + "}");
        Debug.Log("STRUCTURES PASS " + checks);
        CollectionBuild.Build();
    }
}
