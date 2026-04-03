using WireGuard.Shared.Models;

namespace WireGuard.Service.Tunnels;

public interface ITunnelManager
{
    Task<IReadOnlyList<TunnelInfo>> ListTunnelsAsync(CancellationToken ct = default);
    Task<TunnelInfo?> GetTunnelStatusAsync(string name, CancellationToken ct = default);
    Task StartTunnelAsync(string name, CancellationToken ct = default);
    Task StopTunnelAsync(string name, CancellationToken ct = default);
    Task RestartTunnelAsync(string name, CancellationToken ct = default);
    Task ImportTunnelAsync(string name, string confContent, CancellationToken ct = default);
    Task EditTunnelAsync(string name, string confContent, CancellationToken ct = default);
    Task DeleteTunnelAsync(string name, CancellationToken ct = default);
    Task<string?> ExportTunnelAsync(string name, CancellationToken ct = default);
}
