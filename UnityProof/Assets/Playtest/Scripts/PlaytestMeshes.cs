using System.Collections.Generic;
using UnityEngine;
using InfiniteConquest.Proof;

namespace InfiniteConquest.Playtest {
    // Procedural, flat-shaded meshes for the board and the typed stand-ins. Pointy-top like BoardLayout.
    public static class PlaytestMeshes {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        // n-sided prism (or frustum) standing on y=0. radius = circumradius. phase 30 = pointy-top hex.
        public static Mesh Prism(int sides, float bottomRadius, float topRadius, float height, float phase = 30f) {
            string key = $"prism{sides}:{bottomRadius:F3}:{topRadius:F3}:{height:F3}:{phase}";
            if (cache.TryGetValue(key, out var m)) return m;
            var v = new List<Vector3>(); var t = new List<int>();
            Vector3 C(float r, int i, float y) { float a = Mathf.Deg2Rad * (360f / sides * i + phase); return new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a)); }
            // caps
            void Cap(float r, float y, bool up) {
                int c = v.Count; v.Add(new Vector3(0, y, 0));
                for (int i = 0; i < sides; i++) v.Add(C(r, i, y));
                for (int i = 0; i < sides; i++) {
                    int a = c + 1 + i, b = c + 1 + (i + 1) % sides;
                    if (up) { t.Add(c); t.Add(b); t.Add(a); } else { t.Add(c); t.Add(a); t.Add(b); }
                }
            }
            if (topRadius > 0.0001f) Cap(topRadius, height, true);
            Cap(bottomRadius, 0, false);
            for (int i = 0; i < sides; i++) {
                int c = v.Count; int j = (i + 1) % sides;
                if (topRadius > 0.0001f) {
                    v.Add(C(topRadius, i, height)); v.Add(C(topRadius, j, height)); v.Add(C(bottomRadius, j, 0)); v.Add(C(bottomRadius, i, 0));
                    t.Add(c); t.Add(c + 1); t.Add(c + 2); t.Add(c); t.Add(c + 2); t.Add(c + 3);
                } else {
                    v.Add(new Vector3(0, height, 0)); v.Add(C(bottomRadius, j, 0)); v.Add(C(bottomRadius, i, 0));
                    t.Add(c); t.Add(c + 1); t.Add(c + 2);
                }
            }
            m = Build(key, v, t); cache[key] = m; return m;
        }

        // Flat hexagonal ring (outline) at y, pointy-top, radii are across-the-flats sizes.
        public static Mesh HexRing(float outerFlats, float innerFlats) {
            string key = $"ring:{outerFlats:F3}:{innerFlats:F3}";
            if (cache.TryGetValue(key, out var m)) return m;
            float ro = outerFlats / Mathf.Sqrt(3f), ri = innerFlats / Mathf.Sqrt(3f);
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i < 6; i++) {
                float a = Mathf.Deg2Rad * (60 * i + 30), b = Mathf.Deg2Rad * (60 * (i + 1) + 30);
                int c = v.Count;
                v.Add(new Vector3(ro * Mathf.Cos(a), 0, ro * Mathf.Sin(a))); v.Add(new Vector3(ro * Mathf.Cos(b), 0, ro * Mathf.Sin(b)));
                v.Add(new Vector3(ri * Mathf.Cos(b), 0, ri * Mathf.Sin(b))); v.Add(new Vector3(ri * Mathf.Cos(a), 0, ri * Mathf.Sin(a)));
                t.Add(c); t.Add(c + 2); t.Add(c + 1); t.Add(c); t.Add(c + 3); t.Add(c + 2);
            }
            m = Build(key, v, t); cache[key] = m; return m;
        }

        // Hex slab with the flats measured like BoardLayout.TileSize.
        public static Mesh HexSlab(float flats, float height) => Prism(6, flats / Mathf.Sqrt(3f), flats / Mathf.Sqrt(3f) * .96f, height);

        public static Mesh Quad() {
            if (cache.TryGetValue("quad", out var m)) return m;
            var v = new List<Vector3> { new Vector3(-.5f, -.5f), new Vector3(.5f, -.5f), new Vector3(.5f, .5f), new Vector3(-.5f, .5f) };
            var t = new List<int> { 0, 2, 1, 0, 3, 2 };
            m = Build("quad", v, t); cache["quad"] = m; return m;
        }

        static Mesh Build(string name, List<Vector3> v, List<int> t) {
            // Unweld so RecalculateNormals gives a crisp, faceted look.
            var fv = new Vector3[t.Count]; var ft = new int[t.Count];
            for (int i = 0; i < t.Count; i++) { fv[i] = v[t[i]]; ft[i] = i; }
            var m = new Mesh { name = name }; m.vertices = fv; m.triangles = ft;
            m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }

        public static GameObject Make(string name, Mesh mesh, Material mat, Transform parent, Vector3 localPos) {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            go.transform.SetParent(parent, false); go.transform.localPosition = localPos;
            return go;
        }
    }
}
