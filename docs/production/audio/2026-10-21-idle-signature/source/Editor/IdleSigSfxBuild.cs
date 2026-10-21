using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using InfiniteConquest.Playtest;

// AI-215: idle and signature sounds wired (IdleSignatureAudio.cs). Check: each of the 123 idle sounds belongs to a card
// that sits on the board and is what that card's idle plays; each of the 32 signature sounds is what its card plays on
// arrival (8 characters as before, plus 6 spells, 6 lands, 6 structures and 6 capitals); every other card keeps its
// old arrival cue. Build: Check, then the unchanged perf build chain.
public static class IdleSigSfxBuild {
    const string Dir = "Assets/Playtest/Resources/MuseSfx";
    static readonly string[] BoardTypes = { "CHARACTER", "LAND", "STRUCTURE", "CAPITAL" };

    public static void Build() { Check(); PerfBuild.Build(); }

    public static void Check() {
        int checks = 0; var fails = new List<string>();
        Action<bool, string> check = (ok, m) => { checks++; if (!ok) fails.Add(m); };
        PlaytestCatalog.Load();
        var files = Directory.GetFiles(Dir, "*.mp3").Select(Path.GetFileNameWithoutExtension).ToArray();
        var idle = files.Where(f => f.EndsWith("_idle")).Select(f => f.Substring(0, f.Length - 5)).ToArray();
        var sig = files.Where(f => f.EndsWith("_signature")).Select(f => f.Substring(0, f.Length - 10)).ToArray();
        check(idle.Length == 123, "123 idle sounds, found " + idle.Length);
        check(sig.Length == 32, "32 signature sounds, found " + sig.Length);
        var host = new GameObject("Idle signature checks");
        var bank = host.AddComponent<SfxBank>();
        var byType = new Dictionary<string, int>();
        try {
            typeof(SfxBank).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(bank, null);
            foreach (var id in idle) {
                var card = PlaytestCatalog.All.FirstOrDefault(c => c.id == id);
                check(card != null, "idle card in catalog " + id);
                if (card == null) continue;
                check(BoardTypes.Contains(card.type), "idle card stays on the board " + id + " " + card.type);
                var clip = bank.ResolveLibrary(id + "_idle");
                check(clip != null && clip.length > 0, "idle imports " + id);
                check(bank.Resolve(id, "idle", out var own) == clip && own, "idle plays own " + id);
                bank.Play(id, "idle", Vector3.zero, PlaytestGame.IdleGain);
                check(bank.LastCue == id + "_idle", "idle playback " + id);
            }
            foreach (var id in sig) {
                var card = PlaytestCatalog.All.FirstOrDefault(c => c.id == id);
                check(card != null, "signature card in catalog " + id);
                if (card == null) continue;
                byType[card.type] = byType.TryGetValue(card.type, out var n) ? n + 1 : 1;
                var clip = bank.ResolveLibrary(id + "_signature");
                check(clip != null && clip.length > 0, "signature imports " + id);
                check(PlaytestGame.ArrivalCue(bank, card) == "signature", "arrival plays signature " + id);
                check(bank.Resolve(id, "signature", out var own) == clip && own, "signature plays own " + id);
            }
            check(byType.TryGetValue("CHARACTER", out var ch) && ch == 8, "8 character signatures (unchanged)");
            foreach (var t in new[] { "SPELL", "LAND", "STRUCTURE", "CAPITAL" })
                check(byType.TryGetValue(t, out var k) && k == 6, "6 " + t + " signatures");
            // Every card without a signature keeps the cue it played before.
            int unchanged = 0;
            foreach (var card in PlaytestCatalog.All.Where(c => !sig.Contains(c.id))) {
                string old = card.type == "LAND" ? "place" : card.type == "SPELL" ? "cast" : "deploy";
                check(PlaytestGame.ArrivalCue(bank, card) == old, "arrival unchanged " + card.id);
                unchanged++;
            }
            check(unchanged == PlaytestCatalog.All.Length - 32, "unchanged arrival cues " + unchanged);
        } finally { UnityEngine.Object.DestroyImmediate(host); }
        Directory.CreateDirectory("Build");
        File.WriteAllText("Build/idle-sig-sfx-validation.json", "{\"passed\":" + (fails.Count == 0 ? "true" : "false") + ",\"checks\":" + checks + ",\"failed\":" + fails.Count +
            ",\"idleSounds\":" + idle.Length + ",\"signatureSounds\":" + sig.Length + ",\"signatureByType\":{" + string.Join(",", byType.OrderBy(k => k.Key).Select(k => "\"" + k.Key + "\":" + k.Value)) +
            "},\"failures\":[" + string.Join(",", fails.Take(50).Select(f => "\"" + f.Replace("\"", "'") + "\"")) + "]}");
        if (fails.Count > 0) throw new Exception("IDLE_SIG FAIL " + fails.Count + "/" + checks + ": " + string.Join("; ", fails.Take(10)));
        Debug.Log("IDLE_SIG_VALIDATE PASS " + checks + " checks");
    }
}
