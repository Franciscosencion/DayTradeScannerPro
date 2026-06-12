using FluentAssertions;
using TradeScanner.Application.Export;
using TradeScanner.Core.Domain.Entities;
using TradeScanner.Core.Domain.Enums;
using Xunit;

namespace TradeScanner.Tests.Unit;

public class CsvExportServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly CsvExportService _svc = new();

    public CsvExportServiceTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static List<ScanResult> SampleResults() =>
    [
        new ScanResult
        {
            Symbol = "AAPL", TradeScore = 85, Price = 185.50m, ChangePercent = 3.5m,
            Volume = 2_000_000, AvgVolume = 1_500_000, MomentumScore = 30m, VolumeScore = 25m,
            TechnicalScore = 20m, NewsScore = 10m, Provider = MarketDataProvider.PolygonIo,
            ScannedAt = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc)
        },
        new ScanResult
        {
            Symbol = "TSLA", TradeScore = 72, Price = 240.00m, ChangePercent = -1.2m,
            Volume = 3_500_000, AvgVolume = 3_000_000, MomentumScore = 20m, VolumeScore = 28m,
            TechnicalScore = 15m, NewsScore = 9m, Provider = MarketDataProvider.Finnhub,
            ScannedAt = new DateTime(2026, 6, 12, 10, 0, 0, DateTimeKind.Utc)
        }
    ];

    [Fact]
    public async Task ExportToCsv_CreatesFile()
    {
        var path = Path.Combine(_tempDir, "results.csv");
        await _svc.ExportToCsvAsync(SampleResults(), path);
        File.Exists(path).Should().BeTrue();
    }

    [Fact]
    public async Task ExportToCsv_ContainsHeaderRow()
    {
        var path = Path.Combine(_tempDir, "results.csv");
        await _svc.ExportToCsvAsync(SampleResults(), path);
        var lines = await File.ReadAllLinesAsync(path);
        lines[0].Should().Contain("Symbol");
        lines[0].Should().Contain("TradeScore");
        lines[0].Should().Contain("Price");
    }

    [Fact]
    public async Task ExportToCsv_ContainsAllSymbols()
    {
        var path = Path.Combine(_tempDir, "results.csv");
        await _svc.ExportToCsvAsync(SampleResults(), path);
        var content = await File.ReadAllTextAsync(path);
        content.Should().Contain("AAPL");
        content.Should().Contain("TSLA");
    }

    [Fact]
    public async Task ExportToCsv_RowCountMatchesResults()
    {
        var path = Path.Combine(_tempDir, "results.csv");
        var results = SampleResults();
        await _svc.ExportToCsvAsync(results, path);
        var lines = await File.ReadAllLinesAsync(path);
        // header + data rows + possible empty trailing line
        lines.Count(l => l.Length > 0).Should().Be(results.Count + 1);
    }

    [Fact]
    public async Task ExportToExcel_CreatesXlsxFile()
    {
        var path = Path.Combine(_tempDir, "results.xlsx");
        await _svc.ExportToExcelAsync(SampleResults(), path);
        File.Exists(path).Should().BeTrue();
        new FileInfo(path).Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExportToExcel_FileIsValidZip()
    {
        // .xlsx is a ZIP archive; verify magic bytes
        var path = Path.Combine(_tempDir, "results.xlsx");
        await _svc.ExportToExcelAsync(SampleResults(), path);
        var bytes = await File.ReadAllBytesAsync(path);
        // ZIP magic: 50 4B 03 04
        bytes[0].Should().Be(0x50);
        bytes[1].Should().Be(0x4B);
    }
}
