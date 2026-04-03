using Avalonia.Controls;
using WireGuard.UI.ViewModels;

namespace WireGuard.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.Settings.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnStateChanged(System.EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized
            && DataContext is MainWindowViewModel vm
            && vm.Settings.MinimizeToTray)
        {
            Hide();
            WindowState = WindowState.Normal;
        }
    }
}
