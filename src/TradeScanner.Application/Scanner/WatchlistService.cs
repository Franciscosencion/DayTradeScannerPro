using TradeScanner.Core.Domain.Entities;
using TradeScanner.Core.Domain.ValueObjects;
using TradeScanner.Core.Interfaces;
using TradeScanner.Infrastructure.Repositories;

namespace TradeScanner.Application.Scanner;

public class WatchlistService(
    WatchlistRepository watchlistRepo,
    IMarketDataProvider provider) : IWatchlistService
{
    public async Task<IReadOnlyList<WatchlistEntry>> GetWatchlistAsync(CancellationToken ct = default) =>
        await watchlistRepo.GetAllAsync(ct);

    public async Task AddSymbolAsync(string symbol, CancellationToken ct = default)
    {
        if (await watchlistRepo.ExistsAsync(symbol, ct)) return;
        await watchlistRepo.AddAsync(new WatchlistEntry { Symbol = symbol.ToUpperInvariant() }, ct);
    }

    public async Task RemoveSymbolAsync(string symbol, CancellationToken ct = default) =>
        await watchlistRepo.RemoveAsync(symbol, ct);

    public async Task UpdateEntryAsync(WatchlistEntry entry, CancellationToken ct = default) =>
        await watchlistRepo.UpdateAsync(entry, ct);

    public async Task<bool> IsWatchedAsync(string symbol, CancellationToken ct = default) =>
        await watchlistRepo.ExistsAsync(symbol, ct);

    public async Task<IReadOnlyList<Quote>> GetWatchlistQuotesAsync(CancellationToken ct = default)
    {
        var entries = await watchlistRepo.GetAllAsync(ct);
        if (entries.Count == 0) return [];
        return await provider.GetQuotesAsync(entries.Select(e => e.Symbol), ct);
    }
}
