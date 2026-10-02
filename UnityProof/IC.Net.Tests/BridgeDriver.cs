using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace IC.Net.Tests
{
    /// <summary>
    /// Tiny test-only stdio driver for the rules-bridge jar (AI-079, protocol
    /// v1.1.0). Spawns `java -cp &lt;classes&gt; RulesBridge`, speaks JSON
    /// lines, and returns parsed responses. NOT part of the shipped library.
    /// </summary>
    internal sealed class BridgeDriver : IAsyncDisposable
    {
        private readonly Process _proc;
        private readonly StreamWriter _stdin;
        private readonly StreamReader _stdout;
        private int _seq;

        private BridgeDriver(Process proc)
        {
            _proc = proc;
            _stdin = proc.StandardInput;
            _stdout = proc.StandardOutput;
        }

        public static Task<BridgeDriver> StartAsync(string javaBin, string classpath)
        {
            var psi = new ProcessStartInfo(javaBin, $"-cp \"{classpath}\" RulesBridge")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Could not start the rules bridge");
            return Task.FromResult(new BridgeDriver(proc));
        }

        /// <summary>Sends one request; returns the parsed JSON response.</summary>
        public async Task<JsonElement> RequestAsync(string op, Dictionary<string, object>? fields = null)
        {
            var id = "t" + Interlocked.Increment(ref _seq);
            var payload = new Dictionary<string, object?> { ["id"] = id, ["op"] = op };
            if (fields != null)
                foreach (var kv in fields) payload[kv.Key] = kv.Value;
            var line = JsonSerializer.Serialize(payload);
            await _stdin.WriteLineAsync(line);
            await _stdin.FlushAsync();
            var response = await _stdout.ReadLineAsync();
            if (response == null)
                throw new InvalidOperationException("Bridge closed stdout unexpectedly");
            using var doc = JsonDocument.Parse(response);
            AssertId(doc.RootElement, id);
            return doc.RootElement.Clone();
        }

        private static void AssertId(JsonElement root, string id)
        {
            if (!root.TryGetProperty("id", out var got) || got.GetString() != id)
                throw new InvalidOperationException($"Bridge response id mismatch (want {id})");
            if (root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.False)
            {
                var err = root.TryGetProperty("error", out var e) ? e.GetString() : "?";
                throw new InvalidOperationException($"Bridge error: {err}");
            }
        }

        public bool HasExited => _proc.HasExited;

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!_proc.HasExited)
                {
                    _stdin.Close();
                    await Task.Delay(500);
                    if (!_proc.HasExited) _proc.Kill();
                }
            }
            catch { /* best effort */ }
            _proc.Dispose();
        }
    }
}
