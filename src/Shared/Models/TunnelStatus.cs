namespace WireGuard.Shared.Models;

public enum TunnelStatus
{
    Unknown,
    Stopped,
    Running,
    StartPending,
    StopPending,
    Error
}
