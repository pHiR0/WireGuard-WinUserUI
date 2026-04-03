using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using WireGuard.Shared.IPC;

namespace WireGuard.Service.IPC;

public sealed class PipeServer
{
    private readonly RequestHandler _requestHandler;
    private readonly ILogger _logger;

    // FILE_FLAG_FIRST_PIPE_INSTANCE (0x00080000): CreateNamedPipe fails with
    // ERROR_ACCESS_DENIED if a pipe instance with the same name already exists.
    // This prevents pipe-squatting attacks where a low-priv process creates the
    // pipe first to intercept connections intended for our privileged service.
    private const int FirstPipeInstanceFlag = 0x00080000;
    private bool _isFirstInstance = true;

    public PipeServer(RequestHandler requestHandler, ILogger logger)
    {
        _requestHandler = requestHandler;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("Named pipe server starting on pipe '{PipeName}'", PipeConstants.PipeName);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var pipe = CreatePipeServer();
                await pipe.WaitForConnectionAsync(ct);

                // Handle client on a separate task, don't await — allows accepting more clients
                _ = HandleClientAsync(pipe, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (UnauthorizedAccessException) when (_isFirstInstance)
            {
                // Another process already owns a pipe with this name — potential squatting.
                _logger.LogCritical(
                    "SECURITY: A pipe named '{PipeName}' already exists and was not created by this service. " +
                    "This may indicate a pipe-squatting attack. Service will not start.",
                    PipeConstants.PipeName);
                throw; // escalate: let the host terminate the service
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting pipe connection");
            }
        }

        _logger.LogInformation("Named pipe server stopped");
    }

    private NamedPipeServerStream CreatePipeServer()
    {
        var pipeSecurity = new PipeSecurity();

        // Allow SYSTEM full control (when running as a Windows Service)
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        // Allow Administrators full control (covers dev scenario: running as elevated admin)
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        // Allow authenticated local users to read/write (so non-admin UI users can connect)
        // Note: Named Pipes are local-only by default; no explicit network deny needed.
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        // On the very first instance, add FILE_FLAG_FIRST_PIPE_INSTANCE so that
        // CreateNamedPipe fails with ACCESS_DENIED if a pipe with this name already
        // exists — this detects and blocks pipe-squatting attacks.
        var options = PipeOptions.Asynchronous | PipeOptions.WriteThrough;
        if (_isFirstInstance)
        {
            options |= (PipeOptions)FirstPipeInstanceFlag;
            _isFirstInstance = false;
        }

        return NamedPipeServerStreamAcl.Create(
            PipeConstants.PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            options,
            inBufferSize: 0,
            outBufferSize: 0,
            pipeSecurity);
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        string? callingUser = null;
        try
        {
            while (pipe.IsConnected && !ct.IsCancellationRequested)
            {
                var data = await PipeMessageIO.ReadMessageAsync(pipe, ct);
                if (data is null) break; // client disconnected

                // Windows requires at least one read before GetImpersonationUserName works.
                // Identify the user on the first message, then reuse for subsequent ones.
                callingUser ??= GetCallingUser(pipe);
                _logger.LogDebug("Request from '{User}'", callingUser);

                var request = IpcRequest.Deserialize(data);
                if (request is null)
                {
                    _logger.LogWarning("Received invalid request from '{User}'", callingUser);
                    var errorResp = IpcResponse.Fail("Invalid request format");
                    await PipeMessageIO.WriteMessageAsync(pipe, errorResp.Serialize(), ct);
                    continue;
                }

                var response = await _requestHandler.HandleAsync(request, callingUser, ct);
                await PipeMessageIO.WriteMessageAsync(pipe, response.Serialize(), ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling pipe client from '{User}'", callingUser ?? "unknown");
        }
        finally
        {
            await pipe.DisposeAsync();
        }
    }

    private static string GetCallingUser(NamedPipeServerStream pipe)
    {
        // GetImpersonationUserName returns DOMAIN\Username
        var rawName = pipe.GetImpersonationUserName();

        // Strip domain prefix if present, normalize to lowercase
        var backslash = rawName.IndexOf('\\');
        var username = backslash >= 0 ? rawName[(backslash + 1)..] : rawName;
        return username.ToLowerInvariant();
    }
}
