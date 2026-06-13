using System.Windows.Controls;
using TradeScanner.UI.ViewModels.Charts;

namespace TradeScanner.UI.Views.Charts;

public partial class ChartsView : Page
{
    private bool _webViewReady;

    public ChartsView(ChartsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.NavigateChart = NavigateToTradingView;
        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        try
        {
            await ChartWebView.EnsureCoreWebView2Async();
            _webViewReady = true;

            // Load default symbol on first open
            if (DataContext is ChartsViewModel vm && !string.IsNullOrWhiteSpace(vm.Symbol))
                NavigateToTradingView(vm.Symbol.Trim().ToUpperInvariant());
        }
        catch (Exception ex)
        {
            // WebView2 runtime unavailable — status message is the only fallback
            if (DataContext is ChartsViewModel vm)
                vm.StatusMessage = $"TradingView chart unavailable: {ex.Message}";
        }
    }

    private void NavigateToTradingView(string symbol)
    {
        if (!_webViewReady || string.IsNullOrWhiteSpace(symbol)) return;
        // Navigate directly to TradingView's widget embed URL — avoids about:blank origin
        // restrictions that prevent the embed script from loading when using NavigateToString.
        var studies = Uri.EscapeDataString("VWAP@tv-basicstudies|Volume@tv-basicstudies");
        var url = $"https://s.tradingview.com/widgetembed/?symbol={Uri.EscapeDataString(symbol)}" +
                  $"&interval=5&theme=dark&style=1&locale=en" +
                  $"&timezone={Uri.EscapeDataString("America/New_York")}" +
                  $"&withdateranges=1&hide_side_toolbar=0&allow_symbol_change=1" +
                  $"&studies={studies}";
        ChartWebView.Source = new Uri(url);
    }
}
