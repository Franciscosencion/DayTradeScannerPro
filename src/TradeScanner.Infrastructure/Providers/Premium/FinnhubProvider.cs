using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TradeScanner.Core.Domain.Enums;
using TradeScanner.Core.Domain.ValueObjects;
using TradeScanner.Core.Interfaces;

namespace TradeScanner.Infrastructure.Providers.Premium;

public class FinnhubProvider(IHttpClientFactory httpFactory, ILogger<FinnhubProvider> logger) : IMarketDataProvider, INewsProvider
{
    private readonly HttpClient _http = httpFactory.CreateClient("Finnhub");
    private readonly SemaphoreSlim _throttle = new(20, 20); // limit concurrent quote calls
    private string _apiKey = string.Empty;
    private const string BaseUrl = "https://finnhub.io/api/v1";

    public MarketDataProvider ProviderType => MarketDataProvider.Finnhub;
    public string DisplayName => "Finnhub";
    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);
    public int Priority => 2;

    public void SetApiKey(string apiKey)
    {
        _apiKey = apiKey;
        _http.DefaultRequestHeaders.Remove("X-Finnhub-Token");
        _http.DefaultRequestHeaders.Add("X-Finnhub-Token", apiKey);
    }

    public async Task<Quote?> GetQuoteAsync(string symbol, CancellationToken ct = default)
    {
        try
        {
            var q = await _http.GetFromJsonAsync<FinnhubQuote>($"{BaseUrl}/quote?symbol={symbol}", ct);
            if (q == null || q.C == 0) return null;
            return new Quote(symbol, (decimal)q.C, (decimal)q.O, (decimal)q.H, (decimal)q.L,
                (decimal)q.Pc, 0, q.Dp ?? 0, (decimal)q.D, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Finnhub GetQuote failed for {Symbol}", symbol);
            return null;
        }
    }

    public async Task<IReadOnlyList<Quote>> GetQuotesAsync(IEnumerable<string> symbols, CancellationToken ct = default)
    {
        var tasks = symbols.Select(async s =>
        {
            await _throttle.WaitAsync(ct);
            try { return await GetQuoteAsync(s, ct); }
            finally { _throttle.Release(); }
        });
        var results = await Task.WhenAll(tasks);
        return results.Where(q => q != null).Cast<Quote>().ToList();
    }

    public async Task<IReadOnlyList<PriceBar>> GetHistoricalBarsAsync(string symbol, string interval, int count, CancellationToken ct = default)
    {
        try
        {
            var resolution = interval switch { "1m" => "1", "5m" => "5", "1h" => "60", "1d" => "D", _ => "5" };
            var to = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var from = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeSeconds();
            var resp = await _http.GetFromJsonAsync<FinnhubCandles>(
                $"{BaseUrl}/stock/candle?symbol={symbol}&resolution={resolution}&from={from}&to={to}", ct);

            if (resp?.S != "ok" || resp.T == null) return [];

            return resp.T.Select((t, i) => new PriceBar(
                symbol,
                DateTimeOffset.FromUnixTimeSeconds(t).UtcDateTime,
                (decimal)resp.O![i], (decimal)resp.H![i], (decimal)resp.L![i], (decimal)resp.C![i],
                (long)resp.V![i], interval)).TakeLast(count).ToList();
        }
        catch { return []; }
    }

    public async Task<IReadOnlyList<string>> GetMostActiveSymbolsAsync(int count = 100, CancellationToken ct = default)
    {
        // Finnhub has no "most active" endpoint — return expanded hardcoded day-trading universe
        IReadOnlyList<string> universe =
        [
            // Mega-cap tech
            "AAPL","MSFT","NVDA","AMZN","GOOGL","META","TSLA","AVGO","ORCL","NFLX",
            // Large-cap tech / semiconductors
            "AMD","CRM","ADBE","INTC","QCOM","MU","AMAT","MRVL","TXN","KLAC",
            // Financials
            "JPM","BAC","V","MA","GS","MS","AXP","WFC","C","SCHW",
            // Healthcare
            "UNH","JNJ","LLY","PFE","ABBV","MRK","AMGN","BMY","GILD","CVS",
            // Consumer / retail
            "WMT","COST","HD","TGT","NKE","SBUX","MCD","LOW","CMG","TJX",
            // Energy
            "XOM","CVX","COP","OXY","SLB",
            // High-liquidity ETFs
            "SPY","QQQ","IWM","DIA","GLD","TLT","XLF","XLE","SQQQ","TQQQ",
            // High-beta / popular day-trade names
            "PLTR","SOFI","RIVN","LCID","NIO","BABA","COIN","HOOD","MARA","RIOT",
            // Growth / fintech / cloud
            "UPST","RBLX","UBER","SQ","PYPL","NET","DDOG","CRWD","ZS","SNOW",
            // Additional active names
            "SNAP","PINS","JD","AFRM","LYFT","SHOP","ABNB","OKTA","TSM","ARKK",
            // International / sector
            "BIDU","HUBS","BILL","PATH","MNDY",
        ];
        return await Task.FromResult(universe.Take(count).ToList());
    }

    public async Task<bool> ValidateApiKeyAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync($"{BaseUrl}/quote?symbol=AAPL", ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<IReadOnlyList<NewsItem>> GetNewsAsync(string symbol, int count = 10, CancellationToken ct = default)
    {
        try
        {
            var from = DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd");
            var to = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var items = await _http.GetFromJsonAsync<List<FinnhubNews>>(
                $"{BaseUrl}/company-news?symbol={symbol}&from={from}&to={to}", ct);

            return items?.Take(count).Select(n => new NewsItem(
                n.Id.ToString(), symbol, n.Headline ?? "", n.Summary ?? "",
                n.Source ?? "", n.Url ?? "", n.Sentiment ?? 0,
                DateTimeOffset.FromUnixTimeSeconds(n.Datetime).UtcDateTime)).ToList() ?? [];
        }
        catch { return []; }
    }

    public async Task<IReadOnlyList<NewsItem>> GetMarketNewsAsync(int count = 20, CancellationToken ct = default)
    {
        try
        {
            var items = await _http.GetFromJsonAsync<List<FinnhubNews>>($"{BaseUrl}/news?category=general", ct);
            return items?.Take(count).Select(n => new NewsItem(
                n.Id.ToString(), "MARKET", n.Headline ?? "", n.Summary ?? "",
                n.Source ?? "", n.Url ?? "", 0,
                DateTimeOffset.FromUnixTimeSeconds(n.Datetime).UtcDateTime)).ToList() ?? [];
        }
        catch { return []; }
    }

    // Response DTOs
    private record FinnhubQuote(
        [property: JsonPropertyName("c")] double C,
        [property: JsonPropertyName("d")] double D,
        [property: JsonPropertyName("dp")] decimal? Dp,
        [property: JsonPropertyName("h")] double H,
        [property: JsonPropertyName("l")] double L,
        [property: JsonPropertyName("o")] double O,
        [property: JsonPropertyName("pc")] double Pc);

    private record FinnhubCandles(
        [property: JsonPropertyName("s")] string? S,
        [property: JsonPropertyName("t")] List<long>? T,
        [property: JsonPropertyName("o")] List<double>? O,
        [property: JsonPropertyName("h")] List<double>? H,
        [property: JsonPropertyName("l")] List<double>? L,
        [property: JsonPropertyName("c")] List<double>? C,
        [property: JsonPropertyName("v")] List<double>? V);

    private record FinnhubNews(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("headline")] string? Headline,
        [property: JsonPropertyName("summary")] string? Summary,
        [property: JsonPropertyName("source")] string? Source,
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("datetime")] long Datetime,
        [property: JsonPropertyName("sentiment")] double? Sentiment);
}
