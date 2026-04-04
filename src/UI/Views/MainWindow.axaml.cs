using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using WireGuard.UI.ViewModels;

namespace WireGuard.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(System.EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainWindowViewModel vm)
        {
            vm.OpenEditorRequested += OpenTunnelEditor;
            vm.PickConfFileRequested += PickConfFileAsync;
        }
    }

    private async void OpenTunnelEditor(TunnelEditorViewModel editorVm)
    {
        var dialog = new TunnelEditorWindow(editorVm);
        await dialog.ShowDialog(this);
    }

    private async Task<string?> PickConfFileAsync()
    {
        var options = new FilePickerOpenOptions
        {
            Title = "Seleccionar archivo .conf de WireGuard",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("WireGuard Config") { Patterns = ["*.conf"] },
                new FilePickerFileType("Todos los archivos") { Patterns = ["*"] },
            ],
        };

        var files = await StorageProvider.OpenFilePickerAsync(options);
        if (files.Count == 0) return null;

        try
        {
            await using var stream = await files[0].OpenReadAsync();
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }
        catch { return null; }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Si minimizar al tray está activo Y no estamos saliendo explícitamente, ocultar en vez de cerrar.
        if (DataContext is MainWindowViewModel vm && vm.Settings.MinimizeToTray && !App.IsExiting)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }
}
