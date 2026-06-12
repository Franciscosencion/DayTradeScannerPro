using System.Windows.Controls;
using TradeScanner.UI.ViewModels.Settings;

namespace TradeScanner.UI.Views.Settings;

public partial class SettingsView : Page
{
    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadAsync();
    }
}
