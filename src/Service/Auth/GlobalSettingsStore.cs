using Microsoft.Win32;

namespace WireGuard.Service.Auth;

/// <summary>
/// Reads and writes global service settings stored in the Windows Registry under
/// HKLM\SOFTWARE\WireGuard-WinUserUI. These settings apply machine-wide and
/// require Administrator privileges to modify.
/// </summary>
public sealed class GlobalSettingsStore
{
    private const string RegistryKey = @"SOFTWARE\WireGuard-WinUserUI";
    private const string AllUsersDefaultOperatorValue = "AllUsersDefaultOperator";

    /// <summary>
    /// When enabled, any authenticated user who does NOT belong to any WireGuard UI
    /// role group is automatically treated as an Operator instead of having no role.
    /// Default: false.
    /// </summary>
    public bool GetAllUsersDefaultOperator()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(RegistryKey);
            return key?.GetValue(AllUsersDefaultOperatorValue) is int v && v != 0;
        }
        catch { return false; }
    }

    /// <summary>Sets the AllUsersDefaultOperator flag. Requires the service to run with
    /// sufficient privileges to write to HKLM\SOFTWARE.</summary>
    public void SetAllUsersDefaultOperator(bool enabled)
    {
        using var key = Registry.LocalMachine.CreateSubKey(RegistryKey, writable: true);
        key.SetValue(AllUsersDefaultOperatorValue, enabled ? 1 : 0, RegistryValueKind.DWord);
    }
}
