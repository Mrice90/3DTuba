using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using InfiniteConquest.Playtest;

public static class AudioFollowupBuild {
    static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static int checks;
    static void Check(bool ok, string label) { if (!ok) throw new Exception("AUDIO_FOLLOWUP FAIL " + label); checks++; }
    static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
    static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Private).Invoke(obj, args);
    static void Drain(IEnumerator e) { while (e.MoveNext()) if (e.Current is IEnumerator nested) Drain(nested); }
    static int Count(SfxBank bank, string cue) => bank.CorePlaybackCounts.TryGetValue(cue, out var value) ? value : 0;
    public static void Build() {
        var host = new GameObject("Audio followup game"); host.SetActive(false);
        var game = host.AddComponent<PlaytestGame>();
        var soundHost = new GameObject("Audio followup bank"); var bank = soundHost.AddComponent<SfxBank>();
        var capitals = new List<GameObject>();
        try {
            Call(bank, "Awake"); Set(game, "sfx", bank); Set(game, "speed", 200f);
            var mode = typeof(PlaytestGame).GetField("mode", Private);
            mode.SetValue(game, Enum.Parse(mode.FieldType, "Live"));
            var model = (MatchModel)typeof(PlaytestGame).GetField("model", Private).GetValue(game);
            // Actual match event handler, rather than a mirrored mapping table.
            Drain((IEnumerator)Call(game, "Apply", new WireEvent { @event = "MATCH_STARTED", detail = "audio test" }));
            Check(Count(bank, "board_build") == 1 && Count(bank, "match_start") == 1, "match event fires both shared cues once");
            int before = bank.CoreCuesPlayed; bank.Muted = true;
            bank.PlayCore("board_build"); Check(bank.CoreCuesPlayed == before, "muted shared sounds do not trigger");
            Check(bank.CoreRequestCounts["board_build"] == 2, "silent event routing can be verified independently of mute");
            bank.Muted = false; bank.PlayCore("board_build", 0); bank.PlayCore("nonexistent");
            Check(bank.CoreCuesPlayed == before, "zero gain and missing cues safely skip");
            Call(game, "PlayActionAudio", new BridgeAction { type = "pass_reaction" });
            Check(Count(bank, "end_turn_button") == 0, "passing a reaction is not an end-turn sound");
            Call(game, "PlayActionAudio", new BridgeAction { type = "end_turn" });
            Check(Count(bank, "end_turn_button") == 1, "end-turn confirmation sound");
            Call(game, "TogglePause"); Check(Count(bank, "ui_open_panel") == 1, "pause opens with panel cue");
            Call(game, "TogglePause"); Check(Count(bank, "ui_close_panel") == 1, "resume closes with panel cue");
            model.CapitalMax[0] = 20; model.CapitalHp[0] = 6;
            Call(game, "PlayLowCapitalWarning", 0); Check(Count(bank, "capital_warning") == 0, "above quarter health is quiet");
            model.CapitalHp[0] = 5; Call(game, "PlayLowCapitalWarning", 0); Call(game, "PlayLowCapitalWarning", 0);
            Check(Count(bank, "capital_warning") == 1, "quarter-health warning once per capital");
            model.CapitalHp[1] = 0; Call(game, "PlayLowCapitalWarning", 1);
            Check(Count(bank, "capital_warning") == 1, "dead capital does not add warning over match end");
            Call(game, "ResetSharedAudio"); Call(game, "PlayLowCapitalWarning", 0);
            Check(Count(bank, "capital_warning") == 2, "new match re-arms warning");
            var pieces = (Dictionary<string, Piece>)typeof(PlaytestGame).GetField("pieces", Private).GetValue(game);
            Call(game, "RevealCapitalAudio"); Check(Count(bank, "capital_reveal") == 0, "wait for both capitals");
            for (int seat = 0; seat < 2; seat++) {
                var go = new GameObject("Audio capital " + seat); capitals.Add(go);
                var piece = go.AddComponent<Piece>(); piece.Owner = seat; piece.Card = new CardEntry { type = "CAPITAL" };
                pieces["audio-capital-" + seat] = piece;
            }
            Call(game, "RevealCapitalAudio"); Call(game, "RevealCapitalAudio");
            Check(Count(bank, "capital_reveal") == 1, "pair reveal once, not on every state refresh");
            var land = bank.Resolve("zeus_olympian_cloudbank", "deploy", out var ownLand);
            Check(ownLand && land == bank.Resolve("zeus_olympian_cloudbank", "place", out _), "reuse legacy land placement for deploy");
            var spell = bank.Resolve("poseidon_apex_maelstrom_verdict", "deploy", out var ownSpell);
            Check(ownSpell && spell == bank.Resolve("poseidon_apex_maelstrom_verdict", "cast", out _), "reuse legacy spell cast for deploy");
            // AI-CARD-SFX-BUILD: PR #24 gave this card its own signature; it is preferred over reusing the cast.
            var ownSig = bank.ResolveLibrary("poseidon_apex_maelstrom_verdict_signature");
            Check(bank.Resolve("poseidon_apex_maelstrom_verdict", "signature", out var ownSignature) == (ownSig ?? spell) && ownSignature, "apex spell signature: own clip, else its cast");
            var signature = Resources.Load<AudioClip>("Sfx/zeus_capital_olympus_citadel_signature");
            var librarySig = bank.ResolveLibrary("zeus_capital_olympus_citadel_signature");
            Check(signature != null && bank.Resolve("zeus_capital_olympus_citadel", "signature", out _) == (librarySig ?? signature), "exact signature preferred (PR #24 library pick, else legacy)");
        } finally {
            foreach (var go in capitals) UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(soundHost);
        }
        Directory.CreateDirectory("Build");
        File.WriteAllText("Build/audio-followup-validation.json", "{\"passed\":true,\"checks\":" + checks + "}");
        Debug.Log("AUDIO_FOLLOWUP_VALIDATE PASS " + checks);
        MuseAudioBuild.Build();
    }
}
