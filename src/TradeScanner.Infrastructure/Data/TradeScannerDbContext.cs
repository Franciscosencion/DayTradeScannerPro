using Microsoft.EntityFrameworkCore;
using TradeScanner.Core.Domain.Entities;

namespace TradeScanner.Infrastructure.Data;

public class TradeScannerDbContext(DbContextOptions<TradeScannerDbContext> options) : DbContext(options)
{
    public DbSet<StockSymbol> StockSymbols => Set<StockSymbol>();
    public DbSet<ScanResult> ScanResults => Set<ScanResult>();
    public DbSet<WatchlistEntry> WatchlistEntries => Set<WatchlistEntry>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<ProviderConfig> ProviderConfigs => Set<ProviderConfig>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppSettings>().HasKey(x => x.Id);
        modelBuilder.Entity<AppSettings>().HasData(new AppSettings { Id = 1 });

        modelBuilder.Entity<ScanResult>()
            .Property(x => x.SignalsSerialized)
            .HasColumnName("Signals");

        modelBuilder.Entity<ScanResult>()
            .Ignore(x => x.Signals);

        modelBuilder.Entity<ScanResult>()
            .Ignore(x => x.VolumeRatio);

        modelBuilder.Entity<StockSymbol>()
            .HasIndex(x => x.Symbol)
            .IsUnique();

        modelBuilder.Entity<WatchlistEntry>()
            .HasIndex(x => x.Symbol)
            .IsUnique();

        modelBuilder.Entity<ScanResult>()
            .HasIndex(x => x.ScannedAt);

        modelBuilder.Entity<ScanResult>()
            .HasIndex(x => x.Symbol);
    }
}
