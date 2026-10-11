using UnityEngine;

namespace InfiniteConquest.Proof {
    // Single source for board geometry. The alpha plays on BoardGeometry.HEX: pointy-top hexes in
    // odd-row offset coordinates (odd rows shifted half a hex right). Everything that places tiles or
    // tokens in world space goes through here. Width/Height is the rules grid; the drawn board can be
    // larger (BoardConfig preview), with the rules grid centred inside it.
    public static class BoardLayout {
        public const int Width = BoardConfig.RulesWidth, Height = BoardConfig.RulesHeight;
        public const float Spacing = 1.3f;                   // centre-to-centre within a row
        public const float RowSpacing = Spacing * 0.8660254f; // sqrt(3)/2: rows of touching hexes
        public const float TileSize = 1.18f;                 // across the flats, leaving a small gap
        public const float TileThickness = .18f;
        public static float TileTop => TileThickness / 2f;

        // x, y are rules coordinates. Cells outside the rules grid (negative or past the edge) are the
        // drawn-only preview ring. Row parity stays tied to the rules row, so engine adjacency is unchanged
        // wherever the rules grid sits inside the drawn board.
        public static Vector3 CellCenter(int x, int y) {
            var o = BoardConfig.Offset;
            // The mean odd-row shift is a quarter hex, so subtract it to keep the board centred.
            float column = x + o.x + .5f * (y & 1) - (BoardConfig.Width - 1) / 2f - .25f;
            return new Vector3(column * Spacing, 0, (y + o.y - (BoardConfig.Height - 1) / 2f) * RowSpacing);
        }
        // Rules cell of a drawn cell.
        public static Vector2Int FromDrawn(int drawnX, int drawnY) => new Vector2Int(drawnX - BoardConfig.Offset.x, drawnY - BoardConfig.Offset.y);

        // Camera / plinth multiplier: drawn-board half-diagonal over the 4 x 6 board the framing was tuned on.
        public static float ViewScale => HalfDiagonal(BoardConfig.Width, BoardConfig.Height) / HalfDiagonal(4, 6);
        // Camera distances grow a little faster than the board, so a larger board still clears the side panels.
        public static float CameraScale => 1 + (ViewScale - 1) * 1.3f;
        static float HalfDiagonal(int w, int h) {
            float x = (w - .5f) * Spacing + TileSize, z = (h - 1) * RowSpacing + TileSize * 2f / Mathf.Sqrt(3f);
            return Mathf.Sqrt(x * x + z * z) / 2f;
        }

        static Mesh tileMesh;
        // Flat-shaded hexagonal prism, pointy-top, centred on the origin.
        public static Mesh TileMesh {
            get {
                if (tileMesh != null) return tileMesh;
                float r = TileSize / Mathf.Sqrt(3f), h = TileThickness / 2f;
                var corner = new Vector3[6];
                for (int i = 0; i < 6; i++) {
                    float a = Mathf.Deg2Rad * (60 * i + 30);
                    corner[i] = new Vector3(r * Mathf.Cos(a), 0, r * Mathf.Sin(a));
                }
                var v = new System.Collections.Generic.List<Vector3>();
                var t = new System.Collections.Generic.List<int>();
                void Fan(float y, bool up) {
                    int c = v.Count; v.Add(new Vector3(0, y, 0));
                    for (int i = 0; i < 6; i++) v.Add(corner[i] + Vector3.up * y);
                    for (int i = 0; i < 6; i++) {
                        int a = c + 1 + i, b = c + 1 + (i + 1) % 6;
                        if (up) { t.Add(c); t.Add(b); t.Add(a); } else { t.Add(c); t.Add(a); t.Add(b); }
                    }
                }
                Fan(h, true); Fan(-h, false);
                for (int i = 0; i < 6; i++) {
                    Vector3 p = corner[i], q = corner[(i + 1) % 6];
                    int c = v.Count;
                    v.Add(p + Vector3.up * h); v.Add(q + Vector3.up * h); v.Add(q - Vector3.up * h); v.Add(p - Vector3.up * h);
                    t.Add(c); t.Add(c + 1); t.Add(c + 2); t.Add(c); t.Add(c + 2); t.Add(c + 3);
                }
                tileMesh = new Mesh { name = "Hex tile" };
                tileMesh.SetVertices(v); tileMesh.SetTriangles(t, 0);
                tileMesh.RecalculateNormals(); tileMesh.RecalculateBounds();
                return tileMesh;
            }
        }

        public static GameObject CreateTile(int x, int y, Material material) {
            var tile = new GameObject($"Tile {x},{y}", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            tile.transform.position = CellCenter(x, y);
            tile.GetComponent<MeshFilter>().sharedMesh = TileMesh;
            tile.GetComponent<MeshRenderer>().sharedMaterial = material;
            var collider = tile.GetComponent<MeshCollider>();
            collider.sharedMesh = TileMesh; collider.convex = true;
            return tile;
        }
    }
}
