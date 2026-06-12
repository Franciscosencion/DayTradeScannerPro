using TradeScanner.Core.Domain.Entities;
using TradeScanner.Core.Domain.Enums;

namespace TradeScanner.Core.Interfaces;

public interface IProviderConfigService
{
    Task LoadAndApplyAsync(CancellationToken ct = default);
    Task SaveKeyAsync(MarketDataProvider provider, string apiKey, CancellationToken ct = default);
    Task<IReadOnlyList<ProviderConfig>> GetAllAsync(CancellationToken ct = default);
    Task<string?> GetDecryptedKeyAsync(MarketDataProvider provider, CancellationToken ct = default);
}
