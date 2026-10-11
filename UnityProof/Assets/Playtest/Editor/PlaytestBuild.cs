using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using InfiniteConquest.Playtest;

// AI-080 playtest build. Batch:
//   Unity -batchmode -quit -projectPath UnityProof -executeMethod PlaytestBuild.Build -buildPath <dir>\InfiniteConquestPlaytest.exe
// Run UnityProof/Tools/stage_playtest_assets.py first to stage models/SFX (the build works without them:
// every card then shows its typed stand-in and generic cues).
public static class PlaytestBuild {
    public const string ScenePath = "Assets/Scenes/Playtest.unity";
    const string ProofScenePath = "Assets/Scenes/MovementProof.unity";
    const string TokensDir = "Assets/Playtest/Resources/Tokens";
    const string ParticleMatPath = "Assets/Playtest/Materials/ParticleAdditive.mat";
    const string DumpPath = "Assets/Playtest/Data/dump-seed-42.txt";
    static readonly List<string> results = new List<string>();
    static void Check(bool ok, string name) { results.Add((ok ? "PASS " : "FAIL ") + name); Debug.Log("PLAYTEST_VALIDATE " + (ok ? "PASS " : "FAIL ") + name); if (!ok) throw new Exception("FAILED: " + name); }
    static string Arg(string name, string fallback) {
        var a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
        return fallback;
    }

