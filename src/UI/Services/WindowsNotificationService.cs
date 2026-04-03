using System;
using System.Runtime.InteropServices;
using Microsoft.Toolkit.Uwp.Notifications;

namespace WireGuard.UI.Services;

/// <summary>
/// Sends Windows 11 toast notifications using Microsoft.Toolkit.Uwp.Notifications.
/// For unpackaged Win32 apps the library handles registration automatically.
/// Calls are always wrapped in try/catch so failures are silent.
/// </summary>
public sealed class WindowsNotificationService : INotificationService
{
    // Set the Application User Model ID so Windows knows who is sending the toast.
    // Required for unpackaged desktop apps.
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    public WindowsNotificationService()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            SetCurrentProcessExplicitAppUserModelID("WireGuard.WinUserUI");
    }

    public void ShowTunnelConnected(string tunnelName) =>
        ShowToast("Túnel conectado", $"El túnel «{tunnelName}» se ha conectado correctamente.");

    public void ShowTunnelDisconnected(string tunnelName) =>
        ShowToast("Túnel desconectado", $"El túnel «{tunnelName}» se ha desconectado.");

    public void ShowError(string title, string message) =>
        ShowToast(title, message);

    private static void ShowToast(string title, string body)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        try
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(body)
                .Show();
        }
        catch { /* silently ignore */ }
    }
}
