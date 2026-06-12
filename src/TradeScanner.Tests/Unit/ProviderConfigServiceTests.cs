using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TradeScanner.Application.Config;
using TradeScanner.Core.Domain.Enums;
using TradeScanner.Core.Interfaces;
using TradeScanner.Infrastructure.Data;
using TradeScanner.Infrastructure.Repositories;
using Xunit;

namespace TradeScanner.Tests.Unit;

public class ProviderConfigServiceTests : IDisposable
{
    private readonly TradeScannerDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Mock<ISecurityService> _security;

    public ProviderConfigServiceTests()
    {
        var options = new DbContextOptionsBuilder<TradeScannerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TradeScannerDbContext(options);

        var services = new ServiceCollection();
        services.AddSingleton(_db);
        services.AddScoped<ProviderConfigRepository>();
        _scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        _security = new Mock<ISecurityService>();
        _security.Setup(s => s.Encrypt(It.IsAny<string>())).Returns<string>(k => $"ENC:{k}");
        _security.Setup(s => s.TryDecrypt(It.IsAny<string>(), out It.Ref<string>.IsAny))
            .Returns((string cipher, out string plain) =>
            {
                plain = cipher.StartsWith("ENC:") ? cipher[4..] : string.Empty;
                return plain.Length > 0;
            });
    }

    public void Dispose() => _db.Dispose();

    private ProviderConfigService BuildService(IEnumerable<IMarketDataProvider>? providers = null)
    {
        providers ??= [];
        return new ProviderConfigService(
            providers,
            _security.Object,
            _scopeFactory,
            NullLogger<ProviderConfigService>.Instance);
    }

    [Fact]
    public async Task SaveKey_PersistsEncryptedKeyToDatabase()
    {
        var svc = BuildService();
        await svc.SaveKeyAsync(MarketDataProvider.PolygonIo, "my-secret-key");

        var configs = await svc.GetAllAsync();
        configs.Should().ContainSingle(c => c.Provider == MarketDataProvider.PolygonIo);
        configs.First().EncryptedApiKey.Should().Be("ENC:my-secret-key");
    }

    [Fact]
    public async Task GetDecryptedKey_ReturnsPlaintextAfterSave()
    {
        var svc = BuildService();
        await svc.SaveKeyAsync(MarketDataProvider.Finnhub, "finnhub-token-123");

        var key = await svc.GetDecryptedKeyAsync(MarketDataProvider.Finnhub);

        key.Should().Be("finnhub-token-123");
    }

    [Fact]
    public async Task GetDecryptedKey_ReturnsNullWhenNoKeyStored()
    {
        var svc = BuildService();
        var key = await svc.GetDecryptedKeyAsync(MarketDataProvider.PolygonIo);
        key.Should().BeNull();
    }

    [Fact]
    public async Task SaveKey_OverwritesExistingKey()
    {
        var svc = BuildService();
        await svc.SaveKeyAsync(MarketDataProvider.PolygonIo, "old-key");
        await svc.SaveKeyAsync(MarketDataProvider.PolygonIo, "new-key");

        var key = await svc.GetDecryptedKeyAsync(MarketDataProvider.PolygonIo);
        key.Should().Be("new-key");

        var configs = await svc.GetAllAsync();
        configs.Count(c => c.Provider == MarketDataProvider.PolygonIo).Should().Be(1);
    }

    [Fact]
    public async Task LoadAndApply_CallsSetApiKeyOnMatchingProvider()
    {
        var provider = new Mock<IMarketDataProvider>();
        provider.Setup(p => p.ProviderType).Returns(MarketDataProvider.PolygonIo);

        var svc = BuildService([provider.Object]);
        await svc.SaveKeyAsync(MarketDataProvider.PolygonIo, "polygon-key");

        await svc.LoadAndApplyAsync();

        provider.Verify(p => p.SetApiKey("polygon-key"), Times.AtLeastOnce);
    }

    [Fact]
    public async Task LoadAndApply_SkipsProviderWithNoSavedKey()
    {
        var provider = new Mock<IMarketDataProvider>();
        provider.Setup(p => p.ProviderType).Returns(MarketDataProvider.Finnhub);

        var svc = BuildService([provider.Object]);

        await svc.LoadAndApplyAsync();

        provider.Verify(p => p.SetApiKey(It.IsAny<string>()), Times.Never);
    }
}
