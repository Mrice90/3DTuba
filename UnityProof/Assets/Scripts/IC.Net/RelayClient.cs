using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("IC.Net.Tests")]

namespace IC.Net
{
    /// <summary>
    /// relay-1 client over WebSocket (System.Net.WebSockets.ClientWebSocket).
    /// Pure C#: no UnityEngine references, netstandard2.1, C# 9.
    ///
    /// Usage:
    ///   var uri = new Uri($"ws://{host}:{port}/rooms/{roomId}/ws?dataVersion=lab-2&seat={seat}");
    ///   await using var client = await RelayClient.ConnectAsync(uri, seat, roomId);
    ///   client.IntentReceived += i => ApplyToBridge(i.ActionId);
    ///   await client.SendHelloAsync();
    ///   await client.CommitSeedAsync(seed, salt);
    ///   await client.RevealSeedAsync(seed, salt);   // then SeedReceived fires with the final seed
    ///   await client.SendIntentAsync(actionId);
    ///   await client.AnnounceTurnAsync(turn, activeSeat);
    ///   // on HashRequested: await client.SendHashAsync(turn, bridgeHash);
    ///
    /// Events fire on the receive-loop thread (a thread-pool thread), NOT the
    /// Unity main thread: marshal to the main thread before touching Unity
    /// objects. All sends are serialized internally; concurrent Send* calls
    /// are safe.
    /// </summary>
    public sealed class RelayClient : IAsyncDisposable
    {
        public event Action<RelayWelcome>? Welcomed;
        public event Action<RelayIntent>? IntentReceived;
        public event Action<string>? SeedReceived;
        public event Action<long>? HashRequested;
        public event Action<long>? HashMatched;
        public event Action<RelayHashMismatch>? HashMismatched;
        public event Action<RelayTimer>? TimerReceived;
        public event Action<RelayForfeit>? Forfeited;
        public event Action<string>? ErrorReceived;
        public event Action<string>? ByeReceived;
        public event Action? Closed;

        public string Seat { get; }
        public string RoomId { get; }
        public long LastSeq => _lastSeq;

        private readonly IRelayTransport _transport;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _loopCts = new CancellationTokenSource();
        private readonly Task _loop;
        private long _lastSeq;
        private int _disposed;

        private RelayClient(IRelayTransport transport, string seat, string roomId)
        {
            _transport = transport;
            Seat = seat;
            RoomId = roomId;
            _loop = ReceiveLoopAsync(_loopCts.Token);
        }

        /// <summary>
        /// Opens the WebSocket to the relay upgrade path.
        /// wsUri: ws(s)://host/rooms/{roomId}/ws?dataVersion=lab-2&amp;seat={seat}
        /// </summary>
        public static async Task<RelayClient> ConnectAsync(Uri wsUri, string seat, string roomId, CancellationToken ct = default)
        {
            if (wsUri == null) throw new ArgumentNullException(nameof(wsUri));
            if (string.IsNullOrEmpty(seat)) throw new ArgumentException("seat is required", nameof(seat));
            var transport = new ClientWebSocketTransport();
            try
            {
                await transport.ConnectAsync(wsUri, ct).ConfigureAwait(false);
            }
            catch
            {
                await transport.DisposeAsync().ConfigureAwait(false);
                throw;
            }
            return new RelayClient(transport, seat, roomId);
        }

        /// <summary>
        /// Test seam: build a client over an injected transport (no real socket).
        /// </summary>
        internal static RelayClient StartForTest(IRelayTransport transport, string seat, string roomId)
        {
            if (transport == null) throw new ArgumentNullException(nameof(transport));
            return new RelayClient(transport, seat, roomId);
        }

        /// <summary>SHA-256 hex of "seed|salt" — the commit body. Matches relay.js.</summary>
        public static string CommitFor(string seed, string salt)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(seed + "|" + salt));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        public Task SendHelloAsync(long lastSeq = 0, CancellationToken ct = default) =>
            SendAsync(RelayMessages.Hello(Seat, lastSeq), ct);

