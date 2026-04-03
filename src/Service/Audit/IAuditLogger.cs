using WireGuard.Shared.IPC;
using WireGuard.Shared.Models;

namespace WireGuard.Service.Audit;

public interface IAuditLogger
{
    Task LogAsync(AuditEntry entry, CancellationToken ct = default);
    Task<AuditPage> QueryAsync(AuditQuery query, CancellationToken ct = default);
}

public sealed class AuditEntry
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public required string Username { get; init; }
    public required string Action { get; init; }
    public string? Tunnel { get; init; }
    public required string Result { get; init; }
    public string? Error { get; init; }
}
