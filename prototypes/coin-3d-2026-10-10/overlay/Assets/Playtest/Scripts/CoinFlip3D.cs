using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace InfiniteConquest.Playtest {
    // 3D starting-player coin (AI-080-COIN-3D-PRESENTATION). A traditional toss: the coin leaves a
    // pedestal, turns end over end about its horizontal diameter while it rises and falls, lands,
    // wobbles flat and shows the face of the seat the rules engine already chose. The animation never
    // decides anything; `winner` is the engine's starting_player.
    //
    // Runs on its own stage far below the board with its own camera, then removes itself.
    public sealed class CoinFlip3D : MonoBehaviour {
        public static readonly Vector3 StageOrigin = new Vector3(0, -500, 0);
        public const float Launch = .2f, Flight = 1.55f, Settle = .75f, Hold = 1.2f;
        public const int Turns = 5;
        public const float Thickness = .08f;

        public int Winner { get; private set; }
        public int StartFace { get; private set; }
        public bool Landed { get; private set; }
        public bool Revealed { get; private set; }
        public bool Done { get; private set; }
        public CoinSkin Skin { get; private set; }
        public Transform Coin => coin;
        public Material[] Materials => mats;
        // Seat whose face points up (towards the camera side), read from the transform, not the plan.
        public int FaceUpSeat => Vector3.Dot(coin.up, Vector3.up) >= 0 ? 0 : 1;
        // Evidence for the end-over-end check: how far the flip axis ever leaves the horizontal
        // (measured on the transform), and how many times a face swept past edge-on.
        public float MaxAxisTilt { get; private set; }
        public int FaceCrossings { get; private set; }

        Transform coin, stage; Camera stageCam; Material[] mats;
        Action<string> playUi; bool fast; string captureDir, captureTag; float startAngle, endAngle, yaw0;

        public static CoinFlip3D Play(int winner, CoinSkin skin, Shader lit, Action<string> playUi,
                                      bool fast = false, string captureDir = null, string captureTag = null, int? startFace = null) {
            var go = new GameObject("Coin flip (3D)");
            var flip = go.AddComponent<CoinFlip3D>();
            flip.Setup(Mathf.Clamp(winner, 0, 1), skin, lit, playUi, fast, captureDir, captureTag, startFace);
            return flip;
        }

        void Setup(int winner, CoinSkin skin, Shader lit, Action<string> ui, bool fastMode, string capture, string tag, int? forcedStart) {
            Winner = winner; Skin = skin; playUi = ui; fast = fastMode; captureDir = capture; captureTag = tag ?? "flip";
            stage = transform; stage.position = StageOrigin;

            // Start face is cosmetic only; the total turn is chosen so the coin ends on the winner.
            StartFace = forcedStart ?? UnityEngine.Random.Range(0, 2);
            startAngle = StartFace * 180f;
            float delta = Mathf.Repeat(winner * 180f - startAngle, 360f);
            endAngle = startAngle + Turns * 360f + delta;
            yaw0 = UnityEngine.Random.Range(-28f, 28f);

            var template = Resources.Load<Material>(CoinSkin.ResourceFolder + "CoinLitTemplate");
            Material Make(Texture tex, Color tint, float metal, float smooth) {
                var m = template != null ? new Material(template) : new Material(lit != null ? lit : Shader.Find("Universal Render Pipeline/Lit"));
                m.mainTexture = tex; if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                m.color = tint; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
                m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth);
                m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black);
                return m;
            }
            if (skin == null) skin = ScriptableObject.CreateInstance<CoinSkin>();
            mats = new Material[3];
            mats[CoinMesh.Front] = Make(skin.front, skin.faceTint, skin.faceMetallic, skin.faceSmoothness);
            mats[CoinMesh.Back] = Make(skin.back, skin.faceTint, skin.faceMetallic, skin.faceSmoothness);
            mats[CoinMesh.Rim] = skin.rimMaterial != null ? new Material(skin.rimMaterial)
                                                          : Make(skin.rimTexture, skin.rimColor, skin.rimMetallic, skin.rimSmoothness);

            coin = new GameObject("Coin").transform; coin.SetParent(stage, false);
            coin.gameObject.AddComponent<MeshFilter>().sharedMesh = CoinMesh.Build(.5f, Thickness);
            var mr = coin.gameObject.AddComponent<MeshRenderer>(); mr.sharedMaterials = mats;

            var plinth = new GameObject("Coin pedestal (hex)"); plinth.transform.SetParent(stage, false);
            plinth.transform.localPosition = new Vector3(0, -.05f, 0); plinth.transform.localRotation = Quaternion.Euler(0, 30, 0);
            plinth.AddComponent<MeshFilter>().sharedMesh = CoinMesh.Build(1.25f, .1f, .02f, 6);
            var slate = Make(null, new Color(.09f, .11f, .15f), .2f, .35f);
            plinth.AddComponent<MeshRenderer>().sharedMaterials = new[] { slate, slate, slate };

            var spot = new GameObject("Coin key").AddComponent<Light>(); spot.transform.SetParent(stage, false);
            spot.type = LightType.Spot; spot.range = 12; spot.spotAngle = 55; spot.intensity = 6; spot.shadows = LightShadows.Soft;
            spot.color = new Color(1f, .95f, .86f);
            spot.transform.localPosition = new Vector3(-1.2f, 4.2f, -1.8f); spot.transform.LookAt(stage.position + Vector3.up * .5f);
            var fill = new GameObject("Coin fill").AddComponent<Light>(); fill.transform.SetParent(stage, false);
            fill.type = LightType.Point; fill.range = 6; fill.intensity = 1.4f; fill.color = new Color(.6f, .8f, 1f);
            fill.transform.localPosition = new Vector3(1.4f, 1.6f, -1.6f);

            var main = Camera.main;
            stageCam = new GameObject("Coin camera").AddComponent<Camera>(); stageCam.transform.SetParent(stage, false);
            stageCam.clearFlags = CameraClearFlags.SolidColor; stageCam.backgroundColor = new Color(.02f, .035f, .06f);
            stageCam.fieldOfView = 38; stageCam.nearClipPlane = .05f; stageCam.farClipPlane = 40;
            stageCam.depth = (main != null ? main.depth : 0) + 10;

            Pose(0);
            StartCoroutine(Run());
        }

        // Coin height (centre above pedestal), turn angle, yaw and wobble for time t since start.
        void Pose(float t) {
            float rest = Thickness / 2, y = rest, angle = startAngle, yaw = yaw0, tilt = 0, flightT = t - Launch;
            if (flightT < 0) {
                // resting on the pedestal before the toss
            } else if (flightT < Flight) {
                float s = flightT / Flight;
                y = rest + 4 * 1.55f * s * (1 - s);                                                // ballistic rise and fall
                angle = Mathf.Lerp(startAngle, endAngle, s);                                       // constant spin while airborne
                yaw = Mathf.Lerp(yaw0, 0, Mathf.SmoothStep(0, 1, s));
                tilt = 6 * Mathf.Sin(s * Mathf.PI * 3) * (1 - s);                                  // slight precession
            } else {
                float k = flightT - Flight;
                angle = endAngle; yaw = 0;
                y = rest + .07f * Mathf.Abs(Mathf.Sin(k * 14f)) * Mathf.Exp(-k * 9f);             // bounce
                tilt = 9 * Mathf.Exp(-k * 6f) * Mathf.Sin(k * 26f);                                // rim wobble while settling
                if (k > Settle) { y = rest; tilt = 0; }
            }
            FaceCrossings = Mathf.Max(FaceCrossings, Mathf.FloorToInt((angle - startAngle + 90f) / 180f));
            var rot = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(tilt, Vector3.forward) * Quaternion.AngleAxis(angle, Vector3.right);
            float ny = Mathf.Abs((rot * Vector3.up).y);                                            // keep the lowest rim point on or above the pedestal
            y = Mathf.Max(y, .5f * Mathf.Sqrt(Mathf.Max(0, 1 - ny * ny)) + rest * ny);
            coin.localPosition = new Vector3(0, y, 0);
            coin.localRotation = rot;

            // Camera follows the toss, then pushes in steeply so the settled face reads.
            float land = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - Launch - Flight * .8f) / (Settle + .3f)));
            float pitch = Mathf.Lerp(30, 62, land), dist = Mathf.Lerp(4.3f, 2.25f, land);
            float lookY = Mathf.Lerp(Mathf.Lerp(.7f, y * .55f + .35f, flightT > 0 ? 1 : 0), rest, land);
            var target = new Vector3(0, lookY, 0);
            stageCam.transform.localPosition = target + Quaternion.Euler(pitch, 0, 0) * new Vector3(0, 0, -dist);
            stageCam.transform.LookAt(stage.TransformPoint(target), Vector3.up);
        }

        // Real time normally; with a capture folder, a fixed 30 fps step so the saved clip is smooth.
        IEnumerator Run() {
            float total = Launch + Flight + Settle, t0 = Time.unscaledTime; int frame = 0;
            float Now() => captureDir != null ? frame / 30f : Time.unscaledTime - t0;
            if (fast) {
                Pose(total + .01f); Landed = Revealed = true; Log(); yield return null; Finish(); yield break;
            }
            playUi?.Invoke("card");
            while (true) {
                float t = Now();
                Pose(Mathf.Min(t, total + .01f));
                if (t >= Launch && t <= Launch + Flight) MaxAxisTilt = Mathf.Max(MaxAxisTilt, Mathf.Abs(coin.right.y));
                if (!Landed && t >= Launch + Flight) { Landed = true; playUi?.Invoke("click"); }
                if (captureDir != null) yield return Capture($"{captureTag}-{frame:000}.png");
                frame++;
                if (t >= total) break;
                yield return null;
            }
            Revealed = true; Log();
            playUi?.Invoke("turn");
            var face = mats[Winner == 0 ? CoinMesh.Front : CoinMesh.Back];
            var glow = Skin != null ? Skin.emission : Color.black;
            float r0 = Now();
            while (Now() - r0 < Hold) {
                float p = (Now() - r0) / Hold;
                face.SetColor("_EmissionColor", glow * Mathf.Sin(Mathf.Clamp01(p * 1.6f) * Mathf.PI));
                if (captureDir != null) yield return Capture($"{captureTag}-{frame:000}.png");
                frame++;
                yield return null;
            }
            Finish();
        }

        void Log() => Debug.Log($"COIN_PRESENT winner={Winner} faceUp={FaceUpSeat} start={StartFace} skin={(Skin != null ? Skin.name : "none")} crossings={FaceCrossings} axisTilt={MaxAxisTilt:0.000}");

        IEnumerator Capture(string name) {
            yield return new WaitForEndOfFrame();
            Directory.CreateDirectory(captureDir);
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(captureDir, name), tex.EncodeToPNG());
            Destroy(tex);
        }

        void Finish() { Done = true; Destroy(gameObject); }
        void OnDestroy() { if (mats != null) foreach (var m in mats) if (m != null) Destroy(m); }

        // ----- Prototype proof: -coinTest <dir> plays every skin x winner x start face, checks the
        // settled face and the skin's textures, writes coin-test.json and quits.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void MaybeRunTest() {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-coinTest");
            if (i < 0 || i + 1 >= args.Length) return;
            new GameObject("Coin test").AddComponent<CoinTestRunner>().dir = args[i + 1];
        }

        sealed class CoinTestRunner : MonoBehaviour {
            public string dir;
            [Serializable] class Case { public string skin; public int winner, startFace, faceUp, crossings; public float axisTilt; public bool faceTexture, rimTexture, passed; }
            [Serializable] class Report { public bool passed; public int cases; public List<Case> results = new List<Case>(); }

            IEnumerator Start() {
                yield return new WaitForSecondsRealtime(1f);
                var game = FindFirstObjectByType<PlaytestGame>(); if (game != null) game.enabled = false;
                var report = new Report(); report.passed = true;
                foreach (var skinName in new[] { CoinSkin.DefaultName, "CapitalArtTest" }) {
                    var skin = CoinSkin.Load(skinName);
                    for (int winner = 0; winner < 2; winner++) for (int start = 0; start < 2; start++) {
                        bool capture = start != winner;    // record the flips that must change face
                        var flip = Play(winner, skin, null, null, false, capture ? dir : null, $"{skinName}-seat{winner}", start);
                        while (!flip.Revealed) yield return null;
                        var up = flip.Materials[flip.FaceUpSeat == 0 ? CoinMesh.Front : CoinMesh.Back];
                        var c = new Case {
                            skin = skin != null ? skin.name : "missing", winner = winner, startFace = start, faceUp = flip.FaceUpSeat,
                            crossings = flip.FaceCrossings, axisTilt = flip.MaxAxisTilt,
                            faceTexture = skin != null && up.mainTexture == skin.FaceFor(winner) && skin.FaceFor(winner) != null,
                            rimTexture = skin != null && (skin.rimMaterial != null || flip.Materials[CoinMesh.Rim].mainTexture == skin.rimTexture),
                        };
                        c.passed = skin != null && skin.name == skinName && c.faceUp == winner && c.faceTexture && c.rimTexture
                                   && c.axisTilt < .25f && c.crossings >= Turns * 2;
                        report.passed &= c.passed; report.results.Add(c);
                        Debug.Log("COIN_TEST " + JsonUtility.ToJson(c));
                        while (!flip.Done) yield return null;
                        yield return null;
                    }
                }
                report.cases = report.results.Count;
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "coin-test.json"), JsonUtility.ToJson(report, true));
                Debug.Log("COIN_TEST_DONE passed=" + report.passed);
                Application.Quit(report.passed ? 0 : 3);
            }
        }
    }
}
