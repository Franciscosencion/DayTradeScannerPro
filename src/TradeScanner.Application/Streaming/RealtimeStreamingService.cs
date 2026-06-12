using Microsoft.Extensions.Logging;
using TradeScanner.Core.Domain.Enums;
using TradeScanner.Core.Domain.ValueObjects;
using TradeScanner.Core.Interfaces;
using TradeScanner.Infrastructure.Streaming;

namespace TradeScanner.Application.Streaming;

public sealed class RealtimeStreamingService(
    PolygonWebSocketClient polygon,
    FinnhubWebSocketClient finnhub,
    ILogger<RealtimeStreamingService> logger) : IRealtimeStreamingService, IAsyncDisposable
{
    private MarketDataProvider _activeProvider;
    private string _savedApiKey = string.Empty;
    private List<string> _savedSymbols = [];
    private int _retryCount;
    private bool _running;

    public bool IsConnected => _activeProvider switch
    {
        MarketDataProvider.PolygonIo => polygon.IsConnected,
        MarketDataProvider.Finnhub   => finnhub.IsConnected,
        _                            => false
    };

    public event EventHandler<RealtimeTrade>? TradeReceived;
    public event EventHandler<string>? StatusChanged;

    public async Task StartAsync(
        string apiKey,
        MarketDataProvider provider,
        IEnumerable<string> symbols,
        CancellationToken ct = default)
    {
        if (_running) await StopAsync();

        _activeProvider = provider;
        _savedApiKey = apiKey;
        _savedSymbols = symbols.ToList();
        _retryCount = 0;
        _running = true;

        WireEvents(provider, subscribe: true);

        switch (provider)
        {
            case MarketDataProvider.PolygonIo:
                await polygon.ConnectAsync(apiKey, _savedSymbols, ct);
                break;
            case MarketDataProvider.Finnhub:
                await finnhub.ConnectAsync(apiKey, _savedSymbols, ct);
                break;
            default:
                logger.LogWarning("Provider {Provider} does not support WebSocket streaming", provider);
                StatusChanged?.Invoke(this, $"{provider} does not support WebSocket streaming");
                _running = false;
                WireEvents(provider, subscribe: false);
                break;
        }
    }

    public async Task SubscribeAsync(IEnumerable<string> symbols)
    {
        var list = symbols.ToList();

        // Track for reconnect
        foreach (var s in list.Where(s => !_savedSymbols.Contains(s)))
            _savedSymbols.Add(s);

        switch (_activeProvider)
        {
            case MarketDataProvider.PolygonIo: await polygon.SubscribeAsync(list); break;
            case MarketDataProvider.Finnhub:   await finnhub.SubscribeAsync(list);  break;
        }
    }

    public async Task StopAsync()
    {
        _running = false;
        WireEvents(_activeProvider, subscribe: false);
        switch (_activeProvider)
        {
            case MarketDataProvider.PolygonIo: await polygon.DisconnectAsync(); break;
            case MarketDataProvider.Finnhub:   await finnhub.DisconnectAsync();  break;
        }
        StatusChanged?.Invoke(this, "Streaming stopped");
    }

    public ValueTask DisposeAsync() => new(StopAsync());

    // -------------------------------------------------------------------------

    private void WireEvents(MarketDataProvider provider, bool subscribe)
    {
        switch (provider)
        {
            case MarketDataProvider.PolygonIo:
                if (subscribe)
                {
                    polygon.TradeReceived   += OnTrade;
                    polygon.StatusChanged   += OnStatus;
                    polygon.ConnectionLost  += OnConnectionLost;
                }
                else
                {
                    polygon.TradeReceived   -= OnTrade;
                    polygon.StatusChanged   -= OnStatus;
                    polygon.ConnectionLost  -= OnConnectionLost;
                }
                break;

            case MarketDataProvider.Finnhub:
                if (subscribe)
                {
                    finnhub.TradeReceived   += OnTrade;
                    finnhub.StatusChanged   += OnStatus;
                    finnhub.ConnectionLost  += OnConnectionLost;
                }
                else
                {
                    finnhub.TradeReceived   -= OnTrade;
                    finnhub.StatusChanged   -= OnStatus;
                    finnhub.ConnectionLost  -= OnConnectionLost;
                }
                break;
        }
    }

    private void OnTrade(object? s, RealtimeTrade t) => TradeReceived?.Invoke(this, t);
    private void OnStatus(object? s, string m) => StatusChanged?.Invoke(this, m);

    private async void OnConnectionLost(object? s, EventArgs e)
    {
        if (!_running) return;

        // Exponential backoff: 2, 4, 8, 16, 32, 60, 60, 60 …
        var delaySeconds = Math.Min(2 << Math.Min(_retryCount, 5), 60);
        _retryCount++;

        logger.LogWarning("Streaming connection lost (attempt {N}). Retrying in {Delay}s", _retryCount, delaySeconds);
        StatusChanged?.Invoke(this, $"Connection lost — reconnecting in {delaySeconds}s (attempt {_retryCount})...");

        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        if (!_running) return;

        try
        {
            switch (_activeProvider)
            {
                case MarketDataProvider.PolygonIo:
                    await polygon.ConnectAsync(_savedApiKey, _savedSymbols);
                    break;
                case MarketDataProvider.Finnhub:
                    await finnhub.ConnectAsync(_savedApiKey, _savedSymbols);
                    break;
            }

            _retryCount = 0;
            StatusChanged?.Invoke(this, $"{_activeProvider} streaming: reconnected");
            logger.LogInformation("Streaming reconnected to {Provider}", _activeProvider);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Reconnect attempt {N} failed", _retryCount);
            // ConnectionLost will fire again from the failed client, triggering another retry
        }
    }
}
