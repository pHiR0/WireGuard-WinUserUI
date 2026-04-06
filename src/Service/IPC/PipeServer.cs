using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using WireGuard.Shared.IPC;
using WindowsIdentity = System.Security.Principal.WindowsIdentity;

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

    // --- Rate limiting ---
    // Max concurrent connections per user SID. Prevents a single user from
    // exhausting server capacity (pipe-based denial-of-service).
    private const int MaxConnectionsPerUser = 4;
    private readonly ConcurrentDictionary<string, int> _activeConnectionsBySid = new();

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

        // DENY network access explicitly — defence-in-depth against remote pipe connections.
        // Named pipes are local-only by default, but an explicit deny makes the intent clear
        // and guards against any future configuration drift.
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.NetworkSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Deny));

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

    /// <summary>
    /// Identifies the calling user via GetImpersonationUserName + NTAccount→SID translation.
    /// Returns the SID string and the normalised username, or null if the caller could not
    /// be identified.
    /// </summary>
    private (string Sid, string Username)? IdentifyCaller(NamedPipeServerStream pipe)
    {
        try
        {
            // GetImpersonationUserName returns DOMAIN\Username after the first read.
            var rawName = pipe.GetImpersonationUserName();
            if (string.IsNullOrEmpty(rawName))
            {
                _logger.LogWarning("SECURITY: GetImpersonationUserName returned empty for connected client");
                return null;
            }

            // Translate the account name to a SecurityIdentifier (SID) for robust identity.
            // NTAccount.Translate is in System.Security.Principal.Windows — no extra deps needed.
            var account = new NTAccount(rawName);
            var sid = (SecurityIdentifier)account.Translate(typeof(SecurityIdentifier));

            // Normalise: strip DOMAIN\ prefix, lowercase
            var backslash = rawName.IndexOf('\\');
            var username = backslash >= 0 ? rawName[(backslash + 1)..] : rawName;
            return (sid.Value, username.ToLowerInvariant());
        }
        catch (IdentityNotMappedException ex)
        {
            _logger.LogWarning(ex, "SECURITY: Could not resolve SID for connected pipe client");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SECURITY: Failed to identify connected client");
            return null;
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        string? callingUser = null;
        string? callingSid = null;
        try
        {
            // Read the first message — Windows needs at least one read before
            // GetImpersonationUserName / RunAsClient work reliably.
            var firstData = await PipeMessageIO.ReadMessageAsync(pipe, ct);
            if (firstData is null) return; // client disconnected immediately

            // --- SID-level caller identification ---
            var callerInfo = IdentifyCaller(pipe);
            if (callerInfo is null)
            {
                _logger.LogWarning("SECURITY: Rejecting unidentified pipe client");
                var deny = IpcResponse.Fail("Caller identity could not be verified");
                await PipeMessageIO.WriteMessageAsync(pipe, deny.Serialize(), ct);
                return;
            }

            callingSid = callerInfo.Value.Sid;
            callingUser = callerInfo.Value.Username;

            // --- Per-user connection rate limiting ---
            var currentCount = _activeConnectionsBySid.AddOrUpdate(callingSid, 1, (_, c) => c + 1);
            if (currentCount > MaxConnectionsPerUser)
            {
                _activeConnectionsBySid.AddOrUpdate(callingSid, 0, (_, c) => Math.Max(0, c - 1));
                _logger.LogWarning(
                    "SECURITY: Rate limit exceeded for user '{User}' (SID {Sid}): {Count} active connections",
                    callingUser, callingSid, currentCount);
                var deny = IpcResponse.Fail("Too many concurrent connections");
                await PipeMessageIO.WriteMessageAsync(pipe, deny.Serialize(), ct);
                return;
            }

            _logger.LogInformation("Pipe client connected: '{User}' (SID {Sid})", callingUser, callingSid);

            // Capture the caller's token group SIDs via impersonation.
            // Token groups are resolved by the OS at logon — no DC query at runtime.
            // This enables the fast role-check path in WindowsGroupRoleStore.
            HashSet<string>? tokenGroupSids = null;
            try
            {
                pipe.RunAsClient(() =>
                {
                    using var identity = WindowsIdentity.GetCurrent();
                    if (identity.Groups is { } groups)
                    {
                        tokenGroupSids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (IdentityReference g in groups)
                        {
                            if (g is SecurityIdentifier sid)
                                tokenGroupSids.Add(sid.Value);
                        }
                    }
                });
                _logger.LogDebug("Captured {Count} token group SID(s) for '{User}'", tokenGroupSids?.Count ?? 0, callingUser);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "RunAsClient unavailable for '{User}' — role check will use slow path", callingUser);
            }

            try
            {
                // Process the first message we already read
                await ProcessMessageAsync(pipe, firstData, callingUser, callingSid, tokenGroupSids, ct);

                // Process subsequent messages
                while (pipe.IsConnected && !ct.IsCancellationRequested)
                {
                    var data = await PipeMessageIO.ReadMessageAsync(pipe, ct);
                    if (data is null) break;

                    await ProcessMessageAsync(pipe, data, callingUser, callingSid, tokenGroupSids, ct);
                }
            }
            finally
            {
                _activeConnectionsBySid.AddOrUpdate(callingSid, 0, (_, c) => Math.Max(0, c - 1));
            }

            _logger.LogInformation("Pipe client disconnected: '{User}' (SID {Sid})", callingUser, callingSid);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling pipe client from '{User}' (SID {Sid})",
                callingUser ?? "unknown", callingSid ?? "unknown");
        }
        finally
        {
            await pipe.DisposeAsync();
        }
    }

    private async Task ProcessMessageAsync(
        NamedPipeServerStream pipe, byte[] data, string callingUser, string? callingSid, IReadOnlySet<string>? tokenGroupSids, CancellationToken ct)
    {
        _logger.LogDebug("Request from '{User}'", callingUser);

        var request = IpcRequest.Deserialize(data);
        if (request is null)
        {
            _logger.LogWarning("Received invalid request from '{User}'", callingUser);
            var errorResp = IpcResponse.Fail("Invalid request format");
            await PipeMessageIO.WriteMessageAsync(pipe, errorResp.Serialize(), ct);
            return;
        }

        var response = await _requestHandler.HandleAsync(request, callingUser, callingSid, tokenGroupSids, ct);
        await PipeMessageIO.WriteMessageAsync(pipe, response.Serialize(), ct);
    }
}
