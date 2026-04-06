using WireGuard.Shared.Models;

namespace WireGuard.Service.Auth;

public interface IRoleStore
{
    /// <param name="userSid">
    /// Optional Windows SID string (e.g. S-1-5-21-...). When provided and the
    /// account cannot be found by <paramref name="username"/> in the local SAM
    /// (e.g. domain accounts), the role is resolved by enumerating group members
    /// and comparing SIDs — which works for both local and domain accounts.
    /// </param>
    Task<UserRole> GetRoleAsync(string username, string? userSid = null, CancellationToken ct = default);
    Task SetRoleAsync(string username, UserRole role, CancellationToken ct = default);
    Task RemoveUserAsync(string username, CancellationToken ct = default);
    Task<IReadOnlyList<UserInfo>> ListUsersAsync(CancellationToken ct = default);
}