    [MenuItem("Infinite Conquest/Prepare playtest scene")]
    public static void Prepare() {
        results.Clear();
        ProofBuild.Validate(); // the 14 AI-036 movement assertions stay green
        Check(true, "ProofBuild.Validate (AI-036 fixture + hex geometry)");
        AssetDatabase.Refresh();
        // Apply PlaytestTokenImport settings to tokens imported before the postprocessor existed.
        foreach (var guid in AssetDatabase.FindAssets("", new[] { TokensDir })) {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(p);
            if (imp is TextureImporter ti && ti.maxTextureSize != 1024) { ti.maxTextureSize = 1024; ti.SaveAndReimport(); }
            else if (imp is ModelImporter mi && mi.materialImportMode != ModelImporterMaterialImportMode.None) { mi.materialImportMode = ModelImporterMaterialImportMode.None; mi.importAnimation = true; mi.SaveAndReimport(); }
        }
        int mats = 0;
        if (Directory.Exists(TokensDir)) foreach (var dir in Directory.GetDirectories(TokensDir)) {
            string d = dir.Replace('\\', '/');
            if (File.Exists(d + "/Token_BaseColor.png")) { TokenPreview.BuildMaterial(d); mats++; }
        }
        Debug.Log("PLAYTEST token materials built: " + mats);
        var particle = ParticleMaterial();
        var dump = AssetDatabase.LoadAssetAtPath<TextAsset>(DumpPath);
        Check(dump != null, "bundled AI-066 seed-42 dump present");
        var evs = WireEvent.ParseJsonl(dump.text);
        Check(evs.Count == 235 && evs.Last().@event == "GAME_OVER" && evs.Last().player == 0, "seed-42 dump = 235 events, GAME_OVER winner 0");
        Check(evs.Count(e => e.@event == "CARD_PLAYED" && e.hasTo) == evs.Count(e => e.@event == "CARD_PLAYED"), "every CARD_PLAYED carries a target hex");
        PlaytestCatalog.Load();
        var cat = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Playtest/Resources/Playtest/cards.json");
        Check(cat != null, "catalog cards.json present");
        var cards = JsonUtility.FromJson<CatalogProbe>(cat.text).cards;
        Check(cards.Length == 139, "catalog has 139 cards");
        foreach (var t in new[] { "LAND", "STRUCTURE", "CHARACTER", "CAPITAL", "SPELL" }) Check(cards.Any(c => c.type == t), "catalog has type " + t);
        int withModel = 0;
        foreach (var c in cards.Where(c => !string.IsNullOrEmpty(c.model))) {
            Check(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Playtest/Resources/" + c.model + ".fbx") != null, "model imports: " + c.id);
            withModel++;
        }
        Debug.Log("PLAYTEST real models: " + withModel);
        foreach (var e in evs.Where(e => e.card_id != null)) Check(cards.Any(c => c.id == e.card_id) || e.card_id.Contains("capital"), "dump card in catalog: " + e.card_id);
        BridgeSelfTest();

        var scene = File.Exists(ScenePath) ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                                           : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var game = UnityEngine.Object.FindAnyObjectByType<PlaytestGame>();
        if (game == null) game = new GameObject("Playtest").AddComponent<PlaytestGame>();
        game.LitShader = Shader.Find("Universal Render Pipeline/Lit");
        game.UnlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        game.ParticleMaterial = particle;
        game.DefaultDump = dump;
        Check(game.LitShader != null && game.UnlitShader != null && particle != null, "URP shaders + particle material referenced by the scene");
        if (Camera.main == null) {
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); cam.tag = "MainCamera";
        }
        EditorUtility.SetDirty(game);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true), new EditorBuildSettingsScene(ProofScenePath, true) };
        // The proof scene keeps its explicit shader reference (ProofBuild contract).
        var proofScene = EditorSceneManager.OpenScene(ProofScenePath, OpenSceneMode.Single);
        var proof = UnityEngine.Object.FindAnyObjectByType<InfiniteConquest.Proof.MovementProof>();
        Check(proof != null && proof.ProofShader != null, "MovementProof scene keeps its shader reference for -proofSmoke");
        Directory.CreateDirectory("Build");
        File.WriteAllText("Build/playtest-validation.json", "{\"passed\":true,\"checks\":" + results.Count + ",\"realModels\":" + withModel + "}");
    }

    [MenuItem("Infinite Conquest/Build playtest (Windows)")]
    public static void Build() {
        Prepare();
        PlayerSettings.productName = "Infinite Conquest Playtest";
        PlayerSettings.defaultScreenWidth = 1600; PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        string path = Arg("-buildPath", "Build/Playtest/InfiniteConquestPlaytest.exe");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { ScenePath, ProofScenePath }, locationPathName = path,
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("Playtest build failed: " + report.summary.result);
        Debug.Log("PLAYTEST_BUILD PASS " + path + " size=" + report.summary.totalSize);
    }

    [Serializable] class CatalogProbe { public CardEntry[] cards; }

    static Material ParticleMaterial() {
        Directory.CreateDirectory(Path.GetDirectoryName(ParticleMatPath));
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ParticleMatPath);
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, ParticleMatPath); }
        mat.shader = shader;
        // Transparent, additive (URP particle GUI conventions set by hand for batch mode).
        mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 2);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetColor("_BaseColor", Color.white);
        var soft = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        string texPath = "Assets/Playtest/Materials/SoftDot.png";
        if (!File.Exists(texPath)) {
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f;
                float a = Mathf.Clamp01(1 - d); a *= a;
                soft.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            File.WriteAllBytes(texPath, soft.EncodeToPNG()); AssetDatabase.ImportAsset(texPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(texPath); imp.alphaIsTransparency = true; imp.SaveAndReimport();
        }
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        EditorUtility.SetDirty(mat); AssetDatabase.SaveAssets();
        return mat;
    }

    // BridgeClient against canned AI-079 v1.0.0 lines.
    static void BridgeSelfTest() {
        var sentWriter = new StringWriter();
        var transcriptWriter = new StringWriter();
        var canned = "{\"id\":\"unity-1\",\"ok\":true,\"revision\":0,\"events\":[{\"turn\":1,\"player\":0,\"event\":\"CARD_PLAYED\",\"card_id\":\"zeus_arc_relay_scout\",\"instance_id\":\"i1\",\"to\":{\"x\":1,\"y\":1},\"seq\":0}],\"legal\":[{\"id\":\"r0-a1\",\"type\":\"move\",\"instance_id\":\"i1\",\"card_id\":\"zeus_arc_relay_scout\",\"to\":{\"x\":1,\"y\":2}},{\"id\":\"r0-a2\",\"type\":\"end_turn\"}],\"state\":{\"turn\":1,\"active_player\":0,\"winner\":null,\"phase\":\"PLAY\",\"players\":[{\"gp\":2,\"hand\":[{\"instance_id\":\"i2\",\"card_id\":\"zeus_arc_relay_scout\"}]},{\"gp\":0,\"hand_count\":6}],\"board\":[{\"x\":1,\"y\":1,\"stack\":[{\"instance_id\":\"i1\",\"card_id\":\"zeus_arc_relay_scout\",\"owner\":0}]}]}}\n"
                   + "{\"id\":\"unity-2\",\"ok\":true,\"revision\":0,\"events\":[],\"legal\":[{\"id\":\"r0-a1\",\"type\":\"move\",\"instance_id\":\"i1\",\"card_id\":\"zeus_arc_relay_scout\",\"to\":{\"x\":1,\"y\":2}},{\"id\":\"r0-a2\",\"type\":\"end_turn\"}]}\n"
                   + "{\"id\":\"unity-3\",\"ok\":false,\"revision\":0,\"error_code\":\"INVALID_ACTION\",\"error\":\"unknown or stale action id: zz\"}\n";
        var client = new BridgeClient(null, sentWriter, new StringReader(canned), null, transcriptWriter);
        client.New(42, 0); client.Legal(); client.Act("zz");
        var got = new List<BridgeResponse>();
        var until = DateTime.Now.AddSeconds(5);
        while (got.Count < 3 && DateTime.Now < until) { if (client.TryReceive(out var r)) got.Add(r); else System.Threading.Thread.Sleep(10); }
        var sent = sentWriter.ToString().Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        Check(sent.Length == 3 && sent[0].Contains("\"op\":\"new\"") && sent[0].Contains("\"human_player\":0") && sent[1].Contains("\"op\":\"legal\"") && sent[2].Contains("\"op\":\"act\"") && sent[2].Contains("\"action_id\":\"zz\""), "bridge client writes v1.0.0 new/legal/act lines");
        Check(got.Count == 3, "bridge client reads three responses");
        Check(got[0].ok && got[0].events.Length == 1 && got[0].events[0].hasTo && got[0].events[0].to.y == 1 && got[0].state.players[0].hand.Length == 1 && got[0].state.board[0].stack[0].owner == 0, "bridge response: events + state parsed");
        Check(got[1].legal.Length == 2 && got[1].legal[0].to.y == 2 && got[1].legal[1].type == "end_turn", "bridge response: legal actions parsed");
        Check(!got[2].ok && got[2].error_code == "INVALID_ACTION" && got[2].error.Contains("stale"), "bridge response: illegal id error surfaced");
        var transcript = transcriptWriter.ToString().Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        Check(transcript.Length == 6 && transcript.Count(s => s.Contains("\"direction\":\"request\"")) == 3
              && transcript.Count(s => s.Contains("\"direction\":\"response\"")) == 3,
              "bridge transcript records exact request/response lines without changing protocol streams");
    }
}

