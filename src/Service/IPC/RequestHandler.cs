using Microsoft.Extensions.Logging;
using WireGuard.Service.Audit;
using WireGuard.Service.Auth;
using WireGuard.Service.Tunnels;
using WireGuard.Shared.IPC;
using WireGuard.Shared.Models;

namespace WireGuard.Service.IPC;

public sealed class RequestHandler
{
    private readonly ITunnelManager _tunnelManager;
    private readonly IRoleStore _roleStore;
    private readonly IAuthorizationService _authService;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<RequestHandler> _logger;

    public RequestHandler(
        ITunnelManager tunnelManager,
        IRoleStore roleStore,
        IAuthorizationService authService,
        IAuditLogger auditLogger,
        ILogger<RequestHandler> logger)
    {
        _tunnelManager = tunnelManager;
        _roleStore = roleStore;
        _authService = authService;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, string callingUser, CancellationToken ct)
    {
        var role = await _roleStore.GetRoleAsync(callingUser, ct);

        if (!_authService.IsAuthorized(role, request.Command))
        {
            _logger.LogWarning("Access denied: user '{User}' (role {Role}) attempted {Command}",
                callingUser, role, request.Command);

            await _auditLogger.LogAsync(new AuditEntry
            {
                Username = callingUser,
                Action = request.Command.ToString(),
                Tunnel = request.TunnelName,
                Result = "Denied",
                Error = $"Insufficient permissions (role: {role})",
            }, ct);

            return IpcResponse.Fail("Access denied", request.RequestId);
        }

        try
        {
            var response = await ExecuteAsync(request, callingUser, role, ct);

            await _auditLogger.LogAsync(new AuditEntry
            {
                Username = callingUser,
                Action = request.Command.ToString(),
                Tunnel = request.TunnelName,
                Result = response.Success ? "Success" : "Failed",
                Error = response.Error,
            }, ct);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling command {Command} from user '{User}'",
                request.Command, callingUser);

            await _auditLogger.LogAsync(new AuditEntry
            {
                Username = callingUser,
                Action = request.Command.ToString(),
                Tunnel = request.TunnelName,
                Result = "Error",
                Error = ex.Message,
            }, ct);

            return IpcResponse.Fail($"Internal error: {ex.Message}", request.RequestId);
        }
    }

    private async Task<IpcResponse> ExecuteAsync(IpcRequest request, string callingUser, UserRole role, CancellationToken ct)
    {
        switch (request.Command)
        {
            // --- Phase 1 commands ---

            case IpcCommand.ListTunnels:
                var tunnels = await _tunnelManager.ListTunnelsAsync(ct);
                return IpcResponse.Ok(tunnels, request.RequestId);

            case IpcCommand.GetTunnelStatus:
                if (string.IsNullOrEmpty(request.TunnelName))
                    return IpcResponse.Fail("TunnelName is required", request.RequestId);
                var status = await _tunnelManager.GetTunnelStatusAsync(request.TunnelName, ct);
                return status is not null
                    ? IpcResponse.Ok(status, request.RequestId)
                    : IpcResponse.Fail($"Tunnel '{request.TunnelName}' not found", request.RequestId);

            case IpcCommand.StartTunnel:
                if (string.IsNullOrEmpty(request.TunnelName))
                    return IpcResponse.Fail("TunnelName is required", request.RequestId);
                await _tunnelManager.StartTunnelAsync(request.TunnelName, ct);
                return IpcResponse.Ok(requestId: request.RequestId);

            case IpcCommand.StopTunnel:
                if (string.IsNullOrEmpty(request.TunnelName))
                    return IpcResponse.Fail("TunnelName is required", request.RequestId);
                await _tunnelManager.StopTunnelAsync(request.TunnelName, ct);
                return IpcResponse.Ok(requestId: request.RequestId);

            case IpcCommand.GetCurrentUser:
                return IpcResponse.Ok(new UserInfo
                {
                    Username = callingUser,
                    Role = role,
                }, request.RequestId);

            // --- Phase 2: Tunnel management ---

            case IpcCommand.RestartTunnel:
                if (string.IsNullOrEmpty(request.TunnelName))
                    return IpcResponse.Fail("TunnelName is required", request.RequestId);
                await _tunnelManager.RestartTunnelAsync(request.TunnelName, ct);
                return IpcResponse.Ok(requestId: request.RequestId);

            case IpcCommand.ImportTunnel:
                if (string.IsNullOrEmpty(request.TunnelName))
                    return IpcResponse.Fail("TunnelName is required", request.RequestId);
                if (string.IsNullOrEmpty(request.ConfContent))
                    return IpcResponse.Fail("ConfContent is required", request.RequestId);
                {
                    var confText = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(request.ConfContent));
                    await _tunnelManager.ImportTunnelAsync(request.TunnelName, confText, ct);
                    return IpcResponse.Ok(requestId: request.RequestId);
                }

            case IpcCommand.CreateTunnel:
                if (string.IsNullOrEmpty(request.TunnelName))
                    return IpcResponse.Fail("TunnelName is required", request.RequestId);
                if (string.IsNullOrEmpty(request.ConfContent))
                    return IpcResponse.Fail("ConfContent is required", request.RequestId);
                {
                    var confText = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(request.ConfContent));
                    await _tunnelManager.ImportTunnelAsync(request.TunnelName, confText, ct);
                    return IpcResponse.Ok(requestId: request.RequestId);
                }

            case IpcCommand.EditTunnel:
                if (string.IsNullOrEmpty(request.TunnelName))
                    return IpcResponse.Fail("TunnelName is required", request.RequestId);
                if (string.IsNullOrEmpty(request.ConfContent))
                    return IpcResponse.Fail("ConfContent is required", request.RequestId);
                {
                    var confText = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(request.ConfContent));
                    await _tunnelManager.EditTunnelAsync(request.TunnelName, confText, ct);
                    return IpcResponse.Ok(requestId: request.RequestId);
                }

            case IpcCommand.DeleteTunnel:
                if (string.IsNullOrEmpty(request.TunnelName))
                    return IpcResponse.Fail("TunnelName is required", request.RequestId);
                await _tunnelManager.DeleteTunnelAsync(request.TunnelName, ct);
                return IpcResponse.Ok(requestId: request.RequestId);

            case IpcCommand.ExportTunnel:
                if (string.IsNullOrEmpty(request.TunnelName))
                    return IpcResponse.Fail("TunnelName is required", request.RequestId);
                var confContent = await _tunnelManager.ExportTunnelAsync(request.TunnelName, ct);
                if (confContent is null)
                    return IpcResponse.Fail($"Configuration for tunnel '{request.TunnelName}' not found", request.RequestId);
                var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(confContent));
                return IpcResponse.Ok(b64, request.RequestId);

            case IpcCommand.SetTunnelAutoStart:
                if (string.IsNullOrEmpty(request.TunnelName))
                    return IpcResponse.Fail("TunnelName is required", request.RequestId);
                if (request.AutoStart is null)
                    return IpcResponse.Fail("AutoStart is required", request.RequestId);
                await _tunnelManager.SetTunnelAutoStartAsync(request.TunnelName, request.AutoStart.Value, ct);
                return IpcResponse.Ok(requestId: request.RequestId);

            // --- Phase 2: User management ---

            case IpcCommand.ListUsers:
                var users = await _roleStore.ListUsersAsync(ct);
                return IpcResponse.Ok(users, request.RequestId);

            case IpcCommand.SetUserRole:
                if (string.IsNullOrEmpty(request.Username))
                    return IpcResponse.Fail("Username is required", request.RequestId);
                if (request.Role is null)
                    return IpcResponse.Fail("Role is required", request.RequestId);
                await _roleStore.SetRoleAsync(request.Username, request.Role.Value, ct);
                return IpcResponse.Ok(requestId: request.RequestId);

            case IpcCommand.RemoveUser:
                if (string.IsNullOrEmpty(request.Username))
                    return IpcResponse.Fail("Username is required", request.RequestId);
                await _roleStore.RemoveUserAsync(request.Username, ct);
                return IpcResponse.Ok(requestId: request.RequestId);

            // --- Phase 2: Audit ---

            case IpcCommand.GetAuditLog:
                var auditQuery = request.AuditQuery ?? new AuditQuery();
                var page = await _auditLogger.QueryAsync(auditQuery, ct);
                return IpcResponse.Ok(page, request.RequestId);

            default:
                return IpcResponse.Fail($"Unknown command: {request.Command}", request.RequestId);
        }
    }
}
