using Avalonia;
using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WireGuard.UI;

sealed class Program
{
    private const string MutexName  = "WireGuardManager_SingleInstance_pHiR0_v1";
    private const string ShowPipe   = "WireGuardManagerShow_pHiR0_v1";
    private const int    DuplicateGuardIntervalMs = 30_000; // check every 30 s

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

        // Start periodic duplicate-session guard (handles race conditions at simultaneous launch).
        _ = Task.Run(() => DuplicateGuardLoopAsync(cts.Token));

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

    /// <summary>
    /// Periodically checks if there are multiple instances of this app running in the same
    /// Windows session (same user). If so, the one with the most recent start time exits.
    /// This handles the edge case where two instances launch simultaneously and both pass
    /// the mutex check before one acquires it.
    /// </summary>
    private static async Task DuplicateGuardLoopAsync(CancellationToken ct)
    {
        // Wait a bit on first run to let the app initialize fully
        try { await Task.Delay(DuplicateGuardIntervalMs, ct); }
        catch (OperationCanceledException) { return; }

        var currentProcess = Process.GetCurrentProcess();
        int currentSessionId = currentProcess.SessionId;
        int currentPid = currentProcess.Id;

        DateTime currentStartTime;
        try { currentStartTime = currentProcess.StartTime; }
        catch { return; } // Can't determine start time — can't compare, bail out

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var peers = Process.GetProcessesByName(currentProcess.ProcessName)
                    .Where(p => p.Id != currentPid && p.SessionId == currentSessionId)
                    .ToList();

                foreach (var p in peers)
                {
                    try
                    {
                        DateTime peerStart = p.StartTime;
                        // If a peer started BEFORE us, we are the duplicate — exit gracefully.
                        if (peerStart < currentStartTime)
                        {
                            // Signal the older instance to show itself before we exit
                            SignalExistingInstance();
                            Environment.Exit(0);
                            return;
                        }
                    }
                    catch { /* access denied or process gone — skip */ }
                    finally { p.Dispose(); }
                }
            }
            catch { /* ignore any enumeration errors */ }

            try { await Task.Delay(DuplicateGuardIntervalMs, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
