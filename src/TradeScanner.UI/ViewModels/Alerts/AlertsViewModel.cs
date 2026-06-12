using System.Collections.ObjectModel;
using WpfApp = System.Windows.Application;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TradeScanner.Core.Domain.Entities;
using TradeScanner.Core.Domain.Enums;
using TradeScanner.Core.Interfaces;
using TradeScanner.UI.Infrastructure;

namespace TradeScanner.UI.ViewModels.Alerts;

public partial class AlertsViewModel : ViewModelBase
{
    private readonly IAlertService _alertService;

    [ObservableProperty] private string _newSymbol = string.Empty;
    [ObservableProperty] private AlertType _newAlertType = AlertType.PriceAbove;
    [ObservableProperty] private decimal _newThreshold;
    [ObservableProperty] private AlertRule? _selectedAlert;

    public ObservableCollection<AlertRule> Alerts { get; } = [];
    public IReadOnlyList<AlertType> AlertTypes { get; } = Enum.GetValues<AlertType>();

    public AlertsViewModel(IAlertService alertService)
    {
        _alertService = alertService;
        _alertService.AlertTriggered += OnAlertTriggered;
    }

    public async Task LoadAsync()
    {
        var alerts = await _alertService.GetActiveAlertsAsync();
        WpfApp.Current.Dispatcher.Invoke(() =>
        {
            Alerts.Clear();
            foreach (var a in alerts) Alerts.Add(a);
        });
    }

    [RelayCommand]
    private async Task AddAlertAsync()
    {
        if (string.IsNullOrEmpty(NewSymbol) || NewThreshold <= 0) return;

        var rule = new AlertRule
        {
            Symbol = NewSymbol.ToUpperInvariant(),
            AlertType = NewAlertType,
            Threshold = NewThreshold,
            Message = $"{NewSymbol} {NewAlertType} {NewThreshold}"
        };

        await _alertService.AddAlertAsync(rule);
        NewSymbol = string.Empty;
        NewThreshold = 0;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task DeleteAlertAsync(AlertRule rule)
    {
        await _alertService.RemoveAlertAsync(rule.Id);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ToggleAlertAsync(AlertRule rule)
    {
        await _alertService.ToggleAlertAsync(rule.Id);
        await LoadAsync();
    }

    private void OnAlertTriggered(object? sender, AlertTriggeredEventArgs e)
    {
        WpfApp.Current.Dispatcher.Invoke(() =>
        {
            StatusMessage = $"ALERT: {e.Symbol} {e.Rule.AlertType} triggered at {e.CurrentValue:F2}";
        });
    }
}
