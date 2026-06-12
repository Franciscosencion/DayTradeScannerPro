using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TradeScanner.Core.Interfaces;
using TradeScanner.Infrastructure.Data;
using TradeScanner.Infrastructure.Http;
using TradeScanner.Infrastructure.Providers;
using TradeScanner.Infrastructure.Providers.Failover;
using TradeScanner.Infrastructure.Providers.Free;
using TradeScanner.Infrastructure.Providers.Premium;
using TradeScanner.Infrastructure.Streaming;

using TradeScanner.Infrastructure.Repositories;
using TradeScanner.Infrastructure.Security;

namespace TradeScanner.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string dbPath)
    {
        services.AddDbContext<TradeScannerDbContext>(opt =>
            opt.UseSqlite($"Data Source={dbPath}"));

        services.AddScoped<ScanResultRepository>();
        services.AddScoped<WatchlistRepository>();
        services.AddScoped<AlertRepository>();
        services.AddScoped<ProviderConfigRepository>();

        services.AddSingleton<ISecurityService, DpapiSecurityService>();

        services.AddResilientHttpClient("Polygon");
        services.AddResilientHttpClient("Finnhub");
        services.AddResilientHttpClient("Yahoo");
        services.AddResilientHttpClient("AlphaVantage");
        services.AddResilientHttpClient("FMP");
        services.AddResilientHttpClient("TwelveData");
        services.AddResilientHttpClient("Stooq");

        services.AddSingleton<PolygonProvider>();
        services.AddSingleton<FinnhubProvider>();
        services.AddSingleton<FmpProvider>();
        services.AddSingleton<TwelveDataProvider>();
        services.AddSingleton<AlphaVantageProvider>();
        services.AddSingleton<StooqProvider>();
        services.AddSingleton<YahooFinanceProvider>();

        // Register each concrete provider also as IMarketDataProvider for IEnumerable<IMarketDataProvider> injection in ProviderFactory
        // Order matters: last registered = default single-instance resolution; FailoverChain registered last so ScannerService uses it
        services.AddSingleton<IMarketDataProvider>(sp => sp.GetRequiredService<PolygonProvider>());
        services.AddSingleton<IMarketDataProvider>(sp => sp.GetRequiredService<FinnhubProvider>());
        services.AddSingleton<IMarketDataProvider>(sp => sp.GetRequiredService<FmpProvider>());
        services.AddSingleton<IMarketDataProvider>(sp => sp.GetRequiredService<TwelveDataProvider>());
        services.AddSingleton<IMarketDataProvider>(sp => sp.GetRequiredService<AlphaVantageProvider>());
        services.AddSingleton<IMarketDataProvider>(sp => sp.GetRequiredService<StooqProvider>());
        services.AddSingleton<IMarketDataProvider>(sp => sp.GetRequiredService<YahooFinanceProvider>());

        services.AddSingleton<IProviderFactory, ProviderFactory>();
        services.AddSingleton<FailoverProviderChain>();

        services.AddSingleton<PolygonWebSocketClient>();
        services.AddSingleton<FinnhubWebSocketClient>();

        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TradeScannerDbContext>();
        await db.Database.MigrateAsync();
    }
}
