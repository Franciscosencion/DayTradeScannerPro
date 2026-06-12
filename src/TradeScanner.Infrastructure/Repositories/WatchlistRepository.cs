using Microsoft.EntityFrameworkCore;
using TradeScanner.Core.Domain.Entities;
using TradeScanner.Infrastructure.Data;

namespace TradeScanner.Infrastructure.Repositories;

public class WatchlistRepository(TradeScannerDbContext db)
{
    public async Task<IReadOnlyList<WatchlistEntry>> GetAllAsync(CancellationToken ct = default) =>
        await db.WatchlistEntries.OrderBy(w => w.SortOrder).ThenBy(w => w.Symbol).ToListAsync(ct);

    public async Task AddAsync(WatchlistEntry entry, CancellationToken ct = default)
    {
        db.WatchlistEntries.Add(entry);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(string symbol, CancellationToken ct = default)
    {
        await db.WatchlistEntries.Where(w => w.Symbol == symbol).ExecuteDeleteAsync(ct);
    }

    public async Task UpdateAsync(WatchlistEntry entry, CancellationToken ct = default)
    {
        db.WatchlistEntries.Update(entry);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistsAsync(string symbol, CancellationToken ct = default) =>
        await db.WatchlistEntries.AnyAsync(w => w.Symbol == symbol, ct);
}
