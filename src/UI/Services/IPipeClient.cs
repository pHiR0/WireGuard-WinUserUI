using WireGuard.Shared.IPC;
using WireGuard.Shared.Models;

namespace WireGuard.UI.Services;

public interface IPipeClient : IAsyncDisposable
{
    bool IsConnected { get; }
    event Action? Disconnected;
    event Action? Reconnected;

    Task ConnectAsync(CancellationToken ct = default);
    Task<IpcResponse> SendAsync(IpcRequest request, CancellationToken ct = default);

    // Phase 1
    Task<IReadOnlyList<TunnelInfo>> ListTunnelsAsync(CancellationToken ct = default);
    Task<TunnelInfo?> GetTunnelStatusAsync(string name, CancellationToken ct = default);
    Task StartTunnelAsync(string name, CancellationToken ct = default);
    Task StopTunnelAsync(string name, CancellationToken ct = default);
    Task<UserInfo?> GetCurrentUserAsync(CancellationToken ct = default);

    // Phase 2 — Tunnel management
    Task RestartTunnelAsync(string name, CancellationToken ct = default);
    Task ImportTunnelAsync(string name, string confContent, CancellationToken ct = default);
    Task EditTunnelAsync(string name, string confContent, CancellationToken ct = default);
    Task DeleteTunnelAsync(string name, CancellationToken ct = default);
    Task<string?> ExportTunnelAsync(string name, CancellationToken ct = default);
    Task SetTunnelAutoStartAsync(string name, bool autoStart, CancellationToken ct = default);

    // Phase 2 — User management
    Task<IReadOnlyList<UserInfo>> ListUsersAsync(CancellationToken ct = default);
    Task SetUserRoleAsync(string username, UserRole role, CancellationToken ct = default);
    Task RemoveUserAsync(string username, CancellationToken ct = default);

    // Phase 2 — Audit
    Task<AuditPage> GetAuditLogAsync(AuditQuery? query = null, CancellationToken ct = default);

    // Phase 3 — Global settings (Admin only)
    Task<bool> GetAllUsersDefaultOperatorAsync(CancellationToken ct = default);
    Task SetAllUsersDefaultOperatorAsync(bool enabled, CancellationToken ct = default);
}
