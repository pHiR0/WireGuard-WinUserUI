namespace WireGuard.Shared.Models;

public sealed class TunnelInfo
{
    public required string Name { get; init; }
    public TunnelStatus Status { get; init; }
    public DateTimeOffset LastChecked { get; init; }

    /// <summary>True when the WireGuard tunnel Windows service is configured for automatic start.</summary>
    public bool AutoStart { get; init; }

    // Tunnel stats (populated when the tunnel is Running and wg.exe is available)
    public string? TunnelAddress { get; init; }
    public string? Endpoint { get; init; }
    public string? LastHandshake { get; init; }
    public long RxBytes { get; init; }
    public long TxBytes { get; init; }
}
