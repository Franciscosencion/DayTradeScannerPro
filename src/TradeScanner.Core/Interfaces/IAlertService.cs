using TradeScanner.Core.Domain.Entities;
using TradeScanner.Core.Domain.ValueObjects;

namespace TradeScanner.Core.Interfaces;

public interface IAlertService
{
    event EventHandler<AlertTriggeredEventArgs>? AlertTriggered;

    Task EvaluateAlertsAsync(IEnumerable<Quote> quotes, CancellationToken ct = default);
    Task<IReadOnlyList<AlertRule>> GetActiveAlertsAsync(CancellationToken ct = default);
    Task AddAlertAsync(AlertRule alert, CancellationToken ct = default);
    Task RemoveAlertAsync(int alertId, CancellationToken ct = default);
    Task ToggleAlertAsync(int alertId, CancellationToken ct = default);
}

public class AlertTriggeredEventArgs(AlertRule rule, string symbol, decimal currentValue) : EventArgs
{
    public AlertRule Rule { get; } = rule;
    public string Symbol { get; } = symbol;
    public decimal CurrentValue { get; } = currentValue;
    public DateTime TriggeredAt { get; } = DateTime.UtcNow;
}
