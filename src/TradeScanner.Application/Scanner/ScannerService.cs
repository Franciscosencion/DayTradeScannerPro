using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradeScanner.Core.Domain.Entities;
using TradeScanner.Core.Domain.Enums;
using TradeScanner.Core.Interfaces;
using TradeScanner.Infrastructure.Repositories;

namespace TradeScanner.Application.Scanner;

public class ScannerService(
    IMarketDataProvider provider,
    IRankingEngine rankingEngine,
    IServiceScopeFactory scopeFactory,
    ILogger<ScannerService> logger) : IScannerService
{
    private CancellationTokenSource? _cts;
    private Task? _scanLoop;
    private TimeSpan _scanInterval = TimeSpan.FromMinutes(1);
    private AppSettings _settings = new();

    public bool IsRunning { get; private set; }
    public bool RelaxedScanMode { get; set; }
    public event EventHandler<IReadOnlyList<ScanResult>>? ScanCompleted;
    public event EventHandler<string>? StatusChanged;
    public event EventHandler<Exception>? ScanError;

    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings;
        _scanInterval = TimeSpan.FromSeconds((int)settings.ScanFrequency == 0 ? 5 : (int)settings.ScanFrequency);
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (IsRunning) return;

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        IsRunning = true;
        StatusChanged?.Invoke(this, "Scanner started");
        logger.LogInformation("Scanner started with interval {Interval}", _scanInterval);

        _scanLoop = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var results = await RunSingleScanAsync(_cts.Token);
                    ScanCompleted?.Invoke(this, results);
                    await Task.Delay(_scanInterval, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Scanner error");
                    ScanError?.Invoke(this, ex);
                    await Task.Delay(TimeSpan.FromSeconds(30), _cts.Token).ConfigureAwait(false);
                }
            }
        }, _cts.Token);

        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (!IsRunning) return;
        _cts?.Cancel();
        if (_scanLoop != null) await _scanLoop.ConfigureAwait(false);
        IsRunning = false;
        StatusChanged?.Invoke(this, "Scanner stopped");
        logger.LogInformation("Scanner stopped");
    }

    public async Task<IReadOnlyList<ScanResult>> RunSingleScanAsync(CancellationToken ct = default)
    {
        StatusChanged?.Invoke(this, "Fetching symbols...");

        var symbols = await provider.GetMostActiveSymbolsAsync(200, ct);
        StatusChanged?.Invoke(this, RelaxedScanMode
            ? $"Scanning {symbols.Count} symbols (Relaxed Mode — all filters bypassed)..."
            : $"Scanning {symbols.Count} symbols...");

        var quotes = await provider.GetQuotesAsync(symbols, ct);

        var filtered = quotes
            .Where(q => q.Price >= _settings.MinPrice && q.Price <= _settings.MaxPrice)
            .Where(q => RelaxedScanMode || Math.Abs(q.ChangePercent) >= _settings.MinChangePercent)
            .Where(q => RelaxedScanMode || q.Volume >= _settings.MinVolume)
            .ToList();

        StatusChanged?.Invoke(this, $"Ranking {filtered.Count} candidates...");

        var results = new List<ScanResult>();
        foreach (var quote in filtered.Take(100))
        {
            if (ct.IsCancellationRequested) break;
            var history = await provider.GetHistoricalBarsAsync(quote.Symbol, "5m", 50, ct);
            var score = rankingEngine.CalculateScore(quote, history, []);

            if (!RelaxedScanMode && score.Score < _settings.MinTradeScore) continue;

            results.Add(new ScanResult
            {
                Symbol = quote.Symbol,
                TradeScore = score.Score,
                MomentumScore = score.MomentumComponent,
                VolumeScore = score.VolumeComponent,
                TechnicalScore = score.TechnicalComponent,
                NewsScore = score.NewsComponent,
                Price = quote.Price,
                ChangePercent = quote.ChangePercent,
                Volume = quote.Volume,
                AvgVolume = history.Count > 0 ? (long)history.Average(b => b.Volume) : 0,
                Provider = provider.ProviderType,
                ScannedAt = DateTime.UtcNow
            });
        }

        var ranked = rankingEngine.RankResults(results)
            .Take(_settings.MaxResultsPerScan)
            .ToList();

        // Use a scope for the scoped repository
        using var scope = scopeFactory.CreateScope();
        var resultRepo = scope.ServiceProvider.GetRequiredService<ScanResultRepository>();
        await resultRepo.SaveResultsAsync(ranked, ct);

        if (ranked.Count == 0)
        {
            var hint = RelaxedScanMode
                ? "No data returned — configure a free API key in the Providers tab (Finnhub/Alpha Vantage/TwelveData)"
                : "0 results — enable Relaxed Mode to bypass filters, or configure a free API key in Providers";
            StatusChanged?.Invoke(this, hint);
        }
        else
        {
            StatusChanged?.Invoke(this, $"Scan complete — {ranked.Count} results");
        }
        logger.LogInformation("Scan completed: {Count} results", ranked.Count);

        return ranked;
    }
}
