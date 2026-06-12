using System.Collections.ObjectModel;
using WpfApp = System.Windows.Application;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TradeScanner.Core.Domain.Entities;
using TradeScanner.Core.Domain.ValueObjects;
using TradeScanner.Core.Interfaces;
using TradeScanner.UI.Infrastructure;

namespace TradeScanner.UI.ViewModels.Watchlist;

public partial class WatchlistViewModel : ViewModelBase
{
    private readonly IWatchlistService _watchlist;

    [ObservableProperty] private string _newSymbol = string.Empty;
    [ObservableProperty] private WatchlistEntry? _selectedEntry;

    public ObservableCollection<WatchlistEntry> Entries { get; } = [];
    public ObservableCollection<WatchlistQuoteRow> QuoteRows { get; } = [];

    public WatchlistViewModel(IWatchlistService watchlist)
    {
        _watchlist = watchlist;
    }

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var entries = await _watchlist.GetWatchlistAsync();
            WpfApp.Current.Dispatcher.Invoke(() =>
            {
                Entries.Clear();
                foreach (var e in entries) Entries.Add(e);

                // Seed rows from saved entries immediately so the list is never blank
                QuoteRows.Clear();
                foreach (var e in entries)
                    QuoteRows.Add(new WatchlistQuoteRow(e.Symbol));
            });

            // Then try to enrich with live quotes (best-effort)
            try
            {
                var quotes = await _watchlist.GetWatchlistQuotesAsync();
                if (quotes.Count > 0)
                {
                    var quoteMap = quotes.ToDictionary(q => q.Symbol);
                    WpfApp.Current.Dispatcher.Invoke(() =>
                    {
                        QuoteRows.Clear();
                        foreach (var e in entries)
                        {
                            quoteMap.TryGetValue(e.Symbol, out var q);
                            QuoteRows.Add(q != null ? new WatchlistQuoteRow(q) : new WatchlistQuoteRow(e.Symbol));
                        }
                    });
                }
            }
            catch { /* quote fetch failed — rows already seeded from entries above */ }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task AddSymbolAsync()
    {
        var symbol = NewSymbol.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(symbol)) return;
        await _watchlist.AddSymbolAsync(symbol);
        NewSymbol = string.Empty;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task RemoveSymbolAsync(string symbol)
    {
        await _watchlist.RemoveSymbolAsync(symbol);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshQuotesAsync()
    {
        await LoadAsync();
    }
}

public class WatchlistQuoteRow
{
    public string Symbol { get; }
    public decimal Price { get; }
    public decimal ChangePercent { get; }
    public decimal Change { get; }
    public long Volume { get; }
    public bool IsPositive => ChangePercent >= 0;

    public WatchlistQuoteRow(Quote quote)
    {
        Symbol = quote.Symbol;
        Price = quote.Price;
        ChangePercent = quote.ChangePercent;
        Change = quote.Change;
        Volume = quote.Volume;
    }

    // Fallback constructor when no live quote available
    public WatchlistQuoteRow(string symbol)
    {
        Symbol = symbol;
    }
}
