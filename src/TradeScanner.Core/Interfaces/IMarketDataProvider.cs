using TradeScanner.Core.Domain.Enums;
using TradeScanner.Core.Domain.ValueObjects;

namespace TradeScanner.Core.Interfaces;

public interface IMarketDataProvider
{
    MarketDataProvider ProviderType { get; }
    string DisplayName { get; }
    bool IsAvailable { get; }
    int Priority { get; }

    Task<Quote?> GetQuoteAsync(string symbol, CancellationToken ct = default);
    Task<IReadOnlyList<Quote>> GetQuotesAsync(IEnumerable<string> symbols, CancellationToken ct = default);
    Task<IReadOnlyList<PriceBar>> GetHistoricalBarsAsync(string symbol, string interval, int count, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetMostActiveSymbolsAsync(int count = 100, CancellationToken ct = default);
    Task<bool> ValidateApiKeyAsync(CancellationToken ct = default);

    // Default no-op — providers that don't use API keys (Stooq, Yahoo) inherit this
    void SetApiKey(string key) { }
}
