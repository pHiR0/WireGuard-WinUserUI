namespace WireGuard.Shared.Models;

public sealed class TunnelInfo
{
    public required string Name { get; init; }
    public TunnelStatus Status { get; init; }
    public DateTimeOffset LastChecked { get; init; }
}
