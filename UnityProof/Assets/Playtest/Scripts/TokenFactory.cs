using System.Collections.Generic;
using UnityEngine;
using InfiniteConquest.Proof;

namespace InfiniteConquest.Playtest {
    // Builds a board piece for any card: the staged Meshy model at its AI-063 budget when one exists,
    // otherwise a typed stand-in (LAND slab, STRUCTURE prism tower, CHARACTER robot capsule + name plate,
    // CAPITAL spire). SPELL has no board presence; it is a VFX burst (see Vfx.SpellBurst).
    public sealed class TokenFactory {
        readonly Shader lit, unlit;
        readonly Font font;
        readonly Material fontMaterial;
        readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        public bool ForceStandIns;

        public TokenFactory(Shader lit, Shader unlit, Font font) {
            this.lit = lit; this.unlit = unlit; this.font = font;
            fontMaterial = font != null ? font.material : null;
        }

        public Material Lit(Color c, float smooth = .35f, float metal = .2f) {
            string key = "L" + ColorUtility.ToHtmlStringRGBA(c) + smooth + metal;
            if (mats.TryGetValue(key, out var m)) return m;
            m = new Material(lit); m.SetColor("_BaseColor", c); m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            mats[key] = m; return m;
        }
        public Material Glow(Color c) {
            string key = "U" + ColorUtility.ToHtmlStringRGBA(c);
            if (mats.TryGetValue(key, out var m)) return m;
            m = new Material(unlit); m.SetColor("_BaseColor", c); m.color = c;
            mats[key] = m; return m;
        }

        public GameObject Create(CardEntry card, int owner, out bool real) {
            var root = new GameObject(card.id);
            var piece = root.AddComponent<Piece>();
            piece.Card = card; piece.Owner = owner;
            real = false;
            if (!ForceStandIns && card.HasModel && card.type != "SPELL") real = TryModel(card, root.transform);
            if (!real) StandIn(card, root.transform);
            piece.IsRealModel = real;
            if (card.type == "CHARACTER" || card.type == "STRUCTURE" || card.type == "CAPITAL") NamePlate(card, root.transform, piece);
            // Every piece carries a thin base ring in its owner's faction colour so ownership reads at a glance.
            var ring = PlaytestMeshes.Make("Owner ring", PlaytestMeshes.HexRing(.62f, .5f), Glow(PlaytestCatalog.FactionColor(card.faction) * .9f), root.transform, new Vector3(0, .006f, 0));
            ring.SetActive(card.type != "LAND");
            piece.OwnerRing = ring;
            // Collider for clicking pieces.
            var col = root.AddComponent<CapsuleCollider>();
            var b = Bounds(root);
            col.center = root.transform.InverseTransformPoint(b.center); col.height = Mathf.Max(.2f, b.size.y); col.radius = Mathf.Max(.2f, Mathf.Min(b.size.x, b.size.z) * .4f);
            piece.Height = b.size.y;
            return root;
        }

        bool TryModel(CardEntry card, Transform root) {
            var prefab = Resources.Load<GameObject>(card.model);
            if (prefab == null) return false;
            var model = Object.Instantiate(prefab, root, false);
            model.name = "Model";
            var tokenMat = Resources.Load<Material>(System.IO.Path.GetDirectoryName(card.model).Replace('\\', '/') + "/Token");
            var fallback = Lit(Color.Lerp(PlaytestCatalog.FactionColor(card.faction), new Color(.75f, .75f, .78f), .55f), .45f, .35f);
            foreach (var r in model.GetComponentsInChildren<Renderer>()) {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = tokenMat != null ? tokenMat : fallback;
                r.sharedMaterials = slots;
            }
            // AI-063 budget: scale uniformly to the type's height, capped by its hex footprint.
            model.transform.localScale = Vector3.one;
            var b = Bounds(model);
            PlaytestCatalog.Budget(card.type, out float height, out float footprint);
            float s = Mathf.Min(height / Mathf.Max(.001f, b.size.y), footprint / Mathf.Max(.001f, Mathf.Max(b.size.x, b.size.z)));
            model.transform.localScale = Vector3.one * s;
            b = Bounds(model);
            model.transform.position += new Vector3(root.position.x - b.center.x, root.position.y - b.min.y, root.position.z - b.center.z);
            return true;
        }

