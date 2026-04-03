using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using WireGuard.UI.ViewModels;

namespace WireGuard.UI.Views;

public partial class TunnelEditorWindow : Window
{
    public TunnelEditorWindow()
    {
        InitializeComponent();
    }

    public TunnelEditorWindow(TunnelEditorViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.CloseRequested += success => Close(success);
        viewModel.PickFileRequested += PickConfFileAsync;
    }

    private async Task<string?> PickConfFileAsync()
    {
        var options = new FilePickerOpenOptions
        {
            Title = "Seleccionar archivo .conf de WireGuard",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("WireGuard Config")
                {
                    Patterns = ["*.conf"],
                    MimeTypes = ["text/plain"],
                },
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
}
