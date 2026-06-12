using TradeScanner.Core.Domain.Enums;

namespace TradeScanner.Core.Domain.Entities;

public class AlertRule
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public AlertType AlertType { get; set; }
    public decimal Threshold { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool NotifySound { get; set; } = true;
    public bool NotifyPopup { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastTriggeredAt { get; set; }
    public int TriggerCount { get; set; }

    // Navigation
    public int? StockSymbolId { get; set; }
    public StockSymbol? StockSymbol { get; set; }
}
