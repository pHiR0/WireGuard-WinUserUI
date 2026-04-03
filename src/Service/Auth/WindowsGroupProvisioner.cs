using System;
using System.DirectoryServices.AccountManagement;
using Microsoft.Extensions.Logging;

namespace WireGuard.Service.Auth;

/// <summary>
/// Creates the four WireGuard UI Windows local groups on first run if they don't already exist.
/// Run from Worker.ExecuteAsync before serving pipe connections.
/// </summary>
public sealed class WindowsGroupProvisioner
{
    private readonly ILogger<WindowsGroupProvisioner> _logger;

    private static readonly (string Name, string Description)[] Groups =
    [
        (WindowsGroupRoleStore.GroupAdministrator,
         "Administradores de WireGuard Manager con acceso completo"),

        (WindowsGroupRoleStore.GroupAdvancedOperator,
         "Operadores avanzados de WireGuard Manager: importar, editar, eliminar y exportar túneles"),

        (WindowsGroupRoleStore.GroupOperator,
         "Operadores de WireGuard Manager: iniciar y detener túneles"),

        (WindowsGroupRoleStore.GroupViewer,
         "Usuarios de WireGuard Manager con acceso de solo lectura"),
    ];

    public WindowsGroupProvisioner(ILogger<WindowsGroupProvisioner> logger)
    {
        _logger = logger;
    }

    public void EnsureGroupsExist()
    {
        try
        {
            using var ctx = new PrincipalContext(ContextType.Machine);
            foreach (var (name, description) in Groups)
            {
                var existing = GroupPrincipal.FindByIdentity(ctx, name);
                if (existing is not null)
                {
                    existing.Dispose();
                    _logger.LogDebug("Windows group '{Name}' already exists", name);
                    continue;
                }

                using var group = new GroupPrincipal(ctx)
                {
                    Name = name,
                    Description = description,
                    IsSecurityGroup = true,
                };
                group.Save();
                _logger.LogInformation("Created Windows group '{Name}'", name);
            }
        }
        catch (Exception ex)
        {
            // Non-fatal: service may not have sufficient privileges in dev environment
            _logger.LogWarning(ex,
                "Could not create WireGuard UI Windows groups (need local administrator rights)");
        }
    }
}
