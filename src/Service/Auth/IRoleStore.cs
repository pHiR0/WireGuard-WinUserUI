using WireGuard.Shared.Models;

namespace WireGuard.Service.Auth;

public interface IRoleStore
{
    /// <param name="userSid">
    /// Optional Windows SID string (e.g. S-1-5-21-...). Used as fallback for domain
    /// accounts that cannot be found by <paramref name="username"/> in the local SAM.
    /// </param>
    Task<UserRole> GetRoleAsync(string username, string? userSid = null, CancellationToken ct = default);
    Task SetRoleAsync(string username, UserRole role, CancellationToken ct = default);
    Task RemoveUserAsync(string username, CancellationToken ct = default);
    Task<IReadOnlyList<UserInfo>> ListUsersAsync(CancellationToken ct = default);
}

/// <summary>
/// Optional extension of <see cref="IRoleStore"/> that exposes a fast token-based
/// role resolution path. When the caller's Windows token groups are available (via
/// pipe impersonation), role lookup is O(n) over token SIDs — no DC query required.
/// </summary>
public interface IFastRoleStore : IRoleStore
{
    /// <param name="tokenGroupSids">
    /// Group SID strings from the caller's Windows impersonation token.
    /// Resolved by the OS at user logon — no DC query at runtime.
    /// </param>
    Task<UserRole> GetRoleWithTokenAsync(
        string username, string? userSid, IReadOnlySet<string> tokenGroupSids, CancellationToken ct = default);
}
