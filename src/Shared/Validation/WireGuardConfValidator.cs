using System.Net;
using System.Text.RegularExpressions;

namespace WireGuard.Shared.Validation;

/// <summary>
/// Validates WireGuard .conf file content.
/// Returns structured lists of errors and warnings.
/// </summary>
public static partial class WireGuardConfValidator
{
    public sealed class ValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public List<string> Errors { get; } = [];
        public List<string> Warnings { get; } = [];
    }

    /// <summary>
    /// Validate the text content of a WireGuard .conf file.
    /// </summary>
    public static ValidationResult Validate(string confContent)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(confContent))
        {
            result.Errors.Add("Configuration content is empty.");
            return result;
        }

        var lines = confContent.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

        string? currentSection = null;
        bool hasInterface = false;
        bool hasPeer = false;
        bool interfaceHasPrivateKey = false;
        bool interfaceHasAddress = false;
        int peerCount = 0;
        bool currentPeerHasPublicKey = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            var lineNum = i + 1;

            // Skip empty lines and comments
            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            // Section header
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                // Finalize previous peer section
                if (currentSection == "Peer" && !currentPeerHasPublicKey)
                    result.Errors.Add($"Line {lineNum}: [Peer] section (peer #{peerCount}) is missing PublicKey.");

                var sectionName = line[1..^1];

                if (sectionName == "Interface")
                {
                    if (hasInterface)
                        result.Errors.Add($"Line {lineNum}: Duplicate [Interface] section.");
                    hasInterface = true;
                    currentSection = "Interface";
                }
                else if (sectionName == "Peer")
                {
                    hasPeer = true;
                    peerCount++;
                    currentPeerHasPublicKey = false;
                    currentSection = "Peer";
                }
                else
                {
                    result.Warnings.Add($"Line {lineNum}: Unknown section [{sectionName}].");
                    currentSection = sectionName;
                }
                continue;
            }

            // Key = Value
            var eqIndex = line.IndexOf('=');
            if (eqIndex < 0)
            {
                result.Errors.Add($"Line {lineNum}: Invalid syntax (expected Key = Value).");
                continue;
            }

            var key = line[..eqIndex].Trim();
            var value = line[(eqIndex + 1)..].Trim();

            if (currentSection == null)
            {
                result.Errors.Add($"Line {lineNum}: Key '{key}' is outside any section.");
                continue;
            }

            if (currentSection == "Interface")
                ValidateInterfaceKey(key, value, lineNum, result, ref interfaceHasPrivateKey, ref interfaceHasAddress);
            else if (currentSection == "Peer")
                ValidatePeerKey(key, value, lineNum, result, ref currentPeerHasPublicKey);
        }

        // Final peer check
        if (currentSection == "Peer" && !currentPeerHasPublicKey)
            result.Errors.Add($"[Peer] section (peer #{peerCount}) is missing PublicKey.");

        if (!hasInterface)
            result.Errors.Add("[Interface] section is required.");
        if (!hasPeer)
            result.Errors.Add("At least one [Peer] section is required.");
        if (hasInterface && !interfaceHasPrivateKey)
            result.Errors.Add("[Interface] is missing PrivateKey.");
        if (hasInterface && !interfaceHasAddress)
            result.Errors.Add("[Interface] is missing Address.");

        return result;
    }

    private static void ValidateInterfaceKey(string key, string value, int lineNum, ValidationResult result,
        ref bool hasPrivateKey, ref bool hasAddress)
    {
        switch (key)
        {
            case "PrivateKey":
                hasPrivateKey = true;
                ValidateBase64Key(value, lineNum, "PrivateKey", result);
                break;
            case "Address":
                hasAddress = true;
                ValidateCidrList(value, lineNum, result);
                break;
            case "DNS":
                ValidateIpList(value, lineNum, result);
                break;
            case "ListenPort":
                ValidatePort(value, lineNum, result);
                break;
            case "MTU":
                if (!int.TryParse(value, out var mtu) || mtu < 576 || mtu > 65535)
                    result.Warnings.Add($"Line {lineNum}: MTU '{value}' is outside typical range (576-65535).");
                break;
            case "Table":
            case "PreUp":
            case "PostUp":
            case "PreDown":
            case "PostDown":
            case "SaveConfig":
                // Known keys, no strict validation
                break;
            default:
                result.Warnings.Add($"Line {lineNum}: Unknown [Interface] key '{key}'.");
                break;
        }
    }

    private static void ValidatePeerKey(string key, string value, int lineNum, ValidationResult result,
        ref bool hasPublicKey)
    {
        switch (key)
        {
            case "PublicKey":
                hasPublicKey = true;
                ValidateBase64Key(value, lineNum, "PublicKey", result);
                break;
            case "PresharedKey":
                ValidateBase64Key(value, lineNum, "PresharedKey", result);
                break;
            case "AllowedIPs":
                ValidateCidrList(value, lineNum, result);
                break;
            case "Endpoint":
                ValidateEndpoint(value, lineNum, result);
                break;
            case "PersistentKeepalive":
                if (!int.TryParse(value, out var ka) || ka < 0 || ka > 65535)
                    result.Errors.Add($"Line {lineNum}: PersistentKeepalive '{value}' must be 0-65535.");
                break;
            default:
                result.Warnings.Add($"Line {lineNum}: Unknown [Peer] key '{key}'.");
                break;
        }
    }

    private static void ValidateBase64Key(string value, int lineNum, string keyName, ValidationResult result)
    {
        try
        {
            var bytes = Convert.FromBase64String(value);
            if (bytes.Length != 32)
                result.Errors.Add($"Line {lineNum}: {keyName} must be 32 bytes (got {bytes.Length}).");
        }
        catch (FormatException)
        {
            result.Errors.Add($"Line {lineNum}: {keyName} is not valid base64.");
        }
    }

    private static void ValidateCidrList(string value, int lineNum, ValidationResult result)
    {
        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        foreach (var cidr in parts)
        {
            var slashIdx = cidr.IndexOf('/');
            if (slashIdx < 0)
            {
                result.Errors.Add($"Line {lineNum}: '{cidr}' is not a valid CIDR (missing /prefix).");
                continue;
            }

            var ipPart = cidr[..slashIdx];
            var prefixPart = cidr[(slashIdx + 1)..];

            if (!IPAddress.TryParse(ipPart, out var ip))
            {
                result.Errors.Add($"Line {lineNum}: '{ipPart}' is not a valid IP address.");
                continue;
            }

            if (!int.TryParse(prefixPart, out var prefix))
            {
                result.Errors.Add($"Line {lineNum}: '{prefixPart}' is not a valid prefix length.");
                continue;
            }

            var maxPrefix = ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? 128 : 32;
            if (prefix < 0 || prefix > maxPrefix)
                result.Errors.Add($"Line {lineNum}: Prefix length {prefix} is out of range (0-{maxPrefix}).");
        }
    }

    private static void ValidateIpList(string value, int lineNum, ValidationResult result)
    {
        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        foreach (var ip in parts)
        {
            if (!IPAddress.TryParse(ip, out _))
                result.Errors.Add($"Line {lineNum}: '{ip}' is not a valid IP address.");
        }
    }

    private static void ValidatePort(string value, int lineNum, ValidationResult result)
    {
        if (!int.TryParse(value, out var port) || port < 1 || port > 65535)
            result.Errors.Add($"Line {lineNum}: Port '{value}' must be 1-65535.");
    }

    private static void ValidateEndpoint(string value, int lineNum, ValidationResult result)
    {
        // Format: host:port or [ipv6]:port
        var lastColon = value.LastIndexOf(':');
        if (lastColon < 0)
        {
            result.Errors.Add($"Line {lineNum}: Endpoint '{value}' must be host:port.");
            return;
        }

        var portStr = value[(lastColon + 1)..];
        ValidatePort(portStr, lineNum, result);

        var host = value[..lastColon];
        // IPv6 in brackets
        if (host.StartsWith('[') && host.EndsWith(']'))
            host = host[1..^1];

        if (!IPAddress.TryParse(host, out _) && !HostNameRegex().IsMatch(host))
            result.Errors.Add($"Line {lineNum}: Endpoint host '{host}' is not a valid IP or hostname.");
    }

    [GeneratedRegex(@"^[a-zA-Z0-9]([a-zA-Z0-9\-\.]*[a-zA-Z0-9])?$")]
    private static partial Regex HostNameRegex();
}
