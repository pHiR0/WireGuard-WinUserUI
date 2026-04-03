using WireGuard.Shared.IPC;
using WireGuard.Shared.Models;

namespace WireGuard.Service.Auth;

public interface IAuthorizationService
{
    bool IsAuthorized(UserRole role, IpcCommand command);
}

public sealed class AuthorizationService : IAuthorizationService
{
    // Permission matrix: which roles can execute which commands
    private static readonly Dictionary<IpcCommand, UserRole> MinimumRoles = new()
    {
        // Phase 1
        [IpcCommand.ListTunnels] = UserRole.Viewer,
        [IpcCommand.GetTunnelStatus] = UserRole.Viewer,
        [IpcCommand.GetCurrentUser] = UserRole.Viewer,
        [IpcCommand.StartTunnel] = UserRole.Operator,
        [IpcCommand.StopTunnel] = UserRole.Operator,

        // Phase 2 — Tunnel management
        [IpcCommand.RestartTunnel] = UserRole.Operator,
        [IpcCommand.ImportTunnel] = UserRole.AdvancedOperator,
        [IpcCommand.CreateTunnel] = UserRole.AdvancedOperator,
        [IpcCommand.EditTunnel] = UserRole.AdvancedOperator,
        [IpcCommand.DeleteTunnel] = UserRole.AdvancedOperator,
        [IpcCommand.ExportTunnel] = UserRole.Admin,

        // Phase 2 — User management
        [IpcCommand.ListUsers] = UserRole.Admin,
        [IpcCommand.SetUserRole] = UserRole.Admin,
        [IpcCommand.RemoveUser] = UserRole.Admin,

        // Phase 2 — Audit
        [IpcCommand.GetAuditLog] = UserRole.Admin,
    };

    public bool IsAuthorized(UserRole role, IpcCommand command)
    {
        if (role == UserRole.None)
            return false;

        if (role == UserRole.Admin)
            return true;

        if (!MinimumRoles.TryGetValue(command, out var minimumRole))
            return false;

        return role >= minimumRole;
    }
}
