using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WireGuard.Shared.Models;

namespace WireGuard.Service.Auth;

/// <summary>
/// Stores user-role mappings in a JSON file protected with DPAPI (machine scope).
/// Location: %ProgramData%\WireGuard-WinUserUI\roles.json
/// </summary>
public sealed class JsonRoleStore : IRoleStore
{
    private readonly string _filePath;
    private readonly ILogger<JsonRoleStore> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public JsonRoleStore(ILogger<JsonRoleStore> logger)
    {
        _logger = logger;
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "WireGuard-WinUserUI");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "roles.json");
    }

    public async Task<UserRole> GetRoleAsync(string username, string? userSid = null, CancellationToken ct = default)
    {
        var users = await LoadAsync(ct);
        var normalized = username.ToLowerInvariant();
        return users.TryGetValue(normalized, out var role) ? role : UserRole.None;
    }

    public async Task SetRoleAsync(string username, UserRole role, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var users = await LoadAsync(ct);
            var normalized = username.ToLowerInvariant();

            // Prevent downgrading the last Admin
            if (users.TryGetValue(normalized, out var currentRole) && currentRole == UserRole.Admin && role != UserRole.Admin)
            {
                var adminCount = users.Values.Count(r => r == UserRole.Admin);
                if (adminCount <= 1)
                    throw new InvalidOperationException("Cannot downgrade the last Admin user.");
            }

            users[normalized] = role;
            await SaveAsync(users, ct);
            _logger.LogInformation("Set role {Role} for user '{User}'", role, username);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveUserAsync(string username, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var users = await LoadAsync(ct);
            var normalized = username.ToLowerInvariant();

            if (users.TryGetValue(normalized, out var role) && role == UserRole.Admin)
            {
                var adminCount = users.Values.Count(r => r == UserRole.Admin);
                if (adminCount <= 1)
                    throw new InvalidOperationException("Cannot remove the last Admin user.");
            }

            if (users.Remove(normalized))
            {
                await SaveAsync(users, ct);
                _logger.LogInformation("Removed user '{User}'", username);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<UserInfo>> ListUsersAsync(CancellationToken ct = default)
    {
        var users = await LoadAsync(ct);
        return users.Select(kv => new UserInfo
        {
            Username = kv.Key,
            Role = kv.Value,
        }).ToList();
    }

    private async Task<Dictionary<string, UserRole>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath))
            return new Dictionary<string, UserRole>();

        try
        {
            var encrypted = await File.ReadAllBytesAsync(_filePath, ct);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.LocalMachine);
            var json = Encoding.UTF8.GetString(decrypted);
            return JsonSerializer.Deserialize<Dictionary<string, UserRole>>(json, JsonOptions)
                   ?? new Dictionary<string, UserRole>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load role store from {Path}", _filePath);
            return new Dictionary<string, UserRole>();
        }
    }

    private async Task SaveAsync(Dictionary<string, UserRole> users, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(users, JsonOptions);
        var plainBytes = Encoding.UTF8.GetBytes(json);
        var encrypted = ProtectedData.Protect(plainBytes, null, DataProtectionScope.LocalMachine);
        await File.WriteAllBytesAsync(_filePath, encrypted, ct);
    }
}
