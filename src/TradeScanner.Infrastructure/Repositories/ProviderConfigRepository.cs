using Microsoft.EntityFrameworkCore;
using TradeScanner.Core.Domain.Entities;
using TradeScanner.Core.Domain.Enums;
using TradeScanner.Infrastructure.Data;

namespace TradeScanner.Infrastructure.Repositories;

public class ProviderConfigRepository(TradeScannerDbContext db)
{
    public Task<List<ProviderConfig>> GetAllAsync(CancellationToken ct = default) =>
        db.ProviderConfigs.ToListAsync(ct);

    public async Task<ProviderConfig?> GetByProviderAsync(MarketDataProvider provider, CancellationToken ct = default) =>
        await db.ProviderConfigs.FirstOrDefaultAsync(p => p.Provider == provider, ct);

    public async Task UpsertAsync(ProviderConfig config, CancellationToken ct = default)
    {
        var existing = await db.ProviderConfigs
            .FirstOrDefaultAsync(p => p.Provider == config.Provider, ct);

        if (existing == null)
            db.ProviderConfigs.Add(config);
        else
            db.Entry(existing).CurrentValues.SetValues(config);

        await db.SaveChangesAsync(ct);
    }
}
