using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WireGuard.Shared.Validation;
using WireGuard.UI.Services;

namespace WireGuard.UI.ViewModels;

public partial class ImportTunnelViewModel : ViewModelBase
{
    private readonly IPipeClient _pipeClient;

    [ObservableProperty]
    private string _tunnelName = string.Empty;

    [ObservableProperty]
    private string _confContent = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    [ObservableProperty]
    private bool _isValid;

    [ObservableProperty]
    private bool _isImporting;

    [ObservableProperty]
    private bool _importSuccess;

    public ImportTunnelViewModel(IPipeClient pipeClient)
    {
        _pipeClient = pipeClient;
    }

    partial void OnConfContentChanged(string value)
    {
        ValidateConf();
    }

    [RelayCommand]
    private void ValidateConf()
    {
        if (string.IsNullOrWhiteSpace(ConfContent))
        {
            IsValid = false;
            ValidationMessage = string.Empty;
            return;
        }

        var result = WireGuardConfValidator.Validate(ConfContent);
        IsValid = result.IsValid;

        if (result.IsValid)
        {
            var msg = "Configuración válida.";
            if (result.Warnings.Count > 0)
                msg += $" Advertencias: {string.Join("; ", result.Warnings)}";
            ValidationMessage = msg;
        }
        else
        {
            ValidationMessage = string.Join("\n", result.Errors);
        }
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (string.IsNullOrWhiteSpace(TunnelName) || !IsValid) return;

        try
        {
            IsImporting = true;
            ErrorMessage = string.Empty;
            ImportSuccess = false;

            await _pipeClient.ImportTunnelAsync(TunnelName.Trim(), ConfContent);
            ImportSuccess = true;

            // Reset form
            TunnelName = string.Empty;
            ConfContent = string.Empty;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsImporting = false;
        }
    }

    [RelayCommand]
    private void SetConfFromFile(string content)
    {
        ConfContent = content;
    }
}
