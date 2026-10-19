using UnityEngine;

namespace InfiniteConquest.Playtest {
    // Idle glow for the coin/hex cosmetic sets (staging proposal, not yet in UnityProof). Put it on a coin or a tile
    // whose material has the set's Emission map. It breathes the emission, adds random lightning flickers when
    // Flicker is on (celestial), and Flash() gives a burst for the coin landing: wire CoinFlip.OnLand to it.
    // Works with Built-in Standard and URP Lit (both read _EmissionColor). Uses a MaterialPropertyBlock, so tiles
    // that share one material keep sharing it.
    public sealed class CosmeticGlow : MonoBehaviour {
        public Renderer Target;
        [ColorUsage(false, true)] public Color Emission = Color.white * 2.5f; // HDR tint times the Emission map
        public float PulseSeconds = 2.4f, PulseDepth = .22f;
        public bool Flicker;                                // celestial: short random arcs
        public float FlickerEvery = 1.6f, FlickerBoost = .9f;
        public float FlashBoost = 1.8f, FlashDecay = .18f;
        [Tooltip("Random per-object phase so a board of tiles does not pulse in lockstep.")] public bool RandomPhase = true;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock block;
        static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        float phase, nextArc, arcStart = -10f, flashStart = -10f;
        readonly System.Collections.Generic.List<int> slots = new System.Collections.Generic.List<int>();

        // Recommended emission per set (HDR intensity), matching the preview renders.
        public static Color For(string setId) {
            switch (setId) {
                case "celestial": return new Color(.75f, .85f, 1f) * 2.5f;
                case "abyssal": return new Color(.8f, 1f, 1f) * 2.2f;
                case "obsidian": return new Color(1f, .85f, .75f) * 3f;
                default: return Color.white * 2f;
            }
        }

        void Awake() {
            if (Target == null) Target = GetComponentInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            phase = RandomPhase ? Random.value * PulseSeconds : 0f;
            nextArc = Time.time + Random.Range(.3f, FlickerEvery);
            if (Target == null) return;
            // Only slots with an Emission map glow (the coin's plain Coin_Rim slot stays dark).
            var mats = Target.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) {
                if (mats[i] == null || !mats[i].HasProperty(EmissionMapId) || mats[i].GetTexture(EmissionMapId) == null) continue;
                mats[i].EnableKeyword("_EMISSION");
                slots.Add(i);
            }
        }

        public void Flash() { flashStart = Time.time; }

        void Update() {
            if (Target == null) return;
            float t = Time.time;
            float g = 1f - PulseDepth + PulseDepth * Mathf.Sin(2f * Mathf.PI * (t + phase) / PulseSeconds);
            if (Flicker) {
                if (t >= nextArc) { arcStart = t; nextArc = t + Random.Range(FlickerEvery * .5f, FlickerEvery * 1.5f); }
                float d = t - arcStart;
                if (d < .12f) g += FlickerBoost * (1f - d / .12f);
            }
            float f = t - flashStart;
            if (f >= 0f) g += FlashBoost * Mathf.Exp(-f / FlashDecay);
            foreach (int i in slots) {
                Target.GetPropertyBlock(block, i);
                block.SetColor(EmissionId, Emission * g);
                Target.SetPropertyBlock(block, i);
            }
        }
    }
}
