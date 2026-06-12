using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TradeScanner.Application.Ranking;
using TradeScanner.Application.Scanner;
using TradeScanner.Core.Domain.Entities;
using TradeScanner.Core.Domain.ValueObjects;
using TradeScanner.Core.Interfaces;
using TradeScanner.Infrastructure.Data;
using TradeScanner.Infrastructure.Repositories;
using Xunit;

namespace TradeScanner.Tests.Unit;

public class ScannerFilterTests
{
    private static ScannerService BuildService(
        IMarketDataProvider provider,
        AppSettings? settings = null)
    {
        var options = new DbContextOptionsBuilder<TradeScannerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var services = new ServiceCollection();
        services.AddSingleton(new TradeScannerDbContext(options));
        services.AddScoped<ScanResultRepository>();

        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var svc = new ScannerService(
            provider,
            new RankingEngine(),
            scopeFactory,
            NullLogger<ScannerService>.Instance);

        if (settings != null) svc.UpdateSettings(settings);
        return svc;
    }

    [Fact]
    public async Task RunSingleScan_FiltersOutBelowMinPrice()
    {
        var provider = MockProvider(
            new Quote("CHEAP", 0.50m, 0.50m, 0.50m, 0.50m, 0.40m, 1_000_000, 10m, 0.1m, DateTime.UtcNow));

        var svc = BuildService(provider, new AppSettings { MinPrice = 1m, MinChangePercent = 0m, MinVolume = 0, MinTradeScore = 0 });
        var results = await svc.RunSingleScanAsync();

        results.Should().NotContain(r => r.Symbol == "CHEAP");
    }

    [Fact]
    public async Task RunSingleScan_FiltersOutBelowMinVolume()
    {
        var provider = MockProvider(
            new Quote("LOWVOL", 50m, 50m, 51m, 49m, 49m, 10_000, 3m, 1.5m, DateTime.UtcNow));

        var svc = BuildService(provider, new AppSettings { MinVolume = 500_000, MinPrice = 0m, MinChangePercent = 0m, MinTradeScore = 0 });
        var results = await svc.RunSingleScanAsync();

        results.Should().NotContain(r => r.Symbol == "LOWVOL");
    }

    [Fact]
    public async Task RunSingleScan_FiltersOutBelowMinChangePercent()
    {
        var provider = MockProvider(
            new Quote("FLAT", 100m, 100m, 100.1m, 99.9m, 99.9m, 2_000_000, 0.1m, 0.1m, DateTime.UtcNow));

        var svc = BuildService(provider, new AppSettings { MinChangePercent = 2m, MinPrice = 0m, MinVolume = 0, MinTradeScore = 0 });
        var results = await svc.RunSingleScanAsync();

        results.Should().NotContain(r => r.Symbol == "FLAT");
    }

    [Fact]
    public async Task RunSingleScan_PassesStockThatMeetsAllFilters()
    {
        var provider = MockProvider(
            new Quote("GOOD", 50m, 48m, 53m, 47m, 45m, 5_000_000, 5m, 2.5m, DateTime.UtcNow));

        var svc = BuildService(provider, new AppSettings
        {
            MinPrice = 1m, MaxPrice = 500m, MinVolume = 100_000, MinChangePercent = 1m, MinTradeScore = 0
        });
        var results = await svc.RunSingleScanAsync();

        results.Should().Contain(r => r.Symbol == "GOOD");
    }

    [Fact]
    public async Task RunSingleScan_RespectsMaxResultsPerScan()
    {
        var quotes = Enumerable.Range(1, 20)
            .Select(i => new Quote($"SYM{i:00}", 50m, 48m, 53m, 47m, 45m,
                5_000_000, 5m + i * 0.1m, 2.5m, DateTime.UtcNow))
            .ToList();

        var provider = MockProviderMany(quotes);
        var svc = BuildService(provider, new AppSettings
        {
            MinPrice = 1m, MaxPrice = 500m, MinVolume = 0, MinChangePercent = 0m,
            MinTradeScore = 0, MaxResultsPerScan = 5
        });

        var results = await svc.RunSingleScanAsync();

        results.Count.Should().BeLessThanOrEqualTo(5);
    }

    // -------------------------------------------------------------------------

    private static IMarketDataProvider MockProvider(Quote quote)
    {
        var mock = new Mock<IMarketDataProvider>();
        mock.Setup(p => p.GetMostActiveSymbolsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([quote.Symbol]);
        mock.Setup(p => p.GetQuotesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([quote]);
        mock.Setup(p => p.GetHistoricalBarsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        mock.Setup(p => p.Priority).Returns(1);
        mock.Setup(p => p.ProviderType).Returns(Core.Domain.Enums.MarketDataProvider.PolygonIo);
        return mock.Object;
    }

    private static IMarketDataProvider MockProviderMany(IReadOnlyList<Quote> quotes)
    {
        var mock = new Mock<IMarketDataProvider>();
        mock.Setup(p => p.GetMostActiveSymbolsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(quotes.Select(q => q.Symbol).ToList());
        mock.Setup(p => p.GetQuotesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(quotes);
        mock.Setup(p => p.GetHistoricalBarsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        mock.Setup(p => p.Priority).Returns(1);
        mock.Setup(p => p.ProviderType).Returns(Core.Domain.Enums.MarketDataProvider.PolygonIo);
        return mock.Object;
    }
}
