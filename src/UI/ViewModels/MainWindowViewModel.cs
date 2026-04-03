using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WireGuard.Shared.Models;
using WireGuard.UI.Services;

namespace WireGuard.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IPipeClient _pipeClient;
    private CancellationTokenSource? _backgroundCts;
    private const int RefreshIntervalMs = 5000;
    private const int ReconnectIntervalMs = 1000;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ServiceStatusColor), nameof(ServiceStatusText))]
    private bool _isConnected;

    [ObservableProperty]
    private string _currentUser = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOperator), nameof(IsAdvancedOperator), nameof(IsAdmin))]
    private UserRole _currentRole;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private string? _lastExportedContent;

    [ObservableProperty]
    private string? _lastExportedTunnelName;

    public ObservableCollection<TunnelViewModel> Tunnels { get; } = [];

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsOperator => CurrentRole >= UserRole.Operator;
    public bool IsAdvancedOperator => CurrentRole >= UserRole.AdvancedOperator;
    public bool IsAdmin => CurrentRole >= UserRole.Admin;

    // Bottom toolbar service status
    public string ServiceStatusColor => IsConnected ? "#4ADE80" : "#F87171";
    public string ServiceStatusText => IsConnected ? "Conectado al servicio" : "Desconectado del servicio";

    // Sub-ViewModels for tabs
    public UserManagementViewModel UserManagement { get; }
    public AuditLogViewModel AuditLog { get; }
    public ImportTunnelViewModel ImportTunnel { get; }

    public MainWindowViewModel() : this(new PipeClient()) { }

    public MainWindowViewModel(IPipeClient pipeClient)
    {
        _pipeClient = pipeClient;
        UserManagement = new UserManagementViewModel(pipeClient);
        AuditLog = new AuditLogViewModel(pipeClient);
        ImportTunnel = new ImportTunnelViewModel(pipeClient);

        pipeClient.Disconnected += () =>
            Dispatcher.UIThread.Post(() => { IsConnected = false; Tunnels.Clear(); });

        pipeClient.Reconnected += () =>
            Dispatcher.UIThread.Post(() => { IsConnected = true; });

        // Start auto-connect + refresh background loop
        _backgroundCts = new CancellationTokenSource();
        _ = BackgroundLoopAsync(_backgroundCts.Token);
    }

    private async Task BackgroundLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!_pipeClient.IsConnected)
                {
                    await _pipeClient.ConnectAsync(ct);
                    var userInfo = await _pipeClient.GetCurrentUserAsync(ct);
                    var tunnels = await _pipeClient.ListTunnelsAsync(ct);
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (userInfo is not null)
                        {
                            CurrentUser = userInfo.Username;
                            CurrentRole = userInfo.Role;
                        }
                        IsConnected = true;
                        ErrorMessage = string.Empty;
                        SyncTunnelList(tunnels);
                    });
                }
                else
                {
                    var tunnels = await _pipeClient.ListTunnelsAsync(ct);
                    await Dispatcher.UIThread.InvokeAsync(() => SyncTunnelList(tunnels));
                }
            }
            catch (OperationCanceledException) { break; }
            catch { /* conexión no disponible, reintentar */ }

            var delay = IsConnected ? RefreshIntervalMs : ReconnectIntervalMs;
            try { await Task.Delay(delay, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private void SyncTunnelList(IReadOnlyList<TunnelInfo> infos)
    {
        // Must be called on UI thread
        var byName = Tunnels.ToDictionary(t => t.Name);
        var newNames = infos.Select(i => i.Name).ToHashSet();

        // Remove stale tunnels
        foreach (var stale in byName.Keys.Where(n => !newNames.Contains(n)).ToList())
            Tunnels.Remove(byName[stale]);

        // Update existing or add new
        foreach (var info in infos)
        {
            if (byName.TryGetValue(info.Name, out var vm))
                vm.UpdateFrom(info);
            else
                Tunnels.Add(new TunnelViewModel { Name = info.Name, Status = info.Status, LastChecked = info.LastChecked });
        }
    }

    [RelayCommand]
    private async Task RefreshTunnelsAsync()
    {
        if (!IsConnected) return;
        try
        {
            IsRefreshing = true;
            ErrorMessage = string.Empty;
            var tunnels = await _pipeClient.ListTunnelsAsync();
            SyncTunnelList(tunnels);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error al actualizar: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task StartTunnelAsync(TunnelViewModel? tunnel)
    {
        if (tunnel is null || !IsConnected || !IsOperator) return;
        try
        {
            tunnel.IsLoading = true;
            ErrorMessage = string.Empty;
            await _pipeClient.StartTunnelAsync(tunnel.Name);
            await RefreshTunnelsAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { tunnel.IsLoading = false; }
    }

    [RelayCommand]
    private async Task StopTunnelAsync(TunnelViewModel? tunnel)
    {
        if (tunnel is null || !IsConnected || !IsOperator) return;
        try
        {
            tunnel.IsLoading = true;
            ErrorMessage = string.Empty;
            await _pipeClient.StopTunnelAsync(tunnel.Name);
            await RefreshTunnelsAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { tunnel.IsLoading = false; }
    }

    [RelayCommand]
    private async Task RestartTunnelAsync(TunnelViewModel? tunnel)
    {
        if (tunnel is null || !IsConnected || !IsOperator) return;
        try
        {
            tunnel.IsLoading = true;
            ErrorMessage = string.Empty;
            await _pipeClient.RestartTunnelAsync(tunnel.Name);
            await RefreshTunnelsAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { tunnel.IsLoading = false; }
    }

    [RelayCommand]
    private async Task DeleteTunnelAsync(TunnelViewModel? tunnel)
    {
        if (tunnel is null || !IsConnected || !IsAdvancedOperator) return;
        try
        {
            tunnel.IsLoading = true;
            ErrorMessage = string.Empty;
            await _pipeClient.DeleteTunnelAsync(tunnel.Name);
            await RefreshTunnelsAsync();
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { tunnel.IsLoading = false; }
    }

    [RelayCommand]
    private async Task ExportTunnelAsync(TunnelViewModel? tunnel)
    {
        if (tunnel is null || !IsConnected || !IsAdvancedOperator) return;
        try
        {
            ErrorMessage = string.Empty;
            var content = await _pipeClient.ExportTunnelAsync(tunnel.Name);
            if (content is not null)
            {
                LastExportedContent = content;
                LastExportedTunnelName = tunnel.Name;
            }
            else
            {
                ErrorMessage = "No se encontró el archivo de configuración para este túnel.";
            }
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    private async Task EditTunnelAsync(TunnelViewModel? tunnel)
    {
        if (tunnel is null || !IsConnected || !IsAdvancedOperator) return;
        try
        {
            ErrorMessage = string.Empty;
            var content = await _pipeClient.ExportTunnelAsync(tunnel.Name);
            if (content is null)
            {
                ErrorMessage = "No se pudo cargar la configuración del túnel para editar.";
                return;
            }
            ImportTunnel.EnterEditMode(tunnel.Name, content);
            // Switch to the Importar/Editar tab (index 1)
            SelectedTabIndex = 1;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    public async ValueTask DisposeAsync()
    {
        _backgroundCts?.Cancel();
        _backgroundCts?.Dispose();
        await _pipeClient.DisposeAsync();
    }
}

