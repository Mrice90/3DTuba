using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using InfiniteConquest.Playtest;

public static class Sp1AudioBuild {
    [Serializable] public class Entry { public string card_id, cue; public float duration_seconds; }
    [Serializable] public class Manifest { public Entry[] entries; }
    static string Arg(string name) {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
        throw new Exception("Missing argument " + name);
    }
    public static void Build() {
        AssetDatabase.Refresh();
        var data = Resources.Load<TextAsset>("Playtest/sp1-audio");
        if (data == null) throw new Exception("Missing SP1 audio manifest");
        var entries = JsonUtility.FromJson<Manifest>(data.text).entries;
        if (entries.Length != 230 || entries.Select(e => e.card_id).Distinct().Count() != 88)
            throw new Exception("SP1 coverage mismatch");
        var go = new GameObject("SP1 audio validation");
        var bank = go.AddComponent<SfxBank>();
        foreach (var e in entries) {
            var clip = bank.Resolve(e.card_id, e.cue, out bool specific);
            var original = Resources.Load<AudioClip>("Sfx/" + e.card_id + "_" + e.cue);
            if (!specific || clip == null || original == null || Mathf.Abs(original.length - e.duration_seconds) > .03f)
                throw new Exception("SP1 clip missing or wrong duration: " + e.card_id + "_" + e.cue);
        }
        UnityEngine.Object.DestroyImmediate(go);
        Debug.Log("SP1_AUDIO_VALIDATE PASS 230 cues across 88 cards");
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0) throw new Exception("No enabled build scenes");
        var path = Arg("-buildPath");
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = scenes, locationPathName = path, target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        if (result.summary.result != BuildResult.Succeeded) throw new Exception("SP1 build failed: " + result.summary.result);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(path), "audio-build-validation.json"),
            "{\"passed\":true,\"sp1Cards\":88,\"sp1Cues\":230,\"bytes\":" + result.summary.totalSize + "}");
        Debug.Log("SP1_AUDIO_BUILD PASS " + path);
    }
}