        void StandIn(CardEntry card, Transform root) {
            Color fc = PlaytestCatalog.FactionColor(card.faction), acc = PlaytestCatalog.FactionAccent(card.faction), dark = PlaytestCatalog.FactionDark(card.faction);
            float hex = BoardLayout.TileSize, cu = PlaytestCatalog.ContractUnit;
            int h = Mathf.Abs(card.id.GetHashCode());
            switch (card.type) {
                case "LAND": {
                    // Flat hex slab tinted by faction and a terrain hue guessed from the card name.
                    var terrain = TerrainColor(card, h);
                    PlaytestMeshes.Make("Slab", PlaytestMeshes.HexSlab(hex * .98f, .2f * cu), Lit(terrain, .25f, .05f), root, Vector3.zero);
                    PlaytestMeshes.Make("Rim", PlaytestMeshes.HexRing(hex * .98f, hex * .88f), Glow(fc * .75f), root, new Vector3(0, .2f * cu + .004f, 0));
                    // A few terrain studs so lands read as ground, not a flat tint.
                    var rng = new System.Random(h);
                    for (int i = 0; i < 3; i++) {
                        float a = (float)rng.NextDouble() * 6.28f, r = .15f + (float)rng.NextDouble() * .25f;
                        var stud = PlaytestMeshes.Make("Stud", PlaytestMeshes.Prism(5, .06f + (float)rng.NextDouble() * .05f, .02f, .05f + (float)rng.NextDouble() * .06f, 0), Lit(Color.Lerp(terrain, dark, .4f), .2f, 0), root, new Vector3(Mathf.Cos(a) * r, .2f * cu, Mathf.Sin(a) * r));
                        stud.transform.localRotation = Quaternion.Euler(0, a * 57f, 0);
                    }
                    break;
                }
                case "STRUCTURE": {
                    float H = 1.3f * cu, R = hex * .9f / 2f * .72f;
                    PlaytestMeshes.Make("Base", PlaytestMeshes.Prism(6, R, R * .92f, .12f), Lit(dark, .3f, .5f), root, Vector3.zero);
                    PlaytestMeshes.Make("Tower", PlaytestMeshes.Prism(6, R * .78f, R * .55f, H * .75f, h % 2 == 0 ? 30 : 0), Lit(Color.Lerp(fc, new Color(.6f, .62f, .68f), .55f), .5f, .6f), root, new Vector3(0, .12f, 0));
                    PlaytestMeshes.Make("Core band", PlaytestMeshes.Prism(6, R * .62f, R * .6f, .06f), Glow(acc), root, new Vector3(0, .12f + H * .45f, 0));
                    PlaytestMeshes.Make("Cap", PlaytestMeshes.Prism(6, R * .6f, 0, H * .25f), Lit(fc, .6f, .7f), root, new Vector3(0, .12f + H * .75f, 0));
                    break;
                }
                case "CAPITAL": {
                    float H = 2.1f * cu, R = hex / 2f * .9f;
                    PlaytestMeshes.Make("Plinth", PlaytestMeshes.Prism(6, R, R * .9f, .14f), Lit(dark, .3f, .6f), root, Vector3.zero);
                    PlaytestMeshes.Make("Keep", PlaytestMeshes.Prism(6, R * .7f, R * .5f, H * .45f), Lit(Color.Lerp(fc, Color.white, .35f), .55f, .6f), root, new Vector3(0, .14f, 0));
                    PlaytestMeshes.Make("Ring", PlaytestMeshes.Prism(6, R * .56f, R * .56f, .05f), Glow(acc), root, new Vector3(0, .14f + H * .45f, 0));
                    PlaytestMeshes.Make("Spire", PlaytestMeshes.Prism(6, R * .38f, .02f, H * .55f), Lit(fc, .7f, .8f), root, new Vector3(0, .19f + H * .45f, 0));
                    for (int i = 0; i < 3; i++) {
                        float a = Mathf.Deg2Rad * (120 * i + 90);
                        PlaytestMeshes.Make("Pylon", PlaytestMeshes.Prism(4, .07f, .02f, H * .4f, 45), Lit(fc, .6f, .8f), root, new Vector3(Mathf.Cos(a) * R * .8f, .14f, Mathf.Sin(a) * R * .8f));
                    }
                    break;
                }
                default: { // CHARACTER (and unknown): capsule robot with visor and shoulder pods
                    float H = 1.5f * cu;
                    var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    Object.Destroy(body.GetComponent<Collider>());
                    body.name = "Body"; body.transform.SetParent(root, false);
                    body.transform.localScale = new Vector3(H * .42f, H * .38f, H * .36f);
                    body.transform.localPosition = new Vector3(0, H * .38f, 0);
                    body.GetComponent<Renderer>().sharedMaterial = Lit(Color.Lerp(fc, new Color(.55f, .57f, .62f), .35f), .55f, .6f);
                    var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Object.Destroy(head.GetComponent<Collider>());
                    head.name = "Head"; head.transform.SetParent(root, false);
                    head.transform.localScale = Vector3.one * H * .3f; head.transform.localPosition = new Vector3(0, H * .9f, 0);
                    head.GetComponent<Renderer>().sharedMaterial = Lit(new Color(.8f, .82f, .86f), .7f, .8f);
                    var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.Destroy(visor.GetComponent<Collider>());
                    visor.name = "Visor"; visor.transform.SetParent(root, false);
                    visor.transform.localScale = new Vector3(H * .24f, H * .06f, H * .08f); visor.transform.localPosition = new Vector3(0, H * .92f, H * .12f);
                    visor.GetComponent<Renderer>().sharedMaterial = Glow(acc);
                    for (int s = -1; s <= 1; s += 2) {
                        var pod = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        Object.Destroy(pod.GetComponent<Collider>());
                        pod.name = "Shoulder"; pod.transform.SetParent(root, false);
                        pod.transform.localScale = new Vector3(H * .12f, H * .12f, H * .2f); pod.transform.localPosition = new Vector3(s * H * .25f, H * .66f, 0);
                        pod.GetComponent<Renderer>().sharedMaterial = Lit(fc, .6f, .7f);
                    }
                    // energy lance (no bows: techno-myth art direction)
                    var lance = PlaytestMeshes.Make("Lance", PlaytestMeshes.Prism(4, .02f, .02f, H * .7f, 45), Glow(acc), root, new Vector3(H * .33f, H * .25f, H * .05f));
                    lance.transform.localRotation = Quaternion.Euler(12, 0, 0);
                    PlaytestMeshes.Make("Base disc", PlaytestMeshes.Prism(12, .2f, .19f, .04f, 0), Lit(dark, .3f, .5f), root, Vector3.zero);
                    break;
                }
            }
        }

