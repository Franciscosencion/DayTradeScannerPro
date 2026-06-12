using TradeScanner.Core.Domain.ValueObjects;

namespace TradeScanner.Core.Interfaces;

public interface INewsProvider
{
    Task<IReadOnlyList<NewsItem>> GetNewsAsync(string symbol, int count = 10, CancellationToken ct = default);
    Task<IReadOnlyList<NewsItem>> GetMarketNewsAsync(int count = 20, CancellationToken ct = default);
}
