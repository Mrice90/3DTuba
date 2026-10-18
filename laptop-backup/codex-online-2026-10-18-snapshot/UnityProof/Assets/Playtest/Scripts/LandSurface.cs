using UnityEngine;
using InfiniteConquest.Proof;

namespace InfiniteConquest.Playtest {
    // Touching pointy-top footprints; owner-facing rotations are for upright units, never the ground.
    public sealed class LandSurface : MonoBehaviour {
        public const float Flats = BoardLayout.Spacing;
        public static float GroundHeight => .25f * PlaytestCatalog.ContractUnit;
        Renderer surface;
        readonly GameObject[] edges = new GameObject[6];
        TokenFactory factory;
        static readonly Mesh[] edgeMeshes = new Mesh[6];
        public int HiddenEdgeMask { get; private set; }
        public float SurfaceHeight { get; private set; }
        public string AppliedSkinName { get; private set; }
        public void Build(TokenFactory f, bool imported, int owner) {
            factory = f; SurfaceHeight = imported ? .045f : GroundHeight;
            surface = PlaytestMeshes.Make("Connected ground", PlaytestMeshes.Prism(6, Flats / Mathf.Sqrt(3f), Flats / Mathf.Sqrt(3f), SurfaceHeight),
                f.Lit(new Color(.18f, .22f, .25f)), transform, Vector3.zero).GetComponent<Renderer>();
            for (int i = 0; i < 6; i++) edges[i] = PlaytestMeshes.Make("Ground edge " + i, EdgeMesh(i),
                f.Glow(Color.gray), transform, Vector3.up * (SurfaceHeight + .001f));
            ApplySkin(BoardSkin.Selected(owner));
        }
        public void ApplySkin(BoardSkin skin) {
            if (skin == null || factory == null) return;
            AppliedSkinName=skin.name;
            surface.sharedMaterial = factory.Lit(skin.groundColor, skin.smoothness, skin.metallic);
            foreach (var edge in edges) if (edge != null) edge.GetComponent<Renderer>().sharedMaterial = factory.Glow(skin.edgeColor * .45f);
        }
        public void SetConnections(int mask) {
            HiddenEdgeMask = mask;
            for (int i = 0; i < 6; i++) if (edges[i] != null) edges[i].SetActive((mask & (1 << i)) == 0);
        }
        public static Vector3 Corner(int i, float flats = Flats) {
            float a = Mathf.Deg2Rad * (30 + 60 * i);
            return new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (flats / Mathf.Sqrt(3f));
        }
        public static Vector3 EdgeNormal(int i) {
            float a = Mathf.Deg2Rad * (60 + 60 * i);
            return new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
        }
        static Mesh EdgeMesh(int i) {
            if (edgeMeshes[i] != null) return edgeMeshes[i];
            var mesh = new Mesh { name = "Ground boundary " + i };
            mesh.vertices = new[] { Corner(i), Corner(i + 1), Corner(i + 1, Flats - .015f), Corner(i, Flats - .015f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            edgeMeshes[i] = mesh; return mesh;
        }
    }
}