        static Color TerrainColor(CardEntry card, int hash) {
            string n = (card.name + " " + card.id).ToLowerInvariant();
            Color c;
            if (n.Contains("reef") || n.Contains("coral")) c = new Color(.85f, .45f, .5f);
            else if (n.Contains("trench") || n.Contains("abyss") || n.Contains("deep")) c = new Color(.1f, .18f, .38f);
            else if (n.Contains("tide") || n.Contains("sea") || n.Contains("shoal") || n.Contains("lagoon") || n.Contains("current") || n.Contains("wave") || n.Contains("harbor") || n.Contains("water")) c = new Color(.15f, .45f, .6f);
            else if (n.Contains("storm") || n.Contains("cloud") || n.Contains("sky") || n.Contains("thunder")) c = new Color(.45f, .45f, .6f);
            else if (n.Contains("peak") || n.Contains("mount") || n.Contains("cliff") || n.Contains("plateau") || n.Contains("ridge") || n.Contains("olymp")) c = new Color(.55f, .5f, .45f);
            else if (n.Contains("grove") || n.Contains("field") || n.Contains("meadow") || n.Contains("garden")) c = new Color(.35f, .55f, .3f);
            else c = card.faction == "POSEIDON" ? new Color(.2f, .5f, .55f) : new Color(.6f, .52f, .35f);
            float jitter = ((hash % 17) - 8) / 100f;
            return Color.Lerp(new Color(c.r + jitter, c.g + jitter, c.b + jitter), PlaytestCatalog.FactionColor(card.faction), .18f);
        }

        void NamePlate(CardEntry card, Transform root, Piece piece) {
            if (font == null) return;
            var b = Bounds(root.gameObject);
            var plate = new GameObject("Name plate");
            plate.transform.SetParent(root, false);
            plate.transform.position = new Vector3(root.position.x, b.max.y + .12f, root.position.z);
            var bg = PlaytestMeshes.Make("Plate", PlaytestMeshes.Quad(), Glow(PlaytestCatalog.FactionDark(card.faction)), plate.transform, new Vector3(0, 0, .005f));
            var text = new GameObject("Text", typeof(MeshRenderer), typeof(TextMesh));
            text.transform.SetParent(plate.transform, false);
            var tm = text.GetComponent<TextMesh>();
            tm.font = font; tm.text = card.name; tm.fontSize = 48; tm.characterSize = .015f;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
            tm.color = Color.Lerp(PlaytestCatalog.FactionAccent(card.faction), Color.white, .5f);
            text.GetComponent<MeshRenderer>().sharedMaterial = fontMaterial;
            float w = Mathf.Max(.45f, card.name.Length * .064f);
            bg.transform.localScale = new Vector3(w, .16f, 1);
            // a faction stripe under the plate
            var stripe = PlaytestMeshes.Make("Stripe", PlaytestMeshes.Quad(), Glow(PlaytestCatalog.FactionColor(card.faction)), plate.transform, new Vector3(0, -.09f, .004f));
            stripe.transform.localScale = new Vector3(w, .02f, 1);
            plate.AddComponent<Billboard>();
            piece.Plate = plate;
        }

        public static Bounds Bounds(GameObject go) {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * .2f);
            Bounds b = default; bool first = true;
            foreach (var r in rs) {
                if (r is ParticleSystemRenderer || r.GetComponent<TextMesh>() != null) continue;
                if (r.transform.parent != null && r.transform.parent.name == "Name plate") continue;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            return first ? new Bounds(go.transform.position, Vector3.one * .2f) : b;
        }
    }

    public sealed class Piece : MonoBehaviour {
        public CardEntry Card;
        public int Owner;
        public string InstanceId;
        public int X = -1, Y = -1;
        public bool IsRealModel;
        public float Height;
        public GameObject Plate, OwnerRing;
        public int Damage;
    }

    public sealed class Billboard : MonoBehaviour {
        void LateUpdate() {
            var cam = Camera.main; if (cam == null) return;
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, cam.transform.up);
        }
    }
}
