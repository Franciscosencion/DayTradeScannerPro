using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TradeScanner.Core.Domain.Enums;
using TradeScanner.Core.Domain.ValueObjects;
using TradeScanner.Core.Interfaces;

namespace TradeScanner.Infrastructure.Providers.Premium;

public class PolygonProvider(IHttpClientFactory httpFactory, ILogger<PolygonProvider> logger) : IMarketDataProvider
{
    private readonly HttpClient _http = httpFactory.CreateClient("Polygon");
    private string _apiKey = string.Empty;

    public MarketDataProvider ProviderType => MarketDataProvider.PolygonIo;
    public string DisplayName => "Polygon.io";
    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);
    public int Priority => 1;

    public void SetApiKey(string apiKey) => _apiKey = apiKey;

    public async Task<Quote?> GetQuoteAsync(string symbol, CancellationToken ct = default)
    {
        try
        {
            var snap = await _http.GetFromJsonAsync<PolygonSnapshotResponse>(
                $"https://api.polygon.io/v2/snapshot/locale/us/markets/stocks/tickers/{symbol}?apiKey={_apiKey}", ct);

            if (snap?.Ticker == null) return null;
            var t = snap.Ticker;
            return new Quote(
                symbol,
                (decimal)(t.Day?.C ?? 0),
                (decimal)(t.Day?.O ?? 0),
                (decimal)(t.Day?.H ?? 0),
                (decimal)(t.Day?.L ?? 0),
                (decimal)(t.PrevDay?.C ?? 0),
                (long)(t.Day?.V ?? 0),
                t.TodaysChangePerc ?? 0,
                (decimal)(t.TodaysChange ?? 0),
                DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Polygon GetQuote failed for {Symbol}", symbol);
            return null;
        }
    }

    public async Task<IReadOnlyList<Quote>> GetQuotesAsync(IEnumerable<string> symbols, CancellationToken ct = default)
    {
        var tickers = string.Join(",", symbols);
        try
        {
            var snap = await _http.GetFromJsonAsync<PolygonSnapshotsResponse>(
                $"https://api.polygon.io/v2/snapshot/locale/us/markets/stocks/tickers?tickers={tickers}&apiKey={_apiKey}", ct);

            return snap?.Tickers?.Select(t => new Quote(
                t.Ticker ?? string.Empty,
                (decimal)(t.Day?.C ?? 0),
                (decimal)(t.Day?.O ?? 0),
                (decimal)(t.Day?.H ?? 0),
                (decimal)(t.Day?.L ?? 0),
                (decimal)(t.PrevDay?.C ?? 0),
                (long)(t.Day?.V ?? 0),
                t.TodaysChangePerc ?? 0,
                (decimal)(t.TodaysChange ?? 0),
                DateTime.UtcNow)).ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Polygon GetQuotes failed");
            return [];
        }
    }

    public async Task<IReadOnlyList<PriceBar>> GetHistoricalBarsAsync(string symbol, string interval, int count, CancellationToken ct = default)
    {
        try
        {
            var multiplier = 1;
            var timespan = interval switch { "1m" => "minute", "5m" => "minute", "1h" => "hour", "1d" => "day", _ => "minute" };
            if (interval == "5m") multiplier = 5;

            var to = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var from = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd");
            var url = $"https://api.polygon.io/v2/aggs/ticker/{symbol}/range/{multiplier}/{timespan}/{from}/{to}?adjusted=true&sort=desc&limit={count}&apiKey={_apiKey}";

            var resp = await _http.GetFromJsonAsync<PolygonAggResponse>(url, ct);
            return resp?.Results?.Select(r => new PriceBar(
                symbol,
                DateTimeOffset.FromUnixTimeMilliseconds(r.T).UtcDateTime,
                (decimal)r.O, (decimal)r.H, (decimal)r.L, (decimal)r.C,
                (long)r.V, interval)).ToList() ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Polygon GetHistoricalBars failed for {Symbol}", symbol);
            return [];
        }
    }

    public async Task<IReadOnlyList<string>> GetMostActiveSymbolsAsync(int count = 100, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetFromJsonAsync<PolygonSnapshotsResponse>(
                $"https://api.polygon.io/v2/snapshot/locale/us/markets/stocks/gainers?apiKey={_apiKey}", ct);
            return resp?.Tickers?.Take(count).Select(t => t.Ticker ?? "").Where(s => s.Length > 0).ToList() ?? [];
        }
        catch { return []; }
    }

    public async Task<bool> ValidateApiKeyAsync(CancellationToken ct = default)
    {
        try
        {
            // /v2/last/trade was deprecated — use previous-day aggs which works on all plans
            var resp = await _http.GetAsync($"https://api.polygon.io/v2/aggs/ticker/AAPL/prev?apiKey={_apiKey}", ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // Response DTOs
    private record PolygonSnapshotResponse([property: JsonPropertyName("ticker")] PolygonTicker? Ticker);
    private record PolygonSnapshotsResponse([property: JsonPropertyName("tickers")] List<PolygonTicker>? Tickers);
    private record PolygonTicker(
        [property: JsonPropertyName("ticker")] string? Ticker,
        [property: JsonPropertyName("todaysChange")] double? TodaysChange,
        [property: JsonPropertyName("todaysChangePerc")] decimal? TodaysChangePerc,
        [property: JsonPropertyName("day")] PolygonOhlcv? Day,
        [property: JsonPropertyName("prevDay")] PolygonOhlcv? PrevDay);
    private record PolygonOhlcv(
        [property: JsonPropertyName("o")] double? O,
        [property: JsonPropertyName("h")] double? H,
        [property: JsonPropertyName("l")] double? L,
        [property: JsonPropertyName("c")] double? C,
        [property: JsonPropertyName("v")] double? V);
    private record PolygonAggResponse([property: JsonPropertyName("results")] List<PolygonBar>? Results);
    private record PolygonBar(
        [property: JsonPropertyName("o")] double O,
        [property: JsonPropertyName("h")] double H,
        [property: JsonPropertyName("l")] double L,
        [property: JsonPropertyName("c")] double C,
        [property: JsonPropertyName("v")] double V,
        [property: JsonPropertyName("t")] long T);
}
