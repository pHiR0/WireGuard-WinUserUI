using Microsoft.Extensions.Logging.Console;
using WireGuard.Service;
using WireGuard.Service.Audit;
using WireGuard.Service.Auth;
using WireGuard.Service.IPC;
using WireGuard.Service.Tunnels;
using WireGuard.Shared.Models;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "WireGuard-WinUserUI";
});

builder.Services.AddSingleton<ITunnelManager, TunnelManager>();
builder.Services.AddSingleton<IRoleStore, WindowsGroupRoleStore>();
builder.Services.AddSingleton<WindowsGroupProvisioner>();
builder.Services.AddSingleton<IAuthorizationService, AuthorizationService>();
builder.Services.AddSingleton<IAuditLogger, JsonAuditLogger>();
builder.Services.AddSingleton<RequestHandler>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

