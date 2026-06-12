using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradeScanner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScanFrequency = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxResultsPerScan = table.Column<int>(type: "INTEGER", nullable: false),
                    MinPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    MaxPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    MinVolume = table.Column<long>(type: "INTEGER", nullable: false),
                    MinChangePercent = table.Column<decimal>(type: "TEXT", nullable: false),
                    MinVolumeRatio = table.Column<decimal>(type: "TEXT", nullable: false),
                    MinTradeScore = table.Column<int>(type: "INTEGER", nullable: false),
                    EnableSoundAlerts = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnablePopupAlerts = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableAutoScan = table.Column<bool>(type: "INTEGER", nullable: false),
                    Theme = table.Column<string>(type: "TEXT", nullable: false),
                    MomentumWeight = table.Column<decimal>(type: "TEXT", nullable: false),
                    VolumeWeight = table.Column<decimal>(type: "TEXT", nullable: false),
                    TechnicalWeight = table.Column<decimal>(type: "TEXT", nullable: false),
                    NewsWeight = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProviderConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Provider = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    EncryptedApiKey = table.Column<string>(type: "TEXT", nullable: false),
                    RateLimitPerMinute = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TotalRequests = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedRequests = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPremium = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockSymbols",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Symbol = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Exchange = table.Column<string>(type: "TEXT", nullable: false),
                    Sector = table.Column<string>(type: "TEXT", nullable: false),
                    MarketCap = table.Column<decimal>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockSymbols", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AlertRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Symbol = table.Column<string>(type: "TEXT", nullable: false),
                    AlertType = table.Column<int>(type: "INTEGER", nullable: false),
                    Threshold = table.Column<decimal>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotifySound = table.Column<bool>(type: "INTEGER", nullable: false),
                    NotifyPopup = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastTriggeredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TriggerCount = table.Column<int>(type: "INTEGER", nullable: false),
                    StockSymbolId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertRules_StockSymbols_StockSymbolId",
                        column: x => x.StockSymbolId,
                        principalTable: "StockSymbols",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ScanResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Symbol = table.Column<string>(type: "TEXT", nullable: false),
                    TradeScore = table.Column<int>(type: "INTEGER", nullable: false),
                    MomentumScore = table.Column<decimal>(type: "TEXT", nullable: false),
                    VolumeScore = table.Column<decimal>(type: "TEXT", nullable: false),
                    TechnicalScore = table.Column<decimal>(type: "TEXT", nullable: false),
                    NewsScore = table.Column<decimal>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false),
                    ChangePercent = table.Column<decimal>(type: "TEXT", nullable: false),
                    Volume = table.Column<long>(type: "INTEGER", nullable: false),
                    AvgVolume = table.Column<long>(type: "INTEGER", nullable: false),
                    Signals = table.Column<string>(type: "TEXT", nullable: false),
                    ScannedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Provider = table.Column<int>(type: "INTEGER", nullable: false),
                    StockSymbolId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScanResults_StockSymbols_StockSymbolId",
                        column: x => x.StockSymbolId,
                        principalTable: "StockSymbols",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WatchlistEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Symbol = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    PriceTarget = table.Column<decimal>(type: "TEXT", nullable: true),
                    StopLoss = table.Column<decimal>(type: "TEXT", nullable: true),
                    AddedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    StockSymbolId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchlistEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WatchlistEntries_StockSymbols_StockSymbolId",
                        column: x => x.StockSymbolId,
                        principalTable: "StockSymbols",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "AppSettings",
                columns: new[] { "Id", "EnableAutoScan", "EnablePopupAlerts", "EnableSoundAlerts", "MaxPrice", "MaxResultsPerScan", "MinChangePercent", "MinPrice", "MinTradeScore", "MinVolume", "MinVolumeRatio", "MomentumWeight", "NewsWeight", "ScanFrequency", "TechnicalWeight", "Theme", "VolumeWeight" },
                values: new object[] { 1, false, true, true, 500.0m, 50, 2.0m, 1.0m, 60, 500000L, 1.5m, 0.35m, 0.10m, 60, 0.25m, "Dark", 0.30m });

            migrationBuilder.CreateIndex(
                name: "IX_AlertRules_StockSymbolId",
                table: "AlertRules",
                column: "StockSymbolId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanResults_ScannedAt",
                table: "ScanResults",
                column: "ScannedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ScanResults_StockSymbolId",
                table: "ScanResults",
                column: "StockSymbolId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanResults_Symbol",
                table: "ScanResults",
                column: "Symbol");

            migrationBuilder.CreateIndex(
                name: "IX_StockSymbols_Symbol",
                table: "StockSymbols",
                column: "Symbol",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistEntries_StockSymbolId",
                table: "WatchlistEntries",
                column: "StockSymbolId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchlistEntries_Symbol",
                table: "WatchlistEntries",
                column: "Symbol",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertRules");

            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "ProviderConfigs");

            migrationBuilder.DropTable(
                name: "ScanResults");

            migrationBuilder.DropTable(
                name: "WatchlistEntries");

            migrationBuilder.DropTable(
                name: "StockSymbols");
        }
    }
}
