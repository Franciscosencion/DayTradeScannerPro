using Microsoft.Extensions.Logging;
using TradeScanner.Core.Domain.Entities;
using TradeScanner.Core.Domain.Enums;
using TradeScanner.Core.Domain.ValueObjects;
using TradeScanner.Core.Interfaces;
using TradeScanner.Infrastructure.Repositories;

namespace TradeScanner.Application.Alerts;

public class AlertService(
    AlertRepository alertRepo,
    ILogger<AlertService> logger) : IAlertService
{
    public event EventHandler<AlertTriggeredEventArgs>? AlertTriggered;

    public async Task EvaluateAlertsAsync(IEnumerable<Quote> quotes, CancellationToken ct = default)
    {
        var activeAlerts = await alertRepo.GetActiveAsync(ct);
        var quoteMap = quotes.ToDictionary(q => q.Symbol);

        foreach (var alert in activeAlerts)
        {
            if (!quoteMap.TryGetValue(alert.Symbol, out var quote)) continue;
            if (IsTriggered(alert, quote))
            {
                logger.LogInformation("Alert triggered: {Symbol} {AlertType}", alert.Symbol, alert.AlertType);
                await alertRepo.RecordTriggerAsync(alert.Id, ct);
                AlertTriggered?.Invoke(this, new AlertTriggeredEventArgs(alert, alert.Symbol, GetRelevantValue(alert, quote)));
            }
        }
    }

    public async Task<IReadOnlyList<AlertRule>> GetActiveAlertsAsync(CancellationToken ct = default) =>
        await alertRepo.GetActiveAsync(ct);

    public async Task AddAlertAsync(AlertRule alert, CancellationToken ct = default) =>
        await alertRepo.AddAsync(alert, ct);

    public async Task RemoveAlertAsync(int alertId, CancellationToken ct = default) =>
        await alertRepo.DeleteAsync(alertId, ct);

    public async Task ToggleAlertAsync(int alertId, CancellationToken ct = default) =>
        await alertRepo.ToggleAsync(alertId, ct);

    private static bool IsTriggered(AlertRule rule, Quote quote) =>
        rule.AlertType switch
        {
            AlertType.PriceAbove => quote.Price > rule.Threshold,
            AlertType.PriceBelow => quote.Price < rule.Threshold,
            AlertType.PercentChangeUp => quote.ChangePercent >= rule.Threshold,
            AlertType.PercentChangeDown => quote.ChangePercent <= -rule.Threshold,
            AlertType.VolumeAbove => quote.Volume >= (long)rule.Threshold,
            _ => false
        };

    private static decimal GetRelevantValue(AlertRule rule, Quote quote) =>
        rule.AlertType switch
        {
            AlertType.PriceAbove or AlertType.PriceBelow => quote.Price,
            AlertType.PercentChangeUp or AlertType.PercentChangeDown => quote.ChangePercent,
            AlertType.VolumeAbove => quote.Volume,
            _ => 0
        };
}
