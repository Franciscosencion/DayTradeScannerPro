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
- **7 data providers** — Polygon.io, Finnhub, FMP, TwelveData, AlphaVantage, Stooq, Yahoo Finance with automatic failover
- **Real-time WebSocket streaming** — live trade ticks from Polygon.io and Finnhub with exponential-backoff reconnect
- **Price/volume/percent-change alerts** — sound + popup notifications
- **Watchlist** — persist and monitor custom symbol lists
- **Interactive charts** — candlestick charts with LiveCharts2
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

| Provider | Tier | Key Required | Streaming |
|---|---|---|---|
| Polygon.io | Premium | Yes | Yes (WebSocket) |
| Finnhub | Premium | Yes | Yes (WebSocket) |
| Financial Modeling Prep | Premium | Yes | No |
| TwelveData | Premium | Yes | No |
| AlphaVantage | Free tier | Yes | No |
| Stooq | Free | No | No (daily only) |
| Yahoo Finance | Free | No | No |

The app uses a **FailoverProviderChain** — if the primary provider fails, it automatically falls back to the next available one.

---

## API Key Setup

1. Open the **Providers** tab in the app.
2. Paste your API key into the text field next to the provider.
3. Click **Save** — the key is encrypted with Windows DPAPI and stored in SQLite.
4. A key icon (🔑) appears when a key is saved. The field clears after saving.
5. Click **Validate** at any time to test connectivity.

Keys are per-user, per-machine. They cannot be extracted without the Windows account that saved them.

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
│   │   ├── Providers/           7 provider implementations + FailoverProviderChain
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
