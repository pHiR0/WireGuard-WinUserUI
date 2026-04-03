using System;
using System.Runtime.InteropServices;
using Microsoft.Toolkit.Uwp.Notifications;

namespace WireGuard.UI.Services;

/// <summary>
/// Sends Windows 11 toast notifications using Microsoft.Toolkit.Uwp.Notifications.
/// Falls back silently if running on an unsupported platform.
/// </summary>
public sealed class WindowsNotificationService : INotificationService
{
    private readonly bool _supported;

    public WindowsNotificationService()
    {
        _supported = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        if (_supported)
        {
            try
            {
                // Register the app with Windows notification system (for unpackaged apps)
                ToastNotificationManagerCompat.OnActivated += _ => { };
            }
            catch { _supported = false; }
        }
    }

    public void ShowTunnelConnected(string tunnelName)
    {
        if (!_supported) return;
        ShowToast(
            "Túnel conectado",
            $"El túnel «{tunnelName}» se ha conectado correctamente.",
            "ms-appx:///Assets/icon.ico");
    }

    public void ShowTunnelDisconnected(string tunnelName)
    {
        if (!_supported) return;
        ShowToast(
            "Túnel desconectado",
            $"El túnel «{tunnelName}» se ha desconectado.",
            "ms-appx:///Assets/icon.ico");
    }

    public void ShowError(string title, string message)
    {
        if (!_supported) return;
        ShowToast(title, message);
    }

    private static void ShowToast(string title, string body, string? logoUri = null)
    {
        try
        {
            var builder = new ToastContentBuilder()
                .AddText(title)
                .AddText(body);

            if (logoUri is not null)
                builder.AddAppLogoOverride(new Uri(logoUri));

            builder.Show();
        }
        catch { /* silently ignore if notifications are not available */ }
    }
}
