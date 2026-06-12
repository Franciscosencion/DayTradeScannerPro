using System.Text;
using ClosedXML.Excel;
using TradeScanner.Core.Domain.Entities;
using TradeScanner.Core.Interfaces;

namespace TradeScanner.Application.Export;

public class CsvExportService : IExportService
{
    public async Task ExportToCsvAsync(IEnumerable<ScanResult> results, string filePath, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Symbol,TradeScore,Price,ChangePercent,Volume,AvgVolume,VolumeRatio,MomentumScore,VolumeScore,TechnicalScore,NewsScore,Provider,ScannedAt");

        foreach (var r in results)
            sb.AppendLine($"{r.Symbol},{r.TradeScore},{r.Price:F2},{r.ChangePercent:F2}%,{r.Volume},{r.AvgVolume},{r.VolumeRatio:F2},{r.MomentumScore:F1},{r.VolumeScore:F1},{r.TechnicalScore:F1},{r.NewsScore:F1},{r.Provider},{r.ScannedAt:yyyy-MM-dd HH:mm:ss}");

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8, ct);
    }

    public Task ExportToExcelAsync(IEnumerable<ScanResult> results, string filePath, CancellationToken ct = default)
    {
        var list = results.ToList();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Scan Results");

        // Header row
        var headers = new[]
        {
            "Symbol", "Trade Score", "Price", "Change %", "Volume", "Avg Volume",
            "Vol Ratio", "Momentum", "Volume Score", "Technical", "News", "Provider", "Scanned At"
        };

        for (int col = 1; col <= headers.Length; col++)
        {
            var cell = ws.Cell(1, col);
            cell.Value = headers[col - 1];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E1E1E");
            cell.Style.Font.FontColor = XLColor.FromHtml("#00BCD4");
        }

        // Data rows
        for (int row = 0; row < list.Count; row++)
        {
            var r = list[row];
            var xlRow = row + 2;

            ws.Cell(xlRow, 1).Value = r.Symbol;
            ws.Cell(xlRow, 2).Value = r.TradeScore;
            ws.Cell(xlRow, 3).Value = (double)r.Price;
            ws.Cell(xlRow, 4).Value = (double)r.ChangePercent;
            ws.Cell(xlRow, 5).Value = r.Volume;
            ws.Cell(xlRow, 6).Value = r.AvgVolume;
            ws.Cell(xlRow, 7).Value = (double)r.VolumeRatio;
            ws.Cell(xlRow, 8).Value = (double)r.MomentumScore;
            ws.Cell(xlRow, 9).Value = (double)r.VolumeScore;
            ws.Cell(xlRow, 10).Value = (double)r.TechnicalScore;
            ws.Cell(xlRow, 11).Value = (double)r.NewsScore;
            ws.Cell(xlRow, 12).Value = r.Provider.ToString();
            ws.Cell(xlRow, 13).Value = r.ScannedAt;

            // Color-code ChangePercent column
            var changePct = r.ChangePercent;
            ws.Cell(xlRow, 4).Style.Font.FontColor =
                changePct >= 0 ? XLColor.FromHtml("#26A69A") : XLColor.FromHtml("#EF5350");

            // Color-code TradeScore column
            ws.Cell(xlRow, 2).Style.Font.FontColor = r.TradeScore switch
            {
                >= 75 => XLColor.FromHtml("#26A69A"),
                >= 50 => XLColor.FromHtml("#FFB300"),
                _     => XLColor.FromHtml("#EF5350")
            };
        }

        // Format columns
        ws.Column(3).Style.NumberFormat.Format = "$#,##0.00";
        ws.Column(4).Style.NumberFormat.Format = "0.00\"%\"";
        ws.Column(5).Style.NumberFormat.Format = "#,##0";
        ws.Column(6).Style.NumberFormat.Format = "#,##0";
        ws.Column(7).Style.NumberFormat.Format = "0.00\"x\"";
        ws.Column(13).Style.NumberFormat.Format = "yyyy-mm-dd hh:mm:ss";
        ws.Columns().AdjustToContents();

        // Auto-filter on header row
        ws.RangeUsed()!.SetAutoFilter();

        wb.SaveAs(filePath);
        return Task.CompletedTask;
    }

    public async Task ExportWatchlistAsync(IEnumerable<WatchlistEntry> entries, string filePath, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Symbol,Notes,PriceTarget,StopLoss,AddedAt");

        foreach (var e in entries)
            sb.AppendLine($"{e.Symbol},{e.Notes},{e.PriceTarget},{e.StopLoss},{e.AddedAt:yyyy-MM-dd}");

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8, ct);
    }
}
