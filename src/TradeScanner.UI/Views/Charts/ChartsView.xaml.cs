using System.Windows.Controls;
using TradeScanner.UI.ViewModels.Charts;

namespace TradeScanner.UI.Views.Charts;

public partial class ChartsView : Page
{
    public ChartsView(ChartsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
