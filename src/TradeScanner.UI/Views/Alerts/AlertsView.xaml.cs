using System.Windows.Controls;
using TradeScanner.UI.ViewModels.Alerts;

namespace TradeScanner.UI.Views.Alerts;

public partial class AlertsView : Page
{
    public AlertsView(AlertsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadAsync();
    }
}
