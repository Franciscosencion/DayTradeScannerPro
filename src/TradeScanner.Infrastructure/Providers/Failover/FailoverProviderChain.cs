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
        // Collect from ALL enabled providers in parallel and merge into a deduped universe.
        // Dynamic API sources (FMP /actives, AlphaVantage TOP_GAINERS_LOSERS) contribute
        // today's actual movers; hardcoded universes (Finnhub, TwelveData) fill in the rest.
        var providers = factory.GetEnabledProviders();
        var tasks = providers.Select(p => TryGetSymbolsAsync(p, count, ct));
        var allLists = await Task.WhenAll(tasks);

        var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var list in allLists)
            foreach (var s in list)
                merged.Add(s);

        logger.LogInformation("Symbol universe: {Count} unique symbols from {Providers} providers",
            merged.Count, providers.Count(p => p.IsAvailable));

        return merged.Take(count).ToList();
    }

    private async Task<IReadOnlyList<string>> TryGetSymbolsAsync(IMarketDataProvider p, int count, CancellationToken ct)
    {
        try { return await p.GetMostActiveSymbolsAsync(count, ct); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Provider {Provider} failed GetMostActiveSymbols", p.DisplayName);
            return [];
        }
    }

    public Task<bool> ValidateApiKeyAsync(CancellationToken ct = default) => Task.FromResult(IsAvailable);
}
