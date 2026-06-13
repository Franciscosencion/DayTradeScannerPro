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
        // Yahoo screener — no auth, browser User-Agent only
        services.AddResilientHttpClient("YahooScreener")
            .ConfigureHttpClient(c =>
            {
                c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
                c.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json,text/plain,*/*");
                c.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
                c.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://finance.yahoo.com/");
            });
        // Yahoo Finance crumb provider creates its own HttpClient with CookieContainer
        services.AddResilientHttpClient("AlphaVantage");
        services.AddResilientHttpClient("FMP");
        services.AddResilientHttpClient("TwelveData");
        services.AddResilientHttpClient("Stooq");

        services.AddSingleton<PolygonProvider>();
        services.AddSingleton<YahooScreenerProvider>();
        services.AddSingleton<FinnhubProvider>();
        services.AddSingleton<FmpProvider>();
        services.AddSingleton<TwelveDataProvider>();
        services.AddSingleton<AlphaVantageProvider>();
        services.AddSingleton<StooqProvider>();
        services.AddSingleton<YahooFinanceProvider>();

        // Register each concrete provider also as IMarketDataProvider for IEnumerable<IMarketDataProvider> injection in ProviderFactory
        // Order matters: last registered = default single-instance resolution; FailoverChain registered last so ScannerService uses it
        services.AddSingleton<IMarketDataProvider>(sp => sp.GetRequiredService<PolygonProvider>());
        services.AddSingleton<IMarketDataProvider>(sp => sp.GetRequiredService<YahooScreenerProvider>());
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

        // If the database was previously created via EnsureCreated (which creates tables
        // but does NOT record anything in __EFMigrationsHistory), MigrateAsync() will
        // try to re-apply every migration and crash with "table already exists".
        // Detect that state and backfill the migration record before migrating.
        await BackfillMigrationHistoryIfNeededAsync(db);

        await db.Database.MigrateAsync();
    }

    private static async Task BackfillMigrationHistoryIfNeededAsync(TradeScannerDbContext db)
    {
        var conn = db.Database.GetDbConnection();
        var opened = conn.State != System.Data.ConnectionState.Open;
        if (opened) await conn.OpenAsync();
        try
        {
            using var cmd = conn.CreateCommand();

            // Ensure the migrations history table exists
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                    "ProductVersion" TEXT NOT NULL
                );
                """;
            await cmd.ExecuteNonQueryAsync();

            // Check whether AppSettings already exists (tables were created without migration tracking)
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='AppSettings'";
            var tablesExist = (long)(await cmd.ExecuteScalarAsync())! > 0;

            // Check whether the InitialCreate migration is already recorded
            cmd.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20260611225422_InitialCreate'";
            var alreadyRecorded = (long)(await cmd.ExecuteScalarAsync())! > 0;

            // If tables are present but the migration isn't recorded, backfill it so
            // MigrateAsync() treats this database as already at the baseline schema.
            if (tablesExist && !alreadyRecorded)
            {
                cmd.CommandText = """
                    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                    VALUES ('20260611225422_InitialCreate', '9.0.0')
                    """;
                await cmd.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            if (opened) await conn.CloseAsync();
        }
    }
}
