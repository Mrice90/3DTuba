using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using InfiniteConquest.Playtest;

// AI-CARD-SFX-BUILD: the 740-cue ElevenLabs set from 3DTuba PR #24 (690 card cues for all 139 cards + 50 core) was
// copied into Resources/MuseSfx under the same <card_id>_<cue> names SfxBank already resolves first (326 replaced in
// place with their loudness-matched versions, 414 new), and each card's cue list in Playtest/cards.json gained the cues
// it now has (so rarity-4 signature summons play). The files are loudness-matched (EBU R128), so Unity's per-clip
// peak normalize is turned off for MuseSfx or it would undo that. Check: every card cue imports with audible data and
// is what SfxBank plays for that card on the matching board event. Build: Check, then the rig-combat build chain.
// The new core turn/coin/mulligan/interrupt/panel cues are imported but not wired here (Codex's event lane).
public static class CardSfxBuild {
    [Serializable] public class Entry { public string key, card_id, cue, scope, sha256; public float lufs; }
    [Serializable] public class Manifest { public int count; public Entry[] entries; }
    const string Dir = "Assets/Playtest/Resources/MuseSfx";

    public static void Build() { Check(); RigCombatBuild.Build(); }

    // AudioImporter has no public normalize property in this Unity version; it is the serialized m_Normalize field.
    static SerializedProperty NormalizeProp(SerializedObject so) => so.FindProperty("m_Normalize") ?? so.FindProperty("normalize");
    static bool Normalize(AudioImporter imp) { var p = NormalizeProp(new SerializedObject(imp)); if (p == null) throw new Exception("CARD_SFX FAIL no normalize property"); return p.boolValue; }

    public static void Check() {
        int checks = 0; var fails = new List<string>();
        Action<bool, string> check = (ok, m) => { checks++; if (!ok) fails.Add(m); };
        AssetDatabase.Refresh();
        int reimported = 0;
        foreach (var path in Directory.GetFiles(Dir, "*.mp3")) {
            var imp = (AudioImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
            if (imp != null && Normalize(imp)) { var so = new SerializedObject(imp); NormalizeProp(so).boolValue = false; so.ApplyModifiedPropertiesWithoutUndo(); imp.SaveAndReimport(); reimported++; }
        }
        PlaytestCatalog.Load();
        var manifest = JsonUtility.FromJson<Manifest>(Resources.Load<TextAsset>("Playtest/card-sfx").text);
        check(manifest.count == 740 && manifest.entries.Length == 740, "740 cues, found " + manifest.entries.Length);
        check(manifest.entries.Select(e => e.key).Distinct().Count() == 740, "unique keys");
        var cards = manifest.entries.Where(e => e.scope == "card").ToArray();
        check(cards.Length == 690, "690 card cues");
        check(cards.Select(e => e.card_id).Distinct().Count() == PlaytestCatalog.All.Length, "every catalog card has sounds");
        var host = new GameObject("Card SFX checks");
        var bank = host.AddComponent<SfxBank>();
        var levels = new Dictionary<string, List<float>>();
        int played = 0;
        try {
            typeof(SfxBank).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(bank, null);
            foreach (var e in manifest.entries) {
                var imp = (AudioImporter)AssetImporter.GetAtPath(Dir + "/" + e.key + ".mp3");
                check(imp != null && !Normalize(imp), "normalize off " + e.key);
                var clip = bank.ResolveLibrary(e.key);
                check(clip != null && clip.length > 0, "import " + e.key);
                if (clip == null) continue;
                var s = new float[clip.samples * clip.channels];
                double sum = 0; float peak = 0;
                if (clip.GetData(s, 0)) foreach (var v in s) { sum += v * v; peak = Mathf.Max(peak, Mathf.Abs(v)); }
                check(peak > .01f && peak <= 1f, "audible " + e.key + " peak " + peak);
                float rmsDb = 10f * Mathf.Log10((float)(sum / Math.Max(1, s.Length)) + 1e-12f);
                string band = e.lufs.ToString("0");
                if (!levels.ContainsKey(band)) levels[band] = new List<float>();
                levels[band].Add(rmsDb);
                if (e.scope != "card") continue;
                var card = PlaytestCatalog.Get(e.card_id);
                check(card.HasCue(e.cue), "catalog cue " + e.key);
                check(bank.Resolve(e.card_id, e.cue, out var own) == clip && own, "plays own " + e.key);
                // The cue names the board events actually request for this card type.
                var events = new List<string>();
                if (e.cue == "deploy") events.Add(card.type == "LAND" ? "place" : card.type == "SPELL" ? "cast" : "deploy");
                if (e.cue == "ability" && card.type == "SPELL") events.Add("resolve_impact");
                if (e.cue == "signature") check(card.type != "CHARACTER" || card.HasCue("signature"), "signature summon selected " + e.key);
                foreach (var ev in events) check(bank.Resolve(e.card_id, ev, out own) == clip && own, "event " + ev + " -> " + e.key);
                bank.Play(e.card_id, e.cue, Vector3.zero);
                check(bank.LastCue == e.card_id + "_" + e.cue, "playback " + e.key);
                played++;
            }
            check(bank.CardCuesPlayed == played && bank.GenericCuesPlayed == 0, "no generic fallback for any card cue");
            bank.Resolve("unknown_missing_card", "attack", out var missing);
            check(!missing, "unknown card keeps the generic fallback");
            check(bank.Resolve("unknown", "turn", out missing) != bank.ResolveLibrary("core_turn_start_player"), "calm turn chime preserved");
        } finally { UnityEngine.Object.DestroyImmediate(host); }
        Directory.CreateDirectory("Build");
        var lv = string.Join(",", levels.OrderBy(k => k.Key).Select(k => "\"" + k.Key + "\":{\"n\":" + k.Value.Count + ",\"rmsDbMin\":" + k.Value.Min().ToString("0.0") + ",\"rmsDbMax\":" + k.Value.Max().ToString("0.0") + "}"));
        File.WriteAllText("Build/card-sfx-validation.json", "{\"passed\":" + (fails.Count == 0 ? "true" : "false") + ",\"checks\":" + checks + ",\"failed\":" + fails.Count +
            ",\"cues\":" + manifest.entries.Length + ",\"cardCues\":" + cards.Length + ",\"cardCuesPlayed\":" + played + ",\"normalizeTurnedOff\":" + reimported +
            ",\"rmsByTargetLufs\":{" + lv + "},\"failures\":[" + string.Join(",", fails.Take(50).Select(f => "\"" + f.Replace("\"", "'") + "\"")) + "]}");
        if (fails.Count > 0) throw new Exception("CARD_SFX FAIL " + fails.Count + "/" + checks + ": " + string.Join("; ", fails.Take(10)));
        Debug.Log("CARD_SFX_VALIDATE PASS " + checks + " checks, " + played + " card cues");
    }
}
