using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TradeScanner.Core.Domain.Enums;
using TradeScanner.Core.Domain.ValueObjects;
using TradeScanner.Core.Interfaces;

namespace TradeScanner.Infrastructure.Providers.Free;

// Fetches today's most-active and top-gaining stocks from Yahoo Finance predefined screeners.
// No API key required — uses a browser User-Agent. Returns full quote data from a single
// pair of HTTP calls, so GetQuotesAsync is served entirely from an in-memory cache.
public class YahooScreenerProvider(IHttpClientFactory httpFactory, ILogger<YahooScreenerProvider> logger) : IMarketDataProvider
{
    private readonly HttpClient _http = httpFactory.CreateClient("YahooScreener");
    private readonly Dictionary<string, Quote> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _fetchLock = new(1, 1);
    private DateTime _cacheTime = DateTime.MinValue;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(45);

    private const string ScreenerBase =
        "https://query1.finance.yahoo.com/v1/finance/screener/predefined/saved";

    public MarketDataProvider ProviderType => MarketDataProvider.YahooFinanceScreener;
    public string DisplayName => "Yahoo Finance Screener";
    public bool IsAvailable => true; // no key — always enabled
    public int Priority => 1;        // first after Polygon (which 403s on free plan)
    public void SetApiKey(string key) { }

    public async Task<IReadOnlyList<string>> GetMostActiveSymbolsAsync(int count = 100, CancellationToken ct = default)
    {
        await _fetchLock.WaitAsync(ct);
        try
        {
            // most_actives + day_gainers in parallel — 2 HTTP calls, no auth needed
            var activesTask = FetchScreenerAsync("most_actives", 50, ct);
            var gainersTask = FetchScreenerAsync("day_gainers", 50, ct);
            await Task.WhenAll(activesTask, gainersTask);

            var merged = new Dictionary<string, Quote>(StringComparer.OrdinalIgnoreCase);
            foreach (var q in activesTask.Result.Concat(gainersTask.Result))
                merged[q.Symbol] = q;

            if (merged.Count == 0) return [];

            _cache.Clear();
            foreach (var kv in merged) _cache[kv.Key] = kv.Value;
            _cacheTime = DateTime.UtcNow;

            logger.LogInformation("Yahoo screener: {Total} symbols ({A} most-active, {G} gainers)",
                merged.Count, activesTask.Result.Count, gainersTask.Result.Count);

            return merged.Keys.Take(count).ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Yahoo screener GetMostActiveSymbols failed");
            return [];
        }
        finally { _fetchLock.Release(); }
    }

    public Task<IReadOnlyList<Quote>> GetQuotesAsync(IEnumerable<string> symbols, CancellationToken ct = default)
    {
        if (DateTime.UtcNow - _cacheTime > CacheTtl || _cache.Count == 0)
            return Task.FromResult<IReadOnlyList<Quote>>([]);

        var results = symbols
            .Where(s => _cache.ContainsKey(s))
            .Select(s => _cache[s])
            .ToList();

        return Task.FromResult<IReadOnlyList<Quote>>(results);
    }

    public Task<Quote?> GetQuoteAsync(string symbol, CancellationToken ct = default)
    {
        if (DateTime.UtcNow - _cacheTime > CacheTtl) return Task.FromResult<Quote?>(null);
        _cache.TryGetValue(symbol, out var q);
        return Task.FromResult(q);
    }

    public Task<IReadOnlyList<PriceBar>> GetHistoricalBarsAsync(
        string symbol, string interval, int count, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PriceBar>>([]);

    public Task<bool> ValidateApiKeyAsync(CancellationToken ct = default) => Task.FromResult(true);

    private async Task<List<Quote>> FetchScreenerAsync(string scrId, int count, CancellationToken ct)
    {
        var url = $"{ScreenerBase}?scrIds={scrId}&count={count}";
        var resp = await _http.GetFromJsonAsync<YfScreenerEnvelope>(url, ct);
        return resp?.Finance?.Result?[0]?.Quotes?
            .Where(q => !string.IsNullOrEmpty(q.Symbol) && q.Price > 0)
            .Select(q => new Quote(
                q.Symbol!,
                q.Price,
                q.Open ?? q.Price,
                q.DayHigh ?? q.Price,
                q.DayLow ?? q.Price,
                q.PreviousClose ?? (q.Price - (q.Change ?? 0)),
                q.Volume ?? 0,
                q.ChangePercent ?? 0,
                q.Change ?? 0,
                DateTime.UtcNow))
            .ToList() ?? [];
    }

    private record YfScreenerEnvelope(
        [property: JsonPropertyName("finance")] YfScreenerFinance? Finance);
    private record YfScreenerFinance(
        [property: JsonPropertyName("result")] List<YfScreenerResult>? Result);
    private record YfScreenerResult(
        [property: JsonPropertyName("quotes")] List<YfScreenerQuote>? Quotes);
    private record YfScreenerQuote(
        [property: JsonPropertyName("symbol")] string? Symbol,
        [property: JsonPropertyName("regularMarketPrice")] decimal Price,
        [property: JsonPropertyName("regularMarketChange")] decimal? Change,
        [property: JsonPropertyName("regularMarketChangePercent")] decimal? ChangePercent,
        [property: JsonPropertyName("regularMarketVolume")] long? Volume,
        [property: JsonPropertyName("regularMarketOpen")] decimal? Open,
        [property: JsonPropertyName("regularMarketDayHigh")] decimal? DayHigh,
        [property: JsonPropertyName("regularMarketDayLow")] decimal? DayLow,
        [property: JsonPropertyName("regularMarketPreviousClose")] decimal? PreviousClose);
}
