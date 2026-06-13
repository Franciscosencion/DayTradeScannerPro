using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TradeScanner.UI.ViewModels.Charts;
using TradeScanner.UI.ViewModels.Dashboard;
using TradeScanner.UI.Views.Alerts;
using TradeScanner.UI.Views.Charts;
using TradeScanner.UI.Views.Dashboard;
using TradeScanner.UI.Views.Providers;
using TradeScanner.UI.Views.Settings;
using TradeScanner.UI.Views.Watchlist;

namespace TradeScanner.UI.Views;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;

    public MainWindow(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
        Loaded += (_, _) => NavigateToDashboard();

        // DashboardViewModel is singleton — wire once so symbol clicks navigate to Charts.
        var dashVm = services.GetRequiredService<DashboardViewModel>();
        dashVm.NavigateToChart = NavigateToChartView;
    }

    private void NavigateToChartView(string symbol)
    {
        var chartsView = _services.GetRequiredService<ChartsView>();
        if (chartsView.DataContext is ChartsViewModel chartsVm)
            chartsVm.Symbol = symbol;
        MainFrame.Navigate(chartsView);
    }

    private void NavigateToDashboard() =>
        MainFrame.Navigate(_services.GetRequiredService<DashboardView>());

    private void OnNavDashboard(object sender, RoutedEventArgs e) =>
        MainFrame.Navigate(_services.GetRequiredService<DashboardView>());

    private void OnNavWatchlist(object sender, RoutedEventArgs e) =>
        MainFrame.Navigate(_services.GetRequiredService<WatchlistView>());

    private void OnNavCharts(object sender, RoutedEventArgs e) =>
        MainFrame.Navigate(_services.GetRequiredService<ChartsView>());

    private void OnNavAlerts(object sender, RoutedEventArgs e) =>
        MainFrame.Navigate(_services.GetRequiredService<AlertsView>());

    private void OnNavProviders(object sender, RoutedEventArgs e) =>
        MainFrame.Navigate(_services.GetRequiredService<ProvidersView>());

    private void OnNavSettings(object sender, RoutedEventArgs e) =>
        MainFrame.Navigate(_services.GetRequiredService<SettingsView>());
}
