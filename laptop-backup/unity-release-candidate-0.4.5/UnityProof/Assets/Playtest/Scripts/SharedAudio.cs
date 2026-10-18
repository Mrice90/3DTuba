using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace InfiniteConquest.Playtest {
    public sealed partial class PlaytestGame {
        bool capitalsRevealed;
        readonly bool[] lowCapitalWarned = new bool[2];

        void ResetSharedAudio() {
            capitalsRevealed = false;
            lowCapitalWarned[0] = lowCapitalWarned[1] = false;
        }
        void RevealCapitalAudio() {
            if (capitalsRevealed || mode != Mode.Live || CapitalOf(0) == null || CapitalOf(1) == null) return;
            capitalsRevealed = true;
            sfx.PlayCore("capital_reveal", .22f);
        }
        void PlayLowCapitalWarning(int owner) {
            if (owner < 0 || owner > 1 || lowCapitalWarned[owner]) return;
            int hp = model.CapitalHp[owner], max = model.CapitalMax[owner];
            if (max <= 0 || hp <= 0 || hp * 4 > max) return;
            lowCapitalWarned[owner] = true;
            sfx.PlayCore("capital_warning", .22f);
        }
        void PlayActionAudio(BridgeAction action) {
            if (action != null && action.type == "end_turn") sfx.PlayCore("end_turn_button", .15f);
        }
        [Serializable] public class CoreSoundCount { public string cue; public int count; }
        [Serializable] public class CoreSoundReport { public int totalRequested, totalPlayed; public bool muted; public CoreSoundCount[] cues; }
        void SaveAudioEventReport() {
            var path = Arg("-audioEventResult");
            if (path == null || sfx == null) return;
            var report = new CoreSoundReport { totalRequested = sfx.CoreRequestCounts.Values.Sum(), totalPlayed = sfx.CoreCuesPlayed, muted = sfx.Muted,
                cues = sfx.CoreRequestCounts.OrderBy(kv => kv.Key).Select(kv => new CoreSoundCount { cue = kv.Key, count = kv.Value }).ToArray() };
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
        }

        [Serializable] public class MuseSoundRow { public string key, card_id, cue, scope; }
        [Serializable] public class MuseSoundFile { public int count; public MuseSoundRow[] entries; }
        // An actual player check: Resource stripping/decoding can differ from editor imports.
        IEnumerator ValidateAudioLibrary() {
            int decoded = 0, resolved = 0;
            string failure = "";
            try {
                var text = Resources.Load<TextAsset>("Playtest/muse-audio");
                if (text == null) throw new Exception("Missing packaged audio manifest");
                var manifest = JsonUtility.FromJson<MuseSoundFile>(text.text);
                if (manifest.count != 326 || manifest.entries == null || manifest.entries.Length != 326)
                    throw new Exception("Expected 326 packaged sounds");
                foreach (var row in manifest.entries) {
                    var clip = sfx.ResolveLibrary(row.key);
                    if (clip == null || clip.samples <= 0 || clip.channels <= 0) throw new Exception("Missing " + row.key);
                    var data = new float[clip.samples * clip.channels];
                    if (!clip.GetData(data, 0) || !data.Any(s => !float.IsNaN(s) && !float.IsInfinity(s) && Mathf.Abs(s) > .001f))
                        throw new Exception("No decoded audio: " + row.key);
                    decoded++;
                    if (row.scope == "card") {
                        if (sfx.Resolve(row.card_id, row.cue, out var specific) != clip || !specific)
                            throw new Exception("Wrong card resolver: " + row.key);
                        resolved++;
                    }
                }
                if (resolved != 287) throw new Exception("Expected 287 card sounds");
            } catch (Exception ex) { failure = ex.Message; }
            string result = "{\"passed\":" + (failure == "").ToString().ToLowerInvariant() +
                ",\"decodedSounds\":" + decoded + ",\"resolvedCardCues\":" + resolved +
                ",\"error\":\"" + failure.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}";
            var path = Arg("-playtestResult");
            if (path != null) File.WriteAllText(path, result);
            Debug.Log("AUDIO_LIBRARY_PLAYER " + result);
            yield return null;
            Application.Quit(failure == "" ? 0 : 1);
        }
    }
}
