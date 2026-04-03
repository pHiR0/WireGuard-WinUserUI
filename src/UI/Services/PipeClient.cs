using System.IO.Pipes;
using System.Security.Principal;
using WireGuard.Shared.IPC;
using WireGuard.Shared.Models;

namespace WireGuard.UI.Services;

public sealed class PipeClient : IPipeClient
{
    private NamedPipeClientStream? _pipe;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private bool _disposed;

    public bool IsConnected => _pipe?.IsConnected == true;

    public event Action? Disconnected;
    public event Action? Reconnected;

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (IsConnected) return;

        _pipe?.Dispose();
        _pipe = new NamedPipeClientStream(
            ".",
            PipeConstants.PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous,
            TokenImpersonationLevel.Identification);

        await _pipe.ConnectAsync(5000, ct);
    }

    public async Task<IpcResponse> SendAsync(IpcRequest request, CancellationToken ct = default)
    {
        // Try to send; if the pipe is broken, attempt reconnect once
        await _sendLock.WaitAsync(ct);
        try
        {
            return await SendCoreAsync(request, ct);
        }
        catch (Exception) when (!_disposed)
        {
            // Pipe broken — try reconnect
            try
            {
                Disconnected?.Invoke();
                await ReconnectAsync(ct);
                Reconnected?.Invoke();
                return await SendCoreAsync(request, ct);
            }
            catch
            {
                throw new InvalidOperationException("Connection lost and reconnect failed.");
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task<IpcResponse> SendCoreAsync(IpcRequest request, CancellationToken ct)
    {
        if (_pipe is null || !_pipe.IsConnected)
            throw new InvalidOperationException("Not connected to the service.");

        var payload = request.Serialize();
        await PipeMessageIO.WriteMessageAsync(_pipe, payload, ct);

        var responseData = await PipeMessageIO.ReadMessageAsync(_pipe, ct);
        if (responseData is null)
            throw new InvalidOperationException("Connection lost.");

        return IpcResponse.Deserialize(responseData)
               ?? throw new InvalidOperationException("Invalid response from service.");
    }

    private async Task ReconnectAsync(CancellationToken ct)
    {
        // Exponential backoff: 1s, 2s, 4s (3 attempts)
        int[] backoffs = [1000, 2000, 4000];
        foreach (var delay in backoffs)
        {
            try
            {
                _pipe?.Dispose();
                _pipe = new NamedPipeClientStream(".", PipeConstants.PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await _pipe.ConnectAsync(5000, ct);
                return;
            }
            catch
            {
                await Task.Delay(delay, ct);
            }
        }
        throw new InvalidOperationException("Reconnect failed after 3 attempts.");
    }

    // --- Helper methods to build typed requests ---

    private IpcRequest Req(IpcCommand cmd, string? tunnel = null) => new()
    {
        Command = cmd,
        TunnelName = tunnel,
        RequestId = Guid.NewGuid().ToString("N"),
    };

    private async Task SendExpectOkAsync(IpcRequest request, CancellationToken ct)
    {
        var response = await SendAsync(request, ct);
        if (!response.Success)
            throw new InvalidOperationException(response.Error ?? $"Command {request.Command} failed.");
    }

    // --- Phase 1 ---

    public async Task<IReadOnlyList<TunnelInfo>> ListTunnelsAsync(CancellationToken ct = default)
    {
        var response = await SendAsync(Req(IpcCommand.ListTunnels), ct);
        if (!response.Success)
            throw new InvalidOperationException(response.Error ?? "Failed to list tunnels.");
        return response.GetData<List<TunnelInfo>>() ?? [];
    }

    public async Task<TunnelInfo?> GetTunnelStatusAsync(string name, CancellationToken ct = default)
    {
        var response = await SendAsync(Req(IpcCommand.GetTunnelStatus, name), ct);
        if (!response.Success) return null;
        return response.GetData<TunnelInfo>();
    }

    public async Task StartTunnelAsync(string name, CancellationToken ct = default)
        => await SendExpectOkAsync(Req(IpcCommand.StartTunnel, name), ct);

    public async Task StopTunnelAsync(string name, CancellationToken ct = default)
        => await SendExpectOkAsync(Req(IpcCommand.StopTunnel, name), ct);

    public async Task<UserInfo?> GetCurrentUserAsync(CancellationToken ct = default)
    {
        var response = await SendAsync(Req(IpcCommand.GetCurrentUser), ct);
        if (!response.Success) return null;
        return response.GetData<UserInfo>();
    }

    // --- Phase 2: Tunnel management ---

    public async Task RestartTunnelAsync(string name, CancellationToken ct = default)
        => await SendExpectOkAsync(Req(IpcCommand.RestartTunnel, name), ct);

    public async Task ImportTunnelAsync(string name, string confContent, CancellationToken ct = default)
    {
        var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(confContent));
        var request = new IpcRequest
        {
            Command = IpcCommand.ImportTunnel,
            TunnelName = name,
            ConfContent = b64,
            RequestId = Guid.NewGuid().ToString("N"),
        };
        await SendExpectOkAsync(request, ct);
    }

    public async Task EditTunnelAsync(string name, string confContent, CancellationToken ct = default)
    {
        var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(confContent));
        var request = new IpcRequest
        {
            Command = IpcCommand.EditTunnel,
            TunnelName = name,
            ConfContent = b64,
            RequestId = Guid.NewGuid().ToString("N"),
        };
        await SendExpectOkAsync(request, ct);
    }

    public async Task DeleteTunnelAsync(string name, CancellationToken ct = default)
        => await SendExpectOkAsync(Req(IpcCommand.DeleteTunnel, name), ct);

    public async Task<string?> ExportTunnelAsync(string name, CancellationToken ct = default)
    {
        var response = await SendAsync(Req(IpcCommand.ExportTunnel, name), ct);
        if (!response.Success) return null;
        var b64 = response.GetData<string>();
        if (b64 is null) return null;
        return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(b64));
    }

    // --- Phase 2: User management ---

    public async Task<IReadOnlyList<UserInfo>> ListUsersAsync(CancellationToken ct = default)
    {
        var response = await SendAsync(Req(IpcCommand.ListUsers), ct);
        if (!response.Success)
            throw new InvalidOperationException(response.Error ?? "Failed to list users.");
        return response.GetData<List<UserInfo>>() ?? [];
    }

    public async Task SetUserRoleAsync(string username, UserRole role, CancellationToken ct = default)
    {
        var request = new IpcRequest
        {
            Command = IpcCommand.SetUserRole,
            Username = username,
            Role = role,
            RequestId = Guid.NewGuid().ToString("N"),
        };
        await SendExpectOkAsync(request, ct);
    }

    public async Task RemoveUserAsync(string username, CancellationToken ct = default)
    {
        var request = new IpcRequest
        {
            Command = IpcCommand.RemoveUser,
            Username = username,
            RequestId = Guid.NewGuid().ToString("N"),
        };
        await SendExpectOkAsync(request, ct);
    }

    // --- Phase 2: Audit ---

    public async Task<AuditPage> GetAuditLogAsync(AuditQuery? query = null, CancellationToken ct = default)
    {
        var request = new IpcRequest
        {
            Command = IpcCommand.GetAuditLog,
            AuditQuery = query ?? new AuditQuery(),
            RequestId = Guid.NewGuid().ToString("N"),
        };
        var response = await SendAsync(request, ct);
        if (!response.Success)
            throw new InvalidOperationException(response.Error ?? "Failed to get audit log.");
        return response.GetData<AuditPage>() ?? new AuditPage { Entries = [], TotalCount = 0, Page = 1, PageSize = 50 };
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        if (_pipe is not null)
        {
            await _pipe.DisposeAsync();
            _pipe = null;
        }
        _sendLock.Dispose();
    }
}
