using System.Buffers;
using System.IO.Pipes;

namespace WireGuard.Shared.IPC;

/// <summary>
/// Framed message protocol over pipes: [4-byte length (little-endian)] [payload].
/// </summary>
public static class PipeMessageIO
{
    public static async Task WriteMessageAsync(PipeStream pipe, byte[] payload, CancellationToken ct = default)
    {
        if (payload.Length > PipeConstants.MaxMessageSize)
            throw new InvalidOperationException($"Message size {payload.Length} exceeds maximum {PipeConstants.MaxMessageSize}.");

        var header = BitConverter.GetBytes(payload.Length); // 4 bytes, little-endian
        await pipe.WriteAsync(header, ct).ConfigureAwait(false);
        await pipe.WriteAsync(payload, ct).ConfigureAwait(false);
        await pipe.FlushAsync(ct).ConfigureAwait(false);
    }

    public static async Task<byte[]?> ReadMessageAsync(PipeStream pipe, CancellationToken ct = default)
    {
        var header = new byte[4];
        var headerRead = await ReadExactAsync(pipe, header, ct).ConfigureAwait(false);
        if (headerRead == 0) return null; // disconnected

        var length = BitConverter.ToInt32(header, 0);
        if (length <= 0 || length > PipeConstants.MaxMessageSize)
            throw new InvalidOperationException($"Invalid message length: {length}.");

        var buffer = new byte[length];
        var bodyRead = await ReadExactAsync(pipe, buffer, ct).ConfigureAwait(false);
        if (bodyRead < length) return null; // incomplete, treat as disconnect

        return buffer;
    }

    private static async Task<int> ReadExactAsync(PipeStream pipe, byte[] buffer, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await pipe.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), ct).ConfigureAwait(false);
            if (read == 0) return totalRead;
            totalRead += read;
        }
        return totalRead;
    }
}
