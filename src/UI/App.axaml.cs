using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using System;
using System.Linq;
using WireGuard.UI.ViewModels;
using WireGuard.UI.Views;

namespace WireGuard.UI;

public partial class App : Application
{
    private TrayIcon? _trayIcon;
    private MainWindowViewModel? _vm;
    private MainWindow? _mainWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            _vm = new MainWindowViewModel();
            _mainWindow = new MainWindow { DataContext = _vm };
            desktop.MainWindow = _mainWindow;

            SetupTrayIcon(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var menu = new NativeMenu();

        var showItem = new NativeMenuItem("Mostrar");
        showItem.Click += (_, _) => ShowMainWindow();

        var exitItem = new NativeMenuItem("Salir");
        exitItem.Click += (_, _) => desktop.Shutdown();

        menu.Add(showItem);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(exitItem);

        _trayIcon = new TrayIcon
        {
            Icon = LoadIcon("icon.ico"),
            ToolTipText = "WireGuard Manager",
            Menu = menu,
            IsVisible = true,
        };

        _trayIcon.Clicked += (_, _) => ShowMainWindow();

        if (_vm != null)
        {
            _vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(MainWindowViewModel.AnyTunnelRunning))
                    UpdateTrayIcon();
            };
        }
    }

    private void UpdateTrayIcon()
    {
        if (_trayIcon == null || _vm == null) return;
        var iconName = _vm.AnyTunnelRunning ? "icon_connected.ico" : "icon.ico";
        var icon = LoadIcon(iconName);
        if (icon != null) _trayIcon.Icon = icon;
    }

    private static WindowIcon? LoadIcon(string fileName)
    {
        try
        {
            var uri = new Uri($"avares://WireGuard.UI/Assets/{fileName}");
            using var stream = AssetLoader.Open(uri);
            return new WindowIcon(stream);
        }
        catch { return null; }
    }

    private void ShowMainWindow()
    {
        if (_mainWindow == null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
