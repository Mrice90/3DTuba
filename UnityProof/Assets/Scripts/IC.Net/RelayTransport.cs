using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IC.Net
{
    /// <summary>
    /// Transport seam for <see cref="RelayClient"/>. The production
    /// implementation is <see cref="ClientWebSocketTransport"/>; tests inject
    /// an in-memory fake so client protocol logic is verifiable without TCP.
    /// </summary>
    internal interface IRelayTransport : IAsyncDisposable
    {
        Task ConnectAsync(Uri uri, CancellationToken ct);
        Task SendTextAsync(string text, CancellationToken ct);

        /// <summary>
        /// Returns the next complete text message, or null when the
        /// transport has closed.
        /// </summary>
        Task<string?> ReceiveTextAsync(CancellationToken ct);
    }

    /// <summary>Production transport over System.Net.WebSockets.ClientWebSocket.</summary>
    internal sealed class ClientWebSocketTransport : IRelayTransport
    {
        private readonly ClientWebSocket _ws = new ClientWebSocket();
        private int _disposed;

        public Task ConnectAsync(Uri uri, CancellationToken ct) =>
            _ws.ConnectAsync(uri, ct);

        public async Task SendTextAsync(string text, CancellationToken ct)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            await _ws.SendAsync(new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
        }

        public async Task<string?> ReceiveTextAsync(CancellationToken ct)
        {
            var buf = new byte[8192];
            var sb = new StringBuilder();
            while (true)
            {
                WebSocketReceiveResult r;
                try
                {
                    r = await _ws.ReceiveAsync(new ArraySegment<byte>(buf), ct).ConfigureAwait(false);
                }
                catch (WebSocketException)
                {
                    return null;
                }
                if (r.MessageType == WebSocketMessageType.Close) return null;
                sb.Append(Encoding.UTF8.GetString(buf, 0, r.Count));
                if (r.EndOfMessage) return sb.ToString();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try
            {
                if (_ws.State == WebSocketState.Open)
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure,
                        "done", CancellationToken.None).ConfigureAwait(false);
            }
            catch { /* best effort */ }
            _ws.Dispose();
        }
    }
}
