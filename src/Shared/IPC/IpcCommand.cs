namespace WireGuard.Shared.IPC;

public enum IpcCommand
{
    // Phase 1
    ListTunnels,
    GetTunnelStatus,
    StartTunnel,
    StopTunnel,
    GetCurrentUser,

    // Phase 2 — Tunnel management
    RestartTunnel,
    ImportTunnel,
    CreateTunnel,
    EditTunnel,
    DeleteTunnel,
    ExportTunnel,

    // Phase 2 — User management
    ListUsers,
    SetUserRole,
    RemoveUser,

    // Phase 2 — Audit
    GetAuditLog,
}
