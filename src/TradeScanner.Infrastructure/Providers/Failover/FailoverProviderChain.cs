using Microsoft.Extensions.Logging;
using TradeScanner.Core.Domain.Enums;
using TradeScanner.Core.Domain.ValueObjects;
using TradeScanner.Core.Interfaces;

namespace TradeScanner.Infrastructure.Providers.Failover;

public class FailoverProviderChain(
    IProviderFactory factory,
    ILogger<FailoverProviderChain> logger) : IMarketDataProvider
{
    public MarketDataProvider ProviderType => MarketDataProvider.YahooFinance; // sentinel
    public string DisplayName => "Auto (Failover Chain)";
    public bool IsAvailable => factory.GetEnabledProviders().Count > 0;
    public int Priority => 0;

    public async Task<Quote?> GetQuoteAsync(string symbol, CancellationToken ct = default)
    {
        foreach (var provider in factory.GetEnabledProviders())
        {
            try
            {
                var quote = await provider.GetQuoteAsync(symbol, ct);
                if (quote?.IsValid == true) return quote;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Provider {Provider} failed for {Symbol}, trying next", provider.DisplayName, symbol);
            }
        }
        return null;
    }

    public async Task<IReadOnlyList<Quote>> GetQuotesAsync(IEnumerable<string> symbols, CancellationToken ct = default)
    {
        var symbolList = symbols.ToList();
        foreach (var provider in factory.GetEnabledProviders())
        {
            try
            {
                var quotes = await provider.GetQuotesAsync(symbolList, ct);
                if (quotes.Count > 0) return quotes;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Provider {Provider} failed GetQuotes, trying next", provider.DisplayName);
            }
        }
        return [];
    }

    public async Task<IReadOnlyList<PriceBar>> GetHistoricalBarsAsync(string symbol, string interval, int count, CancellationToken ct = default)
    {
        foreach (var provider in factory.GetEnabledProviders())
        {
            try
            {
                var bars = await provider.GetHistoricalBarsAsync(symbol, interval, count, ct);
                if (bars.Count > 0) return bars;
            }
            catch { }
        }
        return [];
    }

    public async Task<IReadOnlyList<string>> GetMostActiveSymbolsAsync(int count = 100, CancellationToken ct = default)
    {
        var provider = factory.GetBestAvailableProvider();
        return await provider.GetMostActiveSymbolsAsync(count, ct);
    }

    public Task<bool> ValidateApiKeyAsync(CancellationToken ct = default) => Task.FromResult(IsAvailable);
}
