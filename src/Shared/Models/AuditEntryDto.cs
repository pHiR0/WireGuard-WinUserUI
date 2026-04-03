namespace WireGuard.Shared.Models;

/// <summary>
/// Audit log entry returned to clients via IPC.
/// </summary>
public sealed class AuditEntryDto
{
    public DateTimeOffset Timestamp { get; init; }
    public required string Username { get; init; }
    public required string Action { get; init; }
    public string? Tunnel { get; init; }
    public required string Result { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Paginated audit log response.
/// </summary>
public sealed class AuditPage
{
    public required IReadOnlyList<AuditEntryDto> Entries { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
