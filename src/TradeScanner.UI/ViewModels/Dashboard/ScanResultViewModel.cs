using CommunityToolkit.Mvvm.ComponentModel;
using TradeScanner.Core.Domain.Entities;
using TradeScanner.Core.Domain.Enums;
using TradeScanner.Core.Domain.ValueObjects;

namespace TradeScanner.UI.ViewModels.Dashboard;

public partial class ScanResultViewModel : ObservableObject
{
    private readonly ScanResult _source;

    [ObservableProperty] private decimal _price;
    [ObservableProperty] private decimal _changePercent;
    [ObservableProperty] private long _volume;

    public ScanResultViewModel(ScanResult source)
    {
        _source = source;
        _price = source.Price;
        _changePercent = source.ChangePercent;
        _volume = source.Volume;
    }

    // Static pass-through properties
    public string Symbol => _source.Symbol;
    public int TradeScore => _source.TradeScore;
    public decimal MomentumScore => _source.MomentumScore;
    public decimal VolumeScore => _source.VolumeScore;
    public decimal TechnicalScore => _source.TechnicalScore;
    public decimal NewsScore => _source.NewsScore;
    public long AvgVolume => _source.AvgVolume;
    public string SignalsSerialized => _source.SignalsSerialized;
    public DateTime ScannedAt => _source.ScannedAt;
    public MarketDataProvider Provider => _source.Provider;

    // Recomputed when Volume changes
    public decimal VolumeRatio => AvgVolume > 0 ? (decimal)Volume / AvgVolume : 0;

    partial void OnVolumeChanged(long value) => OnPropertyChanged(nameof(VolumeRatio));

    public void ApplyTrade(RealtimeTrade trade)
    {
        var prevClose = Price != 0 && ChangePercent != 0
            ? Price / (1m + ChangePercent / 100m)
            : Price;

        Price = trade.Price;
        ChangePercent = prevClose != 0 ? (trade.Price - prevClose) / prevClose * 100m : 0m;
        if (trade.Volume > 0) Volume = trade.Volume;
    }
}
