using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TradeScanner.Core.Domain.Enums;
using TradeScanner.Core.Domain.ValueObjects;
using TradeScanner.Core.Interfaces;

namespace TradeScanner.Infrastructure.Providers.Free;

public class YahooFinanceProvider : IMarketDataProvider
{
    private readonly ILogger<YahooFinanceProvider> _logger;
    private readonly CookieContainer _cookieContainer;
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _crumbLock = new(1, 1);
    private readonly SemaphoreSlim _throttle = new(5, 5);
    private string? _crumb;
    private bool _crumbReady;
    private bool _crumbFailed;

    private const string BaseUrl = "https://query1.finance.yahoo.com/v8/finance";

    public YahooFinanceProvider(ILogger<YahooFinanceProvider> logger)
    {
        _logger = logger;
        _cookieContainer = new CookieContainer();
        var handler = new HttpClientHandler
        {
            UseCookies = true,
            AllowAutoRedirect = true,
            CookieContainer = _cookieContainer
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json,text/plain,*/*");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Origin", "https://finance.yahoo.com");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://finance.yahoo.com/");
    }

    public MarketDataProvider ProviderType => MarketDataProvider.YahooFinance;
    public string DisplayName => "Yahoo Finance (Free)";
    public bool IsAvailable => !_crumbFailed;
    public int Priority => 10;

    // Accepts a browser Cookie header string (name=value; name2=value2; ...)
    // and injects those cookies into the shared container, resetting the crumb state.
    public void SetApiKey(string cookies)
    {
        if (string.IsNullOrWhiteSpace(cookies)) return;

        foreach (var segment in cookies.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = segment.IndexOf('=');
            if (eq <= 0) continue;
            var name = segment[..eq].Trim();
            var value = segment[(eq + 1)..].Trim();
            if (string.IsNullOrEmpty(name)) continue;

            foreach (var host in new[]
            {
                "https://yahoo.com", "https://finance.yahoo.com",
                "https://query1.finance.yahoo.com", "https://query2.finance.yahoo.com"
            })
            {
                try { _cookieContainer.Add(new Uri(host), new Cookie(name, value)); } catch { }
            }
        }

        _crumbFailed = false;
        _crumbReady = false;
        _crumb = null;
        _logger.LogInformation("Yahoo Finance: browser cookies applied, crumb will be re-fetched on next request");
    }

    private async Task EnsureCrumbAsync(CancellationToken ct)
    {
        if (_crumbReady || _crumbFailed) return;
        await _crumbLock.WaitAsync(ct);
        try
        {
            if (_crumbReady || _crumbFailed) return;

            // Establish a session — handles GDPR consent redirects for EU/UK users
            using var homeResp = await _http.GetAsync("https://finance.yahoo.com/", ct);
            var finalUrl = homeResp.RequestMessage?.RequestUri?.ToString() ?? "";

            if (finalUrl.Contains("consent.yahoo.com") || finalUrl.Contains("guce.yahoo.com"))
            {
                await TryAcceptConsentAsync(finalUrl, ct);
                using var _ = await _http.GetAsync("https://finance.yahoo.com/", ct);
            }

            // Try query2 first (often less strict), fall back to query1
            foreach (var host in new[] { "query2.finance.yahoo.com", "query1.finance.yahoo.com" })
            {
                using var crumbResp = await _http.GetAsync($"https://{host}/v1/test/getcrumb", ct);
                if (crumbResp.IsSuccessStatusCode)
                {
                    var text = (await crumbResp.Content.ReadAsStringAsync(ct)).Trim();
                    if (!string.IsNullOrWhiteSpace(text) && text.Length < 64)
                    {
                        _crumb = text;
                        _crumbReady = true;
                        _logger.LogInformation("Yahoo Finance crumb acquired");
                        return;
                    }
                }
            }

            _crumbFailed = true;
            _logger.LogWarning("Yahoo Finance unavailable — paste browser cookies in the Providers tab to enable it");
        }
        catch (Exception ex)
        {
            _crumbFailed = true;
            _logger.LogWarning(ex, "Yahoo Finance crumb failed — marking unavailable for this session");
        }
        finally
        {
            _crumbLock.Release();
        }
    }

    private async Task TryAcceptConsentAsync(string consentUrl, CancellationToken ct)
    {
        try
        {
            var match = Regex.Match(consentUrl, @"sessionId=([^&]+)");
            if (!match.Success) return;
            var sessionId = Uri.UnescapeDataString(match.Groups[1].Value);

            var form = new FormUrlEncodedContent(
                new[] { new KeyValuePair<string, string>("reject", "false") });
            await _http.PostAsync(
                $"https://consent.yahoo.com/v2/collectConsent?sessionId={Uri.EscapeDataString(sessionId)}",
                form, ct);
        }
        catch { }
    }

    public async Task<Quote?> GetQuoteAsync(string symbol, CancellationToken ct = default)
    {
        if (_crumbFailed) return null;
        await _throttle.WaitAsync(ct);
        try
        {
            await EnsureCrumbAsync(ct);
            if (_crumbFailed) return null;

            var crumbParam = _crumb != null ? $"&crumb={Uri.EscapeDataString(_crumb)}" : "";
            var url = $"{BaseUrl}/chart/{symbol}?interval=1d&range=2d{crumbParam}";

            using var resp = await _http.GetAsync(url, ct);

            if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                _crumbReady = false;
                await EnsureCrumbAsync(ct);
                if (_crumbFailed) return null;

                crumbParam = _crumb != null ? $"&crumb={Uri.EscapeDataString(_crumb)}" : "";
                url = $"{BaseUrl}/chart/{symbol}?interval=1d&range=2d{crumbParam}";
                using var resp2 = await _http.GetAsync(url, ct);
                if (!resp2.IsSuccessStatusCode) return null;
                return await ParseChartAsync(symbol, resp2, ct);
            }

            if (!resp.IsSuccessStatusCode) return null;
            return await ParseChartAsync(symbol, resp, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Yahoo GetQuote failed for {Symbol}", symbol);
            return null;
        }
        finally
        {
            _throttle.Release();
        }
    }

    private static async Task<Quote?> ParseChartAsync(string symbol, HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
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
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<Quote>> GetQuotesAsync(IEnumerable<string> symbols, CancellationToken ct = default)
    {
        if (_crumbFailed) return [];
        var tasks = symbols.Select(s => GetQuoteAsync(s, ct));
        var results = await Task.WhenAll(tasks);
        return results.Where(q => q != null).Cast<Quote>().ToList();
    }

    public async Task<IReadOnlyList<PriceBar>> GetHistoricalBarsAsync(string symbol, string interval, int count, CancellationToken ct = default)
    {
        if (_crumbFailed) return [];
        await _throttle.WaitAsync(ct);
        try
        {
            await EnsureCrumbAsync(ct);
            if (_crumbFailed) return [];

            var range = interval is "1d" ? "1y" : "60d";
            var crumbParam = _crumb != null ? $"&crumb={Uri.EscapeDataString(_crumb)}" : "";
            var url = $"{BaseUrl}/chart/{symbol}?interval={interval}&range={range}{crumbParam}";

            using var resp = await _http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return [];

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
        finally { _throttle.Release(); }
    }

    public async Task<IReadOnlyList<string>> GetMostActiveSymbolsAsync(int count = 100, CancellationToken ct = default)
    {
        var universe = new[]
        {
            "AAPL","MSFT","NVDA","TSLA","META","AMZN","GOOGL","AMD","NFLX","CRM",
            "PLTR","SOFI","RIVN","LCID","NIO","BABA","JD","SNAP","PINS","UBER",
            "SPY","QQQ","IWM","DIA","GLD","SLV","USO","TLT","HYG","XLF"
        };
        return await Task.FromResult<IReadOnlyList<string>>(universe.Take(count).ToArray());
    }

    public async Task<bool> ValidateApiKeyAsync(CancellationToken ct = default)
    {
        _crumbFailed = false;
        _crumbReady = false;
        _crumb = null;
        await EnsureCrumbAsync(ct);
        return _crumbReady;
    }
}
