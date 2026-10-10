using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace InfiniteConquest.Playtest {
    // AI-094 turn-order coin flip. Sits on the coin's root; the coin model (Resources/Coin/coin.fbx) is a child
    // with three material slots: Coin_Rim, Coin_Heads, Coin_Tails. Heads = Player 1 (seat 0), tails = Player 2 (seat 1).
    //   coin.Play(result, seat => StartTurn(seat))   // toss, spin, land on that face, call back, hold, hide
    // Pass the rules engine's result so the coin always agrees with the match; Play(Random) is for menus and tests.
    // Sound: OnToss and OnLand are empty hooks for now (wire SfxBank or an AudioSource to them).
    public sealed class CoinFlip : MonoBehaviour {
        [Serializable] public sealed class ResultEvent : UnityEvent<int> { }

        public Renderer Target;
        public string SkinId = "olympus";
        public CoinSkin Skin;
        [Tooltip("Coin diameter in world units (the model is 1.0 across).")] public float Size = 1.8f;
        public float Height = 2.0f, TossSeconds = 1.25f, SettleSeconds = .7f, HoldSeconds = 1.1f;
        [Tooltip("Whole turns in the air before landing.")] public int Turns = 5;
        public UnityEvent OnToss = new UnityEvent(), OnLand = new UnityEvent();
        public ResultEvent OnResult = new ResultEvent();
        public event Action<int> Landed;
        public bool Busy { get; private set; }
        public int LastResult { get; private set; } = -1;

        const float HalfThickness = .04f;      // of the 1.0-across model
        const float SpinYaw = 25f;            // drift around the vertical while in the air
        int rim = 0, heads = 1, tails = 2;
        Vector3 rest; float yaw;

        // ------------------------------------------------------------------ setup and skins
        public static CoinFlip Create(Transform parent, Shader lit, string skinId = "olympus") {
            var root = new GameObject("Coin");
            root.transform.SetParent(parent, false);
            var flip = root.AddComponent<CoinFlip>();
            var model = Resources.Load<GameObject>("Coin/coin");
            if (model != null) Instantiate(model, root.transform, false).name = "Model";
            else Debug.LogWarning("COIN model Resources/Coin/coin is missing");
            flip.Target = root.GetComponentInChildren<Renderer>();
            flip.SkinId = skinId;
            flip.UseShader(lit);
            flip.ApplySkin(CoinSkin.Load(skinId) ?? CoinSkin.Load("olympus"));
            root.SetActive(false);
            return flip;
        }

        void Awake() {
            if (Target == null) Target = GetComponentInChildren<Renderer>();
            FindSlots();
            if (Skin == null || Skin.Heads == null) Skin = CoinSkin.Load(SkinId);
            if (Skin != null) ApplySkin(Skin);
        }

        void FindSlots() {
            if (Target == null) return;
            var mats = Target.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) {
                var n = mats[i] != null ? mats[i].name : "";
                if (n.Contains("Heads")) heads = i; else if (n.Contains("Tails")) tails = i; else if (n.Contains("Rim")) rim = i;
            }
        }

        // Replaces the imported materials with fresh ones on a shader the build is known to include.
        public void UseShader(Shader lit) {
            if (Target == null || lit == null) return;
            FindSlots();
            var mats = Target.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = new Material(lit) { name = mats[i] != null ? mats[i].name : "Coin_" + i };
            Target.sharedMaterials = mats;
        }

        public void ApplySkin(CoinSkin skin) {
            if (skin == null || Target == null) return;
            Skin = skin; SkinId = skin.Id;
            FindSlots();
            var mats = Application.isPlaying ? Target.materials : Target.sharedMaterials;
            Paint(mats, rim, null, skin.Rim, 1f, .7f);
            Paint(mats, heads, skin.Heads, Color.white, skin.Metallic, skin.Smoothness);
            Paint(mats, tails, skin.Tails, Color.white, skin.Metallic, skin.Smoothness);
        }

        static void Paint(Material[] mats, int i, Texture2D tex, Color c, float metal, float smooth) {
            if (i < 0 || i >= mats.Length || mats[i] == null) return;
            var m = mats[i];
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c;
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        }

        // ------------------------------------------------------------------ the flip
        public IEnumerator Play(Action<int> onLanded = null) => Play(UnityEngine.Random.Range(0, 2), onLanded);

        // Plays the whole flip where the coin root stands (the board surface) and lands on `result`
        // (0 heads / Player 1, 1 tails / Player 2). onLanded fires on landing, before the hold and hide.
        public IEnumerator Play(int result, Action<int> onLanded) {
            result = Mathf.Clamp(result, 0, 1);
            Busy = true; LastResult = -1;
            gameObject.SetActive(true);
            rest = transform.position; yaw = FacingYaw();
            float t = 0;
            while (t < .25f) { t += Time.deltaTime; Pose(0, result, Mathf.SmoothStep(0, 1, t / .25f)); yield return null; }
            OnToss.Invoke();
            for (t = 0; t < TossSeconds; t += Time.deltaTime) { Pose(t / TossSeconds, result, 1); yield return null; }
            Pose(1, result, 1);
            OnLand.Invoke();
            for (t = 0; t < SettleSeconds; t += Time.deltaTime) { Settle(t / SettleSeconds, result); yield return null; }
            Pose(1, result, 1);
            LastResult = result;
            OnResult.Invoke(result); Landed?.Invoke(result); onLanded?.Invoke(result);
            for (t = 0; t < HoldSeconds; t += Time.deltaTime) yield return null;
            yield return Hide();
            Busy = false;
        }

        public IEnumerator Hide() {
            if (!gameObject.activeSelf) yield break;
            var from = transform.localScale;
            for (float t = 0; t < .25f; t += Time.deltaTime) { transform.localScale = Vector3.Lerp(from, Vector3.zero, t / .25f); yield return null; }
            transform.position = rest; transform.localScale = Vector3.one * Size;
            gameObject.SetActive(false);
        }

        // The coin's orientation at toss progress u (0..1): `Turns` whole turns plus a half turn for tails,
        // around its own X axis, easing out so it slows into the landing. Flat at u = 0 and u = 1.
        public static Quaternion Spin(float u, int result, float yaw, int turns) {
            float total = 360f * turns + (result == 1 ? 180f : 0f);
            float e = .6f * u + .4f * (1 - (1 - u) * (1 - u));
            return Quaternion.Euler(0, yaw + SpinYaw * u, 0) * Quaternion.AngleAxis(total * e, Vector3.right);
        }

        void Pose(float u, int result, float grow) {
            float lift = HalfThickness * Size, arc = Height * 4 * u * (1 - u);
            transform.position = rest + Vector3.up * (lift + arc);
            transform.rotation = Spin(u, result, yaw, Turns);
            transform.localScale = Vector3.one * Size * grow * (1 + .2f * Mathf.Sin(Mathf.PI * u));
        }

        // A small bounce and a decaying, precessing wobble after touchdown.
        void Settle(float u, int result) {
            float decay = (1 - u) * (1 - u);
            float bounce = .12f * Size * Mathf.Abs(Mathf.Sin(u * Mathf.PI * 2.5f)) * decay;
            var axis = Quaternion.Euler(0, u * 900f, 0) * Vector3.right;
            transform.position = rest + Vector3.up * (HalfThickness * Size + bounce);
            transform.rotation = Quaternion.AngleAxis(14f * decay, axis) * Spin(1, result, yaw, Turns);
            transform.localScale = Vector3.one * Size;
        }

        // Turns the coin so its face images read upright from the main camera (image top points away from it).
        static float FacingYaw() {
            var cam = Camera.main;
            if (cam == null) return 0;
            var f = cam.transform.forward; f.y = 0;
            return f.sqrMagnitude < 1e-4f ? 0 : Quaternion.LookRotation(-f).eulerAngles.y;
        }
    }
}
