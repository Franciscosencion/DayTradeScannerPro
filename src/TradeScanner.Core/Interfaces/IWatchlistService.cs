using TradeScanner.Core.Domain.Entities;
using TradeScanner.Core.Domain.ValueObjects;

namespace TradeScanner.Core.Interfaces;

public interface IWatchlistService
{
    Task<IReadOnlyList<WatchlistEntry>> GetWatchlistAsync(CancellationToken ct = default);
    Task AddSymbolAsync(string symbol, CancellationToken ct = default);
    Task RemoveSymbolAsync(string symbol, CancellationToken ct = default);
    Task UpdateEntryAsync(WatchlistEntry entry, CancellationToken ct = default);
    Task<bool> IsWatchedAsync(string symbol, CancellationToken ct = default);
    Task<IReadOnlyList<Quote>> GetWatchlistQuotesAsync(CancellationToken ct = default);
}
