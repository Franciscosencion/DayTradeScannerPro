using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using TradeScanner.Core.Interfaces;
using TradeScanner.UI.Infrastructure;
using WpfApp = System.Windows.Application;
// rc2 uses FinancialPoint(date, high, open, close, low) for candlestick data

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
    [ObservableProperty] private ISeries[] _series = [];
    [ObservableProperty] private Axis[] _xAxes = [new Axis { IsVisible = false }];
    [ObservableProperty] private Axis[] _yAxes = [new Axis { IsVisible = false }];

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

            var quoteTask = provider.GetQuoteAsync(sym);
            var historyTask = provider.GetHistoricalBarsAsync(sym, "5m", 100);
            await Task.WhenAll(quoteTask, historyTask);

            var quote = quoteTask.Result;
            var bars = historyTask.Result;

            if (quote != null)
            {
                LastPrice = $"${quote.Price:F2}";
                ChangeText = $"{quote.ChangePercent:+0.00;-0.00}%  ({quote.Change:+0.00;-0.00})";
                VolumeText = $"{quote.Volume:N0}";
                DayRange = $"${quote.Low:F2} – ${quote.High:F2}";
                IsPositive = quote.ChangePercent >= 0;
            }

            if (bars.Count > 0)
            {
                // FinancialPoint ctor: (DateTime date, double? high, double? open, double? close, double? low)
                var candles = bars.Select(b =>
                    new FinancialPoint(b.Timestamp,
                        (double)b.High, (double)b.Open, (double)b.Close, (double)b.Low))
                    .ToArray();

                WpfApp.Current.Dispatcher.Invoke(() =>
                {
                    Series =
                    [
                        new CandlesticksSeries<FinancialPoint>
                        {
                            Values = candles,
                            Name = sym,
                            UpFill   = new SolidColorPaint(new SKColor(0x26, 0xA6, 0x9A)),
                            UpStroke = new SolidColorPaint(new SKColor(0x26, 0xA6, 0x9A)) { StrokeThickness = 1 },
                            DownFill   = new SolidColorPaint(new SKColor(0xEF, 0x53, 0x50)),
                            DownStroke = new SolidColorPaint(new SKColor(0xEF, 0x53, 0x50)) { StrokeThickness = 1 },
                        }
                    ];

                    XAxes =
                    [
                        new Axis
                        {
                            // FinancialPoint uses DateTime ticks for X; convert back to readable time
                            Labeler = v => new DateTime((long)v).ToString("HH:mm"),
                            LabelsRotation = 45,
                            UnitWidth = TimeSpan.FromMinutes(5).Ticks,
                            SeparatorsPaint = new SolidColorPaint(new SKColor(50, 50, 50)),
                            LabelsPaint = new SolidColorPaint(new SKColor(170, 170, 170)),
                        }
                    ];

                    YAxes =
                    [
                        new Axis
                        {
                            Labeler = v => $"${v:F2}",
                            SeparatorsPaint = new SolidColorPaint(new SKColor(50, 50, 50)),
                            LabelsPaint = new SolidColorPaint(new SKColor(170, 170, 170)),
                        }
                    ];
                });

                StatusMessage = $"{bars.Count} bars loaded — {provider.DisplayName}";
            }
            else
            {
                StatusMessage = $"No chart data for {sym}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Chart error: {ex.Message}";
        }
        finally { IsBusy = false; }
    }
}
