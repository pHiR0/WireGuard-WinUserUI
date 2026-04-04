using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using WireGuard.Shared.Models;
using WireGuard.Shared.Validation;

namespace WireGuard.Service.Tunnels;

public sealed partial class TunnelManager : ITunnelManager
{
    private const string TunnelServicePrefix = "WireGuardTunnel$";
    private readonly ILogger<TunnelManager> _logger;

    // WireGuard config directory — used for import/edit/delete/export
    private static readonly string WireGuardConfDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "WireGuard");

    // WireGuard executable (standard install path)
    private static readonly string WireGuardExe =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WireGuard", "wireguard.exe");

    public TunnelManager(ILogger<TunnelManager> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<TunnelInfo>> ListTunnelsAsync(CancellationToken ct = default)
    {
        var services = ServiceController.GetServices();
        var tunnels = new List<TunnelInfo>();

        foreach (var svc in services)
        {
            using (svc)
            {
                if (!svc.ServiceName.StartsWith(TunnelServicePrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                var tunnelName = svc.ServiceName[TunnelServicePrefix.Length..];
                var status = MapStatus(svc);

                // Fetch live stats from wg.exe when the tunnel is running
                string? address = null, endpoint = null, handshake = null;
                long rxBytes = 0, txBytes = 0;
                if (status == TunnelStatus.Running)
                    (address, endpoint, handshake, rxBytes, txBytes) = WireGuardStats.GetStats(tunnelName, _logger);

                tunnels.Add(new TunnelInfo
                {
                    Name = tunnelName,
                    Status = status,
                    LastChecked = DateTimeOffset.UtcNow,
                    AutoStart = GetServiceAutoStart(svc.ServiceName),
                    TunnelAddress = address,
                    Endpoint = endpoint,
                    LastHandshake = handshake,
                    RxBytes = rxBytes,
                    TxBytes = txBytes,
                });
            }
        }

        _logger.LogDebug("Listed {Count} WireGuard tunnels", tunnels.Count);
        return Task.FromResult<IReadOnlyList<TunnelInfo>>(tunnels);
    }

    public Task<TunnelInfo?> GetTunnelStatusAsync(string name, CancellationToken ct = default)
    {
        var serviceName = TunnelServicePrefix + name;

        try
        {
            using var svc = new ServiceController(serviceName);
            svc.Refresh();
            var info = new TunnelInfo
            {
                Name = name,
                Status = MapStatus(svc),
                LastChecked = DateTimeOffset.UtcNow,
            };
            return Task.FromResult<TunnelInfo?>(info);
        }
        catch (InvalidOperationException)
        {
            _logger.LogWarning("Tunnel service '{ServiceName}' not found", serviceName);
            return Task.FromResult<TunnelInfo?>(null);
        }
    }

    public async Task StartTunnelAsync(string name, CancellationToken ct = default)
    {
        var serviceName = TunnelServicePrefix + name;
        using var svc = new ServiceController(serviceName);
        svc.Refresh();

        if (svc.Status == ServiceControllerStatus.Running)
        {
            _logger.LogInformation("Tunnel '{Name}' is already running", name);
            return;
        }

        _logger.LogInformation("Starting tunnel '{Name}'", name);
        svc.Start();
        await Task.Run(() => svc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30)), ct);
        _logger.LogInformation("Tunnel '{Name}' started", name);
    }

    public async Task StopTunnelAsync(string name, CancellationToken ct = default)
    {
        var serviceName = TunnelServicePrefix + name;
        using var svc = new ServiceController(serviceName);
        svc.Refresh();

        if (svc.Status == ServiceControllerStatus.Stopped)
        {
            _logger.LogInformation("Tunnel '{Name}' is already stopped", name);
            return;
        }

        _logger.LogInformation("Stopping tunnel '{Name}'", name);
        svc.Stop();
        await Task.Run(() => svc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30)), ct);
        _logger.LogInformation("Tunnel '{Name}' stopped", name);
    }

    public async Task RestartTunnelAsync(string name, CancellationToken ct = default)
    {
        _logger.LogInformation("Restarting tunnel '{Name}'", name);
        await StopTunnelAsync(name, ct);
        await StartTunnelAsync(name, ct);
        _logger.LogInformation("Tunnel '{Name}' restarted", name);
    }

    public async Task ImportTunnelAsync(string name, string confContent, CancellationToken ct = default)
    {
        ValidateTunnelName(name);

        var validation = WireGuardConfValidator.Validate(confContent);
        if (!validation.IsValid)
            throw new InvalidOperationException($"Invalid configuration: {string.Join("; ", validation.Errors)}");

        var confPath = GetConfPath(name);

        // Backup if exists
        if (File.Exists(confPath))
        {
            var backupPath = confPath + $".bak.{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            File.Copy(confPath, backupPath, overwrite: true);
            _logger.LogInformation("Backed up existing conf to '{BackupPath}'", backupPath);
        }

        Directory.CreateDirectory(WireGuardConfDir);
        await File.WriteAllTextAsync(confPath, confContent, ct);
        SecureConfFile(confPath);
        _logger.LogInformation("Wrote conf for tunnel '{Name}' to '{Path}'", name, confPath);

        // Install as a WireGuard tunnel service
        await RunWireGuardAsync($"/installtunnelservice \"{confPath}\"", ct);
        _logger.LogInformation("Tunnel '{Name}' installed and registered", name);

        // wireguard.exe /installtunnelservice starts the service immediately and sets it to Automatic.
        // Stop it so the user controls when to connect, and set startup type to Manual.
        await EnsureTunnelStoppedAsync(name, ct);
        await SetServiceStartTypeAsync(TunnelServicePrefix + name, autoStart: false, ct);
    }

    public async Task EditTunnelAsync(string name, string confContent, CancellationToken ct = default)
    {
        var validation = WireGuardConfValidator.Validate(confContent);
        if (!validation.IsValid)
            throw new InvalidOperationException($"Invalid configuration: {string.Join("; ", validation.Errors)}");

        // Check if the tunnel service currently exists and is running
        bool wasRunning = false;
        bool wasAutoStart = false;
        var serviceName = TunnelServicePrefix + name;
        try
        {
            using var svc = new ServiceController(serviceName);
            svc.Refresh();
            wasRunning = svc.Status == ServiceControllerStatus.Running;
            wasAutoStart = GetServiceAutoStart(serviceName);
        }
        catch (InvalidOperationException)
        {
            // Service doesn't exist — will be created fresh
        }

        // Uninstall old service
        try { await RunWireGuardAsync($"/uninstalltunnelservice \"{name}\"", ct); }
        catch { /* may not exist yet */ }

        var confPath = GetConfPath(name);

        // Backup
        if (File.Exists(confPath))
        {
            var backupPath = confPath + $".bak.{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            File.Copy(confPath, backupPath, overwrite: true);
        }

        await File.WriteAllTextAsync(confPath, confContent, ct);
        SecureConfFile(confPath);
        await RunWireGuardAsync($"/installtunnelservice \"{confPath}\"", ct);

        // /installtunnelservice always starts the service; stop it if it wasn't running before the edit.
        if (!wasRunning)
            await EnsureTunnelStoppedAsync(name, ct);

        // Restore the startup type the service had before the edit (re-install always sets Automatic)
        await SetServiceStartTypeAsync(TunnelServicePrefix + name, wasAutoStart, ct);

        if (wasRunning)
        {
            // Re-start the tunnel since it was running before the edit
            await StartTunnelAsync(name, ct);
        }

        _logger.LogInformation("Tunnel '{Name}' edited successfully", name);
    }

    public async Task SetTunnelAutoStartAsync(string name, bool autoStart, CancellationToken ct = default)
    {
        var serviceName = TunnelServicePrefix + name;
        await SetServiceStartTypeAsync(serviceName, autoStart, ct);
        _logger.LogInformation("Tunnel '{Name}' auto-start set to {AutoStart}", name, autoStart);
    }

    public async Task DeleteTunnelAsync(string name, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting tunnel '{Name}'", name);

        // Uninstall the tunnel service
        await RunWireGuardAsync($"/uninstalltunnelservice \"{name}\"", ct);

        // Remove the .conf file
        var confPath = GetConfPath(name);
        if (File.Exists(confPath))
        {
            File.Delete(confPath);
            _logger.LogInformation("Deleted conf file '{Path}'", confPath);
        }

        _logger.LogInformation("Tunnel '{Name}' deleted", name);
    }

    public async Task<string?> ExportTunnelAsync(string name, CancellationToken ct = default)
    {
        var confPath = GetConfPath(name);
        if (!File.Exists(confPath))
        {
            _logger.LogWarning("Conf file not found for tunnel '{Name}' at '{Path}'", name, confPath);
            return null;
        }

        return await File.ReadAllTextAsync(confPath, ct);
    }

    /// <summary>
    /// Waits for a freshly-installed tunnel service to finish starting (StartPending→Running),
    /// then stops it. Used after /installtunnelservice to ensure the tunnel is not left connected.
    /// </summary>
    private async Task EnsureTunnelStoppedAsync(string tunnelName, CancellationToken ct)
    {
        var serviceName = TunnelServicePrefix + tunnelName;
        try
        {
            using var svc = new ServiceController(serviceName);
            svc.Refresh();

            // Wait for StartPending to resolve before we can send Stop
            if (svc.Status == ServiceControllerStatus.StartPending)
                await Task.Run(() => svc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10)), ct);

            svc.Refresh();
            if (svc.Status == ServiceControllerStatus.Running
             || svc.Status == ServiceControllerStatus.StartPending)
            {
                svc.Stop();
                await Task.Run(() => svc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20)), ct);
                _logger.LogInformation("Stopped auto-started tunnel '{Name}' after install/edit", tunnelName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not stop tunnel '{Name}' after install/edit", tunnelName);
        }
    }

    // --- helpers ---

    private static string GetConfPath(string tunnelName)
        => Path.Combine(WireGuardConfDir, $"{tunnelName}.conf");

    /// <summary>
    /// Locks the conf file so only SYSTEM and BUILTIN\Administrators have access,
    /// preventing regular users from reading the private key.
    /// </summary>
    private void SecureConfFile(string path)
    {
        try
        {
            var fs = new FileSecurity(path, AccessControlSections.Access);
            // Remove inherited rules and start clean
            fs.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            foreach (FileSystemAccessRule rule in fs.GetAccessRules(true, true, typeof(SecurityIdentifier)))
                fs.RemoveAccessRule(rule);

            // SYSTEM: FullControl
            fs.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl,
                AccessControlType.Allow));

            // BUILTIN\Administrators: FullControl
            fs.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                FileSystemRights.FullControl,
                AccessControlType.Allow));

            new FileInfo(path).SetAccessControl(fs);
            _logger.LogDebug("Secured ACL on conf file '{Path}'", path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set ACL on conf file '{Path}'", path);
        }
    }

    /// <summary>Returns true if the service is configured for Automatic start.</summary>
    private static bool GetServiceAutoStart(string serviceName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                $@"SYSTEM\CurrentControlSet\Services\{serviceName}");
            if (key?.GetValue("Start") is int startValue)
                return startValue == 2; // 2=Auto, 3=Demand/Manual, 4=Disabled
        }
        catch { /* non-critical */ }
        return false;
    }

    /// <summary>Sets the Windows service startup type using sc.exe.</summary>
    private static async Task SetServiceStartTypeAsync(string serviceName, bool autoStart, CancellationToken ct)
    {
        var startType = autoStart ? "auto" : "demand";
        var psi = new ProcessStartInfo("sc.exe", $"config \"{serviceName}\" start={startType}")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start sc.exe");
        await p.WaitForExitAsync(ct);
    }

    private static void ValidateTunnelName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tunnel name cannot be empty.");
        if (!TunnelNameRegex().IsMatch(name))
            throw new ArgumentException($"Tunnel name '{name}' contains invalid characters. Use only letters, digits, hyphens and underscores.");
    }

    private async Task RunWireGuardAsync(string arguments, CancellationToken ct)
    {
        if (!File.Exists(WireGuardExe))
            throw new InvalidOperationException(
                "WireGuard no está instalado. Instálelo desde wireguard.com antes de continuar.");

        _logger.LogDebug("Running: wireguard.exe {Args}", arguments);

        var psi = new ProcessStartInfo
        {
            FileName = WireGuardExe,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start wireguard.exe");

        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var msg = $"wireguard.exe exited with code {process.ExitCode}. stderr: {stderr}".Trim();
            _logger.LogError("wireguard.exe failed: {Message}", msg);
            throw new InvalidOperationException(msg);
        }
    }

    private static TunnelStatus MapStatus(ServiceController svc)
    {
        try
        {
            return svc.Status switch
            {
                ServiceControllerStatus.Running => TunnelStatus.Running,
                ServiceControllerStatus.Stopped => TunnelStatus.Stopped,
                ServiceControllerStatus.StartPending => TunnelStatus.StartPending,
                ServiceControllerStatus.StopPending => TunnelStatus.StopPending,
                _ => TunnelStatus.Unknown,
            };
        }
        catch
        {
            return TunnelStatus.Error;
        }
    }

    [GeneratedRegex(@"^[a-zA-Z0-9_\-]+$")]
    private static partial Regex TunnelNameRegex();
}
