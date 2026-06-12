using TradeScanner.Core.Domain.Enums;

namespace TradeScanner.Core.Domain.Entities;

public class AppSettings
{
    public int Id { get; set; } = 1;
    public ScanFrequency ScanFrequency { get; set; } = ScanFrequency.OneMinute;
    public int MaxResultsPerScan { get; set; } = 50;
    public decimal MinPrice { get; set; } = 1.0m;
    public decimal MaxPrice { get; set; } = 500.0m;
    public long MinVolume { get; set; } = 500_000;
    public decimal MinChangePercent { get; set; } = 2.0m;
    public decimal MinVolumeRatio { get; set; } = 1.5m;
    public int MinTradeScore { get; set; } = 60;
    public bool EnableSoundAlerts { get; set; } = true;
    public bool EnablePopupAlerts { get; set; } = true;
    public bool EnableAutoScan { get; set; } = false;
    public string Theme { get; set; } = "Dark";

    // Score weights (must sum to 1.0)
    public decimal MomentumWeight { get; set; } = 0.35m;
    public decimal VolumeWeight { get; set; } = 0.30m;
    public decimal TechnicalWeight { get; set; } = 0.25m;
    public decimal NewsWeight { get; set; } = 0.10m;
}
