using TradeScanner.Core.Domain.Enums;
using TradeScanner.Core.Domain.ValueObjects;

namespace TradeScanner.Core.Interfaces;

public interface IRealtimeStreamingService
{
    bool IsConnected { get; }
    event EventHandler<RealtimeTrade>? TradeReceived;
    event EventHandler<string>? StatusChanged;
    Task StartAsync(string apiKey, MarketDataProvider provider, IEnumerable<string> symbols, CancellationToken ct = default);
    Task SubscribeAsync(IEnumerable<string> symbols);
    Task StopAsync();
}
