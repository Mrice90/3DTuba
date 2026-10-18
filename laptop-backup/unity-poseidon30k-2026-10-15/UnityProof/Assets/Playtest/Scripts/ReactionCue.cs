using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace InfiniteConquest.Playtest {
    // AI-080-REACTION-VISUAL-CUE / AI-082-TURN-SOUND-REVIEW (2026-10-11):
    //  - while the bridge holds a reaction window (reaction_pending), a pulsing screen-edge glow, a
    //    "REACT NOW" banner under the top bar (what the opponent just did, how long the turn has been
    //    paused, Cast/Pass guidance and a Pass button) and outlines on the spells that can be cast now;
    //    online, an opponent's window shows a quieter "opponent may react" banner instead;
    //  - calmer turn sounds: one soft chime when your own turn starts, nothing on turn end or on the
    //    opponent's turn start (playback: a quieter chime per turn), and a "Turn chime" Calm/Off setting (Esc > Settings)
    //    that is independent of Mute and of combat sounds.
    // The engine has no reaction timeout, so the banner counts up how long the turn has been paused
    // instead of counting down. Sizes come from the screen height so the cue stays readable on short windows.
    public sealed partial class PlaytestGame {
        Rect reactionRect;
        bool reactionWasOpen;
        float reactionOpenedAt;
        string reactionTrigger = "";
        public int ReactionWindowsOpened { get; private set; }
        static Texture2D cueTexture;
        GUIStyle cueTitleStyle, cueTextStyle;
        int cueStyleHeight = -1;
        const string TurnChimePref = "ic.turnChime";
        bool? turnChime;
        public bool TurnChime {
            get {
                if (!turnChime.HasValue) turnChime = PlayerPrefs.GetInt(TurnChimePref, 1) != 0;
                return turnChime.Value;
            }
            set { turnChime = value; PlayerPrefs.SetInt(TurnChimePref, value ? 1 : 0); }
        }

        // A reaction window is open and it is this client's choice (solo: always the human; online: acting seat).
        public bool ReactionCueActive => mode == Mode.Live && liveState != null && liveState.reaction_pending && !liveState.GameOver
            && (!liveState.multiplayer || liveState.acting_player == humanSeat);
        // Online only: the opponent is deciding whether to react to this client's action.
        public bool OpponentReactionCueActive => mode == Mode.Live && liveState != null && liveState.reaction_pending && !liveState.GameOver
            && liveState.multiplayer && liveState.acting_player != humanSeat;

        // Layout used by DrawReactionCue, scaled from the screen size and anchored under the 62 px top bar.
        public struct ReactionCueLayout { public Rect banner, pass; public float glow; public int titleSize, textSize; }
        public static ReactionCueLayout CueLayout(float W, float H) {
            var l = new ReactionCueLayout();
            l.glow = GlowFor(H);
            l.titleSize = Mathf.RoundToInt(Mathf.Clamp(H / 30f, 17, 28));
            l.textSize = Mathf.RoundToInt(Mathf.Clamp(H / 58f, 11, 15));
            // Title + two text lines (each textSize + 7 with zero padding) + margins.
            float bh = l.titleSize + 12 + 2 * (l.textSize + 7) + 8;
            float bw = Mathf.Min(W - 24, Mathf.Clamp(W * .5f, 420, 680));
            l.banner = new Rect(W / 2 - bw / 2, 66, bw, bh);
            float pw = Mathf.Clamp(bw * .2f, 84, 120), ph = Mathf.Clamp(bh * .5f, 28, 38);
            l.pass = new Rect(l.banner.xMax - pw - 10, l.banner.y + bh / 2 - ph / 2, pw, ph);
            return l;
        }

        // Tracks the window opening/closing; called every frame from OnGUI and by the packaged validation.
        void TrackReactionWindow() {
            bool open = ReactionCueActive;
            if (open && !reactionWasOpen) {
                ReactionWindowsOpened++;
                reactionOpenedAt = Time.unscaledTime;
                reactionTrigger = model != null && model.Log.Count > 0 ? Regex.Replace(model.Log[model.Log.Count - 1], @" \((CHARACTER|SPELL|LAND|STRUCTURE)\)", "") : "";
                if (TurnChime && !sfx.Muted) sfx.PlayUiSoft("turn", .5f, 1.25f);
            }
            reactionWasOpen = open;
        }

        void DrawReactionCue(float W, float H) {
            TrackReactionWindow();
            reactionRect = Rect.zero;
            bool mine = ReactionCueActive, theirs = OpponentReactionCueActive;
            if ((!mine && !theirs) || MatchOver) return;
            var l = CueLayout(W, H);
            if (cueTexture == null) { cueTexture = new Texture2D(1, 1); cueTexture.SetPixel(0, 0, Color.white); cueTexture.Apply(); }
            if (cueStyleHeight != (int)H) {
                cueStyleHeight = (int)H;
                cueTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = l.titleSize, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft, clipping = TextClipping.Clip, padding = new RectOffset(0, 0, 0, 0) };
                cueTextStyle = new GUIStyle(GUI.skin.label) { fontSize = l.textSize, richText = true, wordWrap = false, alignment = TextAnchor.UpperLeft, clipping = TextClipping.Clip, padding = new RectOffset(0, 0, 0, 0) };
                cueTextStyle.normal.textColor = new Color(.95f, .95f, .98f);
            }
            var prev = GUI.color;
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 4.2f);
            var accent = mine ? new Color(1f, .72f, .18f) : new Color(.45f, .7f, 1f);
            if (mine) {
                // Screen-edge glow: three nested strips per edge, brightest at the edge.
                for (int i = 0; i < 3; i++) {
                    float t = l.glow * (3 - i) / 3f;
                    GUI.color = new Color(accent.r, accent.g, accent.b, (.3f + .45f * pulse) * (i + 1) / 3f);
                    GUI.DrawTexture(new Rect(0, 0, W, t), cueTexture); GUI.DrawTexture(new Rect(0, H - t, W, t), cueTexture);
                    GUI.DrawTexture(new Rect(0, 0, t, H), cueTexture); GUI.DrawTexture(new Rect(W - t, 0, t, H), cueTexture);
                }
                OutlineReactionSpells(W, H, accent, pulse);
            }
            // Banner with an accent frame.
            var b = l.banner; reactionRect = b;
            GUI.color = new Color(accent.r, accent.g, accent.b, mine ? .55f + .45f * pulse : .6f);
            GUI.DrawTexture(new Rect(b.x - 3, b.y - 3, b.width + 6, b.height + 6), cueTexture);
            GUI.color = new Color(.04f, .05f, .09f, .94f); GUI.DrawTexture(b, cueTexture);
            GUI.color = prev;
            float textW = b.width - 24 - (mine ? l.pass.width + 10 : 0);
            cueTitleStyle.normal.textColor = accent;
            int secs = Mathf.Max(0, Mathf.FloorToInt(Time.unscaledTime - reactionOpenedAt));
            string title = mine ? "REACT NOW - enemy turn paused" : "Opponent may react - waiting";
            GUI.Label(new Rect(b.x + 12, b.y + 6, textW, l.titleSize + 6), title, cueTitleStyle);
            // Two single lines: what the opponent just did, then what to do (and how long the turn has waited).
            string line1, line2;
            if (mine) {
                line1 = string.IsNullOrEmpty(reactionTrigger) ? "The opponent acted." : "Just now: <b>" + reactionTrigger + "</b>";
                line2 = (liveSelected != null ? "Click a glowing target hex, or Pass" : "Cast a glowing spell from your hand, or Pass") + $"  <color=#a8b3c4> |  {secs / 60}:{secs % 60:00}</color>";
            } else { line1 = "Your action is paused while they decide"; line2 = "whether to cast a reaction spell."; }
            float lineH = l.textSize + 7, ty = b.y + l.titleSize + 12;
            GUI.Label(new Rect(b.x + 12, ty, textW, lineH), line1, cueTextStyle);
            GUI.Label(new Rect(b.x + 12, ty + lineH, textW, lineH), line2, cueTextStyle);
            if (mine) {
                var pass = liveActions.FirstOrDefault(a => a.type == "pass_reaction");
                GUI.enabled = pass != null && !waiting && !paused;
                if (GUI.Button(l.pass, "Pass", buttonStyle)) ActLive(pass);
                GUI.enabled = true;
            }
        }

        // Same layout as DrawHand: outline every hand card that has a legal action (the castable reactions).
        void OutlineReactionSpells(float W, float H, Color accent, float pulse) {
            var hand = liveState?.players != null && liveState.players.Length > humanSeat ? liveState.players[humanSeat].hand : null;
            if (hand == null || hand.Length == 0) return;
            float cw = Mathf.Clamp(H * .15f, 96, 132), ch = cw * 1.39f, gap = 8, lift = cw * .2f;
            float total = hand.Length * cw + (hand.Length - 1) * gap;
            float x0 = Mathf.Max(270, W / 2 - total / 2), y0 = H - 52 - ch - 10;
            float t = Mathf.Max(3, GlowFor(H) * .6f);
            GUI.color = new Color(accent.r, accent.g, accent.b, .45f + .5f * pulse);
            for (int i = 0; i < hand.Length; i++) {
                if (!ActionsFor(hand[i].instance_id).Any()) continue;
                var r = new Rect(x0 + i * (cw + gap) - t, y0 - lift - t, cw + 2 * t, ch + lift + 2 * t);
                GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), cueTexture); GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), cueTexture);
                GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), cueTexture); GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), cueTexture);
            }
            GUI.color = Color.white;
        }
        static float GlowFor(float H) => Mathf.Clamp(H * .018f, 8, 16);

        // ------------------------------------------------------------------ calmer turn sounds
        void PlayTurnCue(int player) {
            if (!TurnChime || sfx == null) return;
            if (mode == Mode.Live) { if (player == humanSeat) sfx.PlayUiSoft("turn", .55f); }
            else sfx.PlayUiSoft("turn", .3f);
        }
    }
}
