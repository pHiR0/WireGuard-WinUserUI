using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WireGuard.UI.Services;

namespace WireGuard.UI.ViewModels;

public partial class PublicIpProviderViewModel : ViewModelBase
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private bool _isPrimary;

    [ObservableProperty]
    private string? _testResult;

    [ObservableProperty]
    private bool _isTesting;

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task TestAsync(CancellationToken ct)
    {
        IsTesting = true;
        TestResult = "Probando...";
        try
        {
            var ip = await PublicIpService.TestProviderAsync(Id, ct);
            TestResult = ip ?? "Sin respuesta";
        }
        catch (OperationCanceledException)
        {
            TestResult = null;
        }
        catch (Exception ex)
        {
            TestResult = $"Error: {ex.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }
}
