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

    // Stats (populated when tunnel is Running and wg.exe is available)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetails))]
    private string? _tunnelAddress;

    [ObservableProperty]
    private string? _endpoint;

    [ObservableProperty]
    private string? _lastHandshake;

    [ObservableProperty]
    private long _rxBytes;

    [ObservableProperty]
    private long _txBytes;

    public bool CanStart => Status is TunnelStatus.Stopped or TunnelStatus.Error or TunnelStatus.Unknown;
    public bool CanStop => Status is TunnelStatus.Running;
    public bool IsPending => Status is TunnelStatus.StartPending or TunnelStatus.StopPending;
    public bool HasDetails => !string.IsNullOrEmpty(TunnelAddress) || !string.IsNullOrEmpty(Endpoint);

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

    public string TransferText
    {
        get
        {
            if (RxBytes == 0 && TxBytes == 0) return string.Empty;
            return $"↓ {FormatBytes(RxBytes)}  ↑ {FormatBytes(TxBytes)}";
        }
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F2} KiB",
            < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F2} MiB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GiB",
        };
    }

    partial void OnStatusChanged(TunnelStatus value)
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(IsPending));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusColor));
    }

    partial void OnRxBytesChanged(long value) => OnPropertyChanged(nameof(TransferText));
    partial void OnTxBytesChanged(long value) => OnPropertyChanged(nameof(TransferText));

    public void UpdateFrom(TunnelInfo info)
    {
        Status = info.Status;
        LastChecked = info.LastChecked;
        TunnelAddress = info.TunnelAddress;
        Endpoint = info.Endpoint;
        LastHandshake = info.LastHandshake;
        RxBytes = info.RxBytes;
        TxBytes = info.TxBytes;
    }
}

