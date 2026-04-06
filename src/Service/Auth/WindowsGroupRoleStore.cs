using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WireGuard.Shared.Models;

namespace WireGuard.Service.Auth;

/// <summary>
/// Determines user roles by checking Windows local group membership.
///
/// Role mapping (highest wins):
///   Builtin\Administrators (S-1-5-32-544)  → Admin
///   "Wireguard UI - Administrator"           → Admin
///   "Wireguard UI - Operador avanzado"       → AdvancedOperator
///   "Wireguard UI - Operador"                → Operator
///   "Wireguard UI - Visualizador"            → Viewer
///   (none of the above)                      → None
/// </summary>
public sealed class WindowsGroupRoleStore : IFastRoleStore
{
    internal const string GroupAdministrator      = "Wireguard UI - Administrator";
    internal const string GroupAdvancedOperator   = "Wireguard UI - Operador avanzado";
    internal const string GroupOperator           = "Wireguard UI - Operador";
    internal const string GroupViewer             = "Wireguard UI - Visualizador";
    private  const string BuiltinAdminSid         = "S-1-5-32-544";

    private readonly ILogger<WindowsGroupRoleStore> _logger;

    /// <summary>
    /// Lazy map of local group SID → UserRole, resolved once at startup.
    /// Used by the fast token-based path so no DC enumeration is needed at runtime.
    /// </summary>
    private readonly Lazy<IReadOnlyDictionary<string, UserRole>> _groupSidToRole;

    public WindowsGroupRoleStore(ILogger<WindowsGroupRoleStore> logger)
    {
        _logger = logger;
        _groupSidToRole = new Lazy<IReadOnlyDictionary<string, UserRole>>(
            ResolveGroupSids, LazyThreadSafetyMode.PublicationOnly);
        // Pre-warm: resolve group SIDs in the background so the first IPC call is fast.
        _ = Task.Run(() => { _ = _groupSidToRole.Value; });
    }

    public Task<UserRole> GetRoleAsync(string username, string? userSid = null, CancellationToken ct = default)
    {
        var role = ResolveRole(username, userSid);
        return Task.FromResult(role);
    }

    public Task<UserRole> GetRoleWithTokenAsync(
        string username, string? userSid, IReadOnlySet<string> tokenGroupSids, CancellationToken ct = default)
    {
        // Fast path: check the caller's token groups against pre-resolved local group SIDs.
        // Token groups are computed by the OS at logon — no DC query at runtime.
        var role = ResolveRoleFromToken(tokenGroupSids);

        if (role == UserRole.None)
        {
            // The user's token doesn't contain any WireGuard group SID. This can happen when
            // the user was added to a local group AFTER their current logon session started
            // (token groups are frozen at logon time). Fall back to the slow path which queries
            // actual group membership in real-time.
            _logger.LogDebug("User '{User}' not found via token groups — falling back to group enumeration", username);
            role = ResolveRole(username, userSid);
        }
        else
        {
            _logger.LogDebug("User '{User}' resolved to role {Role} via token groups (fast path)", username, role);
        }

        return Task.FromResult(role);
    }

    // ──────────────────────────────────────────
    // Fast token-based path
    // ──────────────────────────────────────────

