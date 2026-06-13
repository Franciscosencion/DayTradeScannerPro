using TradeScanner.Core.Domain.Entities;
using TradeScanner.Core.Domain.ValueObjects;

namespace TradeScanner.Core.Interfaces;

public interface IScannerService
{
    bool IsRunning { get; }
    bool RelaxedScanMode { get; set; }
    event EventHandler<IReadOnlyList<ScanResult>>? ScanCompleted;
    event EventHandler<string>? StatusChanged;
    event EventHandler<Exception>? ScanError;

    Task StartAsync(CancellationToken ct = default);
    Task StopAsync();
    Task<IReadOnlyList<ScanResult>> RunSingleScanAsync(CancellationToken ct = default);
}
