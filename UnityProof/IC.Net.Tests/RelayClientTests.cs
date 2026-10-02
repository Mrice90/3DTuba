using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace IC.Net.Tests
{
    /// <summary>
    /// In-memory fake relay-1 transport: scripted server, no TCP. Lets the
    /// client's framing, parsing, dispatch and sequencing be tested
    /// deterministically (and in sandboxes without loopback).
    /// </summary>
    internal sealed class FakeTransport : IRelayTransport
    {
        private readonly ConcurrentQueue<string> _inbound = new ConcurrentQueue<string>();
        private readonly SemaphoreSlim _inboundSignal = new SemaphoreSlim(0);
        private readonly ConcurrentQueue<string> _sent = new ConcurrentQueue<string>();
        private readonly SemaphoreSlim _sentSignal = new SemaphoreSlim(0);
        private volatile bool _closed;

        public Task ConnectAsync(Uri uri, CancellationToken ct) => Task.CompletedTask;

        public Task SendTextAsync(string text, CancellationToken ct)
        {
            _sent.Enqueue(text);
            _sentSignal.Release();
            return Task.CompletedTask;
        }

        public async Task<string?> ReceiveTextAsync(CancellationToken ct)
        {
            while (true)
            {
                await _inboundSignal.WaitAsync(ct).ConfigureAwait(false);
                if (_inbound.TryDequeue(out var text)) return text;
                if (_closed) return null;
            }
        }

        // --- test driver side ---

        public void ServerSend(string text)
        {
            _inbound.Enqueue(text);
            _inboundSignal.Release();
        }

        public async Task<string> TakeSentAsync(int timeoutMs = 5000)
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            try
            {
                await _sentSignal.WaitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("Client sent nothing within " + timeoutMs + "ms");
            }
            if (_sent.TryDequeue(out var text)) return text;
            throw new TimeoutException("Client sent nothing within " + timeoutMs + "ms");
        }

        public string TakeSent(int timeoutMs = 5000) =>
            TakeSentAsync(timeoutMs).GetAwaiter().GetResult();

        public void ServerClose()
        {
            _closed = true;
            _inboundSignal.Release();
        }

        public ValueTask DisposeAsync()
        {
            _closed = true;
            _inboundSignal.Release();
            return default;
        }
    }

    /// <summary>
    /// RelayClient + protocol unit tests. No Java, no node, no TCP — pure
    /// client protocol behavior against the fake transport.
    /// </summary>
    public sealed class RelayClientTests : IAsyncLifetime
    {
        private readonly List<IAsyncDisposable> _disposables = new List<IAsyncDisposable>();

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            for (var i = _disposables.Count - 1; i >= 0; i--)
                try { await _disposables[i].DisposeAsync(); } catch { /* best effort */ }
        }

        private RelayClient NewClient(out FakeTransport transport,
            string seat = "seat-a", string roomId = "ROOM1")
        {
            transport = new FakeTransport();
            _disposables.Add(transport);
            var client = RelayClient.StartForTest(transport, seat, roomId);
            _disposables.Add(client);
            return client;
        }

        private static Task<T> EventAsync<T>(Action<Action<T>> subscribe)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            subscribe(v => tcs.TrySetResult(v));
            return tcs.Task;
        }

        private static async Task<T> WithTimeout<T>(Task<T> task, int ms = 5000)
        {
            var winner = await Task.WhenAny(task, Task.Delay(ms));
            Assert.Same(task, winner);
            return await task;
        }

        private static Dictionary<string, object?> ParseObj(string text) =>
            JsonLite.ParseObject(text);

        [Fact]
        public async Task Hello_WelcomeParsed()
        {
            var client = NewClient(out var transport);

            var welcomeTask = EventAsync<RelayWelcome>(h => client.Welcomed += h);
            await client.SendHelloAsync();

            var hello = ParseObj(transport.TakeSent());
            Assert.Equal("hello", hello["type"]);
            Assert.Equal("seat-a", hello["seat"]);
            Assert.Equal(0L, hello["lastSeq"]);

            transport.ServerSend(
                "{\"type\":\"welcome\",\"protocol\":\"relay-1\",\"seat\":\"seat-a\"," +
                "\"roomId\":\"ROOM1\",\"phase\":\"play\",\"turn\":3,\"seed\":null," +
                "\"log\":[{\"seq\":1,\"seat\":\"seat-a\",\"actionId\":\"r0-a1\"}," +
                "{\"seq\":2,\"seat\":\"seat-b\",\"actionId\":\"r0-a2\"}]}");
            var welcome = await WithTimeout(welcomeTask);

            Assert.Equal("seat-a", welcome.Seat);
            Assert.Equal("ROOM1", welcome.RoomId);
            Assert.Equal("play", welcome.Phase);
            Assert.Equal(3, welcome.Turn);
            Assert.Equal(2, welcome.Log.Count);
            Assert.Equal(1, welcome.Log[0].Seq);
            Assert.Equal("r0-a2", welcome.Log[1].ActionId);
        }

        [Fact]
        public async Task Hello_WithLastSeq_SendsResumeOffset()
        {
            var client = NewClient(out var transport);
            await client.SendHelloAsync(7);
            var hello = ParseObj(transport.TakeSent());
            Assert.Equal(7L, hello["lastSeq"]);
        }

        [Fact]
        public async Task CommitSeed_SendsSha256OfSeedPipeSalt()
        {
            var client = NewClient(out var transport);
            await client.CommitSeedAsync("my-seed", "my-salt");

            var msg = ParseObj(transport.TakeSent());
            Assert.Equal("seed-commit", msg["type"]);

            using var sha = SHA256.Create();
            var expected = BitConverter.ToString(
                sha.ComputeHash(Encoding.UTF8.GetBytes("my-seed|my-salt")))
                .Replace("-", "").ToLowerInvariant();
            Assert.Equal(expected, msg["commit"]);
            Assert.Equal(expected, RelayClient.CommitFor("my-seed", "my-salt"));
        }

        [Fact]
        public async Task RevealSeed_SendsSeedAndSalt()
        {
            var client = NewClient(out var transport);
            await client.RevealSeedAsync("my-seed", "my-salt");
            var msg = ParseObj(transport.TakeSent());
            Assert.Equal("seed-reveal", msg["type"]);
            Assert.Equal("my-seed", msg["seed"]);
            Assert.Equal("my-salt", msg["salt"]);
        }

        [Fact]
        public async Task Intent_BroadcastReceivedInOrder_LastSeqTracked()
        {
            var client = NewClient(out var transport);

            var intents = new List<RelayIntent>();
            var both = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.IntentReceived += i => { intents.Add(i); if (intents.Count == 2) both.TrySetResult(true); };

            await client.SendIntentAsync("r0-a1");
            var sent = ParseObj(transport.TakeSent());
            Assert.Equal("intent", sent["type"]);
            Assert.Equal("r0-a1", sent["actionId"]);

            transport.ServerSend("{\"type\":\"intent\",\"seq\":1,\"seat\":\"seat-a\",\"actionId\":\"r0-a1\"}");
            transport.ServerSend("{\"type\":\"intent\",\"seq\":2,\"seat\":\"seat-b\",\"actionId\":\"r0-a2\"}");
            await WithTimeout(both.Task);

            Assert.Equal(1, intents[0].Seq);
            Assert.Equal(2, intents[1].Seq);
            Assert.Equal("r0-a2", intents[1].ActionId);
            Assert.Equal(2, client.LastSeq);
        }

        [Fact]
        public async Task SeedBroadcast_Received()
        {
            var client = NewClient(out var transport);
            var seedTask = EventAsync<string>(h => client.SeedReceived += h);
            transport.ServerSend("{\"type\":\"seed\",\"seed\":\"" + new string('c', 64) + "\"}");
            Assert.Equal(new string('c', 64), await WithTimeout(seedTask));
        }

        [Fact]
        public async Task HashExchange_RequestRespondMatch()
        {
            var client = NewClient(out var transport);

            var reqTask = EventAsync<long>(h => client.HashRequested += h);
            var okTask = EventAsync<long>(h => client.HashMatched += h);

            await client.AnnounceTurnAsync(5, "seat-a");
            var turn = ParseObj(transport.TakeSent());
            Assert.Equal("turn", turn["type"]);
            Assert.Equal(5L, turn["turn"]);
            Assert.Equal("seat-a", turn["activeSeat"]);

            transport.ServerSend("{\"type\":\"hash-request\",\"turn\":4}");
            Assert.Equal(4, await WithTimeout(reqTask));

            await client.SendHashAsync(4, new string('a', 64));
            var hash = ParseObj(transport.TakeSent());
            Assert.Equal("hash", hash["type"]);
            Assert.Equal(4L, hash["turn"]);
            Assert.Equal(new string('a', 64), hash["hash"]);

            transport.ServerSend("{\"type\":\"hash-ok\",\"turn\":4}");
            Assert.Equal(4, await WithTimeout(okTask));
        }

        [Fact]
        public async Task HashMismatch_RaisesEventWithHashes()
        {
            var client = NewClient(out var transport);
            var mmTask = EventAsync<RelayHashMismatch>(h => client.HashMismatched += h);
            transport.ServerSend(
                "{\"type\":\"hash-mismatch\",\"turn\":2," +
                "\"hashes\":{\"seat-a\":\"" + new string('a', 64) + "\"," +
                "\"seat-b\":\"" + new string('b', 64) + "\"}}");
            var mm = await WithTimeout(mmTask);
            Assert.Equal(2, mm.Turn);
            Assert.Equal(new string('a', 64), mm.Hashes["seat-a"]);
            Assert.Equal(new string('b', 64), mm.Hashes["seat-b"]);
        }

        [Fact]
        public async Task Timer_And_Forfeit_Parsed()
        {
            var client = NewClient(out var transport);
            var timerTask = EventAsync<RelayTimer>(h => client.TimerReceived += h);
            var forfeitTask = EventAsync<RelayForfeit>(h => client.Forfeited += h);

            transport.ServerSend("{\"type\":\"timer\",\"seat\":\"seat-b\",\"msRemaining\":29000}");
            var timer = await WithTimeout(timerTask);
            Assert.Equal("seat-b", timer.Seat);
            Assert.Equal(29000, timer.MsRemaining);

            transport.ServerSend("{\"type\":\"forfeit\",\"seat\":\"seat-b\",\"reason\":\"timeout\"}");
            var forfeit = await WithTimeout(forfeitTask);
            Assert.Equal("seat-b", forfeit.Seat);
            Assert.Equal("timeout", forfeit.Reason);
        }

        [Fact]
        public async Task Error_And_Bye_RaiseEvents()
        {
            var client = NewClient(out var transport);
            var errTask = EventAsync<string>(h => client.ErrorReceived += h);
            var byeTask = EventAsync<string>(h => client.ByeReceived += h);

            transport.ServerSend("{\"type\":\"error\",\"reason\":\"bad seat\"}");
            Assert.Equal("bad seat", await WithTimeout(errTask));
            transport.ServerSend("{\"type\":\"bye\",\"reason\":\"room closed\"}");
            Assert.Equal("room closed", await WithTimeout(byeTask));
        }

        [Fact]
        public async Task UnknownMessageType_Ignored()
        {
            var client = NewClient(out var transport);
            var errored = false;
            client.ErrorReceived += _ => errored = true;
            transport.ServerSend("{\"type\":\"frobnicate\",\"x\":1}");
            await Task.Delay(300);
            Assert.False(errored);
        }

        [Fact]
        public async Task MalformedMessage_ClosesClient()
        {
            var client = NewClient(out var transport);
            var closedTask = EventAsync<object?>(h => client.Closed += () => h(null));
            transport.ServerSend("this is not json");
            await WithTimeout(closedTask); // fail-fast: loop ends, Closed fires
        }

        [Fact]
        public async Task ConcurrentSends_AreSerialized()
        {
            var client = NewClient(out var transport);
            var tasks = new List<Task>();
            for (var i = 0; i < 20; i++)
                tasks.Add(client.SendIntentAsync("r0-a" + i));
            await Task.WhenAll(tasks);
            var seen = new HashSet<string>();
            for (var i = 0; i < 20; i++)
                seen.Add((string)ParseObj(transport.TakeSent())["actionId"]!);
            Assert.Equal(20, seen.Count); // every send got exactly one well-formed line
        }
    }

    /// <summary>JsonLite + RelayMessages unit tests (no transport at all).</summary>
    public sealed class ProtocolCodecTests
    {
        [Fact]
        public void Json_RoundTrip_AllShapes()
        {
            var obj = JsonLite.Obj(
                ("s", (object?)"hi \"there\" \\ \n ☃"),
                ("i", (object?)42L),
                ("neg", (object?)-7L),
                ("f", (object?)1.5),
                ("b", (object?)true),
                ("n", (object?)null),
                ("arr", (object?)new List<object?> { "a", 1L, false, null }),
                ("nested", (object?)JsonLite.Obj(("x", (object?)"y"))));
            var text = JsonLite.Stringify(obj);
            var back = JsonLite.ParseObject(text);
            Assert.Equal("hi \"there\" \\ \n ☃", back["s"]);
            Assert.Equal(42L, back["i"]);
            Assert.Equal(-7L, back["neg"]);
            Assert.Equal(1.5, (double)back["f"]!);
            Assert.Equal(true, back["b"]);
            Assert.Null(back["n"]);
            var arr = Assert.IsType<List<object?>>(back["arr"]);
            Assert.Equal(4, arr.Count);
            Assert.Equal("a", arr[0]);
            var nested = Assert.IsType<Dictionary<string, object?>>(back["nested"]);
            Assert.Equal("y", nested["x"]);
        }

        [Fact]
        public void Json_Escapes_And_Unicode()
        {
            var back = JsonLite.ParseObject("{\"a\":\"\\u00e9\\uD83D\\uDE00\"}");
            Assert.Equal("é😀", back["a"]);
        }

        [Fact]
        public void Json_Rejects_Garbage()
        {
            Assert.Throws<JsonLite.JsonException>(() => JsonLite.ParseObject("{"));
            Assert.Throws<JsonLite.JsonException>(() => JsonLite.ParseObject("[1,]"));
            Assert.Throws<JsonLite.JsonException>(() => JsonLite.ParseObject("{\"a\":}"));
        }

        [Fact]
        public void OutboundMessages_AreWellFormed()
        {
            var hello = JsonLite.ParseObject(RelayMessages.Hello("s1", 9));
            Assert.Equal("hello", hello["type"]);
            Assert.Equal("s1", hello["seat"]);
            Assert.Equal(9L, hello["lastSeq"]);

            var commit = JsonLite.ParseObject(RelayMessages.SeedCommit(new string('d', 64)));
            Assert.Equal("seed-commit", commit["type"]);
            Assert.Equal(new string('d', 64), commit["commit"]);

            var reveal = JsonLite.ParseObject(RelayMessages.SeedReveal("seed", "salt"));
            Assert.Equal("seed-reveal", reveal["type"]);

            var intent = JsonLite.ParseObject(RelayMessages.Intent("r3-a12"));
            Assert.Equal("r3-a12", intent["actionId"]);

            var turn = JsonLite.ParseObject(RelayMessages.Turn(6, "s2"));
            Assert.Equal(6L, turn["turn"]);
            Assert.Equal("s2", turn["activeSeat"]);

            var hash = JsonLite.ParseObject(RelayMessages.Hash(6, new string('e', 64)));
            Assert.Equal(6L, hash["turn"]);
            Assert.Equal(new string('e', 64), hash["hash"]);
        }
    }
}
