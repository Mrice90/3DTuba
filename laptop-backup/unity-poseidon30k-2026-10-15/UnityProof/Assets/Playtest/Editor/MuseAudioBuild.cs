using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using InfiniteConquest.Playtest;

public static class MuseAudioBuild {
    [Serializable] public class Entry { public string key, card_id, cue, scope, sha256; }
    [Serializable] public class Manifest { public int count; public Entry[] entries; }
    static int checks;
    static void Check(bool pass, string label) { if (!pass) throw new Exception("MUSE_AUDIO FAIL " + label); checks++; }
    public static void Build() {
        AssetDatabase.Refresh();
        var manifest = JsonUtility.FromJson<Manifest>(Resources.Load<TextAsset>("Playtest/muse-audio").text);
        Check(manifest.count == 326 && manifest.entries.Length == 326, "all Muse files");
        Check(manifest.entries.Select(e => e.key).Distinct().Count() == 326, "unique resource names");
        var host = new GameObject("Muse audio checks");
        var bank = host.AddComponent<SfxBank>();
        try {
            typeof(SfxBank).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(bank, null);
            foreach (var e in manifest.entries) {
                var clip = bank.ResolveLibrary(e.key);
                Check(clip != null && clip.length > 0 && clip.samples > 0, "import " + e.key);
                var samples = new float[clip.samples * clip.channels];
                Check(clip.GetData(samples, 0) && samples.Any(s => Mathf.Abs(s) > .001f), "audible data " + e.key);
                if (e.scope == "card") {
                    Check(PlaytestCatalog.Get(e.card_id).HasCue(e.cue), "catalog " + e.key);
                    Check(bank.Resolve(e.card_id, e.cue, out var specific) == clip && specific, "runtime pick " + e.key);
                    if (e.cue == "deploy") {
                        string semantic = PlaytestCatalog.Get(e.card_id).type == "LAND" ? "place" : "cast";
                        Check(bank.Resolve(e.card_id, semantic, out specific) == clip && specific, "semantic deploy " + e.key);
                    }
                    if (e.cue == "ability") Check(bank.Resolve(e.card_id, "resolve_impact", out var impactSpecific) == clip && impactSpecific, "spell impact " + e.key);
                }
            }
            bank.Resolve("unknown_missing_card", "attack", out var missing);
            Check(!missing, "missing card keeps fallback");
            bank.LastCue = ""; bank.Muted = true; bank.PlayCore("match_start");
            Check(bank.LastCue == "", "mute core sounds");
            bank.Muted = false; bank.PlayCore("match_start"); Check(bank.LastCue == "core_match_start", "core event playback");
            bank.LastCue = ""; bank.PlayCore("missing_core"); Check(bank.LastCue == "", "missing core safely skips");
            var turn = bank.Resolve("unknown", "turn", out missing);
            Check(turn != bank.ResolveLibrary("core_turn_start_player"), "calm turn preserved");
        } finally { UnityEngine.Object.DestroyImmediate(host); }
        Directory.CreateDirectory("Build");
        File.WriteAllText("Build/muse-audio-validation.json", "{\"passed\":true,\"checks\":" + checks + ",\"imports\":326,\"cardCues\":287,\"coreCues\":39}");
        Debug.Log("MUSE_AUDIO_VALIDATE PASS " + checks + " checks, 326 imports");
        BoardBuild.Build();
    }
}
