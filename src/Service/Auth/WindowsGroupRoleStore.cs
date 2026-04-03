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

    public Task<UserRole> GetRoleAsync(string username, CancellationToken ct = default)
    {
        var role = ResolveRole(username);
        return Task.FromResult(role);
    }

    private UserRole ResolveRole(string username)
    {
        // Strip domain prefix for local lookup: "DESKTOP-XXX\john" → "john"
        var localName = username.Contains('\\') ? username.Split('\\', 2)[1] : username;

        try
        {
            using var ctx = new PrincipalContext(ContextType.Machine);

            // 1. Builtin\Administrators → Admin
            if (IsInGroupBySid(ctx, localName, BuiltinAdminSid))
            {
                _logger.LogDebug("User '{User}' is a local admin → Admin role", username);
                return UserRole.Admin;
            }

            // 2. WireGuard UI - Administrator → Admin
            if (IsInGroup(ctx, localName, GroupAdministrator))
                return UserRole.Admin;

            // 3. WireGuard UI - Operador avanzado → AdvancedOperator
            if (IsInGroup(ctx, localName, GroupAdvancedOperator))
                return UserRole.AdvancedOperator;

            // 4. WireGuard UI - Operador → Operator
            if (IsInGroup(ctx, localName, GroupOperator))
                return UserRole.Operator;

            // 5. WireGuard UI - Visualizador → Viewer
            if (IsInGroup(ctx, localName, GroupViewer))
                return UserRole.Viewer;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check group membership for user '{User}'", username);
        }

        return UserRole.None;
    }

    private static bool IsInGroupBySid(PrincipalContext ctx, string username, string sid)
    {
        try
        {
            using var group = GroupPrincipal.FindByIdentity(ctx, IdentityType.Sid, sid);
            if (group is null) return false;
            using var user  = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, username);
            return user is not null && user.IsMemberOf(group);
        }
        catch { return false; }
    }

    private static bool IsInGroup(PrincipalContext ctx, string username, string groupName)
    {
        try
        {
            using var group = GroupPrincipal.FindByIdentity(ctx, groupName);
            if (group is null) return false;
            using var user  = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, username);
            return user is not null && user.IsMemberOf(group);
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
