using WireGuard.Shared.IPC;
using WireGuard.Shared.Models;

namespace WireGuard.Service.Tests;

public class IpcSerializationTests
{
    [Fact]
    public void IpcRequest_RoundTrip()
    {
        var request = new IpcRequest
        {
            Command = IpcCommand.StartTunnel,
            TunnelName = "myTunnel",
            RequestId = "req-123",
        };

        var bytes = request.Serialize();
        var deserialized = IpcRequest.Deserialize(bytes);

        Assert.NotNull(deserialized);
        Assert.Equal(IpcCommand.StartTunnel, deserialized.Command);
        Assert.Equal("myTunnel", deserialized.TunnelName);
        Assert.Equal("req-123", deserialized.RequestId);
    }

    [Fact]
    public void IpcResponse_Ok_RoundTrip()
    {
        var tunnels = new List<TunnelInfo>
        {
            new() { Name = "tunnel1", Status = TunnelStatus.Running, LastChecked = DateTimeOffset.UtcNow },
            new() { Name = "tunnel2", Status = TunnelStatus.Stopped, LastChecked = DateTimeOffset.UtcNow },
        };

        var response = IpcResponse.Ok(tunnels, "resp-456");
        var bytes = response.Serialize();
        var deserialized = IpcResponse.Deserialize(bytes);

        Assert.NotNull(deserialized);
        Assert.True(deserialized.Success);
        Assert.Null(deserialized.Error);
        Assert.Equal("resp-456", deserialized.RequestId);

        var data = deserialized.GetData<List<TunnelInfo>>();
        Assert.NotNull(data);
        Assert.Equal(2, data.Count);
        Assert.Equal("tunnel1", data[0].Name);
        Assert.Equal(TunnelStatus.Running, data[0].Status);
    }

    [Fact]
    public void IpcResponse_Fail_RoundTrip()
    {
        var response = IpcResponse.Fail("Something went wrong", "resp-789");
        var bytes = response.Serialize();
        var deserialized = IpcResponse.Deserialize(bytes);

        Assert.NotNull(deserialized);
        Assert.False(deserialized.Success);
        Assert.Equal("Something went wrong", deserialized.Error);
        Assert.Equal("resp-789", deserialized.RequestId);
    }

    [Fact]
    public void IpcResponse_GetData_WithUserInfo()
    {
        var user = new UserInfo
        {
            Username = "testuser",
            Role = UserRole.Operator,
        };

        var response = IpcResponse.Ok(user);
        var bytes = response.Serialize();
        var deserialized = IpcResponse.Deserialize(bytes);

        var data = deserialized?.GetData<UserInfo>();
        Assert.NotNull(data);
        Assert.Equal("testuser", data.Username);
        Assert.Equal(UserRole.Operator, data.Role);
    }

    // --- Phase 2 serialization tests ---

    [Fact]
    public void IpcRequest_WithConfContent_RoundTrip()
    {
        var request = new IpcRequest
        {
            Command = IpcCommand.ImportTunnel,
            TunnelName = "vpn1",
            ConfContent = Convert.ToBase64String("test-content"u8.ToArray()),
            RequestId = "req-import",
        };

        var bytes = request.Serialize();
        var deserialized = IpcRequest.Deserialize(bytes);

        Assert.NotNull(deserialized);
        Assert.Equal(IpcCommand.ImportTunnel, deserialized.Command);
        Assert.Equal("vpn1", deserialized.TunnelName);
        Assert.NotNull(deserialized.ConfContent);
        Assert.Equal("test-content", System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(deserialized.ConfContent)));
    }

    [Fact]
    public void IpcRequest_WithUserRole_RoundTrip()
    {
        var request = new IpcRequest
        {
            Command = IpcCommand.SetUserRole,
            Username = "newuser",
            Role = UserRole.AdvancedOperator,
            RequestId = "req-role",
        };

        var bytes = request.Serialize();
        var deserialized = IpcRequest.Deserialize(bytes);

        Assert.NotNull(deserialized);
        Assert.Equal(IpcCommand.SetUserRole, deserialized.Command);
        Assert.Equal("newuser", deserialized.Username);
        Assert.Equal(UserRole.AdvancedOperator, deserialized.Role);
    }

    [Fact]
    public void IpcRequest_WithAuditQuery_RoundTrip()
    {
        var request = new IpcRequest
        {
            Command = IpcCommand.GetAuditLog,
            AuditQuery = new AuditQuery
            {
                Username = "admin",
                Action = "StartTunnel",
                Page = 2,
                PageSize = 25,
            },
            RequestId = "req-audit",
        };

        var bytes = request.Serialize();
        var deserialized = IpcRequest.Deserialize(bytes);

        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized.AuditQuery);
        Assert.Equal("admin", deserialized.AuditQuery.Username);
        Assert.Equal("StartTunnel", deserialized.AuditQuery.Action);
        Assert.Equal(2, deserialized.AuditQuery.Page);
        Assert.Equal(25, deserialized.AuditQuery.PageSize);
    }

    [Fact]
    public void IpcResponse_AuditPage_RoundTrip()
    {
        var page = new AuditPage
        {
            Entries = new List<AuditEntryDto>
            {
                new() { Username = "admin", Action = "StartTunnel", Tunnel = "vpn1", Result = "Success", Timestamp = DateTimeOffset.UtcNow },
                new() { Username = "viewer", Action = "ListTunnels", Result = "Success", Timestamp = DateTimeOffset.UtcNow },
            },
            TotalCount = 42,
            Page = 1,
            PageSize = 50,
        };

        var response = IpcResponse.Ok(page, "audit-resp");
        var bytes = response.Serialize();
        var deserialized = IpcResponse.Deserialize(bytes);

        Assert.NotNull(deserialized);
        var data = deserialized.GetData<AuditPage>();
        Assert.NotNull(data);
        Assert.Equal(42, data.TotalCount);
        Assert.Equal(2, data.Entries.Count);
        Assert.Equal("admin", data.Entries[0].Username);
    }
}
