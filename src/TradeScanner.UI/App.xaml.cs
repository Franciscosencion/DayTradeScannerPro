using System.IO;
using System.Media;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using TradeScanner.Core.Domain.ValueObjects;
using TradeScanner.Core.Interfaces;
using TradeScanner.Infrastructure;
using TradeScanner.UI.ViewModels.Alerts;
using TradeScanner.UI.ViewModels.Charts;
using TradeScanner.UI.ViewModels.Dashboard;
using TradeScanner.UI.ViewModels.Providers;
using TradeScanner.UI.ViewModels.Settings;
using TradeScanner.UI.ViewModels.Watchlist;
using TradeScanner.UI.Views;
using TradeScanner.UI.Views.Alerts;
using TradeScanner.UI.Views.Charts;
using TradeScanner.UI.Views.Dashboard;
using TradeScanner.UI.Views.Providers;
using TradeScanner.UI.Views.Settings;
using TradeScanner.UI.Views.Watchlist;
using AppDI = TradeScanner.Application.DependencyInjection;

namespace TradeScanner.UI;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TradeScanner");
        Directory.CreateDirectory(appDataPath);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(appDataPath, "logs", "tradescanner-.log"),
                rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
            .WriteTo.Debug()
            .CreateLogger();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices(services =>
            {
                services.AddInfrastructure(Path.Combine(appDataPath, "tradescanner.db"));
                AppDI.AddApplication(services);

                services.AddTransient<DashboardView>();
                services.AddTransient<WatchlistView>();
                services.AddTransient<AlertsView>();
                services.AddTransient<SettingsView>();
                services.AddTransient<ProvidersView>();
                services.AddTransient<ChartsView>();

                services.AddSingleton<DashboardViewModel>();
                services.AddTransient<WatchlistViewModel>();
                services.AddTransient<AlertsViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<ProvidersViewModel>();
                services.AddTransient<ChartsViewModel>();

                services.AddSingleton<MainWindow>(sp => new MainWindow(sp));
            })
            .Build();

        await _host.StartAsync();
        await _host.Services.InitializeDatabaseAsync();

        // Restore API keys from encrypted storage and apply to providers
        var providerConfig = _host.Services.GetRequiredService<IProviderConfigService>();
        await providerConfig.LoadAndApplyAsync();

        // Wire up sound alert on scan completion
        var scanner = _host.Services.GetRequiredService<IScannerService>();
        scanner.ScanCompleted += async (_, results) =>
        {
            if (results.Count == 0) return;
            using var scope = _host.Services.CreateScope();
            var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();
            alertService.AlertTriggered += (_, args) =>
            {
                Log.Information("Alert triggered: {Symbol} {AlertType}", args.Symbol, args.Rule.AlertType);
                SystemSounds.Asterisk.Play();
            };
            var quotes = results.Select(r =>
                new Quote(r.Symbol, r.Price, r.Price, r.Price, r.Price,
                    r.Price, r.Volume, r.ChangePercent, 0, r.ScannedAt)).ToList();
            await alertService.EvaluateAlertsAsync(quotes);
        };

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
