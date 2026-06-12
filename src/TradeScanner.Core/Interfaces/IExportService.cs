using TradeScanner.Core.Domain.Entities;

namespace TradeScanner.Core.Interfaces;

public interface IExportService
{
    Task ExportToCsvAsync(IEnumerable<ScanResult> results, string filePath, CancellationToken ct = default);
    Task ExportToExcelAsync(IEnumerable<ScanResult> results, string filePath, CancellationToken ct = default);
    Task ExportWatchlistAsync(IEnumerable<WatchlistEntry> entries, string filePath, CancellationToken ct = default);
}
