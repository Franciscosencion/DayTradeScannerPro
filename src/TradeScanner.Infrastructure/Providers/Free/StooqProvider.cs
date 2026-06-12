using System.Globalization;
using Microsoft.Extensions.Logging;
using TradeScanner.Core.Domain.Enums;
using TradeScanner.Core.Domain.ValueObjects;
using TradeScanner.Core.Interfaces;

namespace TradeScanner.Infrastructure.Providers.Free;

/// <summary>
/// Stooq.com — free, no API key, daily data only.
/// Quote uses last 2 daily bars to compute change-from-prev-close.
/// </summary>
public class StooqProvider(IHttpClientFactory httpFactory, ILogger<StooqProvider> logger) : IMarketDataProvider
{
    private readonly HttpClient _http = CreateClient(httpFactory);
    private const string BaseUrl = "https://stooq.com";

    private static HttpClient CreateClient(IHttpClientFactory factory)
    {
        var client = factory.CreateClient("Stooq");
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
        return client;
    }

    public MarketDataProvider ProviderType => MarketDataProvider.Stooq;
    public string DisplayName => "Stooq (Free)";
    public bool IsAvailable => true;
    public int Priority => 6;

    public async Task<Quote?> GetQuoteAsync(string symbol, CancellationToken ct = default)
    {
        // Fetch the last 2 daily bars to get today's price and previous close
        var bars = await GetDailyBarsAsync(symbol, 2, ct);
        if (bars.Count == 0) return null;

        var latest = bars[^1];
        var prevClose = bars.Count > 1 ? bars[^2].Close : latest.Open;
        var change = latest.Close - prevClose;
        var changePct = prevClose != 0 ? change / prevClose * 100 : 0;

        return new Quote(symbol, latest.Close, latest.Open, latest.High, latest.Low,
            prevClose, latest.Volume, changePct, change, latest.Timestamp);
    }

    public async Task<IReadOnlyList<Quote>> GetQuotesAsync(IEnumerable<string> symbols, CancellationToken ct = default)
    {
        var tasks = symbols.Select(s => GetQuoteAsync(s, ct));
        var results = await Task.WhenAll(tasks);
        return results.Where(q => q != null).Cast<Quote>().ToList();
    }

    public async Task<IReadOnlyList<PriceBar>> GetHistoricalBarsAsync(string symbol, string interval, int count, CancellationToken ct = default)
    {
        // Stooq only provides daily/weekly/monthly — return empty for intraday
        if (interval is not ("1d" or "1w" or "1m"))
            return [];

        return await GetDailyBarsAsync(symbol, count, ct, interval);
    }

    public Task<IReadOnlyList<string>> GetMostActiveSymbolsAsync(int count = 100, CancellationToken ct = default)
    {
        var universe = new[]
        {
            "AAPL","MSFT","NVDA","TSLA","META","AMZN","GOOGL","AMD","NFLX","CRM",
            "PLTR","SOFI","RIVN","LCID","NIO","BABA","JD","SNAP","PINS","UBER",
            "SPY","QQQ","IWM","DIA","GLD","SLV","USO","TLT","HYG","XLF"
        };
        return Task.FromResult<IReadOnlyList<string>>(universe.Take(count).ToArray());
    }

    public Task<bool> ValidateApiKeyAsync(CancellationToken ct = default) => Task.FromResult(true);

    private async Task<IReadOnlyList<PriceBar>> GetDailyBarsAsync(
        string symbol, int count, CancellationToken ct, string interval = "1d")
    {
        try
        {
            var stooqInterval = interval switch { "1w" => "w", "1m" => "m", _ => "d" };
            var to = DateTime.UtcNow;
            // Request extra days to account for weekends and holidays
            var from = to.AddDays(-(count * 2 + 30));
            var sym = Uri.EscapeDataString($"{symbol}.us");
            var url = $"{BaseUrl}/q/d/l/?s={sym}&d1={from:yyyyMMdd}&d2={to:yyyyMMdd}&i={stooqInterval}";

            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return [];

            var csv = await resp.Content.ReadAsStringAsync(ct);
            return ParseCsv(symbol, csv, interval, count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Stooq GetHistoricalBars failed for {Symbol}", symbol);
            return [];
        }
    }

    private static IReadOnlyList<PriceBar> ParseCsv(string symbol, string csv, string interval, int count)
    {
        var bars = new List<PriceBar>();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // First line is header: Date,Open,High,Low,Close,Volume
        foreach (var line in lines.Skip(1))
        {
            var parts = line.Trim().Split(',');
            if (parts.Length < 5) continue;
            if (!DateTime.TryParseExact(parts[0], "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) continue;

            if (!TryParseDecimal(parts[1], out var open)) continue;
            if (!TryParseDecimal(parts[2], out var high)) continue;
            if (!TryParseDecimal(parts[3], out var low)) continue;
            if (!TryParseDecimal(parts[4], out var close)) continue;
            var volume = parts.Length > 5 && long.TryParse(parts[5].Trim(), out var vol) ? vol : 0;

            bars.Add(new PriceBar(symbol, date, open, high, low, close, volume, interval));
        }

        return bars.TakeLast(count).ToList();
    }

    private static bool TryParseDecimal(string s, out decimal value) =>
        decimal.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
}
