using Microsoft.Extensions.DependencyInjection;
using TradeScanner.Application.Alerts;
using TradeScanner.Application.Config;
using TradeScanner.Application.Export;
using TradeScanner.Application.Ranking;
using TradeScanner.Application.Scanner;
using TradeScanner.Application.Streaming;
using TradeScanner.Core.Interfaces;
using TradeScanner.Infrastructure.Providers.Failover;

namespace TradeScanner.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<RankingEngine>();
        services.AddSingleton<IRankingEngine>(sp => sp.GetRequiredService<RankingEngine>());

        // ScannerService is singleton. Explicitly wire FailoverProviderChain as its IMarketDataProvider
        // so it uses the full priority chain instead of the last-registered bare provider.
        services.AddSingleton<ScannerService>(sp => new ScannerService(
            sp.GetRequiredService<FailoverProviderChain>(),
            sp.GetRequiredService<IRankingEngine>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScannerService>>()));
        services.AddSingleton<IScannerService>(sp => sp.GetRequiredService<ScannerService>());

        services.AddScoped<AlertService>();
        services.AddScoped<IAlertService>(sp => sp.GetRequiredService<AlertService>());

        services.AddScoped<WatchlistService>();
        services.AddScoped<IWatchlistService>(sp => sp.GetRequiredService<WatchlistService>());

        services.AddTransient<IExportService, CsvExportService>();

        services.AddSingleton<RealtimeStreamingService>();
        services.AddSingleton<IRealtimeStreamingService>(sp => sp.GetRequiredService<RealtimeStreamingService>());

        services.AddSingleton<ProviderConfigService>();
        services.AddSingleton<IProviderConfigService>(sp => sp.GetRequiredService<ProviderConfigService>());

        return services;
    }
}
