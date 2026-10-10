using System;
using UnityEngine;

namespace InfiniteConquest.Playtest {
    // AI-094 coin cosmetic. A skin is two square face images plus a rim colour. The built-in skins live in
    // Resources/Coin/Skins as <id>_heads.png and <id>_tails.png; to add one, drop in two images with a new id
    // (paint them over docs/production/coin/coin_face_template.png) and call CoinFlip.ApplySkin(CoinSkin.Load(id)).
    // Heads is Player 1 (seat 0), tails is Player 2 (seat 1).
    [Serializable]
    public sealed class CoinSkin {
        public string Id = "olympus";
        public Texture2D Heads, Tails;
        public Color Rim = new Color(.85f, .62f, .22f);
        [Range(0, 1)] public float Metallic = .6f, Smoothness = .65f;

        public static readonly string[] BuiltIn = { "olympus", "bronze" };
        public const string Folder = "Coin/Skins/";

        // Rim colour per built-in skin; unknown ids keep the gold default.
        static Color RimFor(string id) => id == "bronze" ? new Color(.62f, .40f, .20f) : new Color(.85f, .62f, .22f);

        public static CoinSkin Load(string id) {
            var heads = Resources.Load<Texture2D>(Folder + id + "_heads");
            var tails = Resources.Load<Texture2D>(Folder + id + "_tails");
            if (heads == null || tails == null) { Debug.LogWarning("COIN skin '" + id + "' is missing a face image in Resources/" + Folder); return null; }
            return new CoinSkin { Id = id, Heads = heads, Tails = tails, Rim = RimFor(id) };
        }
    }
}
