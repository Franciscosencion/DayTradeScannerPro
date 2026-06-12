using TradeScanner.Core.Domain.ValueObjects;

namespace TradeScanner.Core.Domain.Entities;

public class StockSymbol
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Exchange { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public decimal MarketCap { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<WatchlistEntry> WatchlistEntries { get; set; } = [];
    public ICollection<AlertRule> AlertRules { get; set; } = [];
    public ICollection<ScanResult> ScanResults { get; set; } = [];
}
