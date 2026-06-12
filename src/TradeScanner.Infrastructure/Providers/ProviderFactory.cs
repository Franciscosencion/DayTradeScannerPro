using TradeScanner.Core.Domain.Enums;
using TradeScanner.Core.Interfaces;

namespace TradeScanner.Infrastructure.Providers;

public class ProviderFactory(IEnumerable<IMarketDataProvider> providers) : IProviderFactory
{
    private readonly IReadOnlyList<IMarketDataProvider> _providers = providers
        .Where(p => p is not Failover.FailoverProviderChain)
        .OrderBy(p => p.Priority)
        .ToList();

    public IMarketDataProvider GetProvider(MarketDataProvider provider) =>
        _providers.FirstOrDefault(p => p.ProviderType == provider)
        ?? throw new InvalidOperationException($"Provider {provider} not registered");

    public IMarketDataProvider GetBestAvailableProvider() =>
        GetEnabledProviders().FirstOrDefault()
        ?? throw new InvalidOperationException("No providers available");

    public IReadOnlyList<IMarketDataProvider> GetAllProviders() => _providers;

    public IReadOnlyList<IMarketDataProvider> GetEnabledProviders() =>
        _providers.Where(p => p.IsAvailable).ToList();
}
