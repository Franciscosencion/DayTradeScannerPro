using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using TradeScanner.Application.Scanner;
using TradeScanner.Core.Domain.Entities;
using TradeScanner.Core.Interfaces;
using TradeScanner.Infrastructure.Data;
using TradeScanner.Infrastructure.Repositories;
using Xunit;

namespace TradeScanner.Tests.Unit;

public class WatchlistServiceTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly TradeScannerDbContext _db;
    private readonly WatchlistService _svc;

    public WatchlistServiceTests()
    {
        // Use SQLite in-memory (not EF InMemory) to support ExecuteDeleteAsync
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();
        var options = new DbContextOptionsBuilder<TradeScannerDbContext>()
            .UseSqlite(_conn)
            .Options;
        _db = new TradeScannerDbContext(options);
        _db.Database.EnsureCreated();
        var provider = new Mock<IMarketDataProvider>().Object;
        _svc = new WatchlistService(new WatchlistRepository(_db), provider);
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    [Fact]
    public async Task AddSymbol_AppearsInGetAll()
    {
        await _svc.AddSymbolAsync("AAPL");
        var entries = await _svc.GetWatchlistAsync();
        entries.Should().ContainSingle(e => e.Symbol == "AAPL");
    }

    [Fact]
    public async Task AddSymbol_Duplicate_DoesNotThrow()
    {
        await _svc.AddSymbolAsync("TSLA");
        var act = async () => await _svc.AddSymbolAsync("TSLA");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RemoveSymbol_RemovedFromGetAll()
    {
        await _svc.AddSymbolAsync("NVDA");
        await _svc.RemoveSymbolAsync("NVDA");
        var entries = await _svc.GetWatchlistAsync();
        entries.Should().NotContain(e => e.Symbol == "NVDA");
    }

    [Fact]
    public async Task GetWatchlist_ReturnsAllAddedSymbols()
    {
        await _svc.AddSymbolAsync("AAPL");
        await _svc.AddSymbolAsync("MSFT");
        await _svc.AddSymbolAsync("AMZN");

        var entries = await _svc.GetWatchlistAsync();

        entries.Count.Should().Be(3);
        entries.Select(e => e.Symbol).Should().Contain(["AAPL", "MSFT", "AMZN"]);
    }

    [Fact]
    public async Task GetWatchlist_EmptyByDefault()
    {
        var entries = await _svc.GetWatchlistAsync();
        entries.Should().BeEmpty();
    }
}
