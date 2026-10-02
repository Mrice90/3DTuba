using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace IC.Net.Tests
{
    /// <summary>
    /// End-to-end AI-097 lockstep test: two RelayClients against the real lab
    /// relay (node) + two rules-bridge processes (Java). Same seed, same
    /// relay intents, same order ⇒ identical state hashes every turn.
    ///
    /// Runs only when ICNET_RUN_LOCKSTEP=1 (CI: linux-packaging.yml, where the
    /// release jar already exists). Needs: java, node, ICNET_BRIDGE_JAR (or a
    /// glob-able release jar under releases/alpha-0.7.15-playable).
    /// </summary>
    public sealed class LockstepTests : IAsyncLifetime
    {
        private static bool RunLockstep =>
            Environment.GetEnvironmentVariable("ICNET_RUN_LOCKSTEP") == "1";

        private readonly ITestOutputHelper _out;
        private readonly List<IAsyncDisposable> _disposables = new List<IAsyncDisposable>();
        private readonly List<IDisposable> _syncDisposables = new List<IDisposable>();

        private string _repoRoot = "";
        private int _relayPort;
        private Process? _relayProc;
        private string _bridgeClasspath = "";

        public LockstepTests(ITestOutputHelper output) { _out = output; }

        public async Task InitializeAsync()
        {
            if (!RunLockstep) return;
            _repoRoot = FindRepoRoot();
            var jar = FindBridgeJar();
            _bridgeClasspath = await CompileBridgeAsync(jar);
            _relayPort = await StartRelayAsync();
        }

        public async Task DisposeAsync()
        {
            for (var i = _disposables.Count - 1; i >= 0; i--)
                try { await _disposables[i].DisposeAsync(); } catch { }
            foreach (var d in _syncDisposables)
                try { d.Dispose(); } catch { }
            if (_relayProc != null)
            {
                try
                {
                    if (!_relayProc.HasExited) _relayProc.Kill();
                    _relayProc.Dispose();
                }
                catch { }
            }
        }

        private static string FindRepoRoot()
        {
            var env = Environment.GetEnvironmentVariable("ICNET_REPO_ROOT");
            if (!string.IsNullOrEmpty(env) && Directory.Exists(env)) return env;
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "releases", "alpha-0.7.15-playable")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new InvalidOperationException("Could not find the 3DTuba repo root");
        }

        private string FindBridgeJar()
        {
            var env = Environment.GetEnvironmentVariable("ICNET_BRIDGE_JAR");
            if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;
            var jars = Directory.GetFiles(
                Path.Combine(_repoRoot, "releases", "alpha-0.7.15-playable"),
                "infinite-conquest-alpha-*.jar");
            if (jars.Length == 0)
                throw new InvalidOperationException("No release jar found; build it first (build-release.sh)");
            return jars.OrderBy(f => f).First();
        }

        private async Task<string> CompileBridgeAsync(string jar)
        {
            var classes = Path.Combine(Path.GetTempPath(), "icnet-bridge-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(classes);
            var src = Path.Combine(_repoRoot, "releases", "alpha-0.7.15-playable",
                "tools", "rules-bridge", "RulesBridge.java");
            var javac = await RunProcessAsync("javac", $"-encoding UTF-8 -nowarn -cp \"{jar}\" -d \"{classes}\" \"{src}\"");
            if (javac != 0) throw new InvalidOperationException("javac failed for RulesBridge.java");
            return classes + Path.PathSeparator + jar;
        }

        private async Task<int> StartRelayAsync()
        {
            var port = FreePort();
            var psi = new ProcessStartInfo("node",
                $"\"{Path.Combine(_repoRoot, "prototypes", "lobby-lab", "server.js")}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.Environment["PORT"] = port.ToString();
            psi.Environment["HOST"] = "127.0.0.1";
            _relayProc = Process.Start(psi)
                ?? throw new InvalidOperationException("Could not start the lab relay");
            // Wait for the port to accept.
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    using var c = new TcpClient();
                    await c.ConnectAsync("127.0.0.1", port);
                    _out.WriteLine($"lab relay up on {port}");
                    return port;
                }
                catch { await Task.Delay(200); }
            }
            throw new InvalidOperationException("Lab relay did not come up");
        }

        private static int FreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            var port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        private static async Task<int> RunProcessAsync(string bin, string args)
        {
            var psi = new ProcessStartInfo(bin, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {bin}");
            var err = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync();
            if (p.ExitCode != 0) Console.Error.WriteLine(err);
            return p.ExitCode;
        }

        private static string Uuid(char c) =>
            new string(c, 8) + "-1111-4111-8111-" + new string(c, 12);

        [Fact]
        public async Task TwoClients_SameSeed_SameIntents_IdenticalHashes()
        {
            if (!RunLockstep)
            {
                _out.WriteLine("SKIP: set ICNET_RUN_LOCKSTEP=1 to run the lockstep test");
                return;
            }

            var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_relayPort}") };
            _syncDisposables.Add(http);
            async Task<JsonElement> Post(string path, object body)
            {
                var res = await http.PostAsync(path, new StringContent(
                    JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));
                res.EnsureSuccessStatusCode();
                using var doc = await JsonDocument.ParseAsync(await res.Content.ReadAsStreamAsync());
                return doc.RootElement.Clone();
            }

            // Pair a v2 room.
            var uuidA = Uuid('a'); var uuidB = Uuid('b');
            await Post("/queue", new { uuid = uuidA, name = "A", rating = 1000 });
            await Post("/queue", new { uuid = uuidB, name = "B", rating = 1000 });
            var pair = await Post("/v2/rooms/pair",
                new { uuidA, uuidB, dataVersion = "lab-2" });
            var roomId = pair.GetProperty("roomId").GetString()!;

            // Connect both relay clients.
            var wsBase = $"ws://127.0.0.1:{_relayPort}/rooms/{roomId}/ws?dataVersion=lab-2&seat=";
            await using var clientA = await RelayClient.ConnectAsync(new Uri(wsBase + uuidA), uuidA, roomId);
            await using var clientB = await RelayClient.ConnectAsync(new Uri(wsBase + uuidB), uuidB, roomId);

            var intentsA = new List<RelayIntent>();
            var intentsB = new List<RelayIntent>();
            var lockObj = new object();
            clientA.IntentReceived += i => { lock (lockObj) intentsA.Add(i); };
            clientB.IntentReceived += i => { lock (lockObj) intentsB.Add(i); };

            async Task WaitForSeqAsync(List<RelayIntent> list, long seq, int timeoutMs = 10000)
            {
                var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
                while (DateTime.UtcNow < deadline)
                {
                    lock (lockObj) { if (list.Any(i => i.Seq == seq)) return; }
                    await Task.Delay(25);
                }
                throw new TimeoutException($"Intent seq {seq} not received");
            }

            // Seed commit-reveal.
            var seedATcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var seedBTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            clientA.SeedReceived += s => seedATcs.TrySetResult(s);
            clientB.SeedReceived += s => seedBTcs.TrySetResult(s);
            await clientA.SendHelloAsync();
            await clientB.SendHelloAsync();
            await clientA.CommitSeedAsync("lockstep-seed-a", "salt-a");
            await clientB.CommitSeedAsync("lockstep-seed-b", "salt-b");
            await clientA.RevealSeedAsync("lockstep-seed-a", "salt-a");
            await clientB.RevealSeedAsync("lockstep-seed-b", "salt-b");
            var finalSeed = await seedATcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(finalSeed, await seedBTcs.Task.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal(64, finalSeed.Length);

            // Both bridges start from the same revealed seed (hex -> positive long).
            var bridgeSeed = long.Parse(finalSeed.Substring(0, 15), NumberStyles.HexNumber);
            await using var bridgeA = await BridgeDriver.StartAsync("java", _bridgeClasspath);
            await using var bridgeB = await BridgeDriver.StartAsync("java", _bridgeClasspath);
            _disposables.Add(bridgeA);
            _disposables.Add(bridgeB);

            var newFields = new Dictionary<string, object>
            {
                ["seed"] = bridgeSeed,
                ["human_player"] = 0,
                ["human_faction"] = "ZEUS",
                ["bot_faction"] = "POSEIDON",
                ["difficulty"] = "HERO",
            };
            var respA = await bridgeA.RequestAsync("new", newFields);
            var respB = await bridgeB.RequestAsync("new", newFields);
            Assert.Equal(
                respA.GetProperty("state").GetProperty("turn").GetInt64(),
                respB.GetProperty("state").GetProperty("turn").GetInt64());

            // Game loop: human acts flow through the relay; both bridges apply
            // the same sequenced intents; hashes must match every turn.
            var lastHashedTurn = 0L;
            var hashChecks = 0;
            var relayHashOk = 0;
            var seq = 0L;
            const int maxTurns = 6;
            while (true)
            {
                var legal = respA.GetProperty("legal");
                var phase = respA.GetProperty("state").GetProperty("phase").GetString();
                if (phase == "GAME_OVER") break;
                Assert.True(legal.GetArrayLength() > 0, "No legal actions outside GAME_OVER");
                var actionId = legal[0].GetProperty("id").GetString()!;

                seq++;
                await clientA.SendIntentAsync(actionId);
                await WaitForSeqAsync(intentsA, seq);
                await WaitForSeqAsync(intentsB, seq);

                respA = await bridgeA.RequestAsync("act",
                    new Dictionary<string, object> { ["action_id"] = actionId });
                respB = await bridgeB.RequestAsync("act",
                    new Dictionary<string, object> { ["action_id"] = actionId });
                Assert.Equal(
                    respA.GetProperty("revision").GetInt64(),
                    respB.GetProperty("revision").GetInt64());

                // Direct determinism check: identical hashes on both bridges.
                var hashA = (await bridgeA.RequestAsync("hash")).GetProperty("state_hash").GetString()!;
                var hashB = (await bridgeB.RequestAsync("hash")).GetProperty("state_hash").GetString()!;
                Assert.Equal(hashA, hashB);

                var turn = respA.GetProperty("state").GetProperty("turn").GetInt64();
                if (turn > lastHashedTurn && turn <= maxTurns)
                {
                    lastHashedTurn = turn;
                    hashChecks++;
                    // Full relay hash-exchange round for this turn.
                    var activePlayer = respA.GetProperty("state").GetProperty("active_player").GetInt64();
                    var activeSeat = activePlayer == 0 ? uuidA : uuidB;
                    var reqTcsA = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var reqTcsB = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var okTcsA = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
                    var okTcsB = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
                    void OnReq(long t) { reqTcsA.TrySetResult(t); reqTcsB.TrySetResult(t); }
                    void OnOk(long t) { okTcsA.TrySetResult(t); okTcsB.TrySetResult(t); }
                    clientA.HashRequested += OnReq; clientB.HashRequested += OnReq;
                    clientA.HashMatched += OnOk; clientB.HashMatched += OnOk;
                    try
                    {
                        await clientA.AnnounceTurnAsync(turn, activeSeat);
                        var reqTurn = await reqTcsA.Task.WaitAsync(TimeSpan.FromSeconds(10));
                        await reqTcsB.Task.WaitAsync(TimeSpan.FromSeconds(10));
                        await clientA.SendHashAsync(reqTurn, hashA);
                        await clientB.SendHashAsync(reqTurn, hashB);
                        Assert.Equal(reqTurn, await okTcsA.Task.WaitAsync(TimeSpan.FromSeconds(10)));
                        Assert.Equal(reqTurn, await okTcsB.Task.WaitAsync(TimeSpan.FromSeconds(10)));
                        relayHashOk++;
                    }
                    finally
                    {
                        clientA.HashRequested -= OnReq; clientB.HashRequested -= OnReq;
                        clientA.HashMatched -= OnOk; clientB.HashMatched -= OnOk;
                    }
                }
                if (turn > maxTurns) break;
            }

            Assert.True(hashChecks >= 2, $"Expected >= 2 turn hash checks, got {hashChecks}");
            Assert.Equal(hashChecks, relayHashOk);

            // Reconnect resume: drop B, play 2 more intents, B resumes from its lastSeq.
            var lastSeqB = clientB.LastSeq;
            var countBefore = 0;
            lock (lockObj) countBefore = intentsB.Count;
            await clientB.DisposeAsync();
            await Task.Delay(300);

            var legalNow = respA.GetProperty("legal");
            for (var k = 0; k < 2 && legalNow.GetArrayLength() > 0; k++)
            {
                var aid = legalNow[0].GetProperty("id").GetString()!;
                seq++;
                await clientA.SendIntentAsync(aid);
                await WaitForSeqAsync(intentsA, seq);
                respA = await bridgeA.RequestAsync("act",
                    new Dictionary<string, object> { ["action_id"] = aid });
                legalNow = respA.GetProperty("legal");
                if (respA.GetProperty("state").GetProperty("phase").GetString() == "GAME_OVER") break;
            }

            await using var clientB2 = await RelayClient.ConnectAsync(new Uri(wsBase + uuidB), uuidB, roomId);
            var welcomeTcs = new TaskCompletionSource<RelayWelcome>(TaskCreationOptions.RunContinuationsAsynchronously);
            clientB2.Welcomed += w => welcomeTcs.TrySetResult(w);
            await clientB2.SendHelloAsync(lastSeqB);
            var welcome = await welcomeTcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var missed = welcome.Log.Select(e => e.Seq).ToList();
            Assert.Equal(
                Enumerable.Range((int)lastSeqB + 1, (int)(seq - lastSeqB)).Select(i => (long)i).ToList(),
                missed);

            _out.WriteLine($"Lockstep OK: {seq} intents, {hashChecks} turn hash checks, " +
                $"reconnect replayed {missed.Count} intents, final turn " +
                $"{respA.GetProperty("state").GetProperty("turn").GetInt64()}");
        }
    }
}
