using WireGuard.UI.ViewModels;
using WireGuard.Shared.Models;

namespace WireGuard.UI.Tests;

public class TunnelViewModelTests
{
    [Theory]
    [InlineData(TunnelStatus.Running, "Conectado")]
    [InlineData(TunnelStatus.Stopped, "Desconectado")]
    [InlineData(TunnelStatus.StartPending, "Conectando...")]
    [InlineData(TunnelStatus.StopPending, "Desconectando...")]
    [InlineData(TunnelStatus.Error, "Error")]
    [InlineData(TunnelStatus.Unknown, "Desconocido")]
    public void StatusText_ReturnsExpectedValue(TunnelStatus status, string expected)
    {
        var vm = new TunnelViewModel { Status = status };
        Assert.Equal(expected, vm.StatusText);
    }

    [Theory]
    [InlineData(TunnelStatus.Stopped, true, false)]
    [InlineData(TunnelStatus.Running, false, true)]
    [InlineData(TunnelStatus.Error, true, false)]
    [InlineData(TunnelStatus.StartPending, false, false)]
    [InlineData(TunnelStatus.StopPending, false, false)]
    public void CanStartStop_ReturnsExpectedValues(TunnelStatus status, bool canStart, bool canStop)
    {
        var vm = new TunnelViewModel { Status = status };
        Assert.Equal(canStart, vm.CanStart);
        Assert.Equal(canStop, vm.CanStop);
    }

    [Fact]
    public void UpdateFrom_UpdatesProperties()
    {
        var vm = new TunnelViewModel
        {
            Name = "test",
            Status = TunnelStatus.Stopped,
        };

        var now = DateTimeOffset.UtcNow;
        vm.UpdateFrom(new TunnelInfo
        {
            Name = "test",
            Status = TunnelStatus.Running,
            LastChecked = now,
        });

        Assert.Equal(TunnelStatus.Running, vm.Status);
        Assert.Equal(now, vm.LastChecked);
    }
}
