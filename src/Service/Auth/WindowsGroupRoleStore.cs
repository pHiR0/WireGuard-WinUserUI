using System;
using System.Collections.Generic;
using System.DirectoryServices.AccountManagement;
using System.Linq;
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
public sealed class WindowsGroupRoleStore : IRoleStore
{
    internal const string GroupAdministrator      = "Wireguard UI - Administrator";
    internal const string GroupAdvancedOperator   = "Wireguard UI - Operador avanzado";
    internal const string GroupOperator           = "Wireguard UI - Operador";
    internal const string GroupViewer             = "Wireguard UI - Visualizador";
    private  const string BuiltinAdminSid         = "S-1-5-32-544";

    private readonly ILogger<WindowsGroupRoleStore> _logger;

    public WindowsGroupRoleStore(ILogger<WindowsGroupRoleStore> logger)
    {
        _logger = logger;
    }

    public Task<UserRole> GetRoleAsync(string username, string? userSid = null, CancellationToken ct = default)
    {
        var role = ResolveRole(username, userSid);
        return Task.FromResult(role);
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
    /// local accounts). Falls back to enumerating members and comparing SIDs (required for
    /// domain accounts, UPN accounts, and other non-local users added to local groups).
    /// </summary>
    private static bool IsMemberBySidOrName(GroupPrincipal group, string localName, string? userSid)
    {
        // Fast path: try to find by local SAM name and use IsMemberOf
        try
        {
            using var user = UserPrincipal.FindByIdentity(group.Context, IdentityType.SamAccountName, localName);
            if (user is not null)
                return user.IsMemberOf(group);
        }
        catch { /* local resolution failed — fall through to SID-based check */ }

        // Fallback: SID-based enumeration (works for domain accounts)
        if (string.IsNullOrEmpty(userSid)) return false;
        try
        {
            using var members = group.GetMembers(recursive: true);
            return members.Any(m => string.Equals(m.Sid?.Value, userSid, StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
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
