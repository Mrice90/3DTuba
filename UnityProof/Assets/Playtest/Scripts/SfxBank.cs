using System.Collections.Generic;
using UnityEngine;

namespace InfiniteConquest.Playtest {
    // Per-card SFX lookup: Resources/Sfx/<card_id>_<cue>.wav (copied from ElevenLabs picks/ by
    // stage_playtest_assets.py), falling back to a synthesised generic cue per cue name, so every
    // card makes a sound on deploy/move/attack/hit/destroy even before its picks exist.
    public sealed class SfxBank : MonoBehaviour {
        public static readonly string[] Cues = { "deploy", "move", "attack", "hit", "destroy", "ability", "signature", "idle" };
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, AudioClip> generic = new Dictionary<string, AudioClip>();
        AudioSource[] voices;
        int next;
        public float Volume = .8f;
        public bool Muted;
        public int CardCuesPlayed, GenericCuesPlayed;
        public string LastCue = "";

        void Awake() {
            voices = new AudioSource[8];
            for (int i = 0; i < voices.Length; i++) { voices[i] = gameObject.AddComponent<AudioSource>(); voices[i].playOnAwake = false; voices[i].spatialBlend = 0; }
        }

        // Resolves the clip that would play, and whether it is card-specific.
        public AudioClip Resolve(string cardId, string cue, out bool cardSpecific) {
            cardSpecific = false;
            var entry = PlaytestCatalog.Get(cardId);
            string want = cue;
            // apex signature falls back to the card's own deploy; death/summon naming per AI-077.
            if (!entry.HasCue(want) && want == "signature" && entry.HasCue("deploy")) want = "deploy";
            if (entry.HasCue(want)) {
                string key = cardId + "_" + want;
                if (!clips.TryGetValue(key, out var clip)) { clip = Resources.Load<AudioClip>("Sfx/" + key); clips[key] = clip; }
                if (clip != null) { cardSpecific = true; return clip; }
            }
            return Generic(cue, entry.faction);
        }

        public void Play(string cardId, string cue, float volume = 1f) {
            if (Muted || voices == null) return;
            var clip = Resolve(cardId, cue, out bool specific);
            if (clip == null) return;
            if (specific) CardCuesPlayed++; else GenericCuesPlayed++;
            LastCue = (specific ? cardId + "_" + cue : "generic_" + cue);
            var v = voices[next++ % voices.Length];
            v.pitch = specific ? 1f : Random.Range(.94f, 1.06f);
            v.PlayOneShot(clip, Volume * volume);
        }
        public void PlayUi(string cue) {
            if (Muted || voices == null) return;
            var v = voices[next++ % voices.Length]; v.pitch = 1f;
            v.PlayOneShot(Generic(cue, "ZEUS"), Volume * .6f);
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
