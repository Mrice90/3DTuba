using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using InfiniteConquest.Proof;

namespace InfiniteConquest.Playtest {
    // AI-094: the turn-order coin flip at match start. The rules engine already flips for the first player
    // (MATCH_STARTED detail "coin flip: Player 2 starts"), so the 3D coin shows that result instead of rolling
    // its own; the match then carries on into the winner's TURN_STARTED. Hooked from Apply's MATCH_STARTED case.
    public sealed partial class PlaytestGame {
        CoinFlip coin;
        public CoinFlip Coin => coin;
        public int CoinFlipsPlayed { get; private set; }

        static readonly Regex StarterPattern = new Regex(@"Player\s*([12])\s*starts", RegexOptions.IgnoreCase);

        // Seat that starts, from the engine's MATCH_STARTED detail; falls back to the event's player.
        public static int StarterFromDetail(string detail, int fallback) {
            var m = detail != null ? StarterPattern.Match(detail) : Match.Empty;
            return m.Success ? int.Parse(m.Groups[1].Value) - 1 : Mathf.Clamp(fallback, 0, 1);
        }

        static Vector3 BoardCentre() =>
            (BoardLayout.CellCenter(0, 0) + BoardLayout.CellCenter(BoardLayout.Width - 1, BoardLayout.Height - 1)) / 2f + Vector3.up * BoardLayout.TileTop;

        IEnumerator FlipForFirstPlayer(WireEvent e) {
            if (smoke || speed >= 50) yield break;   // smoke runs and fast playback skip the show
            int first = StarterFromDetail(e.detail, e.player);
            if (coin == null) coin = CoinFlip.Create(transform, LitShader != null ? LitShader : Shader.Find("Universal Render Pipeline/Lit"), PlayerPrefs.GetString("ic.coin", "olympus"));
            coin.transform.position = BoardCentre();
            Banner("Match start — flipping for the first turn", 3f);   // replaces the banner that names the result
            yield return coin.Play(first, seat => {
                CoinFlipsPlayed++;
                Banner($"Coin flip — {Faction(seat)} goes first", 2f);
                model.AddLog($"Coin flip: {Faction(seat)} (Player {seat + 1}) goes first");
            });
        }
    }
}
