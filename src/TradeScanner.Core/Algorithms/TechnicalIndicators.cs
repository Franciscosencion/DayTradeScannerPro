using TradeScanner.Core.Domain.ValueObjects;

namespace TradeScanner.Core.Algorithms;

public static class TechnicalIndicators
{
    public static decimal Sma(IReadOnlyList<PriceBar> bars, int period)
    {
        if (bars.Count < period) return 0;
        return bars.TakeLast(period).Average(b => b.Close);
    }

    public static decimal Rsi(IReadOnlyList<PriceBar> bars, int period = 14)
    {
        if (bars.Count < period + 1) return 50;

        var changes = bars.TakeLast(period + 1).Zip(bars.TakeLast(period + 1).Skip(1))
            .Select(pair => pair.Second.Close - pair.First.Close).ToList();

        var gains = changes.Where(c => c > 0).DefaultIfEmpty(0).Average();
        var losses = changes.Where(c => c < 0).Select(c => -c).DefaultIfEmpty(0).Average();

        if (losses == 0) return 100;
        var rs = gains / losses;
        return 100 - (100 / (1 + rs));
    }

    public static (decimal Upper, decimal Middle, decimal Lower) BollingerBands(
        IReadOnlyList<PriceBar> bars, int period = 20, decimal stdDevMultiplier = 2)
    {
        if (bars.Count < period) return (0, 0, 0);

        var closes = bars.TakeLast(period).Select(b => b.Close).ToList();
        var sma = closes.Average();
        var variance = closes.Average(c => (c - sma) * (c - sma));
        var stdDev = (decimal)Math.Sqrt((double)variance);

        return (sma + stdDevMultiplier * stdDev, sma, sma - stdDevMultiplier * stdDev);
    }

    public static decimal AverageTrueRange(IReadOnlyList<PriceBar> bars, int period = 14)
    {
        if (bars.Count < 2) return 0;

        var trValues = bars.Skip(1).Zip(bars)
            .Select(pair =>
            {
                var (curr, prev) = pair;
                var tr = Math.Max(curr.High - curr.Low,
                    Math.Max(Math.Abs(curr.High - prev.Close), Math.Abs(curr.Low - prev.Close)));
                return tr;
            }).TakeLast(period).ToList();

        return trValues.Any() ? trValues.Average() : 0;
    }

    public static bool IsBreakout(IReadOnlyList<PriceBar> bars, int lookback = 20)
    {
        if (bars.Count < lookback + 1) return false;
        var recent = bars.TakeLast(lookback + 1).ToList();
        var priorHigh = recent.SkipLast(1).Max(b => b.High);
        return recent.Last().Close > priorHigh;
    }

    public static decimal VolumeRatio(IReadOnlyList<PriceBar> bars, int avgPeriod = 20)
    {
        if (bars.Count < avgPeriod + 1) return 1;
        var avgVol = bars.SkipLast(1).TakeLast(avgPeriod).Average(b => b.Volume);
        return avgVol > 0 ? bars.Last().Volume / (decimal)avgVol : 1;
    }
}
