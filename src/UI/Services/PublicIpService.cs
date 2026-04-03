using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WireGuard.UI.Services;

/// <summary>
/// Resolves the machine's current public IP address using multiple fallback methods.
/// Includes rate limiting to avoid flooding external services.
/// </summary>
public sealed class PublicIpService : IDisposable
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    // Ordered fallback list of HTTP-based IP detection services
    private static readonly IReadOnlyList<Func<CancellationToken, Task<string?>>> _httpProviders =
    [
        ct => FetchFromJsonAsync("https://api.ipify.org?format=json", "ip", ct),
        ct => FetchFromJsonAsync("https://ipinfo.io/json", "ip", ct),
        ct => FetchFromJsonAsync("https://api.my-ip.io/v1/ip.json", "ip", ct),
        ct => FetchFromPlainTextAsync("https://ifconfig.me/ip", ct),
        ct => FetchFromPlainTextAsync("https://api4.my-ip.io/v1/ip", ct),
        ct => FetchFromPlainTextAsync("https://checkip.amazonaws.com", ct),
    ];

    private DateTimeOffset _lastFetch = DateTimeOffset.MinValue;
    private const int MinFetchIntervalSeconds = 30;

    /// <summary>
    /// Gets the current public IP. Uses cached value if within the rate-limit window,
    /// unless <paramref name="force"/> is true (e.g. after tunnel connect/disconnect).
    /// </summary>
    public async Task<string?> GetPublicIpAsync(bool force = false, CancellationToken ct = default)
    {
        if (!force && (DateTimeOffset.UtcNow - _lastFetch).TotalSeconds < MinFetchIntervalSeconds)
            return null; // Caller should keep the previous value

        var ip = await ResolveAsync(ct);
        if (ip is not null)
            _lastFetch = DateTimeOffset.UtcNow;
        return ip;
    }

    private static async Task<string?> ResolveAsync(CancellationToken ct)
    {
        // Try each HTTP provider in order, stop at first success
        foreach (var provider in _httpProviders)
        {
            try
            {
                var ip = await provider(ct);
                if (!string.IsNullOrWhiteSpace(ip) && IsValidIp(ip))
                    return ip.Trim();
            }
            catch { /* try next */ }
        }

        // Last resort: DNS method via myip.opendns.com @ resolver1.opendns.com
        try
        {
            var ip = await ResolveViaDnsAsync(ct);
            if (!string.IsNullOrWhiteSpace(ip) && IsValidIp(ip))
                return ip;
        }
        catch { /* all methods failed */ }

        return null;
    }

    private static async Task<string?> FetchFromJsonAsync(string url, string jsonKey, CancellationToken ct)
    {
        using var resp = await _http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty(jsonKey, out var prop))
            return prop.GetString();
        return null;
    }

    private static async Task<string?> FetchFromPlainTextAsync(string url, CancellationToken ct)
    {
        using var resp = await _http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadAsStringAsync(ct)).Trim();
    }

    /// <summary>
    /// Sends a DNS TXT query for "myip.opendns.com" to OpenDNS resolver (208.67.222.222).
    /// Uses raw UDP since System.Net.Dns doesn't support custom resolvers.
    /// </summary>
    private static async Task<string?> ResolveViaDnsAsync(CancellationToken ct)
    {
        // Build a minimal DNS A-record query for "myip.opendns.com"
        // Query ID: 0x1234, Flags: standard query, QCount=1, A record, class IN
        byte[] query = BuildDnsQuery("myip.opendns.com", queryType: 1 /* A */);

        using var udp = new UdpClient();
        udp.Connect(IPAddress.Parse("208.67.222.222"), 53); // resolver1.opendns.com

        await udp.SendAsync(query, query.Length, ct: ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));

        var result = await udp.ReceiveAsync(timeoutCts.Token);
        return ParseDnsARecord(result.Buffer);
    }

    private static byte[] BuildDnsQuery(string hostname, ushort queryType)
    {
        var parts = hostname.Split('.');
        // Header: ID(2) + Flags(2) + QDCOUNT(2) + ANCOUNT(2) + NSCOUNT(2) + ARCOUNT(2)
        var packet = new System.Collections.Generic.List<byte>
        {
            0x12, 0x34,  // Transaction ID
            0x01, 0x00,  // Standard query, recursion desired
            0x00, 0x01,  // QDCOUNT = 1
            0x00, 0x00,  // ANCOUNT = 0
            0x00, 0x00,  // NSCOUNT = 0
            0x00, 0x00,  // ARCOUNT = 0
        };
        // Question: QNAME
        foreach (var part in parts)
        {
            packet.Add((byte)part.Length);
            foreach (var c in part) packet.Add((byte)c);
        }
        packet.Add(0x00); // Terminator
        // QTYPE (A = 0x0001) + QCLASS (IN = 0x0001)
        packet.Add((byte)(queryType >> 8));
        packet.Add((byte)(queryType & 0xFF));
        packet.Add(0x00);
        packet.Add(0x01);
        return [.. packet];
    }

    private static string? ParseDnsARecord(byte[] response)
    {
        if (response.Length < 12) return null;
        int qdCount = (response[4] << 8) | response[5];
        int anCount = (response[6] << 8) | response[7];
        if (anCount == 0) return null;

        // Skip header (12 bytes) and question section
        int pos = 12;
        for (int i = 0; i < qdCount; i++)
        {
            // Skip QNAME (labels)
            while (pos < response.Length && response[pos] != 0)
            {
                if ((response[pos] & 0xC0) == 0xC0) { pos += 2; break; } // compressed
                pos += response[pos] + 1;
            }
            if (pos < response.Length && response[pos] == 0) pos++;
            pos += 4; // QTYPE + QCLASS
        }

        // Read first A record answer
        for (int i = 0; i < anCount; i++)
        {
            if (pos >= response.Length) break;
            // Skip NAME
            if ((response[pos] & 0xC0) == 0xC0) pos += 2;
            else { while (pos < response.Length && response[pos] != 0) pos += response[pos] + 1; pos++; }

            if (pos + 10 > response.Length) break;
            int type = (response[pos] << 8) | response[pos + 1];
            int rdLength = (response[pos + 8] << 8) | response[pos + 9];
            pos += 10; // TYPE(2) + CLASS(2) + TTL(4) + RDLENGTH(2)

            if (type == 1 && rdLength == 4 && pos + 4 <= response.Length) // A record
                return $"{response[pos]}.{response[pos + 1]}.{response[pos + 2]}.{response[pos + 3]}";

            pos += rdLength;
        }
        return null;
    }

    private static bool IsValidIp(string ip)
        => Regex.IsMatch(ip.Trim(), @"^\d{1,3}(\.\d{1,3}){3}$");

    public void Dispose() => _http.Dispose();
}