        public Task CommitSeedAsync(string seed, string salt, CancellationToken ct = default) =>
            SendAsync(RelayMessages.SeedCommit(CommitFor(seed, salt)), ct);

        public Task RevealSeedAsync(string seed, string salt, CancellationToken ct = default) =>
            SendAsync(RelayMessages.SeedReveal(seed, salt), ct);

        public Task SendIntentAsync(string actionId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(actionId)) throw new ArgumentException("actionId is required", nameof(actionId));
            return SendAsync(RelayMessages.Intent(actionId), ct);
        }

        public Task AnnounceTurnAsync(long turn, string activeSeat, CancellationToken ct = default) =>
            SendAsync(RelayMessages.Turn(turn, activeSeat), ct);

        public Task SendHashAsync(long turn, string stateHash, CancellationToken ct = default) =>
            SendAsync(RelayMessages.Hash(turn, stateHash), ct);

        private async Task SendAsync(string text, CancellationToken ct)
        {
            ThrowIfDisposed();
            await _sendLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await _transport.SendTextAsync(text, ct).ConfigureAwait(false);
            }
            finally { _sendLock.Release(); }
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    string? text;
                    try
                    {
                        text = await _transport.ReceiveTextAsync(ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { break; }
                    if (text == null) break; // transport closed
                    Dispatch(text);
                }
            }
            catch (Exception)
            {
                // A protocol violation or transport failure ends the loop;
                // listeners see Closed. (Deliberately not rethrown: the loop
                // is fire-and-forget by design.)
            }
            finally
            {
                try { Closed?.Invoke(); } catch { /* listener errors must not kill the loop */ }
            }
        }

        private void Dispatch(string text)
        {
            Dictionary<string, object?> msg;
            try { msg = JsonLite.ParseObject(text); }
            catch (Exception ex) { throw new RelayProtocolException("Inbound message is not a JSON object", ex); }

            string type;
            try { type = RelayMessages.MessageType(msg); }
            catch (Exception ex) { throw new RelayProtocolException("Inbound message has no type", ex); }

            try
            {
                switch (type)
                {
                    case "welcome":
                        Welcomed?.Invoke(RelayMessages.ParseWelcome(msg));
                        break;
                    case "intent": {
                        var intent = RelayMessages.ParseIntent(msg);
                        if (intent.Seq > _lastSeq) _lastSeq = intent.Seq;
                        IntentReceived?.Invoke(intent);
                        break;
                    }
                    case "seed":
                        SeedReceived?.Invoke(JsonReq.Str(msg, "seed"));
                        break;
                    case "hash-request":
                        HashRequested?.Invoke(JsonReq.Long(msg, "turn"));
                        break;
                    case "hash-ok":
                        HashMatched?.Invoke(JsonReq.Long(msg, "turn"));
                        break;
                    case "hash-mismatch":
                        HashMismatched?.Invoke(RelayMessages.ParseHashMismatch(msg));
                        break;
                    case "timer":
                        TimerReceived?.Invoke(RelayMessages.ParseTimer(msg));
                        break;
                    case "forfeit":
                        Forfeited?.Invoke(RelayMessages.ParseForfeit(msg));
                        break;
                    case "error":
                        ErrorReceived?.Invoke(JsonReq.Str(msg, "reason"));
                        break;
                    case "bye":
                        ByeReceived?.Invoke(JsonReq.Str(msg, "reason"));
                        break;
                    default:
                        // Forward-compatible: ignore unknown server messages.
                        break;
                }
            }
            catch (Exception ex) when (!(ex is RelayProtocolException))
            {
                throw new RelayProtocolException($"Malformed '{type}' message", ex);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed != 0) throw new ObjectDisposedException(nameof(RelayClient));
        }

        public async ValueTask DisposeAsync()
        {
            if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _loopCts.Cancel();
            try { await _loop.ConfigureAwait(false); } catch { /* best effort */ }
            try { await _transport.DisposeAsync().ConfigureAwait(false); } catch { /* best effort */ }
            _loopCts.Dispose();
            _sendLock.Dispose();
        }
    }
}
