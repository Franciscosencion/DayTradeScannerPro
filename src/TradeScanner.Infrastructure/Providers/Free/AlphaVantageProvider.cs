using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using Microsoft.Extensions.Logging;
using TradeScanner.Core.Domain.Enums;
using TradeScanner.Core.Domain.ValueObjects;
using TradeScanner.Core.Interfaces;

namespace TradeScanner.Infrastructure.Providers.Free;

public class AlphaVantageProvider(IHttpClientFactory httpFactory, ILogger<AlphaVantageProvider> logger) : IMarketDataProvider
{
    private readonly HttpClient _http = httpFactory.CreateClient("AlphaVantage");
    private string _apiKey = string.Empty;
    private const string BaseUrl = "https://www.alphavantage.co/query";

    public MarketDataProvider ProviderType => MarketDataProvider.AlphaVantage;
    public string DisplayName => "Alpha Vantage";
    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);
    public int Priority => 5;

    public void SetApiKey(string key) => _apiKey = key;

    public async Task<Quote?> GetQuoteAsync(string symbol, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetFromJsonAsync<AvGlobalQuoteResponse>(
                $"{BaseUrl}?function=GLOBAL_QUOTE&symbol={symbol}&apikey={_apiKey}", ct);
            var gq = resp?.GlobalQuote;
            if (gq == null || string.IsNullOrEmpty(gq.Symbol)) return null;

            var price = ParseDecimal(gq.Price);
            var prev = ParseDecimal(gq.PreviousClose);
            var change = ParseDecimal(gq.Change);
            var pct = ParseDecimal(gq.ChangePercent?.TrimEnd('%').Trim());

            return new Quote(symbol, price, ParseDecimal(gq.Open), ParseDecimal(gq.High),
                ParseDecimal(gq.Low), prev, ParseLong(gq.Volume), pct, change, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AlphaVantage GetQuote failed for {Symbol}", symbol);
            return null;
        }
    }

    public async Task<IReadOnlyList<Quote>> GetQuotesAsync(IEnumerable<string> symbols, CancellationToken ct = default)
    {
        // Free tier: 25 requests/day — limit batch size
        var tasks = symbols.Take(25).Select(s => GetQuoteAsync(s, ct));
        var results = await Task.WhenAll(tasks);
        return results.Where(q => q != null).Cast<Quote>().ToList();
    }

    public async Task<IReadOnlyList<PriceBar>> GetHistoricalBarsAsync(string symbol, string interval, int count, CancellationToken ct = default)
    {
        try
        {
            var avInterval = interval switch
            {
                "1m" => "1min", "5m" => "5min", "15m" => "15min", "1h" => "60min", _ => "5min"
            };

            var url = $"{BaseUrl}?function=TIME_SERIES_INTRADAY&symbol={symbol}&interval={avInterval}&outputsize=compact&apikey={_apiKey}";
            using var rawResp = await _http.GetAsync(url, ct);
            rawResp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await rawResp.Content.ReadAsStringAsync(ct));

            var seriesKey = $"Time Series ({avInterval})";
            if (!doc.RootElement.TryGetProperty(seriesKey, out var series)) return [];

            var bars = new List<PriceBar>();
            foreach (var entry in series.EnumerateObject())
            {
                if (!DateTime.TryParse(entry.Name, out var dt)) continue;
                var v = entry.Value;
                bars.Add(new PriceBar(symbol, dt,
                    GetDecimal(v, "1. open"), GetDecimal(v, "2. high"),
                    GetDecimal(v, "3. low"), GetDecimal(v, "4. close"),
                    GetLong(v, "5. volume"), interval));
            }

            return bars.OrderBy(b => b.Timestamp).TakeLast(count).ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AlphaVantage GetHistoricalBars failed for {Symbol}", symbol);
            return [];
        }
    }

    public async Task<IReadOnlyList<string>> GetMostActiveSymbolsAsync(int count = 100, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetFromJsonAsync<AvTopMoversResponse>(
                $"{BaseUrl}?function=TOP_GAINERS_LOSERS&apikey={_apiKey}", ct);
            if (resp?.MostActivelyTraded?.Count > 0)
                return resp.MostActivelyTraded.Take(count)
                    .Select(t => t.Ticker ?? "").Where(s => s.Length > 0).ToList();
        }
        catch { }

        return ["AAPL","MSFT","NVDA","TSLA","AMD","META","AMZN","GOOGL","SPY","QQQ",
                "NFLX","CRM","PLTR","SOFI","RIVN","NIO","BABA","SNAP","PINS","IWM"];
    }

    public async Task<bool> ValidateApiKeyAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync(
                $"{BaseUrl}?function=GLOBAL_QUOTE&symbol=AAPL&apikey={_apiKey}", ct);
            if (!resp.IsSuccessStatusCode) return false;
            var json = await resp.Content.ReadAsStringAsync(ct);
            return !json.Contains("Invalid API call") && !json.Contains("premium endpoint");
        }
        catch { return false; }
    }

    private static decimal ParseDecimal(string? s) =>
        decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;

    private static long ParseLong(string? s) => long.TryParse(s, out var l) ? l : 0;

    private static decimal GetDecimal(JsonElement el, string key) =>
        el.TryGetProperty(key, out var p) ? ParseDecimal(p.GetString()) : 0;

    private static long GetLong(JsonElement el, string key) =>
        el.TryGetProperty(key, out var p) ? ParseLong(p.GetString()) : 0;

    private record AvGlobalQuoteResponse([property: JsonPropertyName("Global Quote")] AvGlobalQuote? GlobalQuote);
    private record AvGlobalQuote(
        [property: JsonPropertyName("01. symbol")] string? Symbol,
        [property: JsonPropertyName("02. open")] string? Open,
        [property: JsonPropertyName("03. high")] string? High,
        [property: JsonPropertyName("04. low")] string? Low,
        [property: JsonPropertyName("05. price")] string? Price,
        [property: JsonPropertyName("06. volume")] string? Volume,
        [property: JsonPropertyName("08. previous close")] string? PreviousClose,
        [property: JsonPropertyName("09. change")] string? Change,
        [property: JsonPropertyName("10. change percent")] string? ChangePercent);
    private record AvTopMoversResponse(
        [property: JsonPropertyName("most_actively_traded")] List<AvTicker>? MostActivelyTraded);
    private record AvTicker([property: JsonPropertyName("ticker")] string? Ticker);
}
