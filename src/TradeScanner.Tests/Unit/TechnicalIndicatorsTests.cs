using FluentAssertions;
using TradeScanner.Core.Algorithms;
using Xunit;
using TradeScanner.Core.Domain.ValueObjects;

namespace TradeScanner.Tests.Unit;

public class TechnicalIndicatorsTests
{
    [Fact]
    public void Sma_ReturnsCorrectAverage()
    {
        var bars = Enumerable.Range(1, 20)
            .Select(i => new PriceBar("X", DateTime.UtcNow.AddMinutes(-i), 0, 0, 0, i, 0, "1m"))
            .ToList();

        var sma = TechnicalIndicators.Sma(bars, 10);

        // Last 10 values: 11..20 → avg = 15.5
        sma.Should().Be(15.5m);
    }

    [Fact]
    public void Rsi_OverboughtReturnsHighValue()
    {
        // All up bars → RSI near 100
        var bars = Enumerable.Range(1, 20)
            .Select(i => new PriceBar("X", DateTime.UtcNow.AddMinutes(-i), i, i + 1, i - 0.5m, i + 0.5m, 0, "1m"))
            .ToList();

        var rsi = TechnicalIndicators.Rsi(bars, 14);

        rsi.Should().BeGreaterThan(70);
    }

    [Fact]
    public void IsBreakout_AbovePriorHigh_ReturnsTrue()
    {
        var bars = Enumerable.Range(1, 21)
            .Select(i => new PriceBar("X", DateTime.UtcNow.AddMinutes(-21 + i), 100, 105, 95, 100, 0, "1m"))
            .ToList();

        // Last bar breaks above 105
        bars[^1] = bars[^1] with { Close = 110, High = 112 };

        var result = TechnicalIndicators.IsBreakout(bars);

        result.Should().BeTrue();
    }

    [Fact]
    public void VolumeRatio_HighVolume_ReturnsRatioAboveOne()
    {
        var bars = Enumerable.Range(1, 21)
            .Select(i => new PriceBar("X", DateTime.UtcNow.AddMinutes(-21 + i), 0, 0, 0, 0, 1_000_000, "1m"))
            .ToList();

        bars[^1] = bars[^1] with { Volume = 3_000_000 };

        var ratio = TechnicalIndicators.VolumeRatio(bars);

        ratio.Should().BeGreaterThan(1.0m);
    }
}
