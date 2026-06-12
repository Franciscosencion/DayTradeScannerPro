using Microsoft.EntityFrameworkCore;
using TradeScanner.Core.Domain.Entities;
using TradeScanner.Infrastructure.Data;

namespace TradeScanner.Infrastructure.Repositories;

public class ScanResultRepository(TradeScannerDbContext db)
{
    public async Task<IReadOnlyList<ScanResult>> GetRecentAsync(int count = 50, CancellationToken ct = default) =>
        await db.ScanResults
            .OrderByDescending(r => r.ScannedAt)
            .Take(count)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ScanResult>> GetLatestScanAsync(CancellationToken ct = default)
    {
        var latest = await db.ScanResults.MaxAsync(r => (DateTime?)r.ScannedAt, ct);
        if (latest == null) return [];
        var cutoff = latest.Value.AddSeconds(-5);
        return await db.ScanResults
            .Where(r => r.ScannedAt >= cutoff)
            .OrderByDescending(r => r.TradeScore)
            .ToListAsync(ct);
    }

    public async Task SaveResultsAsync(IEnumerable<ScanResult> results, CancellationToken ct = default)
    {
        db.ScanResults.AddRange(results);
        await db.SaveChangesAsync(ct);
    }

    public async Task PruneOldResultsAsync(int keepDays = 7, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-keepDays);
        await db.ScanResults.Where(r => r.ScannedAt < cutoff).ExecuteDeleteAsync(ct);
    }
}
