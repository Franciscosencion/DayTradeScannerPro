using Microsoft.EntityFrameworkCore;
using TradeScanner.Core.Domain.Entities;
using TradeScanner.Infrastructure.Data;

namespace TradeScanner.Infrastructure.Repositories;

public class AlertRepository(TradeScannerDbContext db)
{
    public async Task<IReadOnlyList<AlertRule>> GetActiveAsync(CancellationToken ct = default) =>
        await db.AlertRules.Where(a => a.IsEnabled).ToListAsync(ct);

    public async Task<IReadOnlyList<AlertRule>> GetAllAsync(CancellationToken ct = default) =>
        await db.AlertRules.OrderBy(a => a.Symbol).ToListAsync(ct);

    public async Task AddAsync(AlertRule rule, CancellationToken ct = default)
    {
        db.AlertRules.Add(rule);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        await db.AlertRules.Where(a => a.Id == id).ExecuteDeleteAsync(ct);
    }

    public async Task ToggleAsync(int id, CancellationToken ct = default)
    {
        var rule = await db.AlertRules.FindAsync([id], ct);
        if (rule != null)
        {
            rule.IsEnabled = !rule.IsEnabled;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task RecordTriggerAsync(int id, CancellationToken ct = default)
    {
        var rule = await db.AlertRules.FindAsync([id], ct);
        if (rule != null)
        {
            rule.LastTriggeredAt = DateTime.UtcNow;
            rule.TriggerCount++;
            await db.SaveChangesAsync(ct);
        }
    }
}
