using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradeScanner.Core.Domain.ValueObjects;

namespace TradeScanner.Infrastructure.Streaming;

/// <summary>
/// Thin wrapper around Polygon.io Stocks WebSocket feed.
/// Protocol: connect → auth → subscribe to T.* (trades) → receive loop.
/// </summary>
public sealed class PolygonWebSocketClient(ILogger<PolygonWebSocketClient> logger) : IAsyncDisposable
{
    private static readonly Uri WsUri = new("wss://socket.polygon.io/stocks");

    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private Task? _receiveLoop;

    public event EventHandler<RealtimeTrade>? TradeReceived;
    public event EventHandler<string>? StatusChanged;
    public event EventHandler? ConnectionLost;
    public bool IsConnected => _ws?.State == WebSocketState.Open;

    public async Task ConnectAsync(string apiKey, IEnumerable<string> symbols, CancellationToken ct = default)
    {
        await DisconnectAsync();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ws = new ClientWebSocket();

        logger.LogInformation("Polygon WS connecting...");
        StatusChanged?.Invoke(this, "Connecting to Polygon...");

        await _ws.ConnectAsync(WsUri, _cts.Token);

        // Wait for "connected" status message before authenticating
        await WaitForStatusAsync("connected", _cts.Token);

        await SendJsonAsync(new { action = "auth", @params = apiKey }, _cts.Token);
        await WaitForStatusAsync("auth_success", _cts.Token);

        logger.LogInformation("Polygon WS authenticated");
        StatusChanged?.Invoke(this, "Polygon streaming: connected");

        var symbolList = symbols.ToList();
        if (symbolList.Count > 0)
            await SubscribeAsync(symbolList, _cts.Token);

        _receiveLoop = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
    }

    public async Task SubscribeAsync(IEnumerable<string> symbols, CancellationToken ct = default)
    {
        if (_ws?.State != WebSocketState.Open) return;
        var @params = string.Join(",", symbols.Select(s => $"T.{s}"));
        await SendJsonAsync(new { action = "subscribe", @params }, ct);
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel();
        if (_receiveLoop != null)
        {
            try { await _receiveLoop.ConfigureAwait(false); } catch { }
            _receiveLoop = null;
        }
        if (_ws != null)
        {
            try
            {
                if (_ws.State == WebSocketState.Open)
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "disconnect", CancellationToken.None);
            }
            catch { }
            _ws.Dispose();
            _ws = null;
        }
        _cts?.Dispose();
        _cts = null;
    }

    public ValueTask DisposeAsync() => new(DisconnectAsync());

    // -------------------------------------------------------------------------

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[65536];
        var segment = new ArraySegment<byte>(buffer);
        using var ms = new MemoryStream();

        while (!ct.IsCancellationRequested && _ws?.State == WebSocketState.Open)
        {
            try
            {
                ms.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await _ws.ReceiveAsync(segment, ct);
                    if (result.MessageType == WebSocketMessageType.Close) return;
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                ProcessMessage(Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length));
            }
            catch (OperationCanceledException) { break; }
            catch (WebSocketException ex)
            {
                logger.LogWarning(ex, "Polygon WS receive error");
                StatusChanged?.Invoke(this, "Polygon streaming: disconnected");
                break;
            }
        }

        // Fire ConnectionLost only for unexpected drops, not clean cancellations
        if (!(_cts?.IsCancellationRequested ?? true))
            ConnectionLost?.Invoke(this, EventArgs.Empty);
    }

    private void ProcessMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            // Polygon messages are JSON arrays
            if (root.ValueKind != JsonValueKind.Array) return;

            foreach (var evt in root.EnumerateArray())
            {
                if (!evt.TryGetProperty("ev", out var evEl)) continue;
                var ev = evEl.GetString();

                if (ev == "T")
                    HandleTrade(evt);
                else if (ev == "status")
                    HandleStatus(evt);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Polygon WS message parse error");
        }
    }

    private void HandleTrade(JsonElement evt)
    {
        if (!evt.TryGetProperty("sym", out var symEl)) return;
        var symbol = symEl.GetString();
        if (string.IsNullOrEmpty(symbol)) return;

        var price = evt.TryGetProperty("p", out var pEl) ? pEl.GetDecimal() : 0m;
        var size = evt.TryGetProperty("s", out var sEl) ? sEl.GetInt64() : 0L;
        var tsMs = evt.TryGetProperty("t", out var tEl) ? tEl.GetInt64() : 0L;
        var ts = tsMs > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(tsMs).UtcDateTime : DateTime.UtcNow;

        TradeReceived?.Invoke(this, new RealtimeTrade(symbol, price, size, ts));
    }

    private void HandleStatus(JsonElement evt)
    {
        var status = evt.TryGetProperty("status", out var sEl) ? sEl.GetString() : null;
        var msg = evt.TryGetProperty("message", out var mEl) ? mEl.GetString() : status;
        if (!string.IsNullOrEmpty(msg))
            logger.LogDebug("Polygon status: {Msg}", msg);
    }

    private async Task WaitForStatusAsync(string expectedStatus, CancellationToken ct)
    {
        var buffer = new byte[4096];
        var segment = new ArraySegment<byte>(buffer);
        using var ms = new MemoryStream();

        while (!ct.IsCancellationRequested)
        {
            ms.SetLength(0);
            WebSocketReceiveResult result;
            do
            {
                result = await _ws!.ReceiveAsync(segment, ct);
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            var text = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
            if (text.Contains($"\"status\":\"{expectedStatus}\"", StringComparison.Ordinal))
                return;

            // auth_failed → throw
            if (text.Contains("\"auth_failed\"", StringComparison.Ordinal))
                throw new InvalidOperationException("Polygon authentication failed — check your API key");
        }
    }

    private async Task SendJsonAsync<T>(T payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws!.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
    }
}
