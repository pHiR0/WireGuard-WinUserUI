using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WireGuard.Shared.Models;
using WireGuard.UI.Services;

namespace WireGuard.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IPipeClient _pipeClient;
    private CancellationTokenSource? _refreshCts;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionStatus = "Disconnected";

    [ObservableProperty]
    private string _currentUser = string.Empty;

    [ObservableProperty]
    private UserRole _currentRole;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private int _selectedTabIndex;

    public ObservableCollection<TunnelViewModel> Tunnels { get; } = [];

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsOperator => CurrentRole >= UserRole.Operator;
    public bool IsAdvancedOperator => CurrentRole >= UserRole.AdvancedOperator;
    public bool IsAdmin => CurrentRole >= UserRole.Admin;

    // Sub-ViewModels for Phase 2 tabs
    public UserManagementViewModel UserManagement { get; }
    public AuditLogViewModel AuditLog { get; }
    public ImportTunnelViewModel ImportTunnel { get; }

    public MainWindowViewModel() : this(new PipeClient())
    {
    }

    public MainWindowViewModel(IPipeClient pipeClient)
    {
        _pipeClient = pipeClient;
        UserManagement = new UserManagementViewModel(pipeClient);
        AuditLog = new AuditLogViewModel(pipeClient);
        ImportTunnel = new ImportTunnelViewModel(pipeClient);

        pipeClient.Disconnected += () =>
        {
            IsConnected = false;
            ConnectionStatus = "Reconnecting...";
        };
        pipeClient.Reconnected += () =>
        {
            IsConnected = true;
            ConnectionStatus = "Connected";
        };
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnCurrentRoleChanged(UserRole value)
    {
        OnPropertyChanged(nameof(IsOperator));
        OnPropertyChanged(nameof(IsAdvancedOperator));
        OnPropertyChanged(nameof(IsAdmin));
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        try
        {
            ErrorMessage = string.Empty;
            ConnectionStatus = "Connecting...";
            await _pipeClient.ConnectAsync();
            IsConnected = true;
            ConnectionStatus = "Connected";

            var userInfo = await _pipeClient.GetCurrentUserAsync();
            if (userInfo is not null)
            {
                CurrentUser = userInfo.Username;
                CurrentRole = userInfo.Role;
            }

            await RefreshTunnelsAsync();
            StartAutoRefresh();
        }
        catch (Exception ex)
        {
            IsConnected = false;
            ConnectionStatus = "Disconnected";
            ErrorMessage = $"Failed to connect: {ex.Message}";
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

            Tunnels.Clear();
            foreach (var t in tunnels)
            {
                Tunnels.Add(new TunnelViewModel
                {
                    Name = t.Name,
                    Status = t.Status,
                    LastChecked = t.LastChecked,
                });
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to refresh: {ex.Message}";
            IsConnected = false;
            ConnectionStatus = "Disconnected";
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
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            tunnel.IsLoading = false;
        }
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
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            tunnel.IsLoading = false;
        }
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
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            tunnel.IsLoading = false;
        }
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
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            tunnel.IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ExportTunnelAsync(TunnelViewModel? tunnel)
    {
        if (tunnel is null || !IsConnected || !IsAdmin) return;

        try
        {
            ErrorMessage = string.Empty;
            var content = await _pipeClient.ExportTunnelAsync(tunnel.Name);
            if (content is not null)
            {
                // Store exported content for the view to handle (file save dialog)
                LastExportedContent = content;
                LastExportedTunnelName = tunnel.Name;
            }
            else
            {
                ErrorMessage = "Configuration file not found for this tunnel.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    // Exported content to be consumed by the view for file save
    [ObservableProperty]
    private string? _lastExportedContent;

    [ObservableProperty]
    private string? _lastExportedTunnelName;

    private void StartAutoRefresh()
    {
        StopAutoRefresh();
        _refreshCts = new CancellationTokenSource();
        _ = AutoRefreshLoopAsync(_refreshCts.Token);
    }

    private void StopAutoRefresh()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = null;
    }

    private async Task AutoRefreshLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                if (IsConnected)
                    await RefreshTunnelsAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
