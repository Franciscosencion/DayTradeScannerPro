using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using Microsoft.Extensions.Logging;
using TradeScanner.Core.Domain.Enums;
using TradeScanner.Core.Domain.ValueObjects;
using TradeScanner.Core.Interfaces;

namespace TradeScanner.Infrastructure.Providers.Free;

public class TwelveDataProvider(IHttpClientFactory httpFactory, ILogger<TwelveDataProvider> logger) : IMarketDataProvider
{
    private readonly HttpClient _http = httpFactory.CreateClient("TwelveData");
    private string _apiKey = string.Empty;
    private const string BaseUrl = "https://api.twelvedata.com";

    public MarketDataProvider ProviderType => MarketDataProvider.TwelveData;
    public string DisplayName => "Twelve Data";
    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);
    public int Priority => 4;

    public void SetApiKey(string key) => _apiKey = key;

    public async Task<Quote?> GetQuoteAsync(string symbol, CancellationToken ct = default)
    {
        try
        {
            var q = await _http.GetFromJsonAsync<TdQuote>(
                $"{BaseUrl}/quote?symbol={symbol}&apikey={_apiKey}", ct);
            if (q == null || q.Status == "error") return null;

            var price = D(q.Close);
            var prev = D(q.PreviousClose);
            return new Quote(symbol, price, D(q.Open), D(q.FiftyTwoWeek?.High),
                D(q.FiftyTwoWeek?.Low), prev, L(q.Volume),
                D(q.PercentChange), D(q.Change), DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "TwelveData GetQuote failed for {Symbol}", symbol);
            return null;
        }
    }

    public async Task<IReadOnlyList<Quote>> GetQuotesAsync(IEnumerable<string> symbols, CancellationToken ct = default)
    {
        // Free tier: ~8 requests/minute
        var tasks = symbols.Take(8).Select(s => GetQuoteAsync(s, ct));
        var results = await Task.WhenAll(tasks);
        return results.Where(q => q != null).Cast<Quote>().ToList();
    }

    public async Task<IReadOnlyList<PriceBar>> GetHistoricalBarsAsync(string symbol, string interval, int count, CancellationToken ct = default)
    {
        try
        {
            var tdInterval = interval switch
            {
                "1m" => "1min", "5m" => "5min", "15m" => "15min",
                "1h" => "1h", "1d" => "1day", _ => "5min"
            };

            var resp = await _http.GetFromJsonAsync<TdTimeSeries>(
                $"{BaseUrl}/time_series?symbol={symbol}&interval={tdInterval}&outputsize={count}&apikey={_apiKey}", ct);

            if (resp?.Status != "ok" || resp.Values == null) return [];

            return resp.Values.Select(v => new PriceBar(symbol,
                DateTime.Parse(v.Datetime ?? DateTime.UtcNow.ToString("o")),
                D(v.Open), D(v.High), D(v.Low), D(v.Close), L(v.Volume), interval))
                .OrderBy(b => b.Timestamp).ToList();
        }
        catch { return []; }
    }

    public async Task<IReadOnlyList<string>> GetMostActiveSymbolsAsync(int count = 100, CancellationToken ct = default) =>
        await Task.FromResult<IReadOnlyList<string>>(
        ["AAPL","MSFT","NVDA","TSLA","META","AMZN","GOOGL","AMD","NFLX","CRM",
         "PLTR","SOFI","RIVN","NIO","BABA","JD","SNAP","PINS","SPY","QQQ",
         "IWM","DIA","GLD","SLV","USO","TLT","XLF","ARKK","SQQQ","TQQQ"]);

    public async Task<bool> ValidateApiKeyAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync($"{BaseUrl}/quote?symbol=AAPL&apikey={_apiKey}", ct);
            if (!resp.IsSuccessStatusCode) return false;
            var body = await resp.Content.ReadAsStringAsync(ct);
            return !body.Contains("\"status\":\"error\"");
        }
        catch { return false; }
    }

    private static decimal D(string? s) =>
        decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;
    private static long L(string? s) => long.TryParse(s, out var l) ? l : 0;

    private record TdQuote(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("open")] string? Open,
        [property: JsonPropertyName("close")] string? Close,
        [property: JsonPropertyName("previous_close")] string? PreviousClose,
        [property: JsonPropertyName("change")] string? Change,
        [property: JsonPropertyName("percent_change")] string? PercentChange,
        [property: JsonPropertyName("volume")] string? Volume,
        [property: JsonPropertyName("fifty_two_week")] TdFiftyTwoWeek? FiftyTwoWeek);

    private record TdFiftyTwoWeek(
        [property: JsonPropertyName("high")] string? High,
        [property: JsonPropertyName("low")] string? Low);

    private record TdTimeSeries(
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("values")] List<TdBar>? Values);

    private record TdBar(
        [property: JsonPropertyName("datetime")] string? Datetime,
        [property: JsonPropertyName("open")] string? Open,
        [property: JsonPropertyName("high")] string? High,
        [property: JsonPropertyName("low")] string? Low,
        [property: JsonPropertyName("close")] string? Close,
        [property: JsonPropertyName("volume")] string? Volume);
}
