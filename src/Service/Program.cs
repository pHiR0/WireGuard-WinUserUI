using Microsoft.Extensions.Logging.Console;
using WireGuard.Service;
using WireGuard.Service.Audit;
using WireGuard.Service.Auth;
using WireGuard.Service.IPC;
using WireGuard.Service.Tunnels;
using WireGuard.Shared.Models;

// Handle --add-admin <username> bootstrap command
// Usage: dotnet run --project src/Service -- --add-admin <windowsUsername>
if (args is ["--add-admin", var adminUser])
{
    using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
    var roleStore = new JsonRoleStore(loggerFactory.CreateLogger<JsonRoleStore>());
    await roleStore.SetRoleAsync(adminUser, UserRole.Admin);
    Console.WriteLine($"User '{adminUser}' has been granted the Admin role.");
    return;
}

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "WireGuard-WinUserUI";
});

builder.Services.AddSingleton<ITunnelManager, TunnelManager>();
builder.Services.AddSingleton<IRoleStore, JsonRoleStore>();
builder.Services.AddSingleton<IAuthorizationService, AuthorizationService>();
builder.Services.AddSingleton<IAuditLogger, JsonAuditLogger>();
builder.Services.AddSingleton<RequestHandler>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
