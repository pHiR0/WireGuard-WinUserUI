using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
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

    /// <summary>
    /// Indica que la aplicación está cerrándose de forma explícita (menú tray "Salir").
    /// Lo lee MainWindow.OnClosing para no interceptar el cierre y enviar al tray.
    /// </summary>
    public static bool IsExiting { get; private set; }

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

            // La app vive en el tray: gestionamos el ciclo de vida de forma explícita.
            // NO asignamos desktop.MainWindow para evitar que Avalonia llame a Show()
            // automáticamente tras OnFrameworkInitializationCompleted.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Cuando la ventana se cierra de verdad (no solo se oculta al tray), salimos.
            _mainWindow.Closed += (_, _) => desktop.Shutdown();

            SetupTrayIcon(desktop);

            // Mostrar la ventana solo si NO se ha pedido iniciar minimizado.
            if (!_vm.Settings.StartMinimized)
                _mainWindow.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var menu = new NativeMenu();

        var showItem = new NativeMenuItem("Mostrar");
        showItem.Click += (_, _) => ShowMainWindow();

        var exitItem = new NativeMenuItem("Salir");
        exitItem.Click += (_, _) =>
        {
            IsExiting = true;   // permite que OnClosing no cancele el cierre
            desktop.Shutdown();
        };

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
        // Refresh public IP when restoring from tray (rate-limited — won't flood on rapid show/hide)
        _vm?.RequestPublicIpRefresh();
    }

    /// <summary>
    /// Called from the single-instance listener thread when another process signals us to show.
    /// Marshals the call to the UI thread.
    /// </summary>
    public static void RequestShowMainWindow()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Current is App app)
                app.ShowMainWindow();
        });
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
