using System.Diagnostics;
using System.ServiceProcess;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
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
                tunnels.Add(new TunnelInfo
                {
                    Name = tunnelName,
                    Status = MapStatus(svc),
                    LastChecked = DateTimeOffset.UtcNow,
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
        _logger.LogInformation("Wrote conf for tunnel '{Name}' to '{Path}'", name, confPath);

        // Install as a WireGuard tunnel service
        await RunWireGuardAsync($"/installtunnelservice \"{confPath}\"", ct);
        _logger.LogInformation("Tunnel '{Name}' installed and registered", name);
    }

    public async Task EditTunnelAsync(string name, string confContent, CancellationToken ct = default)
    {
        var validation = WireGuardConfValidator.Validate(confContent);
        if (!validation.IsValid)
            throw new InvalidOperationException($"Invalid configuration: {string.Join("; ", validation.Errors)}");

        // Check if the tunnel service currently exists and is running
        bool wasRunning = false;
        var serviceName = TunnelServicePrefix + name;
        try
        {
            using var svc = new ServiceController(serviceName);
            svc.Refresh();
            wasRunning = svc.Status == ServiceControllerStatus.Running;
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
        await RunWireGuardAsync($"/installtunnelservice \"{confPath}\"", ct);

        if (wasRunning)
        {
            // Re-start the tunnel since it was running before the edit
            await StartTunnelAsync(name, ct);
        }

        _logger.LogInformation("Tunnel '{Name}' edited successfully", name);
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

    // --- helpers ---

    private static string GetConfPath(string tunnelName)
        => Path.Combine(WireGuardConfDir, $"{tunnelName}.conf");

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
            throw new FileNotFoundException($"WireGuard executable not found at '{WireGuardExe}'.");

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
