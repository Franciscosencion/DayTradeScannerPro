namespace TradeScanner.Core.Domain.Entities;

public class WatchlistEntry
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public decimal? PriceTarget { get; set; }
    public decimal? StopLoss { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public int SortOrder { get; set; }

    // Navigation
    public int? StockSymbolId { get; set; }
    public StockSymbol? StockSymbol { get; set; }
}
