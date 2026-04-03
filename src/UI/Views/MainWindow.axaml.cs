using Avalonia.Controls;
using WireGuard.UI.ViewModels;

namespace WireGuard.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty
                && WindowState == WindowState.Minimized
                && DataContext is MainWindowViewModel vm
                && vm.Settings.MinimizeToTray)
            {
                Hide();
                WindowState = WindowState.Normal;
            }
        };
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
}
