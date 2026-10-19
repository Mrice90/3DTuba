using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using InfiniteConquest.Proof;

namespace InfiniteConquest.Playtest {
    // AI-209 (folds in AI-060-LAPTOP-BUDGET-RIG's RigPerfProbe): opt-in laptop profiling probe, does nothing
    // without -perfProbe. Run with -playback so the normal match replays at 1x. Phases:
    //   playback      the replayed match at the game's own settings (60 fps cap)
    //   crowded       one animated rigged character on every drawn hex on top of playback, with summons,
    //                 attacks, hits, deaths and their card sounds happening all the time (60 fps cap)
    //   crowdedUncap  the same with the cap lifted, to show headroom
    // Writes -perfResult (JSON) and quits. Spikes are frames over 33 ms, labelled with what happened that frame.
    public sealed class PerfProbe : MonoBehaviour {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() {
            if (!System.Environment.GetCommandLineArgs().Contains("-perfProbe")) return;
            bootSeconds = Time.realtimeSinceStartup;
            DontDestroyOnLoad(new GameObject("Perf probe").AddComponent<PerfProbe>().gameObject);
        }

        static float bootSeconds;
        static string Arg(string name, string fallback) {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : fallback;
        }

        static readonly (string cat, string name)[] Counters = {
            ("Internal", "Main Thread"), ("Render", "Batches Count"), ("Render", "SetPass Calls Count"), ("Render", "Draw Calls Count"),
            ("Render", "Triangles Count"), ("Render", "Vertices Count"), ("Render", "Shadow Casters Count"),
            ("Memory", "GC Allocated In Frame"), ("Memory", "System Used Memory"), ("Memory", "Total Used Memory"),
            ("Memory", "Gfx Used Memory"), ("Memory", "Texture Memory"), ("Memory", "Mesh Memory"), ("Memory", "Audio Used Memory"),
        };
        readonly List<ProfilerRecorder> recorders = new List<ProfilerRecorder>();
        string frameLabel = "";
        readonly List<float> spawnMs = new List<float>();
        readonly List<string> spawnFirst = new List<string>();

        sealed class Phase {
            public string name; public readonly List<float> ms = new List<float>(); public readonly List<string> labels = new List<string>();
            public readonly Dictionary<string, List<double>> counters = new Dictionary<string, List<double>>();
            public int maxVisibleSkinned, maxPieces, summons, attacks, hits, deaths, sounds;
        }

