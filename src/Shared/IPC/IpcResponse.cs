using System.Text.Json;
using System.Text.Json.Serialization;

namespace WireGuard.Shared.IPC;

public sealed class IpcResponse
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? RequestId { get; init; }
    public JsonElement? Data { get; init; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static IpcResponse Ok(object? data = null, string? requestId = null)
    {
        JsonElement? element = null;
        if (data is not null)
        {
            var raw = JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions);
            element = JsonSerializer.Deserialize<JsonElement>(raw, JsonOptions);
        }

        return new IpcResponse
        {
            Success = true,
            Data = element,
            RequestId = requestId,
        };
    }

    public static IpcResponse Fail(string error, string? requestId = null)
    {
        return new IpcResponse
        {
            Success = false,
            Error = error,
            RequestId = requestId,
        };
    }

    public T? GetData<T>()
    {
        if (Data is null) return default;
        return Data.Value.Deserialize<T>(JsonOptions);
    }

    public byte[] Serialize()
    {
        return JsonSerializer.SerializeToUtf8Bytes(this, JsonOptions);
    }

    public static IpcResponse? Deserialize(ReadOnlySpan<byte> data)
    {
        return JsonSerializer.Deserialize<IpcResponse>(data, JsonOptions);
    }
}
