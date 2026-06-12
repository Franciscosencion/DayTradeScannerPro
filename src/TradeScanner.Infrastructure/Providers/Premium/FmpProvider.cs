using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TradeScanner.Core.Domain.Enums;
using TradeScanner.Core.Domain.ValueObjects;
using TradeScanner.Core.Interfaces;

namespace TradeScanner.Infrastructure.Providers.Premium;

public class FmpProvider(IHttpClientFactory httpFactory, ILogger<FmpProvider> logger) : IMarketDataProvider
{
    private readonly HttpClient _http = httpFactory.CreateClient("FMP");
    private string _apiKey = string.Empty;
    private const string BaseUrl = "https://financialmodelingprep.com/api/v3";

    public MarketDataProvider ProviderType => MarketDataProvider.FinancialModelingPrep;
    public string DisplayName => "Financial Modeling Prep";
    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);
    public int Priority => 3;

    public void SetApiKey(string key) => _apiKey = key;

    public async Task<Quote?> GetQuoteAsync(string symbol, CancellationToken ct = default)
    {
        try
        {
            var quotes = await _http.GetFromJsonAsync<List<FmpQuote>>(
                $"{BaseUrl}/quote/{symbol}?apikey={_apiKey}", ct);
            var q = quotes?.FirstOrDefault();
            if (q == null) return null;
            return new Quote(symbol, (decimal)q.Price, (decimal)q.Open,
                (decimal)q.DayHigh, (decimal)q.DayLow, (decimal)q.PreviousClose,
                (long)q.Volume, (decimal)q.ChangesPercentage, (decimal)q.Change, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FMP GetQuote failed for {Symbol}", symbol);
            return null;
        }
    }

    public async Task<IReadOnlyList<Quote>> GetQuotesAsync(IEnumerable<string> symbols, CancellationToken ct = default)
    {
        try
        {
            var tickers = string.Join(",", symbols);
            var quotes = await _http.GetFromJsonAsync<List<FmpQuote>>(
                $"{BaseUrl}/quote/{tickers}?apikey={_apiKey}", ct);
            return quotes?.Select(q => new Quote(
                q.Symbol ?? "", (decimal)q.Price, (decimal)q.Open,
                (decimal)q.DayHigh, (decimal)q.DayLow, (decimal)q.PreviousClose,
                (long)q.Volume, (decimal)q.ChangesPercentage, (decimal)q.Change, DateTime.UtcNow))
                .ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FMP GetQuotes failed");
            return [];
        }
    }

    public async Task<IReadOnlyList<PriceBar>> GetHistoricalBarsAsync(string symbol, string interval, int count, CancellationToken ct = default)
    {
        try
        {
            var fmpInterval = interval switch
            {
                "1m" => "1min", "5m" => "5min", "15m" => "15min",
                "1h" => "1hour", "1d" => "1day", _ => "5min"
            };

            var bars = await _http.GetFromJsonAsync<List<FmpBar>>(
                $"{BaseUrl}/historical-chart/{fmpInterval}/{symbol}?apikey={_apiKey}", ct);

            return bars?.Select(b => new PriceBar(symbol,
                DateTime.Parse(b.Date ?? DateTime.UtcNow.ToString("o")),
                (decimal)b.Open, (decimal)b.High, (decimal)b.Low, (decimal)b.Close,
                (long)b.Volume, interval))
                .OrderBy(b => b.Timestamp).TakeLast(count).ToList() ?? [];
        }
        catch { return []; }
    }

    public async Task<IReadOnlyList<string>> GetMostActiveSymbolsAsync(int count = 100, CancellationToken ct = default)
    {
        try
        {
            var actives = await _http.GetFromJsonAsync<List<FmpActive>>(
                $"{BaseUrl}/actives?apikey={_apiKey}", ct);
            if (actives?.Count > 0)
                return actives.Take(count).Select(a => a.Ticker ?? "").Where(s => s.Length > 0).ToList();
        }
        catch { }
        return ["AAPL","MSFT","NVDA","TSLA","AMD","META","AMZN","GOOGL","SPY","QQQ"];
    }

    public async Task<bool> ValidateApiKeyAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync($"{BaseUrl}/quote/AAPL?apikey={_apiKey}", ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private record FmpQuote(
        [property: JsonPropertyName("symbol")] string? Symbol,
        [property: JsonPropertyName("price")] double Price,
        [property: JsonPropertyName("changesPercentage")] double ChangesPercentage,
        [property: JsonPropertyName("change")] double Change,
        [property: JsonPropertyName("dayLow")] double DayLow,
        [property: JsonPropertyName("dayHigh")] double DayHigh,
        [property: JsonPropertyName("volume")] double Volume,
        [property: JsonPropertyName("open")] double Open,
        [property: JsonPropertyName("previousClose")] double PreviousClose);

    private record FmpBar(
        [property: JsonPropertyName("date")] string? Date,
        [property: JsonPropertyName("open")] double Open,
        [property: JsonPropertyName("high")] double High,
        [property: JsonPropertyName("low")] double Low,
        [property: JsonPropertyName("close")] double Close,
        [property: JsonPropertyName("volume")] double Volume);

    private record FmpActive([property: JsonPropertyName("ticker")] string? Ticker);
}
