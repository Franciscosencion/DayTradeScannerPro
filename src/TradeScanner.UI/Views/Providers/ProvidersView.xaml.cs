using System.Windows.Controls;
using TradeScanner.UI.ViewModels.Providers;

namespace TradeScanner.UI.Views.Providers;

public partial class ProvidersView : Page
{
    public ProvidersView(ProvidersViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadAsync();
    }
}
