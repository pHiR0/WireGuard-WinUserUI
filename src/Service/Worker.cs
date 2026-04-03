using WireGuard.Service.Auth;
using WireGuard.Service.IPC;

namespace WireGuard.Service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IRoleStore _roleStore;
    private readonly PipeServer _pipeServer;

    public Worker(ILogger<Worker> logger, IRoleStore roleStore, RequestHandler requestHandler)
    {
        _logger = logger;
        _roleStore = roleStore;
        _pipeServer = new PipeServer(requestHandler, logger);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WireGuard-WinUserUI service starting");

        var users = await _roleStore.ListUsersAsync(stoppingToken);
        if (users.Count == 0)
        {
            _logger.LogWarning(
                "No users are configured. Run the following command (as admin) to add the first admin user:{NewLine}" +
                "  dotnet run --project src/Service -- --add-admin <windowsUsername>",
                Environment.NewLine);
        }

        await _pipeServer.RunAsync(stoppingToken);
    }
}
