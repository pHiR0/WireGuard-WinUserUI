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
    private const string AuditEnabledValue = "AuditEnabled";
    private const string AuditMaxSizeKbValue = "AuditMaxSizeKb";

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

    public void SetAllUsersDefaultOperator(bool enabled)
    {
        using var key = Registry.LocalMachine.CreateSubKey(RegistryKey, writable: true);
        key.SetValue(AllUsersDefaultOperatorValue, enabled ? 1 : 0, RegistryValueKind.DWord);
    }

    /// <summary>Whether audit logging is globally enabled. Default: true.</summary>
    public bool GetAuditEnabled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(RegistryKey);
            var val = key?.GetValue(AuditEnabledValue);
            // If the value has never been written, default is true (enabled)
            if (val is int v) return v != 0;
            return true;
        }
        catch { return true; }
    }

    public void SetAuditEnabled(bool enabled)
    {
        using var key = Registry.LocalMachine.CreateSubKey(RegistryKey, writable: true);
        key.SetValue(AuditEnabledValue, enabled ? 1 : 0, RegistryValueKind.DWord);
    }

    /// <summary>
    /// Maximum audit log file size in KB. 0 means no limit.
    /// When exceeded, the oldest entries are removed until the file is 10% below the limit.
    /// </summary>
    public int GetAuditMaxSizeKb()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(RegistryKey);
            if (key?.GetValue(AuditMaxSizeKbValue) is int v && v > 0) return v;
            return 0;
        }
        catch { return 0; }
    }

    public void SetAuditMaxSizeKb(int sizeKb)
    {
        using var key = Registry.LocalMachine.CreateSubKey(RegistryKey, writable: true);
        key.SetValue(AuditMaxSizeKbValue, Math.Max(0, sizeKb), RegistryValueKind.DWord);
    }
}
