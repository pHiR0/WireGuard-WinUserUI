using System.Text.Json;
using Microsoft.Extensions.Logging;
using WireGuard.Shared.IPC;
using WireGuard.Shared.Models;

namespace WireGuard.Service.Audit;

/// <summary>
/// Appends audit entries as JSON Lines to a log file in %ProgramData%.
/// Supports querying with filters and pagination.
/// </summary>
public sealed class JsonAuditLogger : IAuditLogger
{
    private readonly string _filePath;
    private readonly ILogger<JsonAuditLogger> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public JsonAuditLogger(ILogger<JsonAuditLogger> logger)
    {
        _logger = logger;
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "WireGuard-WinUserUI");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "audit.jsonl");
    }

    public async Task LogAsync(AuditEntry entry, CancellationToken ct = default)
    {
        var line = JsonSerializer.Serialize(entry, JsonOptions);

        await _lock.WaitAsync(ct);
        try
        {
            await File.AppendAllTextAsync(_filePath, line + Environment.NewLine, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log entry");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<AuditPage> QueryAsync(AuditQuery query, CancellationToken ct = default)
    {
        if (!File.Exists(_filePath))
            return new AuditPage { Entries = [], TotalCount = 0, Page = query.Page, PageSize = query.PageSize };

        await _lock.WaitAsync(ct);
        try
        {
            var lines = await File.ReadAllLinesAsync(_filePath, ct);
            var entries = new List<AuditEntryDto>();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var entry = JsonSerializer.Deserialize<AuditEntryDto>(line, JsonOptions);
                    if (entry is null) continue;
                    entries.Add(entry);
                }
                catch
                {
                    // Skip malformed lines
                }
            }

            // Apply filters
            IEnumerable<AuditEntryDto> filtered = entries;

            if (!string.IsNullOrEmpty(query.Username))
                filtered = filtered.Where(e => e.Username.Equals(query.Username, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(query.Action))
                filtered = filtered.Where(e => e.Action.Equals(query.Action, StringComparison.OrdinalIgnoreCase));
            if (query.From.HasValue)
                filtered = filtered.Where(e => e.Timestamp >= query.From.Value);
            if (query.To.HasValue)
                filtered = filtered.Where(e => e.Timestamp <= query.To.Value);

            // Order newest first
            var ordered = filtered.OrderByDescending(e => e.Timestamp).ToList();
            var totalCount = ordered.Count;

            // Paginate
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 200);
            var paged = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return new AuditPage
            {
                Entries = paged,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
            };
        }
        finally
        {
            _lock.Release();
        }
    }
}