// Keeps staged tokens light: 1024px textures, no FBX material import (materials are built from
// Token_*.png by TokenPreview.BuildMaterial and assigned at runtime). Animation is imported (AI-060b): rigged
// tokens carry Meshy clips that UnitAnimator plays by name; idle/walk/run clips loop. Static tokens have none.
public sealed class PlaytestTokenImport : AssetPostprocessor {
    static bool IsToken(string path) => path.Replace('\\', '/').StartsWith("Assets/Playtest/Resources/Tokens/");
    void OnPreprocessTexture() {
        if (!IsToken(assetPath)) return;
        var t = (TextureImporter)assetImporter;
        t.maxTextureSize = 1024;
    }
    void OnPreprocessModel() {
        if (!IsToken(assetPath)) return;
        var m = (ModelImporter)assetImporter;
        m.materialImportMode = ModelImporterMaterialImportMode.None;
        m.importAnimation = true; m.importCameras = false; m.importLights = false;
        m.animationType = ModelImporterAnimationType.Generic;
    }
    void OnPreprocessAnimation() {
        if (!IsToken(assetPath)) return;
        var m = (ModelImporter)assetImporter;
        var clips = m.defaultClipAnimations;
        if (clips == null || clips.Length == 0) return;
        foreach (var c in clips) {
            var n = c.name.ToLowerInvariant();
            c.loopTime = n.Contains("idle") || n.Contains("walk") || n.Contains("run") || n.Contains("breath");
        }
        m.clipAnimations = clips;
    }
}
