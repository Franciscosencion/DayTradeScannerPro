# Day Trade Scanner Pro

A commercial-grade Windows desktop stock scanner built with C#/.NET 10, WPF, and MVVM. Scans the market in real time, ranks candidates by a configurable Trade Score, streams live prices via WebSocket, and alerts you when your conditions are met.

![Platform](https://img.shields.io/badge/platform-Windows%2010%2B-blue)
![.NET](https://img.shields.io/badge/.NET-10.0-purple)
![Tests](https://img.shields.io/badge/tests-38%20passing-brightgreen)
![License](https://img.shields.io/badge/license-MIT-green)

---

## Features

- **Automated market scanning** — continuous or single-shot scans with configurable frequency
- **Trade Score 0–100** — composite ranking: Momentum (35%), Volume (30%), Technical (25%), News (10%)
- **Technical indicators** — RSI, SMA, Bollinger Bands, ATR, breakout detection
- **8 data providers** — Polygon.io, Finnhub, FMP, TwelveData, AlphaVantage, Stooq, Yahoo Finance, Yahoo Screener with automatic failover
- **179-symbol scan universe** — all providers run in parallel; their symbol lists are merged and deduplicated each scan
- **Real-time WebSocket streaming** — live trade ticks from Polygon.io and Finnhub with exponential-backoff reconnect
- **Price/volume/percent-change alerts** — sound + popup notifications
- **Watchlist** — persist and monitor custom symbol lists
- **TradingView Advanced Charts** — 5-min candlesticks with VWAP, Volume, drawing tools, and timeframe selector embedded via WebView2 (no key required)
- **Clickable symbols** — click any symbol in the Dashboard to instantly open its TradingView chart
- **Resizable columns** — drag any Dashboard column header to resize; click to sort
- **Export** — formatted `.xlsx` (ClosedXML, color-coded) and `.csv`
- **DPAPI-encrypted API keys** — keys stored in SQLite, never plaintext
- **MSIX installer** — self-contained 72 MB package, Windows 10 1809+

---

## Screenshots

> _App screenshot goes here — run the app and capture the Dashboard view._

---

## Requirements

| Requirement | Version |
|---|---|
| Windows | 10 (build 1809) or Windows 11 |
| .NET SDK (build only) | 10.0.300+ |
| Architecture | x64 |

The MSIX installer is self-contained — no .NET runtime needs to be pre-installed on the target machine.

---

## Installation (End Users)

1. Download `TradeScanner_1.0.0.0.msix` from the [Releases](../../releases) page.
2. If sideloading, first install the signing certificate:
   ```powershell
   # Run as Administrator
   Import-Certificate -FilePath TradeScanner-test.pfx -CertStoreLocation Cert:\LocalMachine\TrustedPeople
   ```
3. Double-click the `.msix` file to install.
4. Launch **Day Trade Scanner Pro** from the Start menu.

---

## Building from Source

### Prerequisites

- .NET 10 SDK (`x64` or `x86` — see note below)
- Visual Studio 2022 17.8+ **or** VS Code with C# Dev Kit

> **Note (x86 SDK):** If you have the x86 .NET SDK at `C:\Program Files (x86)\dotnet\dotnet.exe`, prefix all `dotnet` commands with the full path, or add it to PATH first.

### Build

```powershell
cd src
dotnet build TradeScanner.sln
```

### Run Tests

```powershell
cd src
dotnet test TradeScanner.Tests/TradeScanner.Tests.csproj
```

Expected output: `Passed! - Failed: 0, Passed: 38`

### Build MSIX Installer

```powershell
cd src
.\Build-Msix.ps1          # unsigned package
.\Build-Msix.ps1 -Sign    # creates self-signed cert + signed package (requires elevation)
```

Output: `publish\output\TradeScanner_1.0.0.0.msix`

---

## Architecture

```
TradeScanner.sln
├── TradeScanner.Core/           Domain entities, enums, value objects, interfaces, algorithms
├── TradeScanner.Infrastructure/ EF Core + SQLite, providers, repositories, WebSocket clients, DPAPI
├── TradeScanner.Application/    Scanner, ranking engine, alert service, export, DI registration
├── TradeScanner.UI/             WPF app — MVVM pages, converters, dark theme
└── TradeScanner.Tests/          38 xUnit tests (FluentAssertions + Moq)
```

### Clean architecture layers

```
UI  →  Application  →  Core
         ↓
    Infrastructure  →  Core
```

- `Core` has zero external dependencies
- `Infrastructure` owns all I/O (HTTP, WebSocket, SQLite, DPAPI)
- `Application` orchestrates business logic; never touches WPF
- `UI` is thin — ViewModels bind to Application services

### Trade Score formula

```
Score = Momentum × 0.35 + Volume × 0.30 + Technical × 0.25 + News × 0.10
```

Each sub-score is 0–100 and weights are user-configurable in Settings.

---

## Data Providers

| Provider | Tier | Key Required | Streaming | Symbol Discovery |
|---|---|---|---|---|
| Polygon.io | Premium | Yes | Yes (WebSocket) | Most-active snapshot (paid plan) |
| Yahoo Screener | Free | No | No | ~97 symbols: most-active + day-gainers |
| Finnhub | Premium | Yes | Yes (WebSocket) | 105-symbol curated high-volume list |
| Financial Modeling Prep | Premium | Yes | No | Most-active endpoint |
| TwelveData | Premium | Yes | No | 60-symbol curated list |
| AlphaVantage | Free tier | Yes | No | Top gainers + most-active traded |
| Stooq | Free | No | No (daily only) | Fallback quotes only |
| Yahoo Finance | Free | No | No | Fallback quotes only |

The app uses a **FailoverProviderChain** that runs all providers **in parallel** on each scan. Symbol lists are merged and deduplicated into a single universe (~179 symbols when multiple providers are active). Quote fetching uses the highest-priority available provider with automatic failover.

**Symbol universe without API keys:** The Yahoo Screener provider fetches Yahoo Finance's `most_actives` and `day_gainers` screeners (two parallel HTTP calls, no key required) giving ~97 real-time market leaders. Combined with Finnhub's 105-symbol curated list, the scanner evaluates ~179 unique symbols per scan entirely for free.

**Polygon.io free plan:** The snapshot endpoint returns 403 on the free tier. The app detects this on the first failure and disables Polygon for the remainder of the session, so no retries occur.

---

## API Key Setup

1. Open the **Providers** tab in the app.
2. Paste your API key into the text field next to the provider.
3. Click **Save** — the key is encrypted with Windows DPAPI and stored in SQLite.
4. A key icon (🔑) appears when a key is saved. The field clears after saving.
5. Click **Validate** at any time to test connectivity.

Keys are per-user, per-machine. They cannot be extracted without the Windows account that saved them.

---

## Using the Dashboard

The Dashboard is the main scanner view.

| Control | Action |
|---|---|
| **Start Scanning** | Begin continuous scans at the configured interval |
| **Scan Once** | Run a single scan immediately |
| **Relaxed Mode** | Lowers MinChange%, MinVolume, and MinScore thresholds — useful in slow markets |
| **Stream** | Connect a WebSocket feed (Polygon or Finnhub) to stream live prices into existing results |
| **Export CSV** | Save current results as `.xlsx` or `.csv` |

### Results table

- **Click any symbol** (e.g. `NOK`) to jump directly to its TradingView chart.
- **Click a column header** to sort by that column; click again to reverse.
- **Drag the right edge of any column header** to resize that column.

Columns in the results table:

| Column | Description |
|---|---|
| SYMBOL | Ticker — click to open chart |
| SCORE | Trade Score 0–100 (green ≥ 70, amber 40–69, red < 40) |
| PRICE | Last traded price |
| CHANGE% | Day change vs. previous close |
| VOLUME | Shares traded today |
| VOL RATIO | Today's volume ÷ average volume (high = unusual activity) |
| MOMENTUM | Momentum sub-score 0–100 |
| TECHNICAL | Technical sub-score 0–100 (RSI, Bollinger, breakout) |
| SIGNALS | Active signal tags (e.g. `VOLUME_SURGE`, `RSI_OVERSOLD`) |

---

## Charts

The Charts tab embeds a full **TradingView Advanced Chart** (5-minute candlesticks, Volume and VWAP indicators, drawing tools, multiple timeframes).

- **From the Dashboard:** click any symbol to navigate directly to its chart.
- **Manually:** type a symbol in the box and press **Enter** or click **Load Chart**.
- The stats bar (Price, Change, Vol, Range) populates after clicking Load Chart.
- The chart is interactive — you can change symbol, timeframe, and add indicators directly in the chart widget.

---

## Project Structure

```
TradeScanner/
├── src/
│   ├── TradeScanner.sln
│   ├── Build-Msix.ps1
│   ├── TradeScanner.Core/
│   │   ├── Algorithms/          TechnicalIndicators (RSI, SMA, BB, ATR)
│   │   ├── Domain/
│   │   │   ├── Entities/        AlertRule, ScanResult, WatchlistEntry, ProviderConfig, AppSettings
│   │   │   ├── Enums/           MarketDataProvider, AlertType, ScanFrequency, SignalType
│   │   │   └── ValueObjects/    Quote, PriceBar, TradeScore, RealtimeTrade, NewsItem
│   │   └── Interfaces/          All service contracts
│   ├── TradeScanner.Infrastructure/
│   │   ├── Data/                TradeScannerDbContext, EF migrations
│   │   ├── Providers/           8 provider implementations + FailoverProviderChain
│   │   ├── Repositories/        AlertRepository, WatchlistRepository, ScanResultRepository, ProviderConfigRepository
│   │   ├── Security/            DpapiSecurityService
│   │   └── Streaming/           PolygonWebSocketClient, FinnhubWebSocketClient
│   ├── TradeScanner.Application/
│   │   ├── Alerts/              AlertService
│   │   ├── Config/              ProviderConfigService
│   │   ├── Export/              CsvExportService (CSV + XLSX via ClosedXML)
│   │   ├── Ranking/             RankingEngine
│   │   ├── Scanner/             ScannerService, WatchlistService
│   │   └── Streaming/           RealtimeStreamingService (reconnect + backoff)
│   ├── TradeScanner.UI/
│   │   ├── Views/               Dashboard, Alerts, Charts, Watchlist, Providers, Settings
│   │   ├── ViewModels/          One ViewModel per view
│   │   ├── Converters/          ValueConverters.cs
│   │   ├── Themes/              Dark.xaml
│   │   └── Infrastructure/      NavigationService, ViewModelBase
│   └── TradeScanner.Tests/
│       └── Unit/                38 tests across all application layers
├── docs/
│   └── UserGuide.pdf
└── publish/
    └── output/
        └── TradeScanner_1.0.0.0.msix
```

---

## Development Notes

- `Application.Current` in ViewModels must use `using WpfApp = System.Windows.Application` — `TradeScanner.Application` namespace shadows `System.Windows.Application` in the UI project
- WPF `Page` has no `OnNavigatedTo` virtual — use `Loaded += async (_, _) => await vm.LoadAsync()`
- `ProtectedData` (DPAPI) requires NuGet: `System.Security.Cryptography.ProtectedData`
- `ExecuteDeleteAsync` (EF bulk delete) is not supported by EF InMemory — tests that exercise it use SQLite `:memory:`
- MSIX `Build-Msix.ps1` writes the manifest with `[System.IO.File]::WriteAllText(..., UTF8Encoding(false))` — PS 5.1 `Set-Content -Encoding UTF8` adds a BOM that MakeAppx rejects
- ReadyToRun (`PublishReadyToRun=true`) is disabled in the publish profile — crossgen fails when targeting x64 from an x86 SDK

---

## License

MIT License — see [LICENSE](LICENSE) for details.

---

## Contributing

Pull requests are welcome. For major changes, open an issue first to discuss scope. All PRs must pass the full test suite (`dotnet test`) with 0 failures.
