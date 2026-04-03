namespace WireGuard.UI.Services;

public interface INotificationService
{
    void ShowTunnelConnected(string tunnelName);
    void ShowTunnelDisconnected(string tunnelName);
    void ShowError(string title, string message);
}
