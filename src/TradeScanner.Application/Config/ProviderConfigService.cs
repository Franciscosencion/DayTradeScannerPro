using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradeScanner.Core.Domain.Entities;
using TradeScanner.Core.Domain.Enums;
using TradeScanner.Core.Interfaces;
using TradeScanner.Infrastructure.Repositories;

namespace TradeScanner.Application.Config;

public class ProviderConfigService(
    IEnumerable<IMarketDataProvider> providers,
    ISecurityService security,
    IServiceScopeFactory scopeFactory,
    ILogger<ProviderConfigService> logger) : IProviderConfigService
{
    private readonly IReadOnlyList<IMarketDataProvider> _providers =
        providers.Where(p => p is not Infrastructure.Providers.Failover.FailoverProviderChain).ToList();

    public async Task LoadAndApplyAsync(CancellationToken ct = default)
    {
        var configs = await GetAllAsync(ct);
        int applied = 0;

        foreach (var config in configs)
        {
            if (string.IsNullOrEmpty(config.EncryptedApiKey)) continue;
            if (!security.TryDecrypt(config.EncryptedApiKey, out var key)) continue;
            if (string.IsNullOrEmpty(key)) continue;

            var provider = _providers.FirstOrDefault(p => p.ProviderType == config.Provider);
            if (provider == null) continue;

            provider.SetApiKey(key);
            applied++;
        }

        logger.LogInformation("Loaded and applied {Count} API keys from storage", applied);
    }

    public async Task SaveKeyAsync(MarketDataProvider providerType, string apiKey, CancellationToken ct = default)
    {
        var encrypted = security.Encrypt(apiKey);

        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProviderConfigRepository>();

        var existing = await repo.GetByProviderAsync(providerType, ct);
        var config = existing ?? new ProviderConfig { Provider = providerType };

        config.EncryptedApiKey = encrypted;
        config.DisplayName = _providers.FirstOrDefault(p => p.ProviderType == providerType)?.DisplayName
                             ?? providerType.ToString();
        config.IsEnabled = true;

        await repo.UpsertAsync(config, ct);

        // Apply immediately to the live singleton provider
        var provider = _providers.FirstOrDefault(p => p.ProviderType == providerType);
        provider?.SetApiKey(apiKey);

        logger.LogInformation("Saved API key for {Provider}", providerType);
    }

    public async Task<IReadOnlyList<ProviderConfig>> GetAllAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProviderConfigRepository>();
        return await repo.GetAllAsync(ct);
    }

    public async Task<string?> GetDecryptedKeyAsync(MarketDataProvider provider, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ProviderConfigRepository>();
        var config = await repo.GetByProviderAsync(provider, ct);
        if (config == null || string.IsNullOrEmpty(config.EncryptedApiKey)) return null;
        return security.TryDecrypt(config.EncryptedApiKey, out var key) ? key : null;
    }
}
