using TradeScanner.Core.Algorithms;
using TradeScanner.Core.Domain.Entities;
using TradeScanner.Core.Domain.Enums;
using TradeScanner.Core.Domain.ValueObjects;
using TradeScanner.Core.Interfaces;

namespace TradeScanner.Application.Ranking;

public class RankingEngine : IRankingEngine
{
    private decimal _momentumWeight = 0.35m;
    private decimal _volumeWeight = 0.30m;
    private decimal _technicalWeight = 0.25m;
    private decimal _newsWeight = 0.10m;

    public void UpdateWeights(AppSettings settings)
    {
        _momentumWeight = settings.MomentumWeight;
        _volumeWeight = settings.VolumeWeight;
        _technicalWeight = settings.TechnicalWeight;
        _newsWeight = settings.NewsWeight;
    }

    public TradeScore CalculateScore(Quote quote, IReadOnlyList<PriceBar> history, IReadOnlyList<NewsItem> news)
    {
        var momentum = CalculateMomentumScore(quote, history);
        var volume = CalculateVolumeScore(quote, history);
        var technical = CalculateTechnicalScore(quote, history);
        var newsScore = CalculateNewsScore(news);

        var composite = momentum * _momentumWeight
            + volume * _volumeWeight
            + technical * _technicalWeight
            + newsScore * _newsWeight;

        return new TradeScore(
            (int)Math.Round(Math.Clamp(composite, 0, 100)),
            momentum, volume, technical, newsScore,
            DateTime.UtcNow);
    }

    public IReadOnlyList<ScanResult> RankResults(IEnumerable<ScanResult> results) =>
        results.OrderByDescending(r => r.TradeScore)
               .ThenByDescending(r => r.VolumeRatio)
               .ToList();

    private static decimal CalculateMomentumScore(Quote quote, IReadOnlyList<PriceBar> history)
    {
        var score = 0m;

        // Intraday change percent (0-40 pts)
        var changePct = Math.Abs(quote.ChangePercent);
        score += Math.Min(changePct * 4, 40);

        // Direction bonus (20 pts max)
        if (quote.ChangePercent > 0) score += 20;

        // RSI positioning (0-40 pts)
        var rsi = TechnicalIndicators.Rsi(history);
        if (rsi > 50 && rsi < 70) score += 40; // sweet spot for momentum
        else if (rsi >= 70) score += 20; // overbought
        else if (rsi > 40) score += 10;

        return Math.Clamp(score, 0, 100);
    }

    private static decimal CalculateVolumeScore(Quote quote, IReadOnlyList<PriceBar> history)
    {
        var score = 0m;
        var volRatio = TechnicalIndicators.VolumeRatio(history);

        // Volume ratio scoring
        score += volRatio switch
        {
            >= 5 => 100,
            >= 3 => 80,
            >= 2 => 60,
            >= 1.5m => 40,
            >= 1 => 20,
            _ => 0
        };

        return Math.Clamp(score, 0, 100);
    }

    private static decimal CalculateTechnicalScore(Quote quote, IReadOnlyList<PriceBar> history)
    {
        var score = 0m;

        // Breakout check (30 pts)
        if (TechnicalIndicators.IsBreakout(history)) score += 30;

        // Bollinger band position (30 pts)
        var (upper, middle, lower) = TechnicalIndicators.BollingerBands(history);
        if (upper > 0 && quote.Price > middle && quote.Price < upper) score += 30;
        else if (quote.Price >= upper) score += 15; // extended — partial credit

        // SMA trend (20 pts)
        var sma20 = TechnicalIndicators.Sma(history, 20);
        var sma50 = TechnicalIndicators.Sma(history, 50);
        if (sma20 > 0 && quote.Price > sma20) score += 10;
        if (sma50 > 0 && sma20 > sma50) score += 10;

        // Intraday range quality (20 pts)
        var atr = TechnicalIndicators.AverageTrueRange(history);
        if (atr > 0 && (quote.High - quote.Low) > atr * 0.5m) score += 20;

        return Math.Clamp(score, 0, 100);
    }

    private static decimal CalculateNewsScore(IReadOnlyList<NewsItem> news)
    {
        if (news.Count == 0) return 50; // neutral

        var avgSentiment = (decimal)news.Average(n => n.SentimentScore);
        // Sentiment is typically -1 to +1; map to 0-100
        return Math.Clamp((avgSentiment + 1) * 50, 0, 100);
    }
}
