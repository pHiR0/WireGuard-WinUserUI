using Avalonia;
using System;
using System.IO.Pipes;
using System.Threading;

namespace WireGuard.UI;

sealed class Program
{
    private const string MutexName  = "WireGuardManager_SingleInstance_pHiR0_v1";
    private const string ShowPipe   = "WireGuardManagerShow_pHiR0_v1";

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        using var mutex = new Mutex(true, MutexName, out var isNewInstance);

        if (!isNewInstance)
        {
            // Another instance is running — signal it to show its window and exit.
            SignalExistingInstance();
            return;
        }

        // Start a background thread that listens for "show" signals from future instances.
        var cts = new CancellationTokenSource();
        var listenerThread = new Thread(() => ListenForShowSignals(cts.Token))
        {
            IsBackground = true,
            Name = "ShowSignalListener"
        };
        listenerThread.Start();

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            cts.Cancel();
        }
    }

    /// <summary>Connects to the running instance's named pipe and sends a "show" byte.</summary>
    private static void SignalExistingInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", ShowPipe, PipeDirection.Out);
            client.Connect(1500);
            client.WriteByte(1);
        }
        catch { /* existing instance may have just exited — ignore */ }
    }

    /// <summary>
    /// Keeps a named pipe server alive and calls App.RequestShowMainWindow() each time a
    /// new instance connects.
    /// </summary>
    private static void ListenForShowSignals(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(ShowPipe, PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    transmissionMode: PipeTransmissionMode.Byte,
                    options: PipeOptions.Asynchronous);

                server.WaitForConnection();
                _ = server.ReadByte(); // consume the signal byte

                App.RequestShowMainWindow();
            }
            catch (OperationCanceledException) { break; }
            catch { /* server closed or error — loop and recreate */ }
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
