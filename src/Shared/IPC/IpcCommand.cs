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
    SetTunnelAutoStart,

    // Phase 2 — User management
    ListUsers,
    SetUserRole,
    RemoveUser,

    // Phase 2 — Audit
    GetAuditLog,

    // Phase 3 — Global settings (Admin only)
    GetGlobalSettings,
    SetGlobalSettings,

    // Phase 3 — Audit settings (Admin only)
    GetAuditSettings,
    SetAuditSettings,
}
