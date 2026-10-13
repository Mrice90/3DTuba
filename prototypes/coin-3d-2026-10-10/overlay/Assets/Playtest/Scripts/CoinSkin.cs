using UnityEngine;

namespace InfiniteConquest.Playtest {
    // Swappable look for the starting-player coin (AI-080-COIN-3D-PRESENTATION). Presentation only:
    // which face lands up always comes from the rules engine's starting_player. Ownership/unlock of
    // skins (AI-109) is deliberately not modelled here.
    //
    // Swap without code: edit Resources/CoinSkins/Default.asset, or drop another CoinSkin asset in
    // Resources/CoinSkins/ and launch with -coinSkin <assetName>.
    [CreateAssetMenu(menuName = "Infinite Conquest/Coin Skin", fileName = "CoinSkin")]
    public sealed class CoinSkin : ScriptableObject {
        public const string ResourceFolder = "CoinSkins/";
        public const string DefaultName = "Default";

        public string displayName = "Default";
        [Tooltip("Seat 0 face (Zeus). Square, disc inscribed; UV v=1 is the top of the face.")]
        public Texture2D front;
        [Tooltip("Seat 1 face (Poseidon). Same layout as front.")]
        public Texture2D back;
        public Color faceTint = Color.white;
        [Range(0, 1)] public float faceMetallic = .35f, faceSmoothness = .6f;

        [Tooltip("Optional full override for the edge. When empty the rim is built from the fields below.")]
        public Material rimMaterial;
        [Tooltip("Strip wrapped around the edge: u = around the circumference, v = across the thickness.")]
        public Texture2D rimTexture;
        public Color rimColor = new Color(.85f, .7f, .3f);
        [Range(0, 1)] public float rimMetallic = .85f, rimSmoothness = .7f;

        [Tooltip("Glow pulsed on the winning face when the coin settles. Black disables it.")]
        [ColorUsage(false, true)] public Color emission = new Color(.25f, .2f, .05f);

        public static CoinSkin Load(string name) {
            var skin = string.IsNullOrEmpty(name) ? null : Resources.Load<CoinSkin>(ResourceFolder + name);
            return skin != null ? skin : Resources.Load<CoinSkin>(ResourceFolder + DefaultName);
        }
        public Texture2D FaceFor(int seat) => seat == 0 ? front : back;
    }
}
