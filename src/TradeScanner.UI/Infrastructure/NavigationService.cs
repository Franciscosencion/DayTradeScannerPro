using WpfApp = System.Windows.Application;
using System.Windows.Controls;

namespace TradeScanner.UI.Infrastructure;

public class NavigationService
{
    private Frame? _frame;

    public void Initialize(Frame frame) => _frame = frame;

    public void NavigateTo<TView>() where TView : Page, new()
    {
        WpfApp.Current.Dispatcher.Invoke(() => _frame?.Navigate(new TView()));
    }

    public void NavigateTo(Page page)
    {
        WpfApp.Current.Dispatcher.Invoke(() => _frame?.Navigate(page));
    }
}
