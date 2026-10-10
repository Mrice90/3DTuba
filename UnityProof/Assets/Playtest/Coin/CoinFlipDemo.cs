using UnityEngine;

namespace InfiniteConquest.Playtest {
    // Stand-alone coin flip test (Assets/Playtest/Coin/CoinFlip.unity): flip for a random or forced result,
    // switch skins, and see the call-back "start" the winner's turn. Not used by the match.
    public sealed class CoinFlipDemo : MonoBehaviour {
        public CoinFlip Coin;
        int skin; string status = "Press Flip to decide who goes first.";
        public int Flips { get; private set; }

        void Start() {
            if (Coin == null) Coin = FindAnyObjectByType<CoinFlip>(FindObjectsInactive.Include);
            if (Coin != null) Coin.gameObject.SetActive(false);
        }

        void Flip(int result) {
            if (Coin == null || Coin.Busy) return;
            status = "Flipping…";
            StartCoroutine(Coin.Play(result, StartTurn));
        }

        void StartTurn(int seat) { Flips++; status = $"Player {seat + 1} ({(seat == 0 ? "heads" : "tails")}) goes first — their turn starts."; }

        void OnGUI() {
            var r = new Rect(16, 16, 150, 34);
            GUI.enabled = Coin != null && !Coin.Busy;
            if (GUI.Button(r, "Flip")) Flip(Random.Range(0, 2)); r.x += 158;
            if (GUI.Button(r, "Force heads (P1)")) Flip(0); r.x += 158;
            if (GUI.Button(r, "Force tails (P2)")) Flip(1); r.x += 158;
            if (GUI.Button(r, "Skin: " + CoinSkin.BuiltIn[skin])) { skin = (skin + 1) % CoinSkin.BuiltIn.Length; Coin.ApplySkin(CoinSkin.Load(CoinSkin.BuiltIn[skin])); }
            GUI.enabled = true;
            GUI.Label(new Rect(16, 58, 800, 30), "<size=18>" + status + "</size>");
        }
    }
}
