using System.Collections.Generic;
using UnityEngine;

namespace InfiniteConquest.Playtest {
    // Per-card SFX lookup: Resources/Sfx/<card_id>_<cue>.wav (copied from ElevenLabs picks/ by
    // stage_playtest_assets.py), falling back to a synthesised generic cue per cue name, so every
    // card makes a sound on deploy/move/attack/hit/destroy even before its picks exist.
    // Board cues pass a world position and play on partly-3D voices (panned, never fully attenuated);
    // UI/global cues (click, error, card, turn, turn_end, victory, defeat) stay 2D and can be
    // overridden per event via Overrides or Resources/Sfx/ui_<cue>[_2.._4].wav.
    public sealed class SfxBank : MonoBehaviour {
        public static readonly string[] Cues = { "deploy", "move", "attack", "hit", "destroy", "ability", "signature", "idle", "place", "cast", "resolve_impact" };
        public static readonly string[] UiCues = { "click", "error", "card", "turn", "turn_end", "victory", "defeat" };
        [System.Serializable] public sealed class EventClips { public string cue; public AudioClip[] clips; }
        public EventClips[] Overrides = new EventClips[0];
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, AudioClip> generic = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, List<AudioClip>> ui = new Dictionary<string, List<AudioClip>>();
        AudioSource[] voices, boardVoices;
        int next, nextBoard;
        public float Volume = .8f;
        public bool Muted;
        public int CardCuesPlayed, GenericCuesPlayed;
        public string LastCue = "";

        void Awake() {
            voices = new AudioSource[8];
            for (int i = 0; i < voices.Length; i++) { voices[i] = gameObject.AddComponent<AudioSource>(); voices[i].playOnAwake = false; voices[i].spatialBlend = 0; }
            // Camera sits 3-40 units from the board: linear rolloff from 15 with a .6 blend keeps far cues >60% loud.
            boardVoices = new AudioSource[12];
            for (int i = 0; i < boardVoices.Length; i++) {
                var v = boardVoices[i] = new GameObject("Board voice " + i).AddComponent<AudioSource>();
                v.transform.SetParent(transform, false); v.playOnAwake = false;
                v.spatialBlend = .6f; v.rolloffMode = AudioRolloffMode.Linear; v.minDistance = 15; v.maxDistance = 60; v.dopplerLevel = 0;
            }
        }

        // Resolves the clip that would play, and whether it is card-specific.
        public AudioClip Resolve(string cardId, string cue, out bool cardSpecific) {
            cardSpecific = false;
            var entry = PlaytestCatalog.Get(cardId);
            string want = cue;
            // Preserve older card audio when a semantic land/spell cue has no dedicated clip.
            if (!entry.HasCue(want)) {
                if (want == "place" || want == "cast") want = "deploy";
                else if (want == "resolve_impact") want = "ability";
            }
            // apex signature falls back to the card's own deploy; death/summon naming per AI-077.
            if (!entry.HasCue(want) && want == "signature" && entry.HasCue("deploy")) want = "deploy";
            if (entry.HasCue(want)) {
                string key = cardId + "_" + want;
                if (!clips.TryGetValue(key, out var clip)) { clip = Resources.Load<AudioClip>("Sfx/" + key); clips[key] = clip; }
                if (clip != null) { cardSpecific = true; return clip; }
            }
            return Generic(want, entry.faction);
        }

        public void Play(string cardId, string cue, float volume = 1f) => Play(cardId, cue, null, volume);
        // Board event at a world position: slight pitch/volume variation so repeats don't sound stamped.
        public void Play(string cardId, string cue, Vector3? at, float volume = 1f) {
            if (Muted || voices == null) return;
            var clip = Resolve(cardId, cue, out bool specific);
            if (clip == null) return;
            if (specific) CardCuesPlayed++; else GenericCuesPlayed++;
            LastCue = (specific ? cardId + "_" + cue : "generic_" + cue);
            AudioSource v;
            if (at.HasValue) { v = boardVoices[nextBoard++ % boardVoices.Length]; v.transform.position = at.Value; }
            else v = voices[next++ % voices.Length];
            v.pitch = specific ? Random.Range(.97f, 1.03f) : Random.Range(.94f, 1.06f);
            v.PlayOneShot(clip, Volume * volume * Random.Range(.9f, 1f));
        }
        public void PlayUi(string cue) {
            if (Muted || voices == null) return;
            var list = UiClips(cue);
            var clip = list.Count > 0 ? list[Random.Range(0, list.Count)] : Generic(cue, "ZEUS");
            LastCue = (list.Count > 0 ? "ui_" : "generic_") + cue;
            var v = voices[next++ % voices.Length]; v.pitch = Random.Range(.98f, 1.02f);
            v.PlayOneShot(clip, Volume * .6f);
        }
        // Inspector overrides first, then Resources/Sfx/ui_<cue>.wav and ui_<cue>_2.._4 variants.
        List<AudioClip> UiClips(string cue) {
            if (ui.TryGetValue(cue, out var list)) return list;
            list = new List<AudioClip>();
            foreach (var o in Overrides) if (o != null && o.cue == cue && o.clips != null) foreach (var c in o.clips) if (c != null) list.Add(c);
            if (list.Count == 0)
                for (int i = 1; i <= 4; i++) { var c = Resources.Load<AudioClip>("Sfx/ui_" + cue + (i == 1 ? "" : "_" + i)); if (c != null) list.Add(c); }
            ui[cue] = list; return list;
        }

