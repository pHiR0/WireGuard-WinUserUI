namespace WireGuard.Shared.IPC;

public static class PipeConstants
{
    public const string PipeName = "WireGuard-WinUserUI";

    /// <summary>
    /// Maximum message size in bytes (64 KB).
    /// </summary>
    public const int MaxMessageSize = 64 * 1024;
}
