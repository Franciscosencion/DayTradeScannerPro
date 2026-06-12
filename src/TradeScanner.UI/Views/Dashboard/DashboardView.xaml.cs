using System.Windows.Controls;
using TradeScanner.UI.ViewModels.Dashboard;

namespace TradeScanner.UI.Views.Dashboard;

public partial class DashboardView : Page
{
    public DashboardView(DashboardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
