using UnityEngine;

namespace InfiniteConquest.Proof {
    // Single source for board geometry. The alpha plays on BoardGeometry.HEX: pointy-top hexes in
    // odd-row offset coordinates (odd rows shifted half a hex right). Everything that places tiles or
    // tokens in world space goes through here. Width/Height is the rules grid; the drawn board can be
    // larger or another shape (BoardConfig preview), with the rules grid inside it.
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
        // The drawn board's centroid sits on the origin (for a 4 x 6 rectangle that is exactly the old formula:
        // the mean odd-row shift is a quarter hex).
        public static Vector3 CellCenter(int x, int y) {
            var c = BoardConfig.RawCentroid;
            return new Vector3((x + .5f * (y & 1) - c.x) * Spacing, 0, (y - c.y) * RowSpacing);
        }

        // Camera / plinth multiplier: drawn-board half-diagonal (centre extents plus one tile) over the 4 x 6
        // board the framing was tuned on.
        public static float ViewScale => HalfDiagonal() / HalfDiagonal(4, 6);
        // Camera distances grow faster than the board, so a larger board clears the side panels and top bar.
        public static float CameraScale => 1 + (ViewScale - 1) * 1.7f;
        static float HalfDiagonal(int w, int h) {
            float x = (w - .5f) * Spacing + TileSize, z = (h - 1) * RowSpacing + TileSize * 2f / Mathf.Sqrt(3f);
            return Mathf.Sqrt(x * x + z * z) / 2f;
        }
        static float HalfDiagonal() {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var c in BoardConfig.DrawnCells) {
                var p = CellCenter(c.x, c.y);
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z);
            }
            float x = maxX - minX + TileSize, z = maxZ - minZ + TileSize * 2f / Mathf.Sqrt(3f);
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
