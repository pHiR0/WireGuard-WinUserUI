using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
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
    private readonly INotificationService _notifications;
    private readonly PublicIpService _publicIpService = new();
    private CancellationTokenSource? _backgroundCts;
    private const int RefreshIntervalMs = 5000;
    private const int ReconnectIntervalMs = 1000;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ServiceStatusColor), nameof(ServiceStatusText), nameof(HasNoPermissions))]
    private bool _isConnected;

    [ObservableProperty]
    private string _currentUser = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOperator), nameof(IsAdvancedOperator), nameof(IsAdmin), nameof(CurrentRoleText), nameof(HasNoPermissions))]
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

    [ObservableProperty]
    private string _publicIp = "—";

    [ObservableProperty]
    private bool _isRefreshingPublicIp;

    /// <summary>True from startup until the first tunnel list is received from the service.</summary>
    [ObservableProperty]
    private bool _isLoadingTunnels = true;

    public ObservableCollection<TunnelViewModel> Tunnels { get; } = [];

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsOperator => CurrentRole >= UserRole.Operator;
    public bool IsAdvancedOperator => CurrentRole >= UserRole.AdvancedOperator;
    public bool IsAdmin => CurrentRole >= UserRole.Admin;

    /// <summary>True when connected to the service but the user has no role assigned.</summary>
    public bool HasNoPermissions => IsConnected && CurrentRole == UserRole.None;

    // Role display
    public string CurrentRoleText => CurrentRole switch
    {
        UserRole.Admin            => "Administrador",
        UserRole.AdvancedOperator => "Operador avanzado",
        UserRole.Operator         => "Operador",
        UserRole.Viewer           => "Visualizador",
        _                         => "Sin rol",
    };

    // Bottom toolbar service status
    public string ServiceStatusColor => IsConnected ? "#4ADE80" : "#F87171";
    public string ServiceStatusText => IsConnected ? "Conectado al servicio" : "Desconectado del servicio";

    // WireGuard installation check (well-known install path on Windows)
    private static readonly string _wireGuardExePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WireGuard", "wireguard.exe");
    public bool IsWireGuardInstalled => File.Exists(_wireGuardExePath);

    // Whether any tunnel is currently in Running state (used by tray icon)
    public bool AnyTunnelRunning => Tunnels.Any(t => t.Status == TunnelStatus.Running);

    // Sub-ViewModels for tabs
    public AuditLogViewModel AuditLog { get; }
    public SettingsViewModel Settings { get; }

    /// <summary>Raised when a tunnel editor dialog should be opened.</summary>
    public event Action<TunnelEditorViewModel>? OpenEditorRequested;

    /// <summary>
    /// Raised when the user requests to import a .conf file.
    /// The subscriber should open a file picker and return the file content (or null if cancelled),
    /// then the ViewModel will open the editor with that content.
    /// </summary>
    public event Func<Task<string?>>? PickConfFileRequested;

    public MainWindowViewModel() : this(new PipeClient(), new WindowsNotificationService()) { }

    public MainWindowViewModel(IPipeClient pipeClient, INotificationService? notifications = null)
    {
        _pipeClient = pipeClient;
        _notifications = notifications ?? new WindowsNotificationService();
        AuditLog = new AuditLogViewModel(pipeClient);
        Settings = new SettingsViewModel();

        pipeClient.Disconnected += () =>
            Dispatcher.UIThread.Post(() => { IsConnected = false; IsLoadingTunnels = true; Tunnels.Clear(); });

        pipeClient.Reconnected += () =>
            Dispatcher.UIThread.Post(() => { IsConnected = true; });

        // Start auto-connect + refresh background loop
        _backgroundCts = new CancellationTokenSource();
        _ = BackgroundLoopAsync(_backgroundCts.Token);
        _ = DoRefreshPublicIpAsync(force: true);
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

                    // userInfo is null when service returns "Access denied" (role = None).
                    // We still mark as connected so the UI can show the "no permissions" banner.
                    var role = userInfo?.Role ?? UserRole.None;

                    IReadOnlyList<TunnelInfo> tunnels = [];
                    if (role > UserRole.None)
                    {
                        // Only fetch tunnels when the user has at least Viewer access.
                        try { tunnels = await _pipeClient.ListTunnelsAsync(ct); }
                        catch { /* ignore — stay connected but show empty list */ }
                    }

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (userInfo is not null)
                        {
                            CurrentUser = userInfo.Username;
                            CurrentRole = userInfo.Role;
                        }
                        else
                        {
                            CurrentRole = UserRole.None;
                        }
                        IsConnected = true;
                        IsLoadingTunnels = false;
                        ErrorMessage = string.Empty;
                        SyncTunnelList(tunnels);
                    });
                }
                else
                {
                    if (CurrentRole > UserRole.None)
                    {
                        var tunnels = await _pipeClient.ListTunnelsAsync(ct);
                        await Dispatcher.UIThread.InvokeAsync(() => SyncTunnelList(tunnels));
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch { /* conexión no disponible, reintentar */ }

            var delay = IsConnected
                ? RefreshIntervalMs
                : ReconnectIntervalMs;
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
            {
                var wasRunning = vm.Status == TunnelStatus.Running;
                vm.UpdateFrom(info);
                // Fire notifications on status transitions (only when notifications enabled)
                var transitioned = (!wasRunning && info.Status == TunnelStatus.Running)
                                || (wasRunning && info.Status != TunnelStatus.Running);
                if (Settings.EnableNotifications && transitioned)
                {
                    if (!wasRunning) _notifications.ShowTunnelConnected(info.Name);
                    else            _notifications.ShowTunnelDisconnected(info.Name);
                }
                if (transitioned)
                    _ = DoRefreshPublicIpAsync(force: true);
            }
            else
                Tunnels.Add(new TunnelViewModel
                {
                    Name = info.Name,
                    Status = info.Status,
                    LastChecked = info.LastChecked,
                    TunnelAddress = info.TunnelAddress,
                    Endpoint = info.Endpoint,
                    LastHandshake = info.LastHandshake,
                    RxBytes = info.RxBytes,
                    TxBytes = info.TxBytes,
                    AutoStart = info.AutoStart,
                });
        }
        OnPropertyChanged(nameof(AnyTunnelRunning));
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
    private async Task SetTunnelAutoStartAsync(TunnelViewModel? tunnel)
    {
        if (tunnel is null || !IsConnected || !IsAdvancedOperator) return;
        try
        {
            ErrorMessage = string.Empty;
            await _pipeClient.SetTunnelAutoStartAsync(tunnel.Name, tunnel.AutoStart);
        }
        catch (Exception ex)
        {
            tunnel.AutoStart = !tunnel.AutoStart; // revert on error
            ErrorMessage = ex.Message;
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
            var editorVm = new TunnelEditorViewModel(_pipeClient, TunnelEditorMode.Edit,
                originalName: tunnel.Name, confContent: content);
            OpenEditorRequested?.Invoke(editorVm);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
    }

    [RelayCommand]
    public async Task RefreshPublicIpAsync() => await DoRefreshPublicIpAsync(force: true);

    /// <summary>Rate-limited IP refresh — use when showing the window from tray.</summary>
    public void RequestPublicIpRefresh() => _ = DoRefreshPublicIpAsync(force: false);

    private async Task DoRefreshPublicIpAsync(bool force = false)
    {
        if (IsRefreshingPublicIp) return;
        IsRefreshingPublicIp = true;
        try
        {
            var orderedIds = Settings.GetOrderedEnabledProviderIds();
            var ip = await _publicIpService.GetPublicIpAsync(force, orderedIds);
            if (ip is not null)
                PublicIp = ip;
        }
        catch { /* non-critical — keep existing value */ }
        finally { IsRefreshingPublicIp = false; }
    }

    [RelayCommand]
    private void NewTunnel()
    {
        if (!IsConnected || !IsAdvancedOperator) return;
        var editorVm = new TunnelEditorViewModel(_pipeClient, TunnelEditorMode.New);
        OpenEditorRequested?.Invoke(editorVm);
    }

    [RelayCommand]
    private async Task ImportTunnelFileAsync()
    {
        if (!IsConnected || !IsAdvancedOperator) return;
        if (PickConfFileRequested is null) return;

        var content = await PickConfFileRequested.Invoke();
        if (content is null) return;

        var editorVm = new TunnelEditorViewModel(_pipeClient, TunnelEditorMode.New,
            confContent: content);
        OpenEditorRequested?.Invoke(editorVm);
    }

    [RelayCommand]
    private static void OpenGitHub()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/pHiR0/WireGuard-WinUserUI",
                UseShellExecute = true,
            });
        }
        catch { /* ignore */ }
    }

    public static string AppVersion =>
        System.Reflection.Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
        ?? "en desarrollo";

    public async ValueTask DisposeAsync()
    {
        _backgroundCts?.Cancel();
        _backgroundCts?.Dispose();
        _publicIpService.Dispose();
        await _pipeClient.DisposeAsync();
    }
}

