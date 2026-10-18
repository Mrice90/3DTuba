using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using InfiniteConquest.Proof;

namespace InfiniteConquest.Playtest {
    // AI-060-LAPTOP-BUDGET-RIG: opt-in frame-time probe, does nothing without -rigPerf. Run with -playback so the
    // normal match replays at 1x. Phase 1 measures that playback; phase 2 adds one animated rigged character on
    // every drawn hex (cycling Attack/Hit/Move clips) on top of it. vSync and the 60 fps cap are lifted so the
    // numbers show real headroom. Writes -rigPerfResult (JSON) and quits.
    public sealed class RigPerfProbe : MonoBehaviour {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() {
            if (!System.Environment.GetCommandLineArgs().Contains("-rigPerf")) return;
            DontDestroyOnLoad(new GameObject("Rig perf probe").AddComponent<RigPerfProbe>().gameObject);
        }

        static string Arg(string name, string fallback) {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : fallback;
        }

        IEnumerator Start() {
            float seconds = float.Parse(Arg("-rigPerfSeconds", "30"), System.Globalization.CultureInfo.InvariantCulture);
            yield return new WaitForSeconds(3f);   // let the match load and the first pieces spawn
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            var playback = new List<float>(); int playbackSkinned = 0, playbackPieces = 0;
            yield return Measure(seconds, playback, () => {
                playbackSkinned = Mathf.Max(playbackSkinned, FindObjectsByType<SkinnedMeshRenderer>().Count(r => r.isVisible));
                playbackPieces = Mathf.Max(playbackPieces, FindObjectsByType<Piece>().Length);
            });

            var game = FindAnyObjectByType<PlaytestGame>();
            var factory = new TokenFactory(game != null && game.LitShader != null ? game.LitShader : Shader.Find("Universal Render Pipeline/Lit"),
                                           game != null && game.UnlitShader != null ? game.UnlitShader : Shader.Find("Universal Render Pipeline/Unlit"),
                                           Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            var rigged = PlaytestCatalog.All.Where(c => c.type == "CHARACTER" && c.HasModel).ToList();
            var spawned = new List<Piece>(); int k = 0;
            foreach (var cell in BoardConfig.DrawnCells) {
                for (int tries = 0; tries < rigged.Count; tries++) {
                    var card = rigged[k++ % rigged.Count];
                    var go = factory.Create(card, k % 2, out bool real);
                    var anim = go.GetComponentInChildren<Animator>();
                    if (!real || anim == null || anim.runtimeAnimatorController == null) { Destroy(go); continue; }
                    // lifted a little so they stand on top of whatever playback has on the hex
                    go.transform.position = BoardLayout.CellCenter(cell.x, cell.y) + Vector3.up * (BoardLayout.TileTop + .02f);
                    go.transform.rotation = Quaternion.Euler(0, k % 2 == 0 ? 15 : 195, 0);
                    go.transform.localScale = Vector3.one * .78f;
                    var p = go.GetComponent<Piece>(); p.InstanceId = "perf-" + k; spawned.Add(p);
                    break;
                }
            }
            var full = new List<float>(); int fullSkinned = 0; float next = 0; int turn = 0;
            yield return Measure(seconds, full, () => {
                fullSkinned = Mathf.Max(fullSkinned, FindObjectsByType<SkinnedMeshRenderer>().Count(r => r.isVisible));
                if (Time.unscaledTime < next) return;
                next = Time.unscaledTime + .6f; turn++;
                for (int i = turn % 3; i < spawned.Count; i += 3) {
                    var p = spawned[i];
                    if (turn % 2 == 0) PresentationMotion.Attack(p, p.transform.position + p.transform.forward, 1);
                    else PresentationMotion.Hit(p, 1);
                }
            });

            string Stats(List<float> ms) {
                if (ms.Count == 0) return "{}";
                var s = ms.OrderBy(x => x).ToList();
                float P(float q) => s[Mathf.Clamp((int)(q * (s.Count - 1)), 0, s.Count - 1)];
                return "{\"frames\":" + s.Count + ",\"avgMs\":" + s.Average().ToString("0.00") + ",\"p50Ms\":" + P(.5f).ToString("0.00") +
                       ",\"p95Ms\":" + P(.95f).ToString("0.00") + ",\"p99Ms\":" + P(.99f).ToString("0.00") + ",\"maxMs\":" + s[s.Count - 1].ToString("0.00") +
                       ",\"avgFps\":" + (1000f / s.Average()).ToString("0.0") + ",\"under60Pct\":" + (100f * s.Count(x => x > 1000f / 60f) / s.Count).ToString("0.0") + "}";
            }
            var json = "{\"resolution\":\"" + Screen.width + "x" + Screen.height + "\",\"gpu\":\"" + SystemInfo.graphicsDeviceName.Replace("\"", "'") +
                       "\",\"cpu\":\"" + SystemInfo.processorType.Replace("\"", "'") + "\",\"memoryMb\":" + SystemInfo.systemMemorySize +
                       ",\"seconds\":" + seconds + ",\"playback\":" + Stats(playback) + ",\"playbackMaxPieces\":" + playbackPieces + ",\"playbackMaxVisibleSkinned\":" + playbackSkinned +
                       ",\"fullBoard\":" + Stats(full) + ",\"fullBoardAddedRigged\":" + spawned.Count + ",\"fullBoardMaxVisibleSkinned\":" + fullSkinned +
                       ",\"monoUsedMb\":" + (UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / 1048576) + ",\"allocatedMb\":" + (UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576) + "}";
            File.WriteAllText(Arg("-rigPerfResult", Path.Combine(Application.persistentDataPath, "rig-perf.json")), json);
            Debug.Log("RIG_PERF " + json);
            Application.Quit(0);
        }

        static IEnumerator Measure(float seconds, List<float> ms, System.Action each) {
            float end = Time.realtimeSinceStartup + seconds;
            yield return null;
            while (Time.realtimeSinceStartup < end) {
                ms.Add(Time.unscaledDeltaTime * 1000f);
                each();
                yield return null;
            }
        }
    }
}
