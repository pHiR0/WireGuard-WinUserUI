using WireGuard.Service.Auth;
using WireGuard.Shared.IPC;
using WireGuard.Shared.Models;

namespace WireGuard.Service.Tests;

public class AuthorizationServiceTests
{
    private readonly AuthorizationService _authService = new();

    [Theory]
    [InlineData(UserRole.None, IpcCommand.ListTunnels, false)]
    [InlineData(UserRole.Viewer, IpcCommand.ListTunnels, true)]
    [InlineData(UserRole.Viewer, IpcCommand.GetTunnelStatus, true)]
    [InlineData(UserRole.Viewer, IpcCommand.GetCurrentUser, true)]
    [InlineData(UserRole.Viewer, IpcCommand.StartTunnel, false)]
    [InlineData(UserRole.Viewer, IpcCommand.StopTunnel, false)]
    [InlineData(UserRole.Operator, IpcCommand.ListTunnels, true)]
    [InlineData(UserRole.Operator, IpcCommand.StartTunnel, true)]
    [InlineData(UserRole.Operator, IpcCommand.StopTunnel, true)]
    [InlineData(UserRole.Admin, IpcCommand.ListTunnels, true)]
    [InlineData(UserRole.Admin, IpcCommand.StartTunnel, true)]
    [InlineData(UserRole.Admin, IpcCommand.StopTunnel, true)]
    [InlineData(UserRole.Admin, IpcCommand.GetCurrentUser, true)]
    // Phase 2 — Tunnel management
    [InlineData(UserRole.Operator, IpcCommand.RestartTunnel, true)]
    [InlineData(UserRole.Viewer, IpcCommand.RestartTunnel, false)]
    [InlineData(UserRole.Operator, IpcCommand.ImportTunnel, false)]
    [InlineData(UserRole.AdvancedOperator, IpcCommand.ImportTunnel, true)]
    [InlineData(UserRole.AdvancedOperator, IpcCommand.CreateTunnel, true)]
    [InlineData(UserRole.AdvancedOperator, IpcCommand.EditTunnel, true)]
    [InlineData(UserRole.AdvancedOperator, IpcCommand.DeleteTunnel, true)]
    [InlineData(UserRole.AdvancedOperator, IpcCommand.ExportTunnel, false)]
    [InlineData(UserRole.Admin, IpcCommand.ExportTunnel, true)]
    // Phase 2 — User management
    [InlineData(UserRole.AdvancedOperator, IpcCommand.ListUsers, false)]
    [InlineData(UserRole.Admin, IpcCommand.ListUsers, true)]
    [InlineData(UserRole.Admin, IpcCommand.SetUserRole, true)]
    [InlineData(UserRole.Admin, IpcCommand.RemoveUser, true)]
    [InlineData(UserRole.Operator, IpcCommand.SetUserRole, false)]
    // Phase 2 — Audit
    [InlineData(UserRole.AdvancedOperator, IpcCommand.GetAuditLog, false)]
    [InlineData(UserRole.Admin, IpcCommand.GetAuditLog, true)]
    public void IsAuthorized_ReturnsExpectedResult(UserRole role, IpcCommand command, bool expected)
    {
        var result = _authService.IsAuthorized(role, command);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NoneRole_IsDeniedEverything()
    {
        foreach (var command in Enum.GetValues<IpcCommand>())
        {
            Assert.False(_authService.IsAuthorized(UserRole.None, command));
        }
    }

    [Fact]
    public void AdminRole_IsAllowedEverything()
    {
        foreach (var command in Enum.GetValues<IpcCommand>())
        {
            Assert.True(_authService.IsAuthorized(UserRole.Admin, command));
        }
    }
}
