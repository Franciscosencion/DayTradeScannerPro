using TradeScanner.Core.Domain.Entities;
using TradeScanner.Core.Domain.ValueObjects;

namespace TradeScanner.Core.Interfaces;

public interface IRankingEngine
{
    TradeScore CalculateScore(Quote quote, IReadOnlyList<PriceBar> history, IReadOnlyList<NewsItem> news);
    IReadOnlyList<ScanResult> RankResults(IEnumerable<ScanResult> results);
}
