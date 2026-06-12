using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TradeScanner.Core.Domain.Enums;
using TradeScanner.Core.Domain.ValueObjects;
using TradeScanner.Core.Interfaces;

namespace TradeScanner.Infrastructure.Providers.Free;

public class YahooFinanceProvider(IHttpClientFactory httpFactory, ILogger<YahooFinanceProvider> logger) : IMarketDataProvider
{
    private readonly HttpClient _http = CreateClient(httpFactory);
    private const string BaseUrl = "https://query1.finance.yahoo.com/v8/finance";

    private static HttpClient CreateClient(IHttpClientFactory factory)
    {
        var client = factory.CreateClient("Yahoo");
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        return client;
    }

    public MarketDataProvider ProviderType => MarketDataProvider.YahooFinance;
    public string DisplayName => "Yahoo Finance (Free)";
    public bool IsAvailable => true;
    public int Priority => 10;

    public async Task<Quote?> GetQuoteAsync(string symbol, CancellationToken ct = default)
    {
        try
        {
            var url = $"{BaseUrl}/chart/{symbol}?interval=1d&range=2d";
            using var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            var result = doc.RootElement
                .GetProperty("chart").GetProperty("result")[0];
            var meta = result.GetProperty("meta");

            var price = meta.GetProperty("regularMarketPrice").GetDecimal();
            var prev = meta.GetProperty("previousClose").GetDecimal();
            var change = price - prev;
            var changePct = prev != 0 ? change / prev * 100 : 0;
            var volume = meta.TryGetProperty("regularMarketVolume", out var v) ? v.GetInt64() : 0;

            return new Quote(symbol, price, price, price, price, prev, volume, changePct, change, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Yahoo GetQuote failed for {Symbol}", symbol);
            return null;
        }
    }

    public async Task<IReadOnlyList<Quote>> GetQuotesAsync(IEnumerable<string> symbols, CancellationToken ct = default)
    {
        var tasks = symbols.Select(s => GetQuoteAsync(s, ct));
        var results = await Task.WhenAll(tasks);
        return results.Where(q => q != null).Cast<Quote>().ToList();
    }

    public async Task<IReadOnlyList<PriceBar>> GetHistoricalBarsAsync(string symbol, string interval, int count, CancellationToken ct = default)
    {
        try
        {
            var range = interval is "1d" ? "1y" : "60d";
            var url = $"{BaseUrl}/chart/{symbol}?interval={interval}&range={range}";
            using var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            var result = doc.RootElement.GetProperty("chart").GetProperty("result")[0];
            var timestamps = result.GetProperty("timestamp").EnumerateArray().ToList();
            var ohlcv = result.GetProperty("indicators").GetProperty("quote")[0];

            var opens = ohlcv.GetProperty("open").EnumerateArray().ToList();
            var highs = ohlcv.GetProperty("high").EnumerateArray().ToList();
            var lows = ohlcv.GetProperty("low").EnumerateArray().ToList();
            var closes = ohlcv.GetProperty("close").EnumerateArray().ToList();
            var volumes = ohlcv.GetProperty("volume").EnumerateArray().ToList();

            var bars = new List<PriceBar>();
            for (int i = 0; i < timestamps.Count; i++)
            {
                if (closes[i].ValueKind == JsonValueKind.Null) continue;
                bars.Add(new PriceBar(
                    symbol,
                    DateTimeOffset.FromUnixTimeSeconds(timestamps[i].GetInt64()).UtcDateTime,
                    opens[i].GetDecimal(), highs[i].GetDecimal(),
                    lows[i].GetDecimal(), closes[i].GetDecimal(),
                    volumes[i].ValueKind != JsonValueKind.Null ? volumes[i].GetInt64() : 0,
                    interval));
            }

            return bars.TakeLast(count).ToList();
        }
        catch { return []; }
    }

    public async Task<IReadOnlyList<string>> GetMostActiveSymbolsAsync(int count = 100, CancellationToken ct = default)
    {
        // Return a curated day-trade universe for free tier
        var universe = new[]
        {
            "AAPL","MSFT","NVDA","TSLA","META","AMZN","GOOGL","AMD","NFLX","CRM",
            "PLTR","SOFI","RIVN","LCID","NIO","BABA","JD","SNAP","PINS","TWTR",
            "SPY","QQQ","IWM","DIA","GLD","SLV","USO","TLT","HYG","XLF"
        };
        return await Task.FromResult<IReadOnlyList<string>>(universe.Take(count).ToArray());
    }

    public Task<bool> ValidateApiKeyAsync(CancellationToken ct = default) => Task.FromResult(true);
}
