using UnityEngine;

namespace InfiniteConquest.Proof {
    // Single source for board geometry. The alpha plays on BoardGeometry.HEX: 4 x 6 pointy-top hexes in
    // odd-row offset coordinates (odd rows shifted half a hex right). Everything that places tiles or
    // tokens in world space goes through here; the grid size comes from the rules model (MovementState).
    public static class BoardLayout {
        public const int Width = MovementState.Width, Height = MovementState.Height;
        public const float Spacing = 1.3f;                   // centre-to-centre within a row
        public const float RowSpacing = Spacing * 0.8660254f; // sqrt(3)/2: rows of touching hexes
        public const float TileSize = 1.18f;                 // across the flats, leaving a small gap
        public const float TileThickness = .18f;
        public static float TileTop => TileThickness / 2f;

        public static Vector3 CellCenter(int x, int y) {
            // The mean odd-row shift is a quarter hex, so subtract it to keep the board centred.
            float column = x + .5f * (y & 1) - (Width - 1) / 2f - .25f;
            return new Vector3(column * Spacing, 0, (y - (Height - 1) / 2f) * RowSpacing);
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
