using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TradeScanner.Application.Alerts;
using TradeScanner.Core.Domain.Entities;
using TradeScanner.Core.Domain.Enums;
using TradeScanner.Core.Domain.ValueObjects;
using TradeScanner.Core.Interfaces;
using TradeScanner.Infrastructure.Data;
using TradeScanner.Infrastructure.Repositories;
using Xunit;

namespace TradeScanner.Tests.Unit;

public class AlertServiceTests : IDisposable
{
    private readonly TradeScannerDbContext _db;
    private readonly AlertRepository _repo;
    private readonly AlertService _svc;

    public AlertServiceTests()
    {
        var options = new DbContextOptionsBuilder<TradeScannerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TradeScannerDbContext(options);
        _repo = new AlertRepository(_db);
        _svc = new AlertService(_repo, NullLogger<AlertService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task PriceAbove_WhenPriceExceedsThreshold_TriggersAlert()
    {
        await _repo.AddAsync(new AlertRule
        {
            Symbol = "AAPL", AlertType = AlertType.PriceAbove, Threshold = 150m, IsEnabled = true
        });

        AlertTriggeredEventArgs? fired = null;
        _svc.AlertTriggered += (_, args) => fired = args;

        await _svc.EvaluateAlertsAsync([Quote("AAPL", 155m, 0m, 0)]);

        fired.Should().NotBeNull();
        fired!.Rule.AlertType.Should().Be(AlertType.PriceAbove);
    }

    [Fact]
    public async Task PriceAbove_WhenPriceBelowThreshold_DoesNotTrigger()
    {
        await _repo.AddAsync(new AlertRule
        {
            Symbol = "AAPL", AlertType = AlertType.PriceAbove, Threshold = 150m, IsEnabled = true
        });

        bool triggered = false;
        _svc.AlertTriggered += (_, _) => triggered = true;

        await _svc.EvaluateAlertsAsync([Quote("AAPL", 140m, 0m, 0)]);

        triggered.Should().BeFalse();
    }

    [Fact]
    public async Task PriceBelow_WhenPriceBelowThreshold_Triggers()
    {
        await _repo.AddAsync(new AlertRule
        {
            Symbol = "TSLA", AlertType = AlertType.PriceBelow, Threshold = 200m, IsEnabled = true
        });

        AlertTriggeredEventArgs? fired = null;
        _svc.AlertTriggered += (_, args) => fired = args;

        await _svc.EvaluateAlertsAsync([Quote("TSLA", 190m, 0m, 0)]);

        fired.Should().NotBeNull();
    }

    [Fact]
    public async Task PercentChangeUp_WhenChangeExceedsThreshold_Triggers()
    {
        await _repo.AddAsync(new AlertRule
        {
            Symbol = "NVDA", AlertType = AlertType.PercentChangeUp, Threshold = 5m, IsEnabled = true
        });

        bool triggered = false;
        _svc.AlertTriggered += (_, _) => triggered = true;

        await _svc.EvaluateAlertsAsync([Quote("NVDA", 500m, 6m, 500_000)]);

        triggered.Should().BeTrue();
    }

    [Fact]
    public async Task PercentChangeDown_WhenDropExceedsThreshold_Triggers()
    {
        await _repo.AddAsync(new AlertRule
        {
            Symbol = "AMD", AlertType = AlertType.PercentChangeDown, Threshold = 3m, IsEnabled = true
        });

        bool triggered = false;
        _svc.AlertTriggered += (_, _) => triggered = true;

        await _svc.EvaluateAlertsAsync([Quote("AMD", 100m, -4m, 1_000_000)]);

        triggered.Should().BeTrue();
    }

    [Fact]
    public async Task VolumeAbove_WhenVolumeExceedsThreshold_Triggers()
    {
        await _repo.AddAsync(new AlertRule
        {
            Symbol = "SPY", AlertType = AlertType.VolumeAbove, Threshold = 1_000_000m, IsEnabled = true
        });

        bool triggered = false;
        _svc.AlertTriggered += (_, _) => triggered = true;

        await _svc.EvaluateAlertsAsync([Quote("SPY", 450m, 1m, 2_000_000)]);

        triggered.Should().BeTrue();
    }

    [Fact]
    public async Task InactiveAlert_NeverTriggers()
    {
        await _repo.AddAsync(new AlertRule
        {
            Symbol = "AAPL", AlertType = AlertType.PriceAbove, Threshold = 100m, IsEnabled = false
        });

        bool triggered = false;
        _svc.AlertTriggered += (_, _) => triggered = true;

        await _svc.EvaluateAlertsAsync([Quote("AAPL", 999m, 0m, 0)]);

        triggered.Should().BeFalse();
    }

    [Fact]
    public async Task Alert_ForDifferentSymbol_DoesNotTrigger()
    {
        await _repo.AddAsync(new AlertRule
        {
            Symbol = "AAPL", AlertType = AlertType.PriceAbove, Threshold = 100m, IsEnabled = true
        });

        bool triggered = false;
        _svc.AlertTriggered += (_, _) => triggered = true;

        await _svc.EvaluateAlertsAsync([Quote("MSFT", 500m, 0m, 0)]);

        triggered.Should().BeFalse();
    }

    private static Quote Quote(string symbol, decimal price, decimal changePct, long volume) =>
        new(symbol, price, price, price, price, price, volume, changePct, 0, DateTime.UtcNow);
}
