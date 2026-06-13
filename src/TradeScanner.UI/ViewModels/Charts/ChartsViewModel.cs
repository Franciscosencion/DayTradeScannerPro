using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TradeScanner.Core.Interfaces;
using TradeScanner.UI.Infrastructure;

namespace TradeScanner.UI.ViewModels.Charts;

public partial class ChartsViewModel : ViewModelBase
{
    private readonly IProviderFactory _providerFactory;

    [ObservableProperty] private string _symbol = "AAPL";
    [ObservableProperty] private string _lastPrice = "—";
    [ObservableProperty] private string _changeText = "—";
    [ObservableProperty] private string _volumeText = "—";
    [ObservableProperty] private string _dayRange = "—";
    [ObservableProperty] private bool _isPositive = true;

    // Wired by ChartsView code-behind so ViewModel can drive WebView2 navigation
    // without taking a dependency on UI controls.
    public Action<string>? NavigateChart { get; set; }

    public ChartsViewModel(IProviderFactory providerFactory)
    {
        _providerFactory = providerFactory;
    }

    public async Task LoadAsync() => await LoadChartAsync();

    [RelayCommand]
    private async Task LoadChartAsync()
    {
        var sym = Symbol.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(sym)) return;

        IsBusy = true;
        StatusMessage = $"Loading {sym}...";
        try
        {
            var provider = _providerFactory.GetBestAvailableProvider();
            var quote = await provider.GetQuoteAsync(sym);

            if (quote != null)
            {
                LastPrice = $"${quote.Price:F2}";
                ChangeText = $"{quote.ChangePercent:+0.00;-0.00}%  ({quote.Change:+0.00;-0.00})";
                VolumeText = $"{quote.Volume:N0}";
                DayRange = $"${quote.Low:F2} – ${quote.High:F2}";
                IsPositive = quote.ChangePercent >= 0;
            }

            NavigateChart?.Invoke(sym);
            StatusMessage = $"{sym} · TradingView 5-min · VWAP + Volume";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Chart error: {ex.Message}";
        }
        finally { IsBusy = false; }
    }
}
