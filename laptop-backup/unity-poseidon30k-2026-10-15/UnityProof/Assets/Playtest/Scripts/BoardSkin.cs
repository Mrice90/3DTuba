using UnityEngine;

namespace InfiniteConquest.Playtest {
    [CreateAssetMenu(menuName = "Infinite Conquest/Board Side Skin", fileName = "BoardSkin")]
    public sealed class BoardSkin : ScriptableObject {
        public string displayName;
        public Color tileColor, checkerColor, groundColor, edgeColor;
        [Range(0, 1)] public float metallic = .3f, smoothness = .4f;
        public static readonly string[] Presets = { "Stormforge", "Tideglass", "Moonstone", "Obsidian" };
        public static string DefaultFor(int seat) => seat == 1 ? "Tideglass" : "Stormforge";
        public static BoardSkin Load(string name, int seat = 0) {
            var skin = string.IsNullOrEmpty(name) ? null : Resources.Load<BoardSkin>("BoardSkins/" + name);
            return skin != null ? skin : Resources.Load<BoardSkin>("BoardSkins/" + DefaultFor(seat));
        }
        public static string SelectedName(int seat) => PlayerPrefs.GetString("ic.boardSkin." + Mathf.Clamp(seat, 0, 1), DefaultFor(seat));
        public static BoardSkin Selected(int seat) => Load(MatchCosmetics.Resolve(seat,SelectedName(seat)), seat);
        public static void Select(int seat, string name) {
            if (seat < 0 || seat > 1) return;
            var skin = Load(name, seat);
            PlayerPrefs.SetString("ic.boardSkin." + seat, skin != null ? skin.name : DefaultFor(seat));
            PlayerPrefs.Save();
        }
    }
}
