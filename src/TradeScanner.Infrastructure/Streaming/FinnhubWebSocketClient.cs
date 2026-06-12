using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradeScanner.Core.Domain.ValueObjects;

namespace TradeScanner.Infrastructure.Streaming;

/// <summary>
/// Thin wrapper around Finnhub WebSocket trade feed.
/// Protocol: connect wss://ws.finnhub.io?token=KEY → subscribe per symbol → receive trades.
/// </summary>
public sealed class FinnhubWebSocketClient(ILogger<FinnhubWebSocketClient> logger) : IAsyncDisposable
{
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private Task? _receiveLoop;
    private readonly List<string> _subscribed = [];

    public event EventHandler<RealtimeTrade>? TradeReceived;
    public event EventHandler<string>? StatusChanged;
    public event EventHandler? ConnectionLost;
    public bool IsConnected => _ws?.State == WebSocketState.Open;

    public async Task ConnectAsync(string apiKey, IEnumerable<string> symbols, CancellationToken ct = default)
    {
        await DisconnectAsync();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ws = new ClientWebSocket();

        var uri = new Uri($"wss://ws.finnhub.io?token={Uri.EscapeDataString(apiKey)}");
        logger.LogInformation("Finnhub WS connecting...");
        StatusChanged?.Invoke(this, "Connecting to Finnhub...");

        await _ws.ConnectAsync(uri, _cts.Token);

        logger.LogInformation("Finnhub WS connected");
        StatusChanged?.Invoke(this, "Finnhub streaming: connected");

        var symbolList = symbols.ToList();
        if (symbolList.Count > 0)
            await SubscribeAsync(symbolList, _cts.Token);

        _receiveLoop = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
    }

    public async Task SubscribeAsync(IEnumerable<string> symbols, CancellationToken ct = default)
    {
        if (_ws?.State != WebSocketState.Open) return;
        foreach (var sym in symbols)
        {
            await SendJsonAsync(new { type = "subscribe", symbol = sym }, ct);
            _subscribed.Add(sym);
        }
    }

    public async Task UnsubscribeAsync(IEnumerable<string> symbols, CancellationToken ct = default)
    {
        if (_ws?.State != WebSocketState.Open) return;
        foreach (var sym in symbols)
        {
            await SendJsonAsync(new { type = "unsubscribe", symbol = sym }, ct);
            _subscribed.Remove(sym);
        }
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
        _subscribed.Clear();
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
                logger.LogWarning(ex, "Finnhub WS receive error");
                StatusChanged?.Invoke(this, "Finnhub streaming: disconnected");
                break;
            }
        }

        if (!(_cts?.IsCancellationRequested ?? true))
            ConnectionLost?.Invoke(this, EventArgs.Empty);
    }

    private void ProcessMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeEl)) return;
            if (typeEl.GetString() != "trade") return;
            if (!root.TryGetProperty("data", out var data)) return;

            foreach (var trade in data.EnumerateArray())
            {
                var symbol = trade.TryGetProperty("s", out var sEl) ? sEl.GetString() : null;
                if (string.IsNullOrEmpty(symbol)) continue;

                var price = trade.TryGetProperty("p", out var pEl) ? pEl.GetDecimal() : 0m;
                var volume = trade.TryGetProperty("v", out var vEl) ? (long)vEl.GetDouble() : 0L;
                var tsMs = trade.TryGetProperty("t", out var tEl) ? tEl.GetInt64() : 0L;
                var ts = tsMs > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(tsMs).UtcDateTime : DateTime.UtcNow;

                TradeReceived?.Invoke(this, new RealtimeTrade(symbol, price, volume, ts));
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Finnhub WS message parse error");
        }
    }

    private async Task SendJsonAsync<T>(T payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws!.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
    }
}
