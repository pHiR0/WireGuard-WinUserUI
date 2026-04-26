using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WireGuard.Service.Auth;
using WireGuard.Shared.IPC;
using WireGuard.Shared.Models;

namespace WireGuard.Service.Audit;

/// <summary>
/// Appends audit entries as JSON Lines to a log file in %ProgramData%.
/// Features:
///   - Consecutive identical events (same user+action+result+tunnel) are grouped
///     in-place: first/last timestamp and a repeat count are stored.
///   - File size is bounded by the AuditMaxSizeKb global setting.
///     When exceeded, the oldest entries are removed to reach 90% of the limit.
///   - Audit logging can be globally disabled via the AuditEnabled global setting.
/// </summary>
public sealed class JsonAuditLogger : IAuditLogger
{
    private readonly GlobalSettingsStore _globalSettings;
    private readonly string _filePath;
    private readonly ILogger<JsonAuditLogger> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // --- Deduplication state (guarded by _lock) ---
    private AuditEntry? _lastEntry;          // last written entry (in-memory)
    private long _lastLineStartOffset = -1;  // byte offset of last line in file
    private bool _initialized;               // true once state has been read from disk

    // --- Size-check throttle ---
    private const int SizeCheckInterval = 10;
    private int _writeCount;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public JsonAuditLogger(GlobalSettingsStore globalSettings, ILogger<JsonAuditLogger> logger)
    {
        _globalSettings = globalSettings;
        _logger = logger;
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "WireGuard-WinUserUI");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "audit.jsonl");
    }

    public async Task LogAsync(AuditEntry entry, CancellationToken ct = default)
    {
        // Fast path: if audit is disabled, skip entirely
        if (!_globalSettings.GetAuditEnabled()) return;

        await _lock.WaitAsync(ct);
        try
        {
            await EnsureInitializedAsync(ct);

            bool isDuplicate = _lastEntry is not null
                && string.Equals(_lastEntry.Username, entry.Username, StringComparison.OrdinalIgnoreCase)
                && string.Equals(_lastEntry.Action,   entry.Action,   StringComparison.Ordinal)
                && string.Equals(_lastEntry.Result,   entry.Result,   StringComparison.Ordinal)
                && string.Equals(_lastEntry.Tunnel,   entry.Tunnel,   StringComparison.OrdinalIgnoreCase);

            if (isDuplicate)
            {
                // Update the last entry in-place (count + end timestamp)
                _lastEntry!.TimestampEnd = entry.Timestamp;
                _lastEntry.Count++;
                await UpdateLastLineAsync(ct);
            }
            else
            {
                // Append new line and track its file offset
                await AppendEntryAsync(entry, ct);
                _lastEntry = entry;
            }

            _writeCount++;
            if (_writeCount % SizeCheckInterval == 0)
                await TrimFileBySizeAsync(ct);
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

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Reads the last line of the file to initialise deduplication state.
    /// Called once (lazily) on first LogAsync invocation.
    /// </summary>
    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized) return;
        _initialized = true;

        if (!File.Exists(_filePath)) return;

        try
        {
            await using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (fs.Length == 0) return;

            // Read last 8 KB â€” large enough for any single audit line
            const int readSize = 8192;
            var startPos = Math.Max(0, fs.Length - readSize);
            fs.Seek(startPos, SeekOrigin.Begin);
            var buf = new byte[(int)(fs.Length - startPos)];
            await fs.ReadExactlyAsync(buf, 0, buf.Length, ct);

            // Walk backwards to find the last non-empty line
            int end = buf.Length - 1;
            while (end >= 0 && (buf[end] == '\n' || buf[end] == '\r')) end--;
            if (end < 0) return;

            int start = end;
            while (start > 0 && buf[start - 1] != '\n') start--;

            var lineText = Encoding.UTF8.GetString(buf, start, end - start + 1).TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(lineText)) return;

            _lastEntry = JsonSerializer.Deserialize<AuditEntry>(lineText, JsonOptions);
            _lastLineStartOffset = startPos + start;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit logger: could not read last entry from existing file");
        }
    }

    /// <summary>Appends a new entry to the file, tracking where the line starts.</summary>
    private async Task AppendEntryAsync(AuditEntry entry, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entry, JsonOptions) + "\n");

        await using var fs = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.None);
        _lastLineStartOffset = fs.Length;  // position BEFORE writing = start of new line
        await fs.WriteAsync(bytes, ct);
    }

    /// <summary>
    /// Overwrites the last line with the updated (grouped) entry by truncating the file
    /// at the known start offset and writing the new serialized form.
    /// </summary>
    private async Task UpdateLastLineAsync(CancellationToken ct)
    {
        if (_lastLineStartOffset < 0 || _lastEntry is null) return;

        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_lastEntry, JsonOptions) + "\n");

        await using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        fs.SetLength(_lastLineStartOffset);
        fs.Seek(0, SeekOrigin.End);
        await fs.WriteAsync(bytes, ct);
        // _lastLineStartOffset doesn't change â€” the line starts at the same position
    }

    /// <summary>
    /// Checks the file size against the configured limit.
    /// If exceeded, removes the oldest entries until the file is 10% below the limit.
    /// </summary>
    private async Task TrimFileBySizeAsync(CancellationToken ct)
    {
        var maxSizeKb = _globalSettings.GetAuditMaxSizeKb();
        if (maxSizeKb <= 0) return;  // no limit configured

        if (!File.Exists(_filePath)) return;

        var maxBytes = (long)maxSizeKb * 1024;
        var fileInfo = new FileInfo(_filePath);
        if (fileInfo.Length <= maxBytes) return;

        var targetBytes = (long)(maxBytes * 0.9);

        try
        {
            var lines = await File.ReadAllLinesAsync(_filePath, ct);
            long totalBytes = fileInfo.Length;
            int linesToRemove = 0;

            // Remove oldest entries from the top until we reach the target size
            while (linesToRemove < lines.Length && totalBytes > targetBytes)
            {
                totalBytes -= Encoding.UTF8.GetByteCount(lines[linesToRemove]) + 1; // +1 for \n
                linesToRemove++;
            }

            if (linesToRemove > 0)
            {
                await File.WriteAllLinesAsync(_filePath, lines[linesToRemove..], ct);

                // Reset deduplication state â€” file was restructured
                _initialized = false;
                _lastEntry = null;
                _lastLineStartOffset = -1;
                await EnsureInitializedAsync(ct);

                _logger.LogInformation(
                    "Audit log trimmed: removed {Count} oldest entries (was {OldSize:N0} bytes, limit {Limit:N0} KB)",
                    linesToRemove, fileInfo.Length, maxSizeKb);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to trim audit log by size");
        }
    }

    // -------------------------------------------------------------------------
    // Query
    // -------------------------------------------------------------------------

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
