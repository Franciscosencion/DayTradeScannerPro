using System.Windows.Controls;
using TradeScanner.UI.ViewModels.Watchlist;

namespace TradeScanner.UI.Views.Watchlist;

public partial class WatchlistView : Page
{
    public WatchlistView(WatchlistViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadAsync();
    }
}
