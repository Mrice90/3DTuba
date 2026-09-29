using System.Collections.Generic;
using UnityEngine;
using InfiniteConquest.Proof;

namespace InfiniteConquest.Playtest {
    // The polished hex board: tiles (from BoardLayout), rim outlines, a plinth, hover highlight,
    // legal-target markers and per-hex stacks (land at the base, structures/characters on top of it).
    public sealed class BoardView : MonoBehaviour {
        public const int W = BoardLayout.Width, H = BoardLayout.Height;
        TokenFactory factory;
        readonly Renderer[,] tiles = new Renderer[W, H];
        readonly GameObject[,] markers = new GameObject[W, H];
        readonly GameObject[,] rims = new GameObject[W, H];
        readonly List<Piece>[,] stacks = new List<Piece>[W, H];
        Material[,] baseMats = new Material[W, H];
        Material hoverMat, selectMat, markerMat, markerAttackMat, rimMat, rimHoverMat;
        public Vector2Int Hover = new Vector2Int(-1, -1);
        public Vector2Int Selected = new Vector2Int(-1, -1);
        readonly HashSet<Vector2Int> legal = new HashSet<Vector2Int>();
        readonly HashSet<Vector2Int> attack = new HashSet<Vector2Int>();

        public void Build(TokenFactory f) {
            factory = f;
            var plinth = PlaytestMeshes.Make("Board plinth", PlaytestMeshes.Prism(6, 4.25f, 4.0f, .35f, 30), f.Lit(new Color(.06f, .08f, .12f), .6f, .7f), transform, new Vector3(0, -BoardLayout.TileThickness / 2 - .36f, 0));
            PlaytestMeshes.Make("Plinth glow", PlaytestMeshes.Prism(6, 4.28f, 4.28f, .03f, 30), f.Glow(new Color(.2f, .5f, .7f)), transform, new Vector3(0, -BoardLayout.TileThickness / 2 - .05f, 0));
            hoverMat = f.Lit(new Color(.28f, .42f, .58f), .6f, .3f);
            selectMat = f.Lit(new Color(.55f, .45f, .18f), .6f, .3f);
            markerMat = f.Glow(new Color(.25f, 1f, .85f));
            markerAttackMat = f.Glow(new Color(1f, .3f, .3f));
            rimMat = f.Glow(new Color(.16f, .26f, .36f));
            rimHoverMat = f.Glow(new Color(.9f, .95f, 1f));
            for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) {
                // Home rows carry a faint faction tint: Zeus rows 0-2 (near side), Poseidon rows 3-5.
                var home = y < H / 2 ? PlaytestCatalog.FactionColor("ZEUS") : PlaytestCatalog.FactionColor("POSEIDON");
                float checker = ((x + y) & 1) == 0 ? .0f : .025f;
                var c = Color.Lerp(new Color(.10f + checker, .15f + checker, .21f + checker), home, .09f);
                baseMats[x, y] = f.Lit(c, .55f, .25f);
                var tile = BoardLayout.CreateTile(x, y, baseMats[x, y]);
                tile.transform.SetParent(transform, true);
                var cell = tile.AddComponent<ProofCell>(); cell.X = x; cell.Y = y;
                tiles[x, y] = tile.GetComponent<Renderer>();
                rims[x, y] = PlaytestMeshes.Make("Rim", PlaytestMeshes.HexRing(BoardLayout.TileSize, BoardLayout.TileSize - .07f), rimMat, tile.transform, new Vector3(0, BoardLayout.TileTop + .002f, 0));
                var m = PlaytestMeshes.Make("Legal marker", PlaytestMeshes.HexRing(BoardLayout.TileSize * .82f, BoardLayout.TileSize * .68f), markerMat, tile.transform, new Vector3(0, BoardLayout.TileTop + .01f, 0));
                m.SetActive(false); markers[x, y] = m;
                stacks[x, y] = new List<Piece>();
            }
        }

        public static bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;

        public void SetLegal(IEnumerable<Vector2Int> moves, IEnumerable<Vector2Int> attacks = null) {
            legal.Clear(); attack.Clear();
            if (moves != null) foreach (var p in moves) if (InBounds(p.x, p.y)) legal.Add(p);
            if (attacks != null) foreach (var p in attacks) if (InBounds(p.x, p.y)) attack.Add(p);
            Refresh();
        }
        public bool IsLegal(Vector2Int p) => legal.Contains(p) || attack.Contains(p);
        public int LegalCount => legal.Count + attack.Count;

