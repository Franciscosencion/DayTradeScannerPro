using TradeScanner.Core.Domain.Enums;
using TradeScanner.Core.Domain.ValueObjects;

namespace TradeScanner.Core.Domain.Entities;

public class ScanResult
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public int TradeScore { get; set; }
    public decimal MomentumScore { get; set; }
    public decimal VolumeScore { get; set; }
    public decimal TechnicalScore { get; set; }
    public decimal NewsScore { get; set; }
    public decimal Price { get; set; }
    public decimal ChangePercent { get; set; }
    public long Volume { get; set; }
    public long AvgVolume { get; set; }
    public decimal VolumeRatio => AvgVolume > 0 ? (decimal)Volume / AvgVolume : 0;
    public List<SignalType> Signals { get; set; } = [];
    public string SignalsSerialized { get; set; } = string.Empty;
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    public MarketDataProvider Provider { get; set; }

    // Navigation
    public int? StockSymbolId { get; set; }
    public StockSymbol? StockSymbol { get; set; }
}