        // --- synthesised generic cues (techno-myth: energy hums, zaps and servo clicks) ---
        AudioClip Generic(string cue, string faction) {
            string key = cue + ":" + faction;
            if (generic.TryGetValue(key, out var c)) return c;
            const int rate = 44100;
            float len; System.Func<float, float> f;
            float baseHz = faction == "POSEIDON" ? 180f : 260f;
            var rng = new System.Random(key.GetHashCode());
            float Noise() => (float)(rng.NextDouble() * 2 - 1);
            switch (cue) {
                case "deploy": len = .45f; f = t => Mathf.Sin(2 * Mathf.PI * (baseHz + 500 * t) * t) * Env(t, .01f, .45f) * .6f + Noise() * .08f * Env(t, .002f, .15f); break;
                case "move": len = .22f; f = t => (Mathf.Sin(2 * Mathf.PI * 90 * t) * .4f + Noise() * .25f) * Env(t, .01f, .22f); break;
                case "attack": len = .35f; f = t => Mathf.Sin(2 * Mathf.PI * (1400 - 3200 * t) * t) * Env(t, .003f, .35f) * .55f; break;
                case "hit": len = .25f; f = t => (Noise() * .7f + Mathf.Sin(2 * Mathf.PI * 70 * t) * .5f) * Env(t, .001f, .25f); break;
                case "destroy": len = .7f; f = t => (Noise() * .6f * Env(t, .002f, .7f) + Mathf.Sin(2 * Mathf.PI * (120 - 80 * t) * t) * .5f * Env(t, .01f, .7f)); break;
                case "ability": case "signature": len = .6f; f = t => (Mathf.Sin(2 * Mathf.PI * baseHz * 2 * t) + Mathf.Sin(2 * Mathf.PI * baseHz * 3.01f * t)) * .3f * Env(t, .05f, .6f); break;
                case "turn": len = .3f; f = t => Mathf.Sin(2 * Mathf.PI * (t < .12f ? 660 : 880) * t) * Env(t, .005f, .3f) * .35f; break;
                case "turn_end": len = .25f; f = t => Mathf.Sin(2 * Mathf.PI * (t < .1f ? 880 : 587) * t) * Env(t, .005f, .25f) * .28f; break;
                case "card": len = .2f; f = t => (Noise() * .35f * Mathf.Sin(Mathf.PI * t / .2f) + Mathf.Sin(2 * Mathf.PI * (300 + 1500 * t) * t) * .15f) * Env(t, .01f, .2f); break;
                case "victory": len = 1.1f; f = t => (Mathf.Sin(2 * Mathf.PI * (t < .25f ? 523 : t < .5f ? 659 : 784) * t) + Mathf.Sin(2 * Mathf.PI * 261.6f * t) * .5f) * Env(t, .01f, 1.1f) * .3f; break;
                case "defeat": len = 1.1f; f = t => (Mathf.Sin(2 * Mathf.PI * (t < .3f ? 392 : t < .6f ? 311 : 262) * t) + Mathf.Sin(2 * Mathf.PI * 98 * t) * .5f) * Env(t, .01f, 1.1f) * .3f; break;
                case "click": len = .05f; f = t => Mathf.Sin(2 * Mathf.PI * 1800 * t) * Env(t, .001f, .05f) * .3f; break;
                case "error": len = .25f; f = t => Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 140 * t)) * Env(t, .005f, .25f) * .2f; break;
                default: len = .3f; f = t => Mathf.Sin(2 * Mathf.PI * baseHz * t) * Env(t, .01f, .3f) * .4f; break;
            }
            int n = (int)(rate * len);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f((float)i / rate), -1, 1);
            c = AudioClip.Create("generic_" + key, n, 1, rate, false);
            c.SetData(data, 0);
            generic[key] = c; return c;
        }
        static float Env(float t, float attack, float length) {
            if (t < attack) return t / attack;
            return Mathf.Clamp01(1 - (t - attack) / (length - attack));
        }
    }
}