        void Update() {
            // Pulse the legal-target markers.
            float s = 1 + Mathf.Sin(Time.time * 5f) * .05f;
            foreach (var p in legal) markers[p.x, p.y].transform.localScale = new Vector3(s, 1, s);
            foreach (var p in attack) markers[p.x, p.y].transform.localScale = new Vector3(s, 1, s);
        }

        public void Refresh() {
            for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) {
                var p = new Vector2Int(x, y);
                bool hover = p == Hover, sel = p == Selected;
                tiles[x, y].sharedMaterial = sel ? selectMat : hover ? hoverMat : baseMats[x, y];
                rims[x, y].GetComponent<Renderer>().sharedMaterial = hover || sel ? rimHoverMat : rimMat;
                bool on = legal.Contains(p) || attack.Contains(p);
                markers[x, y].SetActive(on);
                if (on) markers[x, y].GetComponent<Renderer>().sharedMaterial = attack.Contains(p) ? markerAttackMat : markerMat;
            }
        }

        // --- stacks ---
        public List<Piece> StackAt(int x, int y) => InBounds(x, y) ? stacks[x, y] : new List<Piece>();

        public void Add(Piece p, int x, int y, bool snap = true) {
            Remove(p);
            if (!InBounds(x, y)) return;
            p.X = x; p.Y = y; stacks[x, y].Add(p);
            Relayout(x, y, snap);
        }
        public void Remove(Piece p) {
            if (p.X >= 0 && InBounds(p.X, p.Y)) { stacks[p.X, p.Y].Remove(p); Relayout(p.X, p.Y, true); }
        }

        public float LandTop(int x, int y) {
            float top = BoardLayout.TileTop;
            foreach (var q in stacks[x, y]) if (q.Card.type == "LAND") top = Mathf.Max(top, BoardLayout.TileTop + Mathf.Clamp(q.Height, .06f, .25f * PlaytestCatalog.ContractUnit));
            return top;
        }

        // Where a piece should stand inside its hex, given everything else stacked there.
        public Vector3 SlotPosition(Piece p) {
            int x = p.X, y = p.Y;
            var center = BoardLayout.CellCenter(x, y);
            if (p.Card.type == "LAND") {
                int li = 0; foreach (var q in stacks[x, y]) { if (q == p) break; if (q.Card.type == "LAND") li++; }
                return center + Vector3.up * (BoardLayout.TileTop + li * .02f);
            }
            float top = LandTop(x, y);
            var upright = new List<Piece>();
            foreach (var q in stacks[x, y]) if (q.Card.type != "LAND") upright.Add(q);
            int n = upright.Count, i = upright.IndexOf(p);
            if (n <= 1) return center + Vector3.up * top;
            // Structures and capitals hold the centre; characters ring around them.
            bool anchor = p.Card.type == "STRUCTURE" || p.Card.type == "CAPITAL";
            int anchors = 0; foreach (var q in upright) if (q.Card.type == "STRUCTURE" || q.Card.type == "CAPITAL") anchors++;
            if (anchor && anchors == 1) return center + Vector3.up * top;
            int ringCount = n - (anchors == 1 ? 1 : 0), ringIndex = 0;
            foreach (var q in upright) { if (q == p) break; if (!(anchors == 1 && (q.Card.type == "STRUCTURE" || q.Card.type == "CAPITAL"))) ringIndex++; }
            float r = anchors == 1 ? .36f : .24f;
            float a = Mathf.Deg2Rad * (210 + 360f / ringCount * ringIndex);
            return center + new Vector3(Mathf.Cos(a) * r, top, Mathf.Sin(a) * r);
        }
        public float SlotScale(Piece p) {
            if (p.Card.type == "LAND") return 1;
            int n = 0; foreach (var q in stacks[p.X, p.Y]) if (q.Card.type != "LAND") n++;
            return n <= 1 ? 1 : n == 2 ? .78f : .64f;
        }

        public void Relayout(int x, int y, bool snap) {
            if (!InBounds(x, y)) return;
            foreach (var q in stacks[x, y]) {
                if (q == null) continue;
                var tr = q.transform;
                if (snap) { tr.position = SlotPosition(q); tr.localScale = Vector3.one * SlotScale(q); }
                else { q.StartCoroutine(Tween.Move(tr, SlotPosition(q), .25f)); tr.localScale = Vector3.one * SlotScale(q); }
            }
        }

        public void ClearPieces() {
            for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) {
                foreach (var p in stacks[x, y]) if (p != null) Destroy(p.gameObject);
                stacks[x, y].Clear();
            }
        }
        public IEnumerable<Piece> AllPieces() {
            for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) foreach (var p in stacks[x, y]) yield return p;
        }
    }
}
