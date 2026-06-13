using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using Microsoft.Win32;
using WpfApp = System.Windows.Application;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TradeScanner.Core.Domain.Entities;
using TradeScanner.Core.Domain.Enums;
using TradeScanner.Core.Domain.ValueObjects;
using TradeScanner.Core.Interfaces;
using TradeScanner.UI.Infrastructure;

namespace TradeScanner.UI.ViewModels.Dashboard;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IScannerService _scanner;
    private readonly IAlertService _alertService;
    private readonly IRealtimeStreamingService _streaming;
    private readonly IExportService _exportService;
    private readonly IProviderConfigService _configService;

    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _isStreaming;
    [ObservableProperty] private bool _relaxedScanMode;
    [ObservableProperty] private string _scanStatus = "Ready";
    [ObservableProperty] private string _streamingApiKey = string.Empty;
    [ObservableProperty] private string _selectedStreamingProvider = "Polygon";
    [ObservableProperty] private int _resultCount;
    [ObservableProperty] private DateTime _lastScanTime;
    [ObservableProperty] private ScanResultViewModel? _selectedResult;
    [ObservableProperty] private string _currentSortColumn = nameof(ScanResultViewModel.TradeScore);
    [ObservableProperty] private bool _isSortAscending = false;

    public ObservableCollection<ScanResultViewModel> ScanResults { get; } = [];
    public ICollectionView SortedResults { get; }
    public IReadOnlyList<string> StreamingProviders { get; } = ["Polygon", "Finnhub"];

    // Wired by MainWindow so the Dashboard can trigger chart navigation without a UI dependency.
    public Action<string>? NavigateToChart { get; set; }

    [RelayCommand]
    private void OpenChart(string symbol) => NavigateToChart?.Invoke(symbol);

    // Column header labels — include ↑/↓ indicator on the active sort column
    public string ColSymbol    => HeaderLabel(nameof(ScanResultViewModel.Symbol), "SYMBOL");
    public string ColScore     => HeaderLabel(nameof(ScanResultViewModel.TradeScore), "SCORE");
    public string ColPrice     => HeaderLabel(nameof(ScanResultViewModel.Price), "PRICE");
    public string ColChange    => HeaderLabel(nameof(ScanResultViewModel.ChangePercent), "CHANGE%");
    public string ColVolume    => HeaderLabel(nameof(ScanResultViewModel.Volume), "VOLUME");
    public string ColVolRatio  => HeaderLabel(nameof(ScanResultViewModel.VolumeRatio), "VOL RATIO");
    public string ColMomentum  => HeaderLabel(nameof(ScanResultViewModel.MomentumScore), "MOMENTUM");
    public string ColTechnical => HeaderLabel(nameof(ScanResultViewModel.TechnicalScore), "TECHNICAL");

    private string HeaderLabel(string col, string label) =>
        col == CurrentSortColumn ? $"{label} {(IsSortAscending ? "↑" : "↓")}" : label;

    public DashboardViewModel(
        IScannerService scanner,
        IAlertService alertService,
        IRealtimeStreamingService streaming,
        IExportService exportService,
        IProviderConfigService configService)
    {
        _scanner = scanner;
        _alertService = alertService;
        _streaming = streaming;
        _exportService = exportService;
        _configService = configService;

        SortedResults = CollectionViewSource.GetDefaultView(ScanResults);
        SortedResults.SortDescriptions.Add(
            new SortDescription(nameof(ScanResultViewModel.TradeScore), ListSortDirection.Descending));

        _scanner.ScanCompleted += OnScanCompleted;
        _scanner.StatusChanged += OnStatusChanged;
        _scanner.ScanError += OnScanError;

        _streaming.TradeReceived += OnTradeReceived;
        _streaming.StatusChanged += OnStreamingStatusChanged;
    }

    [RelayCommand]
    private void ToggleRelaxedMode() => RelaxedScanMode = !RelaxedScanMode;

    partial void OnRelaxedScanModeChanged(bool value) => _scanner.RelaxedScanMode = value;

    partial void OnCurrentSortColumnChanged(string value) => NotifyHeadersChanged();
    partial void OnIsSortAscendingChanged(bool value) => NotifyHeadersChanged();

    private void NotifyHeadersChanged()
    {
        OnPropertyChanged(nameof(ColSymbol));
        OnPropertyChanged(nameof(ColScore));
        OnPropertyChanged(nameof(ColPrice));
        OnPropertyChanged(nameof(ColChange));
        OnPropertyChanged(nameof(ColVolume));
        OnPropertyChanged(nameof(ColVolRatio));
        OnPropertyChanged(nameof(ColMomentum));
        OnPropertyChanged(nameof(ColTechnical));
    }

    [RelayCommand]
    private void SortBy(string column)
    {
        if (column == CurrentSortColumn)
            IsSortAscending = !IsSortAscending;
        else
        {
            CurrentSortColumn = column;
            // Symbol sorts ascending by default; all numeric columns sort descending
            IsSortAscending = column == nameof(ScanResultViewModel.Symbol);
        }

        SortedResults.SortDescriptions.Clear();
        SortedResults.SortDescriptions.Add(new SortDescription(
            column, IsSortAscending ? ListSortDirection.Ascending : ListSortDirection.Descending));
    }

    [RelayCommand]
    private async Task ToggleScanAsync()
    {
        if (_scanner.IsRunning)
        {
            await _scanner.StopAsync();
            IsScanning = false;
        }
        else
        {
            await _scanner.StartAsync();
            IsScanning = true;
        }
    }

    [RelayCommand]
    private async Task RunScanOnceAsync()
    {
        IsBusy = true;
        try
        {
            var results = await _scanner.RunSingleScanAsync();
            UpdateResults(results);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ToggleStreamingAsync()
    {
        if (_streaming.IsConnected)
        {
            await _streaming.StopAsync();
            IsStreaming = false;
            return;
        }

        var providerType = SelectedStreamingProvider == "Finnhub"
            ? MarketDataProvider.Finnhub
            : MarketDataProvider.PolygonIo;

        // Use typed key if provided; otherwise fall back to saved key
        var key = StreamingApiKey.Trim();
        if (string.IsNullOrEmpty(key))
            key = await _configService.GetDecryptedKeyAsync(providerType) ?? string.Empty;

        if (string.IsNullOrEmpty(key))
        {
            ScanStatus = $"No API key for {SelectedStreamingProvider} — enter one or save it in Providers";
            return;
        }

        var symbols = ScanResults.Select(r => r.Symbol).ToList();
        IsStreaming = true;
        try
        {
            await _streaming.StartAsync(key, providerType, symbols);
        }
        catch (Exception ex)
        {
            ScanStatus = $"Stream error: {ex.Message}";
            IsStreaming = false;
        }
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (ScanResults.Count == 0)
        {
            ScanStatus = "Nothing to export — run a scan first";
            return;
        }

        var dlg = new SaveFileDialog
        {
            Title = "Export scan results",
            Filter = "Excel workbook (*.xlsx)|*.xlsx|CSV file (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = $"scan_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
            DefaultExt = ".xlsx"
        };

        if (dlg.ShowDialog() != true) return;

        IsBusy = true;
        try
        {
            // Reconstruct ScanResult list from VMs for export
            var results = ScanResults.Select(vm => new ScanResult
            {
                Symbol = vm.Symbol,
                TradeScore = vm.TradeScore,
                Price = vm.Price,
                ChangePercent = vm.ChangePercent,
                Volume = vm.Volume,
                AvgVolume = vm.AvgVolume,
                MomentumScore = vm.MomentumScore,
                VolumeScore = vm.VolumeScore,
                TechnicalScore = vm.TechnicalScore,
                NewsScore = vm.NewsScore,
                SignalsSerialized = vm.SignalsSerialized,
                ScannedAt = vm.ScannedAt,
                Provider = vm.Provider
            }).ToList();

            if (dlg.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                await _exportService.ExportToCsvAsync(results, dlg.FileName);
            else
                await _exportService.ExportToExcelAsync(results, dlg.FileName);

            ScanStatus = $"Exported {results.Count} results to {System.IO.Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex)
        {
            ScanStatus = $"Export failed: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AddToWatchlistAsync(string symbol)
    {
        await Task.CompletedTask;
    }

    private void OnScanCompleted(object? sender, IReadOnlyList<ScanResult> results)
    {
        WpfApp.Current.Dispatcher.Invoke(() =>
        {
            UpdateResults(results);
            if (_streaming.IsConnected)
                _ = _streaming.SubscribeAsync(results.Select(r => r.Symbol));
        });
    }

    private void OnStatusChanged(object? sender, string status)
    {
        WpfApp.Current.Dispatcher.Invoke(() => ScanStatus = status);
    }

    private void OnScanError(object? sender, Exception ex)
    {
        WpfApp.Current.Dispatcher.Invoke(() =>
        {
            ScanStatus = $"Error: {ex.Message}";
            IsBusy = false;
        });
    }

    private void OnStreamingStatusChanged(object? sender, string status)
    {
        WpfApp.Current.Dispatcher.Invoke(() =>
        {
            ScanStatus = status;
            // Sync IsStreaming flag when streaming stops unexpectedly
            if (status.Contains("disconnected", StringComparison.OrdinalIgnoreCase) ||
                status.Contains("stopped", StringComparison.OrdinalIgnoreCase))
                IsStreaming = _streaming.IsConnected;
        });
    }

    private void OnTradeReceived(object? sender, RealtimeTrade trade)
    {
        WpfApp.Current.Dispatcher.Invoke(() =>
        {
            var vm = ScanResults.FirstOrDefault(
                r => r.Symbol.Equals(trade.Symbol, StringComparison.OrdinalIgnoreCase));
            vm?.ApplyTrade(trade);
        });
    }

    private void UpdateResults(IReadOnlyList<ScanResult> results)
    {
        ScanResults.Clear();
        foreach (var r in results) ScanResults.Add(new ScanResultViewModel(r));
        ResultCount = results.Count;
        LastScanTime = DateTime.Now;
    }
}
