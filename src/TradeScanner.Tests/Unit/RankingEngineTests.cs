using FluentAssertions;
using TradeScanner.Application.Ranking;
using Xunit;
using TradeScanner.Core.Domain.ValueObjects;

namespace TradeScanner.Tests.Unit;

public class RankingEngineTests
{
    private readonly RankingEngine _engine = new();

    [Fact]
    public void CalculateScore_StrongMomentum_ReturnsHighScore()
    {
        var quote = new Quote("AAPL", 150m, 145m, 152m, 144m, 145m, 2_000_000, 5.0m, 7.5m, DateTime.UtcNow);
        var history = BuildHistory(50, 140m, 150m, true);

        var score = _engine.CalculateScore(quote, history, []);

        score.Score.Should().BeGreaterThanOrEqualTo(50);
    }

    [Fact]
    public void CalculateScore_WeakMomentum_ReturnsLowScore()
    {
        var quote = new Quote("XYZ", 10m, 10.1m, 10.2m, 9.9m, 10.1m, 100_000, -0.5m, -0.05m, DateTime.UtcNow);
        var history = BuildHistory(50, 10m, 10m, false);

        var score = _engine.CalculateScore(quote, history, []);

        score.Score.Should().BeLessThan(75);
    }

    [Fact]
    public void CalculateScore_ScoreInRange()
    {
        var quote = new Quote("TSLA", 200m, 198m, 205m, 197m, 195m, 5_000_000, 3.0m, 6.0m, DateTime.UtcNow);
        var history = BuildHistory(50, 190m, 200m, true);

        var score = _engine.CalculateScore(quote, history, []);

        score.Score.Should().BeInRange(0, 100);
    }

    [Fact]
    public void RankResults_OrdersByTradeScore()
    {
        var results = new[]
        {
            new TradeScanner.Core.Domain.Entities.ScanResult { Symbol = "A", TradeScore = 70 },
            new TradeScanner.Core.Domain.Entities.ScanResult { Symbol = "B", TradeScore = 90 },
            new TradeScanner.Core.Domain.Entities.ScanResult { Symbol = "C", TradeScore = 55 },
        };

        var ranked = _engine.RankResults(results);

        ranked[0].Symbol.Should().Be("B");
        ranked[1].Symbol.Should().Be("A");
        ranked[2].Symbol.Should().Be("C");
    }

    private static IReadOnlyList<PriceBar> BuildHistory(int count, decimal start, decimal end, bool bullish)
    {
        var bars = new List<PriceBar>();
        for (int i = 0; i < count; i++)
        {
            var close = start + (end - start) * i / count;
            var open = bullish ? close - 0.5m : close + 0.5m;
            bars.Add(new PriceBar("TEST", DateTime.UtcNow.AddMinutes(-count + i),
                open, close + 1m, close - 1m, close, 1_000_000, "5m"));
        }
        return bars;
    }
}
