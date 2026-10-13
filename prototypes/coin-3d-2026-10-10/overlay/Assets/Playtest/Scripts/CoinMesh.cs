using System.Collections.Generic;
using UnityEngine;

namespace InfiniteConquest.Playtest {
    // Placeholder coin: pivot at the centre, local +Y = front (seat 0) face normal, -Y = back face,
    // flip axis = local X. Submesh 0 front face, 1 back face, 2 rim (edge band plus both bevels).
    public static class CoinMesh {
        public const int Front = 0, Back = 1, Rim = 2;

        public static Mesh Build(float radius = .5f, float thickness = .08f, float bevel = .018f, int segments = 72) {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>();
            var front = new List<int>(); var back = new List<int>(); var rim = new List<int>();
            float h = thickness / 2, rf = radius - bevel;

            // Faces: planar UV over the face disc. Back v is mirrored so its art reads upright once
            // the coin has turned 180 degrees about X.
            for (int side = 0; side < 2; side++) {
                float y = side == 0 ? h : -h; var normal = side == 0 ? Vector3.up : Vector3.down;
                int centre = v.Count; v.Add(new Vector3(0, y, 0)); n.Add(normal); uv.Add(new Vector2(.5f, .5f));
                for (int i = 0; i <= segments; i++) {
                    float a = 2 * Mathf.PI * i / segments, x = Mathf.Cos(a), z = Mathf.Sin(a);
                    v.Add(new Vector3(x * rf, y, z * rf)); n.Add(normal);
                    uv.Add(new Vector2(x * .5f + .5f, (side == 0 ? z : -z) * .5f + .5f));
                }
                var tris = side == 0 ? front : back;
                for (int i = 0; i < segments; i++) {
                    if (side == 0) { tris.Add(centre); tris.Add(centre + i + 2); tris.Add(centre + i + 1); }
                    else { tris.Add(centre); tris.Add(centre + i + 1); tris.Add(centre + i + 2); }
                }
            }
            // Rim: top bevel, edge band, bottom bevel. Each strip goes from an "upper" ring to a
            // "lower" ring as seen from outside; v runs 1 -> 0 down the whole edge.
            float band = thickness - 2 * bevel, total = band + 2 * bevel * 1.4142f;
            float vTop = 1, vBandTop = 1 - bevel * 1.4142f / total, vBandBottom = vBandTop - band / total;
            Strip(v, n, uv, rim, segments, rf, h, radius, h - bevel, vTop, vBandTop, new Vector2(.7071f, .7071f), new Vector2(.7071f, .7071f));
            Strip(v, n, uv, rim, segments, radius, h - bevel, radius, -h + bevel, vBandTop, vBandBottom, new Vector2(1, 0), new Vector2(1, 0));
            Strip(v, n, uv, rim, segments, radius, -h + bevel, rf, -h, vBandBottom, 0, new Vector2(.7071f, -.7071f), new Vector2(.7071f, -.7071f));

            var mesh = new Mesh { name = "Coin (placeholder)" };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(front, Front); mesh.SetTriangles(back, Back); mesh.SetTriangles(rim, Rim);
            mesh.RecalculateTangents(); mesh.RecalculateBounds();
            return mesh;
        }

        // Ring at (r0, y0) above ring at (r1, y1); normals given as (radial, vertical).
        static void Strip(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tris, int segments,
                          float r0, float y0, float r1, float y1, float v0, float v1, Vector2 n0, Vector2 n1) {
            int start = v.Count;
            for (int i = 0; i <= segments; i++) {
                float a = 2 * Mathf.PI * i / segments, x = Mathf.Cos(a), z = Mathf.Sin(a), u = (float)i / segments;
                v.Add(new Vector3(x * r0, y0, z * r0)); n.Add(new Vector3(x * n0.x, n0.y, z * n0.x)); uv.Add(new Vector2(u, v0));
                v.Add(new Vector3(x * r1, y1, z * r1)); n.Add(new Vector3(x * n1.x, n1.y, z * n1.x)); uv.Add(new Vector2(u, v1));
            }
            for (int i = 0; i < segments; i++) {
                int t0 = start + i * 2, b0 = t0 + 1, t1 = t0 + 2, b1 = t0 + 3;
                tris.Add(t0); tris.Add(t1); tris.Add(b1);
                tris.Add(t0); tris.Add(b1); tris.Add(b0);
            }
        }
    }
}
