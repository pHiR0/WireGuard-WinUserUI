using Moq;
using Microsoft.Extensions.Logging;
using WireGuard.Service.Audit;
using WireGuard.Service.Auth;
using WireGuard.Service.IPC;
using WireGuard.Service.Tunnels;
using WireGuard.Shared.IPC;
using WireGuard.Shared.Models;

namespace WireGuard.Service.Tests;

public class RequestHandlerTests
{
    private readonly Mock<ITunnelManager> _tunnelManagerMock = new();
    private readonly Mock<IRoleStore> _roleStoreMock = new();
    private readonly Mock<IAuditLogger> _auditLoggerMock = new();
    private readonly RequestHandler _handler;

    public RequestHandlerTests()
    {
        var authService = new AuthorizationService();
        _handler = new RequestHandler(
            _tunnelManagerMock.Object,
            _roleStoreMock.Object,
            authService,
            _auditLoggerMock.Object,
            new GlobalSettingsStore(),
            Mock.Of<ILogger<RequestHandler>>());
    }

    [Fact]
    public async Task ListTunnels_AsViewer_ReturnsSuccess()
    {
        // Arrange
        _roleStoreMock.Setup(x => x.GetRoleAsync("testuser", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Viewer);

        var tunnels = new List<TunnelInfo>
        {
            new() { Name = "tunnel1", Status = TunnelStatus.Running, LastChecked = DateTimeOffset.UtcNow }
        };
        _tunnelManagerMock.Setup(x => x.ListTunnelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tunnels);

        var request = new IpcRequest
        {
            Command = IpcCommand.ListTunnels,
            RequestId = "test-1",
        };

        // Act
        var response = await _handler.HandleAsync(request, "testuser", CancellationToken.None);

        // Assert
        Assert.True(response.Success);
        Assert.Equal("test-1", response.RequestId);
        var data = response.GetData<List<TunnelInfo>>();
        Assert.NotNull(data);
        Assert.Single(data);
        Assert.Equal("tunnel1", data[0].Name);
    }

    [Fact]
    public async Task StartTunnel_AsViewer_ReturnsDenied()
    {
        _roleStoreMock.Setup(x => x.GetRoleAsync("viewer", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Viewer);

        var request = new IpcRequest
        {
            Command = IpcCommand.StartTunnel,
            TunnelName = "tunnel1",
            RequestId = "test-2",
        };

        var response = await _handler.HandleAsync(request, "viewer", CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Access denied", response.Error);
    }

    [Fact]
    public async Task StartTunnel_AsOperator_ReturnsSuccess()
    {
        _roleStoreMock.Setup(x => x.GetRoleAsync("operator", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Operator);

        _tunnelManagerMock.Setup(x => x.StartTunnelAsync("tunnel1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new IpcRequest
        {
            Command = IpcCommand.StartTunnel,
            TunnelName = "tunnel1",
            RequestId = "test-3",
        };

        var response = await _handler.HandleAsync(request, "operator", CancellationToken.None);

        Assert.True(response.Success);
        _tunnelManagerMock.Verify(x => x.StartTunnelAsync("tunnel1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartTunnel_WithoutTunnelName_ReturnsFail()
    {
        _roleStoreMock.Setup(x => x.GetRoleAsync("operator", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Operator);

        var request = new IpcRequest
        {
            Command = IpcCommand.StartTunnel,
            RequestId = "test-4",
        };

        var response = await _handler.HandleAsync(request, "operator", CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("TunnelName is required", response.Error);
    }

    [Fact]
    public async Task GetCurrentUser_ReturnsUserInfo()
    {
        _roleStoreMock.Setup(x => x.GetRoleAsync("admin", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Admin);

        var request = new IpcRequest
        {
            Command = IpcCommand.GetCurrentUser,
            RequestId = "test-5",
        };

        var response = await _handler.HandleAsync(request, "admin", CancellationToken.None);

        Assert.True(response.Success);
        var user = response.GetData<UserInfo>();
        Assert.NotNull(user);
        Assert.Equal("admin", user.Username);
        Assert.Equal(UserRole.Admin, user.Role);
    }

    [Fact]
    public async Task UnknownUser_IsDenied()
    {
        _roleStoreMock.Setup(x => x.GetRoleAsync("unknown", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.None);

        var request = new IpcRequest
        {
            Command = IpcCommand.ListTunnels,
            RequestId = "test-6",
        };

        var response = await _handler.HandleAsync(request, "unknown", CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Access denied", response.Error);
    }

    [Fact]
    public async Task AuditLogger_IsCalledOnSuccess()
    {
        _roleStoreMock.Setup(x => x.GetRoleAsync("operator", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Operator);
        _tunnelManagerMock.Setup(x => x.ListTunnelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TunnelInfo>());

        var request = new IpcRequest { Command = IpcCommand.ListTunnels, RequestId = "test-7" };
        await _handler.HandleAsync(request, "operator", CancellationToken.None);

        _auditLoggerMock.Verify(
            x => x.LogAsync(It.Is<AuditEntry>(e =>
                e.Username == "operator" &&
                e.Action == "ListTunnels" &&
                e.Result == "Success"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AuditLogger_IsCalledOnDenied()
    {
        _roleStoreMock.Setup(x => x.GetRoleAsync("viewer", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Viewer);

        var request = new IpcRequest
        {
            Command = IpcCommand.StartTunnel,
            TunnelName = "tunnel1",
            RequestId = "test-8",
        };

        await _handler.HandleAsync(request, "viewer", CancellationToken.None);

        _auditLoggerMock.Verify(
            x => x.LogAsync(It.Is<AuditEntry>(e =>
                e.Username == "viewer" &&
                e.Action == "StartTunnel" &&
                e.Result == "Denied"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // --- Phase 2 tests ---

    [Fact]
    public async Task RestartTunnel_AsOperator_ReturnsSuccess()
    {
        _roleStoreMock.Setup(x => x.GetRoleAsync("operator", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Operator);
        _tunnelManagerMock.Setup(x => x.RestartTunnelAsync("tunnel1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new IpcRequest { Command = IpcCommand.RestartTunnel, TunnelName = "tunnel1", RequestId = "r1" };
        var response = await _handler.HandleAsync(request, "operator", CancellationToken.None);

        Assert.True(response.Success);
        _tunnelManagerMock.Verify(x => x.RestartTunnelAsync("tunnel1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RestartTunnel_AsViewer_Denied()
    {
        _roleStoreMock.Setup(x => x.GetRoleAsync("viewer", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Viewer);

        var request = new IpcRequest { Command = IpcCommand.RestartTunnel, TunnelName = "tunnel1", RequestId = "r2" };
        var response = await _handler.HandleAsync(request, "viewer", CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Access denied", response.Error);
    }

    [Fact]
    public async Task ImportTunnel_AsAdvancedOperator_ReturnsSuccess()
    {
        _roleStoreMock.Setup(x => x.GetRoleAsync("advop", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.AdvancedOperator);
        _tunnelManagerMock.Setup(x => x.ImportTunnelAsync("test", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var confBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("[Interface]\nPrivateKey = test\nAddress = 10.0.0.2/32\n\n[Peer]\nPublicKey = test\nAllowedIPs = 0.0.0.0/0"));
        var request = new IpcRequest
        {
            Command = IpcCommand.ImportTunnel,
            TunnelName = "test",
            ConfContent = confBase64,
            RequestId = "r3",
        };

        var response = await _handler.HandleAsync(request, "advop", CancellationToken.None);

        Assert.True(response.Success);
    }

    [Fact]
    public async Task ImportTunnel_MissingConfContent_ReturnsFail()
    {
        _roleStoreMock.Setup(x => x.GetRoleAsync("advop", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.AdvancedOperator);

        var request = new IpcRequest
        {
            Command = IpcCommand.ImportTunnel,
            TunnelName = "test",
            RequestId = "r4",
        };

        var response = await _handler.HandleAsync(request, "advop", CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("ConfContent is required", response.Error);
    }

    [Fact]
    public async Task DeleteTunnel_AsAdvancedOperator_ReturnsSuccess()
    {
        _roleStoreMock.Setup(x => x.GetRoleAsync("advop", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.AdvancedOperator);
        _tunnelManagerMock.Setup(x => x.DeleteTunnelAsync("tunnel1", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new IpcRequest { Command = IpcCommand.DeleteTunnel, TunnelName = "tunnel1", RequestId = "r5" };
        var response = await _handler.HandleAsync(request, "advop", CancellationToken.None);

        Assert.True(response.Success);
        _tunnelManagerMock.Verify(x => x.DeleteTunnelAsync("tunnel1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteTunnel_AsOperator_Denied()
    {
        _roleStoreMock.Setup(x => x.GetRoleAsync("operator", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Operator);

        var request = new IpcRequest { Command = IpcCommand.DeleteTunnel, TunnelName = "tunnel1", RequestId = "r6" };
        var response = await _handler.HandleAsync(request, "operator", CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Access denied", response.Error);
    }

    [Fact]
    public async Task ListUsers_AsAdmin_ReturnsUsers()
    {
        _roleStoreMock.Setup(x => x.GetRoleAsync("admin", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Admin);
        _roleStoreMock.Setup(x => x.ListUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserInfo>
            {
                new() { Username = "admin", Role = UserRole.Admin },
                new() { Username = "viewer1", Role = UserRole.Viewer },
            });

        var request = new IpcRequest { Command = IpcCommand.ListUsers, RequestId = "r7" };
        var response = await _handler.HandleAsync(request, "admin", CancellationToken.None);

        Assert.True(response.Success);
        var users = response.GetData<List<UserInfo>>();
        Assert.NotNull(users);
        Assert.Equal(2, users.Count);
    }

    [Fact]
    public async Task ListUsers_AsAdvancedOperator_Denied()
    {
        _roleStoreMock.Setup(x => x.GetRoleAsync("advop", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.AdvancedOperator);

        var request = new IpcRequest { Command = IpcCommand.ListUsers, RequestId = "r8" };
        var response = await _handler.HandleAsync(request, "advop", CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Access denied", response.Error);
    }

    [Fact]
    public async Task SetUserRole_AsAdmin_ReturnsSuccess()
    {
        _roleStoreMock.Setup(x => x.GetRoleAsync("admin", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Admin);
        _roleStoreMock.Setup(x => x.SetRoleAsync("newuser", UserRole.Operator, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new IpcRequest
        {
            Command = IpcCommand.SetUserRole,
            Username = "newuser",
            Role = UserRole.Operator,
            RequestId = "r9",
        };
        var response = await _handler.HandleAsync(request, "admin", CancellationToken.None);

        Assert.True(response.Success);
        _roleStoreMock.Verify(x => x.SetRoleAsync("newuser", UserRole.Operator, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetUserRole_MissingUsername_ReturnsFail()
    {
        _roleStoreMock.Setup(x => x.GetRoleAsync("admin", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Admin);

        var request = new IpcRequest { Command = IpcCommand.SetUserRole, Role = UserRole.Viewer, RequestId = "r10" };
        var response = await _handler.HandleAsync(request, "admin", CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("Username is required", response.Error);
    }

    [Fact]
    public async Task SetUserRole_MissingRole_ReturnsFail()
    {
        _roleStoreMock.Setup(x => x.GetRoleAsync("admin", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Admin);

        var request = new IpcRequest { Command = IpcCommand.SetUserRole, Username = "user1", RequestId = "r11" };
        var response = await _handler.HandleAsync(request, "admin", CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("Role is required", response.Error);
    }

    [Fact]
    public async Task RemoveUser_AsAdmin_ReturnsSuccess()
    {
        _roleStoreMock.Setup(x => x.GetRoleAsync("admin", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Admin);
        _roleStoreMock.Setup(x => x.RemoveUserAsync("olduser", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new IpcRequest { Command = IpcCommand.RemoveUser, Username = "olduser", RequestId = "r12" };
        var response = await _handler.HandleAsync(request, "admin", CancellationToken.None);

        Assert.True(response.Success);
        _roleStoreMock.Verify(x => x.RemoveUserAsync("olduser", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExportTunnel_AsAdmin_ReturnsBase64Content()
    {
        _roleStoreMock.Setup(x => x.GetRoleAsync("admin", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Admin);
        _tunnelManagerMock.Setup(x => x.ExportTunnelAsync("tunnel1", It.IsAny<CancellationToken>()))
            .ReturnsAsync("[Interface]\nPrivateKey = test");

        var request = new IpcRequest { Command = IpcCommand.ExportTunnel, TunnelName = "tunnel1", RequestId = "r13" };
        var response = await _handler.HandleAsync(request, "admin", CancellationToken.None);

        Assert.True(response.Success);
        var b64 = response.GetData<string>();
        Assert.NotNull(b64);
        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(b64));
        Assert.Contains("[Interface]", decoded);
    }

    [Fact]
    public async Task ExportTunnel_AsAdvancedOperator_Denied()
    {
        _roleStoreMock.Setup(x => x.GetRoleAsync("advop", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.AdvancedOperator);

        var request = new IpcRequest { Command = IpcCommand.ExportTunnel, TunnelName = "tunnel1", RequestId = "r14" };
        var response = await _handler.HandleAsync(request, "advop", CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Access denied", response.Error);
    }

    // --- Phase 4: SetTunnelAutoStart ---

    [Fact]
    public async Task SetTunnelAutoStart_AsAdvancedOperator_ReturnsSuccess()
    {
        _roleStoreMock.Setup(x => x.GetRoleAsync("advop", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.AdvancedOperator);
        _tunnelManagerMock.Setup(x => x.SetTunnelAutoStartAsync("tunnel1", true, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new IpcRequest
        {
            Command = IpcCommand.SetTunnelAutoStart,
            TunnelName = "tunnel1",
            AutoStart = true,
            RequestId = "r15",
        };
        var response = await _handler.HandleAsync(request, "advop", CancellationToken.None);

        Assert.True(response.Success);
        _tunnelManagerMock.Verify(x => x.SetTunnelAutoStartAsync("tunnel1", true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetTunnelAutoStart_AsOperator_Denied()
    {
        _roleStoreMock.Setup(x => x.GetRoleAsync("operator", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.Operator);

        var request = new IpcRequest
        {
            Command = IpcCommand.SetTunnelAutoStart,
            TunnelName = "tunnel1",
            AutoStart = true,
            RequestId = "r16",
        };
        var response = await _handler.HandleAsync(request, "operator", CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal("Access denied", response.Error);
    }

    [Fact]
    public async Task SetTunnelAutoStart_MissingAutoStart_ReturnsFail()
    {
        _roleStoreMock.Setup(x => x.GetRoleAsync("advop", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.AdvancedOperator);

        var request = new IpcRequest
        {
            Command = IpcCommand.SetTunnelAutoStart,
            TunnelName = "tunnel1",
            RequestId = "r17",
        };
        var response = await _handler.HandleAsync(request, "advop", CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("AutoStart is required", response.Error);
    }

    [Fact]
    public async Task SetTunnelAutoStart_MissingTunnelName_ReturnsFail()
    {
        _roleStoreMock.Setup(x => x.GetRoleAsync("advop", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UserRole.AdvancedOperator);

        var request = new IpcRequest
        {
            Command = IpcCommand.SetTunnelAutoStart,
            AutoStart = true,
            RequestId = "r18",
        };
        var response = await _handler.HandleAsync(request, "advop", CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("TunnelName is required", response.Error);
    }
}
