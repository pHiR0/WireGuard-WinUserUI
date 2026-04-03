using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using WireGuard.Shared.Models;

namespace WireGuard.Service.Tunnels;

/// <summary>
/// Reads tunnel statistics by running `wg show &lt;name&gt;` and parsing the output.
/// Returns null fields when wg.exe is not available or the tunnel is not running.
/// </summary>
internal static partial class WireGuardStats
{
    private static readonly string WgExe =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WireGuard", "wg.exe");

    private static readonly string WireGuardConfDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "WireGuard");

    /// <summary>
    /// Attempts to retrieve stats for a single tunnel. Returns (null,null,null,0,0) on failure.
    /// </summary>
    public static (string? Address, string? Endpoint, string? LastHandshake, long RxBytes, long TxBytes)
        GetStats(string tunnelName, ILogger? logger = null)
    {
        if (!File.Exists(WgExe))
            return (null, null, null, 0, 0);

        try
        {
            // wg show gives runtime stats (endpoint, handshake, transfer)
            var showOutput = RunWg($"show {tunnelName}", logger);
            var (_, endpoint, lastHandshake, rxBytes, txBytes) = ParseOutput(showOutput);

            // Read the .conf file directly for the Address field (wg showconf on Windows
            // does NOT include Address — that is a wireguard-windows platform concept)
            string? address = null;
            try
            {
                var confPath = Path.Combine(WireGuardConfDir, $"{tunnelName}.conf");
                if (File.Exists(confPath))
                {
                    var confContent = File.ReadAllText(confPath);
                    address = ParseAddressFromConf(confContent);
                }
            }
            catch { /* not fatal — address stays null */ }

            return (address, endpoint, lastHandshake, rxBytes, txBytes);
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Failed to get stats for tunnel '{Name}'", tunnelName);
            return (null, null, null, 0, 0);
        }
    }

    /// <summary>
    /// Parses the Address field from wg showconf output:
    ///   [Interface]
    ///   Address = 10.8.0.2/24
    /// </summary>
    private static string? ParseAddressFromConf(string confOutput)
    {
        foreach (var line in confOutput.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("Address", StringComparison.OrdinalIgnoreCase) && trimmed.Contains('='))
            {
                var val = trimmed[(trimmed.IndexOf('=') + 1)..].Trim();
                if (!string.IsNullOrEmpty(val))
                    return val;
            }
        }
        return null;
    }

    private static string RunWg(string args, ILogger? logger)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = WgExe,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5000);
        return output;
    }

    /// <summary>
    /// Parses output like:
    /// <code>
    /// interface: mytunnel
    ///   public key: ...
    ///   listening port: 51820
    ///   address: 10.0.0.1/24
    ///
    /// peer: ...
    ///   endpoint: 1.2.3.4:51820
    ///   allowed ips: 0.0.0.0/0
    ///   latest handshake: 30 seconds ago
    ///   transfer: 1.23 MiB received, 456 KiB sent
    /// </code>
    /// </summary>
    private static (string? Address, string? Endpoint, string? LastHandshake, long RxBytes, long TxBytes)
        ParseOutput(string output)
    {
        string? address = null, endpoint = null, lastHandshake = null;
        long rxBytes = 0, txBytes = 0;

        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("endpoint:", StringComparison.OrdinalIgnoreCase))
                endpoint = trimmed[9..].Trim();
            else if (trimmed.StartsWith("latest handshake:", StringComparison.OrdinalIgnoreCase))
                lastHandshake = trimmed[17..].Trim();
            else if (trimmed.StartsWith("transfer:", StringComparison.OrdinalIgnoreCase))
            {
                // "1.23 MiB received, 456 KiB sent"
                var match = TransferRegex().Match(trimmed);
                if (match.Success)
                {
                    rxBytes = ParseBytes(match.Groups["rxVal"].Value, match.Groups["rxUnit"].Value);
                    txBytes = ParseBytes(match.Groups["txVal"].Value, match.Groups["txUnit"].Value);
                }
            }
        }

        return (address, endpoint, lastHandshake, rxBytes, txBytes);
    }

    private static long ParseBytes(string valueStr, string unit)
    {
        if (!double.TryParse(valueStr, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var val))
            return 0;

        return unit.ToUpperInvariant() switch
        {
            "B" => (long)val,
            "KIB" => (long)(val * 1024),
            "MIB" => (long)(val * 1024 * 1024),
            "GIB" => (long)(val * 1024 * 1024 * 1024),
            _ => (long)val,
        };
    }

    [GeneratedRegex(@"transfer:\s*(?<rxVal>[\d.]+)\s*(?<rxUnit>\w+)\s+received,\s*(?<txVal>[\d.]+)\s*(?<txUnit>\w+)\s+sent",
        RegexOptions.IgnoreCase)]
    private static partial Regex TransferRegex();
}
