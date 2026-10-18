using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using InfiniteConquest.Playtest;

// AI-080-REACTION-VISUAL-CUE / AI-082-TURN-SOUND-REVIEW editor checks: reaction-window cue state and its
// layout on tall and short screens, and the calmer turn sounds (one soft chime on your own turn start,
// nothing on turn end, Calm/Off setting). Then runs the unchanged R3 chain and the player build.
// Full chain: Unity -batchmode -quit -projectPath <copy>/UnityProof -executeMethod InterruptCueBuild.Build -buildPath <exe>
public static class InterruptCueBuild {
    static readonly List<string> checks = new List<string>();
    static void Check(bool ok, string label) {
        if (!ok) throw new Exception("INTERRUPT_VALIDATE FAIL " + label);
        checks.Add(label); Debug.Log("INTERRUPT_VALIDATE PASS " + label);
    }
    static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
    static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Private).Invoke(obj, args);
    static void Drain(IEnumerator e, int depth = 0) {
        int guard = 0;
        while (guard++ < 10000 && e.MoveNext()) {
            var c = e.Current;
            if (c is CustomYieldInstruction) continue;
            if (c is IEnumerator inner && depth < 8) Drain(inner, depth + 1);
        }
    }

    [MenuItem("Infinite Conquest/Validate reaction cue + turn sounds")]
    public static void Validate() {
        checks.Clear();
        // ---- layout: under the 62 px top bar, above the hand, inside the screen, readable text
        foreach (var size in new[] { new Vector2(1920, 1080), new Vector2(1296, 688), new Vector2(1280, 600), new Vector2(1024, 540), new Vector2(800, 480) }) {
            float W = size.x, H = size.y;
            var l = PlaytestGame.CueLayout(W, H);
            float cw = Mathf.Clamp(H * .15f, 96, 132), handTop = H - 52 - cw * 1.39f - 10 - cw * .2f;
            string at = W + "x" + H;
            Check(l.banner.y >= 62 && l.banner.yMax < handTop, at + ": banner sits between the top bar and the raised hand");
            Check(l.banner.x >= 0 && l.banner.xMax <= W && l.banner.width >= Mathf.Min(W - 24, 420), at + ": banner fits the screen width");
            Check(l.pass.x > l.banner.x && l.pass.xMax <= l.banner.xMax && l.pass.y >= l.banner.y && l.pass.yMax <= l.banner.yMax && l.pass.height >= 28, at + ": Pass button inside the banner, at least 28 px tall");
            Check(l.titleSize >= 17 && l.textSize >= 11 && l.titleSize + 12 + 2 * (l.textSize + 7) <= l.banner.height, at + ": title/text sizes readable and fit the banner");
            Check(l.glow >= 8, at + ": edge glow at least 8 px");
        }

        var host = new GameObject("Interrupt validation host"); host.SetActive(false);
        var game = host.AddComponent<PlaytestGame>();
        var soundObject = new GameObject("Interrupt validation sound"); var sound = soundObject.AddComponent<SfxBank>();
        bool chimeBefore = game.TurnChime;
        try {
            typeof(SfxBank).GetMethod("Awake", Private).Invoke(sound, null);
            Set(game, "sfx", sound); Set(game, "speed", 200f);
            var mode = typeof(PlaytestGame).GetField("mode", Private);
            mode.SetValue(game, Enum.Parse(mode.FieldType, "Live"));
            Set(game, "humanSeat", 0);
            var model = (MatchModel)typeof(PlaytestGame).GetField("model", Private).GetValue(game);

            // ---- reaction cue state
            BridgeState State(bool pending, bool multiplayer, int acting, string phase = "MAIN") =>
                new BridgeState { reaction_pending = pending, multiplayer = multiplayer, acting_player = acting, active_player = 1, phase = phase, players = new BridgePlayer[0] };
            Set(game, "liveState", State(false, false, 0));
            Check(!game.ReactionCueActive && !game.OpponentReactionCueActive, "no cue outside a reaction window");
            model.AddLog("Poseidon plays Riptide Lancer (CHARACTER) at 2,3");
            Set(game, "liveState", State(true, false, 0));
            Check(game.ReactionCueActive && !game.OpponentReactionCueActive, "solo: React-now cue while the bot's turn is paused");
            int opened = game.ReactionWindowsOpened;
            Call(game, "TrackReactionWindow"); Call(game, "TrackReactionWindow");
            Check(game.ReactionWindowsOpened == opened + 1, "one window counted once while it stays open");
            Check(((string)typeof(PlaytestGame).GetField("reactionTrigger", Private).GetValue(game)).Contains("Riptide Lancer at 2,3"), "banner names what the opponent just did (card type tag dropped to fit)");
            Set(game, "liveState", State(false, false, 0)); Call(game, "TrackReactionWindow");
            Set(game, "liveState", State(true, false, 0)); Call(game, "TrackReactionWindow");
            Check(game.ReactionWindowsOpened == opened + 2, "a new window after the previous one closed is counted again");
            Set(game, "liveState", State(true, true, 0));
            Check(game.ReactionCueActive, "online: cue when this seat is the one reacting");
            Set(game, "liveState", State(true, true, 1));
            Check(!game.ReactionCueActive && game.OpponentReactionCueActive, "online: opponent's reaction shows the quieter waiting banner instead");
            Set(game, "liveState", State(true, false, 0, "GAME_OVER"));
            Check(!game.ReactionCueActive && !game.OpponentReactionCueActive, "no cue after game over");
            Set(game, "reactionRect", new Rect(100, 66, 400, 70));
            Check((bool)Call(game, "OverHud", new Vector2(200, Screen.height - 90)), "banner area is excluded from board clicks");

            // ---- calmer turn sounds
            var clip = (AudioClip)typeof(SfxBank).GetMethod("Generic", Private).Invoke(sound, new object[] { "turn", "ZEUS" });
            var data = new float[clip.samples]; clip.GetData(data, 0);
            float peak = data.Max(Mathf.Abs);
            int n = data.Length, tail = n / 20;
            float headRms = Mathf.Sqrt(data.Take(n / 200).Average(v => v * v)), tailRms = Mathf.Sqrt(data.Skip(n - tail).Average(v => v * v));
            float bodyRms = Mathf.Sqrt(data.Skip(n / 20).Take(n / 5).Average(v => v * v));
            Check(clip.length >= 1f && peak <= .22f, $"turn chime is one soft cue (length {clip.length:0.00}s, peak {peak:0.000} vs old .35)");
            Check(headRms < bodyRms * .5f && tailRms < bodyRms * .05f, "turn chime fades in and fades out to silence");
            int Crossings(IEnumerable<float> s) { int c = 0; float last = 0; foreach (var v in s) { if (last < 0 && v >= 0) c++; last = v; } return c; }
            int early = Crossings(data.Take(n / 4)), late = Crossings(data.Skip(n / 4).Take(n / 4));
            Check(Mathf.Abs(early - late) <= early * .05f + 2, "turn chime has no rising/falling pitch step");

            sound.Muted = false; game.TurnChime = true;
            sound.LastCue = "";
            Drain((IEnumerator)Call(game, "Apply", new WireEvent { @event = "TURN_ENDED", player = 0, turn = 3 }));
            Check(sound.LastCue == "", "turn end makes no sound");
            Drain((IEnumerator)Call(game, "Apply", new WireEvent { @event = "TURN_STARTED", player = 1, turn = 3 }));
            Check(sound.LastCue == "", "live: opponent's turn start makes no sound (banner only)");
            Drain((IEnumerator)Call(game, "Apply", new WireEvent { @event = "TURN_STARTED", player = 0, turn = 4 }));
            Check(sound.LastCue == "generic_turn", "live: your own turn start plays the calm chime");
            game.TurnChime = false; sound.LastCue = "";
            Drain((IEnumerator)Call(game, "Apply", new WireEvent { @event = "TURN_STARTED", player = 0, turn = 5 }));
            Check(sound.LastCue == "", "Turn chime Off silences turn cues");
            sound.Play("zeus_storm_relay_pylon", "ability");
            Check(sound.LastCue != "", "Turn chime Off leaves combat/card sounds playing");
        } finally {
            game.TurnChime = chimeBefore;
            UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(soundObject);
        }
        Directory.CreateDirectory("Build");
        File.WriteAllText("Build/interrupt-cue-validation.json", "{\"passed\":true,\"checks\":" + checks.Count + ",\"names\":[" + string.Join(",", checks.Select(c => "\"" + c.Replace("\"", "'") + "\"")) + "]}");
    }

    public static void Build() { Validate(); R3Build.Build(); }
}