        IEnumerator Start() {
            foreach (var (cat, name) in Counters) {
                var c = cat == "Render" ? ProfilerCategory.Render : cat == "Memory" ? ProfilerCategory.Memory : ProfilerCategory.Internal;
                recorders.Add(ProfilerRecorder.StartNew(c, name, 1));
            }
            float seconds = float.Parse(Arg("-perfSeconds", "30"), System.Globalization.CultureInfo.InvariantCulture);
            // Load time: process start -> first frame with pieces on the board.
            float firstPieces = -1;
            while (firstPieces < 0 && Time.realtimeSinceStartup < bootSeconds + 60) {
                if (FindObjectsByType<Piece>().Length > 0) firstPieces = Time.realtimeSinceStartup;
                yield return null;
            }
            yield return new WaitForSeconds(2f);
            var phases = new List<Phase>();
            var playback = new Phase { name = "playback" }; phases.Add(playback);
            yield return Measure(seconds, playback, null);

            var game = FindAnyObjectByType<PlaytestGame>();
            var sfx = FindAnyObjectByType<SfxBank>();
            var factory = new TokenFactory(game != null && game.LitShader != null ? game.LitShader : Shader.Find("Universal Render Pipeline/Lit"),
                                           game != null && game.UnlitShader != null ? game.UnlitShader : Shader.Find("Universal Render Pipeline/Unlit"),
                                           Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            var rigged = PlaytestCatalog.All.Where(c => c.type == "CHARACTER" && c.HasModel).ToList();
            var cells = BoardConfig.DrawnCells.ToList();
            var onCell = new Piece[cells.Count];
            var rng = new System.Random(209);
            int k = 0;
            var seen = new HashSet<string>();

            Piece Spawn(int cell, Phase ph) {
                for (int tries = 0; tries < rigged.Count; tries++) {
                    var card = rigged[rng.Next(rigged.Count)];
                    bool first = seen.Add(card.id);
                    float t0 = Time.realtimeSinceStartup;
                    var go = factory.Create(card, k++ % 2, out bool real);
                    float took = (Time.realtimeSinceStartup - t0) * 1000f;
                    var anim = go.GetComponentInChildren<Animator>();
                    if (!real || anim == null || anim.runtimeAnimatorController == null) { Destroy(go); continue; }
                    spawnMs.Add(took); if (first) spawnFirst.Add(card.id + ":" + took.ToString("0.0"));
                    var c = cells[cell];
                    go.transform.position = BoardLayout.CellCenter(c.x, c.y) + Vector3.up * (BoardLayout.TileTop + .02f);
                    go.transform.rotation = Quaternion.Euler(0, k % 2 == 0 ? 15 : 195, 0);
                    go.transform.localScale = Vector3.one * .78f;
                    var p = go.GetComponent<Piece>(); p.InstanceId = "perf-" + k;
                    PresentationMotion.Summon(p, 1);
                    if (sfx != null) { sfx.Play(card.id, "deploy", go.transform.position); if (ph != null) ph.sounds++; }
                    if (ph != null) ph.summons++;
                    frameLabel += (first ? "first-summon " : "summon ") + card.id + "(" + took.ToString("0") + "ms) ";
                    return p;
                }
                return null;
            }

            IEnumerator Kill(int cell, Phase ph) {
                var p = onCell[cell]; onCell[cell] = null;
                if (p == null) yield break;
                if (sfx != null) { sfx.Play(p.Card.id, "destroy", p.transform.position); ph.sounds++; }
                ph.deaths++; frameLabel += "death ";
                yield return PresentationMotion.Destroy(p, 1);
                Destroy(p.gameObject);
                yield return new WaitForSeconds(.3f);
                onCell[cell] = Spawn(cell, ph);
            }

            // Fill the board, staggered like a busy turn.
            float fillStart = Time.realtimeSinceStartup;
            var fill = new Phase { name = "fill" }; phases.Add(fill);
            for (int i = 0; i < cells.Count; i++) {
                onCell[i] = Spawn(i, fill);
                var label = frameLabel; frameLabel = "";
                yield return null;
                fill.ms.Add(Time.unscaledDeltaTime * 1000f); fill.labels.Add(label);
            }
            float fillSeconds = Time.realtimeSinceStartup - fillStart;
            yield return new WaitForSeconds(2f);

            IEnumerator Battle(Phase ph) {
                float nextBeat = 0, nextDeath = Time.unscaledTime + 1.5f; int beat = 0;
                return Measure(seconds, ph, () => {
                    if (Time.unscaledTime >= nextBeat) {
                        nextBeat = Time.unscaledTime + .6f; beat++;
                        for (int i = beat % 3; i < onCell.Length; i += 3) {
                            var p = onCell[i]; if (p == null || PresentationMotion.IsBusy(p)) continue;
                            if (beat % 2 == 0) {
                                PresentationMotion.Attack(p, p.transform.position + p.transform.forward, 1); ph.attacks++;
                                if (sfx != null && i % 2 == 0) { sfx.Play(p.Card.id, "attack", p.transform.position); ph.sounds++; }
                            } else {
                                PresentationMotion.Hit(p, 1); ph.hits++;
                                if (sfx != null && i % 2 == 1) { sfx.Play(p.Card.id, "hit", p.transform.position); ph.sounds++; }
                            }
                        }
                        frameLabel += "beat ";
                    }
                    if (Time.unscaledTime >= nextDeath) {
                        nextDeath = Time.unscaledTime + 2f;
                        for (int n = 0; n < 3; n++) StartCoroutine(Kill(rng.Next(onCell.Length), ph));
                    }
                });
            }

            Application.targetFrameRate = 60;   // what PlaytestGame sets; stated so the phase is explicit
            var crowded = new Phase { name = "crowded" }; phases.Add(crowded);
            yield return Battle(crowded);
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            var uncapped = new Phase { name = "crowdedUncap" }; phases.Add(uncapped);
            yield return Battle(uncapped);

            var sb = new StringBuilder("{");
            sb.Append("\"resolution\":\"" + Screen.width + "x" + Screen.height + "\",\"fullScreen\":" + (Screen.fullScreen ? "true" : "false"));
            sb.Append(",\"gpu\":\"" + Esc(SystemInfo.graphicsDeviceName) + "\",\"gfxApi\":\"" + SystemInfo.graphicsDeviceType + "\",\"vramMb\":" + SystemInfo.graphicsMemorySize);
            sb.Append(",\"cpu\":\"" + Esc(SystemInfo.processorType) + "\",\"cores\":" + SystemInfo.processorCount + ",\"ramMb\":" + SystemInfo.systemMemorySize);
            sb.Append(",\"quality\":\"" + QualitySettings.names[QualitySettings.GetQualityLevel()] + "\",\"seconds\":" + seconds);
            sb.Append(",\"bootSeconds\":" + bootSeconds.ToString("0.00") + ",\"firstPiecesSeconds\":" + firstPieces.ToString("0.00"));
            sb.Append(",\"fillSeconds\":" + fillSeconds.ToString("0.00") + ",\"cells\":" + cells.Count);
            var sp = spawnMs.OrderBy(x => x).ToList();
            if (sp.Count > 0) sb.Append(",\"spawnMs\":{\"n\":" + sp.Count + ",\"avg\":" + sp.Average().ToString("0.0") + ",\"p50\":" + sp[sp.Count / 2].ToString("0.0") + ",\"max\":" + sp[sp.Count - 1].ToString("0.0") + "}");
            sb.Append(",\"firstSpawnMs\":[" + string.Join(",", spawnFirst.Select(s => "\"" + s + "\"")) + "]");
            sb.Append(",\"phases\":{");
            sb.Append(string.Join(",", phases.Select(Stats)));
            sb.Append("},\"monoUsedMb\":" + (UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / 1048576) +
                      ",\"allocatedMb\":" + (UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576) +
                      ",\"reservedMb\":" + (UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1048576) +
                      ",\"gfxDriverMb\":" + (UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver() / 1048576) + "}");
            var json = sb.ToString();
            File.WriteAllText(Arg("-perfResult", Path.Combine(Application.persistentDataPath, "perf.json")), json);
            Debug.Log("PERF_PROBE " + json);
            foreach (var r in recorders) r.Dispose();
            Application.Quit(0);
        }

        static string Esc(string s) => (s ?? "").Replace("\\", "/").Replace("\"", "'");

        string Stats(Phase ph) {
            var o = new StringBuilder("\"" + ph.name + "\":{");
            var s = ph.ms.OrderBy(x => x).ToList();
            if (s.Count == 0) return o.Append("}").ToString();
            float P(float q) => s[Mathf.Clamp((int)(q * (s.Count - 1)), 0, s.Count - 1)];
            o.Append("\"frames\":" + s.Count + ",\"avgMs\":" + s.Average().ToString("0.00") + ",\"p50Ms\":" + P(.5f).ToString("0.00") +
                     ",\"p95Ms\":" + P(.95f).ToString("0.00") + ",\"p99Ms\":" + P(.99f).ToString("0.00") + ",\"maxMs\":" + s[s.Count - 1].ToString("0.00") +
                     ",\"avgFps\":" + (1000f / s.Average()).ToString("0.0") + ",\"over16_7Pct\":" + (100f * s.Count(x => x > 17.5f) / s.Count).ToString("0.0") +
                     ",\"over33\":" + s.Count(x => x > 33.4f) + ",\"over50\":" + s.Count(x => x > 50f) + ",\"over100\":" + s.Count(x => x > 100f));
            o.Append(",\"maxVisibleSkinned\":" + ph.maxVisibleSkinned + ",\"maxPieces\":" + ph.maxPieces + ",\"summons\":" + ph.summons + ",\"attacks\":" + ph.attacks +
                     ",\"hits\":" + ph.hits + ",\"deaths\":" + ph.deaths + ",\"sounds\":" + ph.sounds);
            var spikes = ph.ms.Select((m, i) => (m, i)).Where(t => t.m > 33.4f).OrderByDescending(t => t.m).Take(10)
                              .Select(t => "\"" + t.m.ToString("0.0") + "ms " + Esc(ph.labels[t.i]).Trim() + "\"");
            o.Append(",\"topSpikes\":[" + string.Join(",", spikes) + "]");
            o.Append(",\"counters\":{");
            o.Append(string.Join(",", ph.counters.Where(kv => kv.Value.Count > 0).Select(kv => {
                var v = kv.Value; bool mem = kv.Key.Contains("Memory");
                double scale = mem ? 1.0 / 1048576 : kv.Key == "Main Thread" ? 1e-6 : 1;
                return "\"" + kv.Key + "\":{\"avg\":" + (v.Average() * scale).ToString("0.##") + ",\"max\":" + (v.Max() * scale).ToString("0.##") + "}";
            })));
            return o.Append("}}").ToString();
        }

        IEnumerator Measure(float seconds, Phase ph, System.Action each) {
            float end = Time.realtimeSinceStartup + seconds;
            yield return null;
            frameLabel = "";
            while (Time.realtimeSinceStartup < end) {
                ph.ms.Add(Time.unscaledDeltaTime * 1000f);
                ph.labels.Add(frameLabel); frameLabel = "";
                for (int i = 0; i < recorders.Count; i++) {
                    var r = recorders[i]; if (!r.Valid) continue;
                    if (!ph.counters.TryGetValue(Counters[i].name, out var list)) ph.counters[Counters[i].name] = list = new List<double>();
                    list.Add(r.LastValue);
                }
                if (ph.ms.Count % 15 == 0) {
                    ph.maxVisibleSkinned = Mathf.Max(ph.maxVisibleSkinned, FindObjectsByType<SkinnedMeshRenderer>().Count(r => r.isVisible));
                    ph.maxPieces = Mathf.Max(ph.maxPieces, FindObjectsByType<Piece>().Length);
                }
                each?.Invoke();
                yield return null;
            }
        }
    }
}
