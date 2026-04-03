using WireGuard.Shared.Models;

namespace WireGuard.Service.Auth;

public interface IRoleStore
{
    Task<UserRole> GetRoleAsync(string username, CancellationToken ct = default);
    Task SetRoleAsync(string username, UserRole role, CancellationToken ct = default);
    Task RemoveUserAsync(string username, CancellationToken ct = default);
    Task<IReadOnlyList<UserInfo>> ListUsersAsync(CancellationToken ct = default);
}