    /// <summary>
    /// Resolves the WireGuard group SIDs (and Builtin\Administrators) once at startup.
    /// These are stable for the lifetime of the service — groups don't change SID.
    /// </summary>
    private IReadOnlyDictionary<string, UserRole> ResolveGroupSids()
    {
        var result = new Dictionary<string, UserRole>(StringComparer.OrdinalIgnoreCase);
        // Builtin\Administrators has a well-known SID — no lookup needed.
        result[BuiltinAdminSid] = UserRole.Admin;
        try
        {
            using var ctx = new PrincipalContext(ContextType.Machine);
            foreach (var (groupName, role) in GroupRoleMap())
            {
                try
                {
                    using var group = GroupPrincipal.FindByIdentity(ctx, groupName);
                    if (group?.Sid?.Value is { } sid)
                        result[sid] = role;
                }
                catch { /* group doesn't exist on this machine — skip */ }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to pre-resolve WireGuard group SIDs — fast path disabled");
        }
        _logger.LogDebug("Resolved {Count} group SID(s) for token-based role check", result.Count);
        return result;
    }

    /// <summary>
    /// Checks the caller's token group SIDs against the pre-resolved group map.
    /// O(n) over token groups (typically 20-50 SIDs) — no network, no disk.
    /// </summary>
    private UserRole ResolveRoleFromToken(IReadOnlySet<string> tokenGroupSids)
    {
        var groupMap = _groupSidToRole.Value;
        var highest = UserRole.None;
        foreach (var sid in tokenGroupSids)
        {
            if (groupMap.TryGetValue(sid, out var role) && role > highest)
                highest = role;
        }
        return highest;
    }

    private UserRole ResolveRole(string username, string? userSid)
    {
        // Strip domain prefix for local lookup: "DESKTOP-XXX\john" → "john"
        var localName = username.Contains('\\') ? username.Split('\\', 2)[1] : username;

        try
        {
            using var ctx = new PrincipalContext(ContextType.Machine);

            // 1. Builtin\Administrators → Admin
            //    Try both username-based and SID-based lookups.
            if (IsInBuiltinAdmins(ctx, localName, userSid))
            {
                _logger.LogDebug("User '{User}' is a local admin → Admin role", username);
                return UserRole.Admin;
            }

            // 2-5. WireGuard UI groups — try username first, fall back to SID enumeration
            //      (SID-based is required for domain accounts added to local groups).
            foreach (var (groupName, role) in GroupRoleMap())
            {
                if (IsMemberOfLocalGroup(ctx, localName, userSid, groupName))
                    return role;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check group membership for user '{User}' (SID: {Sid})", username, userSid ?? "—");
        }

        return UserRole.None;
    }

    // ──────────────────────────────────────────
    // Group membership helpers
    // ──────────────────────────────────────────

    /// <summary>
    /// Checks Builtin\Administrators membership. Tries username lookup first; if the user
    /// cannot be found in the local SAM (domain accounts), falls back to SID enumeration.
    /// </summary>
    private static bool IsInBuiltinAdmins(PrincipalContext ctx, string localName, string? userSid)
    {
        try
        {
            using var group = GroupPrincipal.FindByIdentity(ctx, IdentityType.Sid, BuiltinAdminSid);
            if (group is null) return false;
            return IsMemberBySidOrName(group, localName, userSid);
        }
        catch { return false; }
    }

    /// <summary>
    /// Checks membership of a local group by group name.
    /// First tries a fast UserPrincipal lookup (works for local accounts).
    /// If that fails (e.g. domain user not in local SAM), enumerates group members
    /// and compares SIDs — which works for domain accounts added to local groups.
    /// </summary>
    private static bool IsMemberOfLocalGroup(PrincipalContext ctx, string localName, string? userSid, string groupName)
    {
        try
        {
            using var group = GroupPrincipal.FindByIdentity(ctx, groupName);
            if (group is null) return false;
            return IsMemberBySidOrName(group, localName, userSid);
        }
        catch { return false; }
    }

    /// <summary>
    /// Tries to determine membership using UserPrincipal.IsMemberOf first (fast path for
    /// local accounts). Falls back to Win32 NetLocalGroupGetMembers which reads raw SIDs
    /// directly from the local SAM without requiring domain controller access — the same
    /// API used by Get-LocalGroupMember. This reliably handles domain users in local groups.
    /// </summary>
    private static bool IsMemberBySidOrName(GroupPrincipal group, string localName, string? userSid)
    {
        // 1. Win32 direct-SID read from local SAM (no DC needed, same as Get-LocalGroupMember).
        //    Most reliable method for domain users. If it succeeds its result is authoritative.
        if (!string.IsNullOrEmpty(userSid) && group.Name is not null)
        {
            bool? win32Result = null;
            try { win32Result = IsDirectMemberBySidWin32(group.Name, userSid); }
            catch { /* P/Invoke unavailable — fall through */ }
            if (win32Result.HasValue) return win32Result.Value;
        }

        // 2. DirectoryServices: find local SAM user by name + IsMemberOf (fast for local accounts).
        try
        {
            using var user = UserPrincipal.FindByIdentity(group.Context, IdentityType.SamAccountName, localName);
            if (user is not null)
                return user.IsMemberOf(group);
        }
        catch { /* local resolution failed — fall through */ }

        // 3. DirectoryServices member enumeration (last resort; may need DC for recursive).
        if (string.IsNullOrEmpty(userSid)) return false;
        try
        {
            using var members = group.GetMembers(recursive: false);
            return members.Any(m => string.Equals(m.Sid?.Value, userSid, StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }

    // ──────────────────────────────────────────
    // Win32 helpers — NetLocalGroupGetMembers
    // ──────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct LOCALGROUP_MEMBERS_INFO_0 { public IntPtr lgrmi0_sid; }

    [DllImport("Netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int NetLocalGroupGetMembers(
        string? serverName, string localGroupName, int level,
        out IntPtr bufPtr, int prefMaxLen, out int entriesRead,
        out int totalEntries, IntPtr resumeHandle);

    [DllImport("Netapi32.dll")]
    private static extern int NetApiBufferFree(IntPtr buffer);

    /// <summary>
    /// Checks whether a user (identified by SID string) is a direct member of a local group.
    /// Uses NetLocalGroupGetMembers Win32 API — reads raw SIDs from the local SAM with no
    /// domain controller access required. This is the same API used by Get-LocalGroupMember.
    /// </summary>
    private static bool IsDirectMemberBySidWin32(string groupName, string userSid)
    {
        const int NERR_Success = 0;
        var rc = NetLocalGroupGetMembers(null, groupName, 0, out var buf, -1,
                                         out var entriesRead, out _, IntPtr.Zero);
        if (rc != NERR_Success || buf == IntPtr.Zero) return false;
        try
        {
            var size = Marshal.SizeOf<LOCALGROUP_MEMBERS_INFO_0>();
            for (var i = 0; i < entriesRead; i++)
            {
                var entry = Marshal.PtrToStructure<LOCALGROUP_MEMBERS_INFO_0>(IntPtr.Add(buf, i * size));
                if (entry.lgrmi0_sid == IntPtr.Zero) continue;
                try
                {
                    var sid = new SecurityIdentifier(entry.lgrmi0_sid);
                    if (string.Equals(sid.Value, userSid, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch { /* unresolvable SID — skip */ }
            }
            return false;
        }
        finally
        {
            NetApiBufferFree(buf);
        }
    }

    // ──────────────────────────────────────────
    // Management operations (add/remove via Windows groups)
    // ──────────────────────────────────────────

    public Task SetRoleAsync(string username, UserRole role, CancellationToken ct = default)
    {
        var localName = username.Contains('\\') ? username.Split('\\', 2)[1] : username;
        try
        {
            using var ctx  = new PrincipalContext(ContextType.Machine);
            using var user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, localName)
                             ?? throw new InvalidOperationException($"Usuario '{localName}' no encontrado.");

            // Remove from all WireGuard groups first
            RemoveFromAllGroups(ctx, user);

            // Add to the target group (None means no group — already removed)
            var targetGroup = role switch
            {
                UserRole.Admin            => GroupAdministrator,
                UserRole.AdvancedOperator => GroupAdvancedOperator,
                UserRole.Operator         => GroupOperator,
                UserRole.Viewer           => GroupViewer,
                _                         => null,
            };

            if (targetGroup is not null)
            {
                using var group = GroupPrincipal.FindByIdentity(ctx, targetGroup);
                if (group is not null)
                {
                    group.Members.Add(user);
                    group.Save();
                }
            }

            _logger.LogInformation("Set role {Role} for user '{User}' via Windows group", role, username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set role for user '{User}'", username);
            throw;
        }
        return Task.CompletedTask;
    }

    public Task RemoveUserAsync(string username, CancellationToken ct = default)
    {
        var localName = username.Contains('\\') ? username.Split('\\', 2)[1] : username;
        try
        {
            using var ctx  = new PrincipalContext(ContextType.Machine);
            using var user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, localName);
            if (user is not null)
                RemoveFromAllGroups(ctx, user);

            _logger.LogInformation("Removed user '{User}' from all WireGuard groups", username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove user '{User}'", username);
            throw;
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<UserInfo>> ListUsersAsync(CancellationToken ct = default)
    {
        var result = new List<UserInfo>();
        try
        {
            using var ctx = new PrincipalContext(ContextType.Machine);
            foreach (var (groupName, role) in GroupRoleMap())
            {
                using var group = GroupPrincipal.FindByIdentity(ctx, groupName);
                if (group is null) continue;
                foreach (var member in group.GetMembers(recursive: false))
                {
                    if (member is UserPrincipal up)
                        result.Add(new UserInfo { Username = up.SamAccountName ?? up.Name ?? "?", Role = role });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list WireGuard group members");
        }
        return Task.FromResult<IReadOnlyList<UserInfo>>(result);
    }

    private static void RemoveFromAllGroups(PrincipalContext ctx, UserPrincipal user)
    {
        foreach (var (groupName, _) in GroupRoleMap())
        {
            try
            {
                using var group = GroupPrincipal.FindByIdentity(ctx, groupName);
                if (group is null) continue;
                if (group.Members.Contains(user))
                {
                    group.Members.Remove(user);
                    group.Save();
                }
            }
            catch { /* group may not exist yet */ }
        }
    }

    private static IEnumerable<(string GroupName, UserRole Role)> GroupRoleMap() =>
    [
        (GroupAdministrator,    UserRole.Admin),
        (GroupAdvancedOperator, UserRole.AdvancedOperator),
        (GroupOperator,         UserRole.Operator),
        (GroupViewer,           UserRole.Viewer),
    ];
}
