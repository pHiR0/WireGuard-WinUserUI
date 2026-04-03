using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WireGuard.Shared.Models;

namespace WireGuard.UI.ViewModels;

public partial class TunnelViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private TunnelStatus _status;

    [ObservableProperty]
    private DateTimeOffset _lastChecked;

    [ObservableProperty]
    private bool _isLoading;

    public bool CanStart => Status is TunnelStatus.Stopped or TunnelStatus.Error or TunnelStatus.Unknown;
    public bool CanStop => Status is TunnelStatus.Running;
    public bool IsPending => Status is TunnelStatus.StartPending or TunnelStatus.StopPending;

    public string StatusText => Status switch
    {
        TunnelStatus.Running => "Conectado",
        TunnelStatus.Stopped => "Desconectado",
        TunnelStatus.StartPending => "Conectando...",
        TunnelStatus.StopPending => "Desconectando...",
        TunnelStatus.Error => "Error",
        _ => "Desconocido",
    };

    public string StatusColor => Status switch
    {
        TunnelStatus.Running => "#4ADE80",
        TunnelStatus.Stopped => "#64748B",
        TunnelStatus.StartPending or TunnelStatus.StopPending => "#FBBF24",
        TunnelStatus.Error => "#F87171",
        _ => "#64748B",
    };

    partial void OnStatusChanged(TunnelStatus value)
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(IsPending));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusColor));
    }

    public void UpdateFrom(TunnelInfo info)
    {
        Status = info.Status;
        LastChecked = info.LastChecked;
    }
}
