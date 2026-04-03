namespace WireGuard.Shared.Models;

public sealed class UserInfo
{
    public required string Username { get; init; }
    public UserRole Role { get; init; }
}
