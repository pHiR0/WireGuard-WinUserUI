namespace WireGuard.Shared.Models;

/// <summary>
/// Represents intermediate parsed data for a WireGuard .conf file.
/// </summary>
public sealed class TunnelConfig
{
    public required string Name { get; init; }
    public required string ConfContent { get; init; }
}
