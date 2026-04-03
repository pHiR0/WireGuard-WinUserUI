using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WireGuard.Shared.Validation;
using WireGuard.UI.Services;

namespace WireGuard.UI.ViewModels;

public enum TunnelEditorMode { New, Edit }

/// <summary>
/// ViewModel for the tunnel editor dialog (create new / edit existing).
/// Name field is read-only in Edit mode — both at the UI level and because
/// the service always uses the original TunnelName sent in the IPC request.
/// </summary>
public partial class TunnelEditorViewModel : ViewModelBase
{
    private readonly IPipeClient _pipeClient;

    public TunnelEditorMode Mode { get; }
    public bool IsEditMode => Mode == TunnelEditorMode.Edit;
    public string WindowTitle => IsEditMode
        ? $"Editar túnel — {OriginalTunnelName}"
        : "Nuevo túnel";
    public string SaveButtonText => IsEditMode ? "Guardar cambios" : "Crear túnel";

    /// <summary>The original tunnel name (used as the identifier when editing).</summary>
    public string OriginalTunnelName { get; }

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
    private bool _isSaving;

    /// <summary>Raised when the dialog should close. True = saved successfully.</summary>
    public event Action<bool>? CloseRequested;

    /// <summary>
    /// Raised when the user requests a file picker to load a .conf file.
    /// The subscriber should return the file content, or null if cancelled.
    /// </summary>
    public event Func<Task<string?>>? PickFileRequested;

    public TunnelEditorViewModel(IPipeClient pipeClient, TunnelEditorMode mode,
        string originalName = "", string confContent = "")
    {
        _pipeClient = pipeClient;
        Mode = mode;
        OriginalTunnelName = originalName;
        TunnelName = originalName;
        ConfContent = confContent;
        if (!string.IsNullOrEmpty(confContent))
            ValidateConf();
    }

    partial void OnConfContentChanged(string value) => ValidateConf();

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
    private async Task SaveAsync()
    {
        if (!IsValid) return;
        if (!IsEditMode && string.IsNullOrWhiteSpace(TunnelName)) return;

        try
        {
            IsSaving = true;
            ErrorMessage = string.Empty;

            if (IsEditMode)
                // Service enforces that the tunnel name cannot change — it uses OriginalTunnelName as the key.
                await _pipeClient.EditTunnelAsync(OriginalTunnelName, ConfContent);
            else
                await _pipeClient.ImportTunnelAsync(TunnelName.Trim(), ConfContent);

            CloseRequested?.Invoke(true);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsSaving = false; }
    }

    [RelayCommand]
    private async Task LoadFromFileAsync()
    {
        if (PickFileRequested is null) return;
        var content = await PickFileRequested.Invoke();
        if (content is not null)
            ConfContent = content;
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);
}
