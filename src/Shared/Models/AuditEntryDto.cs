namespace WireGuard.Shared.Models;

/// <summary>
/// Audit log entry returned to clients via IPC.
/// Grouped entries (deduplication) have Count > 1 and a non-null TimestampEnd.
/// </summary>
public sealed class AuditEntryDto
{
    public DateTimeOffset Timestamp { get; init; }       // first (or only) occurrence
    public DateTimeOffset? TimestampEnd { get; init; }   // last occurrence when grouped
    public int Count { get; init; } = 1;
    public required string Username { get; init; }
    public required string Action { get; init; }
    public string? Tunnel { get; init; }
    public required string Result { get; init; }
    public string? Error { get; init; }

    // UI display helpers
    public bool IsGrouped => Count > 1;
    public string CountText => Count > 1 ? $"×{Count}" : string.Empty;
}

/// <summary>
/// Global audit configuration (Admin only, stored in HKLM registry).
/// </summary>
public sealed class AuditSettingsData
{
    public bool IsEnabled { get; init; } = true;
    public int MaxSizeKb { get; init; }  // 0 = no limit
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
