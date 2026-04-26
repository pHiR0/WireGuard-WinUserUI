using System.Text.Json;
using System.Text.Json.Serialization;
using WireGuard.Shared.Models;

namespace WireGuard.Shared.IPC;

public sealed class IpcRequest
{
    public required IpcCommand Command { get; init; }
    public string? TunnelName { get; init; }
    public string? RequestId { get; init; }

    // Phase 2 — Tunnel conf content (base64-encoded for import/create/edit)
    public string? ConfContent { get; init; }

    // Phase 2 — User management
    public string? Username { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UserRole? Role { get; init; }

    // Phase 2 — Audit query
    public AuditQuery? AuditQuery { get; init; }

    // Phase 2 — Tunnel auto-start
    public bool? AutoStart { get; init; }

    // Phase 3 — Generic bool value for global settings commands
    public bool? BoolValue { get; init; }

    // Phase 3 — Audit settings
    public AuditSettingsData? AuditSettings { get; init; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public byte[] Serialize()
    {
        return JsonSerializer.SerializeToUtf8Bytes(this, JsonOptions);
    }

    public static IpcRequest? Deserialize(ReadOnlySpan<byte> data)
    {
        return JsonSerializer.Deserialize<IpcRequest>(data, JsonOptions);
    }
}

/// <summary>
/// Parameters for querying the audit log.
/// </summary>
public sealed class AuditQuery
{
    public string? Username { get; init; }
    public string? Action { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
