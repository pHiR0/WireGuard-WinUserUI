using WireGuard.Service.Auth;
using WireGuard.Service.IPC;

namespace WireGuard.Service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly WindowsGroupProvisioner _groupProvisioner;
    private readonly PipeServer _pipeServer;

    public Worker(ILogger<Worker> logger, WindowsGroupProvisioner groupProvisioner, RequestHandler requestHandler)
    {
        _logger = logger;
        _groupProvisioner = groupProvisioner;
        _pipeServer = new PipeServer(requestHandler, logger);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WireGuard-WinUserUI service starting");

        // Ensure the Windows groups exist before accepting connections
        _groupProvisioner.EnsureGroupsExist();
        _logger.LogInformation(
            "Roles gestionados via grupos de Windows: '{A}', '{Ao}', '{O}', '{V}'",
            WindowsGroupRoleStore.GroupAdministrator,
            WindowsGroupRoleStore.GroupAdvancedOperator,
            WindowsGroupRoleStore.GroupOperator,
            WindowsGroupRoleStore.GroupViewer);

        await _pipeServer.RunAsync(stoppingToken);
    }
}

