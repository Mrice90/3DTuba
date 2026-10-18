using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using InfiniteConquest.Playtest;
// AI-081 Poseidon 30K: the 12 batch-07 Poseidon character tokens were swapped in place (same Resources/Tokens path,
// name and .meta GUID) for the 30K decimations in playtest/decimate-batch07-2026-10-15, normalised with the same
// glb_to_playtest_token.py as the originals. Check each one loads, is at most 30K triangles and builds as a real
// model through TokenFactory, then run the inherited structures + full build chain.
public static class PoseidonTokensBuild {
    public static readonly string[] Ids = {
        "poseidon_abyssal_molecrab", "poseidon_delphic_sonar_adept", "poseidon_fast_razorfin_lancer",
        "poseidon_keyword_breakwater_hoplite", "poseidon_kraken_tendril_drone", "poseidon_naiad_flowshaper",
        "poseidon_nereid_current_rider", "poseidon_oceanid_pressure_mage", "poseidon_reefline_defender",
        "poseidon_tidepool_surveyor", "poseidon_triton_waveguard", "poseidon_undertow_stalker" };

    public static int Triangles(GameObject go) =>
        go.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh ? (int)(f.sharedMesh.triangles.Length / 3) : 0) +
        go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Sum(s => s.sharedMesh ? (int)(s.sharedMesh.triangles.Length / 3) : 0);

    public static void Build() {
        int checks = 0; Action<bool, string> check = (ok, m) => { if (!ok) throw new Exception("POSEIDON30K FAIL " + m); checks++; };
        PlaytestCatalog.Load();
        var factory = new TokenFactory(Shader.Find("Universal Render Pipeline/Lit"), Shader.Find("Universal Render Pipeline/Unlit"), Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        int total = 0; var rows = new System.Collections.Generic.List<string>();
        foreach (var id in Ids) {
            var c = PlaytestCatalog.All.FirstOrDefault(x => x.id == id);
            check(c != null, "card " + id);
            check(c.model == "Tokens/" + id + "/" + id, "model ref unchanged " + id + " " + c.model);
            var prefab = Resources.Load<GameObject>(c.model); check(prefab != null, "model loads " + id);
            int tris = Triangles(prefab); total += tris;
            check(tris >= 29000 && tris <= 30000, "30K triangles " + id + " " + tris);
            check(prefab.GetComponentsInChildren<Renderer>().Length == 1, "one renderer " + id);
            check(Resources.Load<Material>("Tokens/" + id + "/Token") != null, "token material " + id);
            var go = factory.Create(c, 1, out bool real);
            check(real, "real model via TokenFactory " + id);
            var piece = go.GetComponent<Piece>();
            check(piece.Height > .2f && piece.Height < 2f, "budget height " + id + " " + piece.Height);
            var model = go.transform.Find("Model"); check(model != null, "model child " + id);
            var b = model.GetComponentInChildren<Renderer>().bounds;
            check(Mathf.Abs(b.min.y) < .01f, "grounded " + id + " " + b.min.y);
            check(Mathf.Abs(b.center.x) < .01f && Mathf.Abs(b.center.z) < .01f, "centred " + id + " " + b.center);
            rows.Add("{\"id\":\"" + id + "\",\"triangles\":" + tris + ",\"height\":" + piece.Height.ToString("0.####") + "}");
            UnityEngine.Object.DestroyImmediate(go);
        }
        Directory.CreateDirectory("Build");
        File.WriteAllText("Build/poseidon-30k-validation.json", "{\"passed\":true,\"checks\":" + checks + ",\"tokens\":" + Ids.Length +
            ",\"triangles\":" + total + ",\"perToken\":[" + string.Join(",", rows) + "]}");
        Debug.Log("POSEIDON30K PASS " + checks + " triangles=" + total);
        StructureModelsBuild.Build();
    }
}
