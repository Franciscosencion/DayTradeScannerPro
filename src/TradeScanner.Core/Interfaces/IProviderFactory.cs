using TradeScanner.Core.Domain.Enums;

namespace TradeScanner.Core.Interfaces;

public interface IProviderFactory
{
    IMarketDataProvider GetProvider(MarketDataProvider provider);
    IMarketDataProvider GetBestAvailableProvider();
    IReadOnlyList<IMarketDataProvider> GetAllProviders();
    IReadOnlyList<IMarketDataProvider> GetEnabledProviders();
}
