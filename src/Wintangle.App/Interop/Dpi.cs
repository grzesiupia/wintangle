using System.Runtime.InteropServices;

namespace Wintangle.App.Interop;

/// <summary>DPI awareness and querying APIs (user32.dll).</summary>
internal static class Dpi
{
    /// <summary>DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2.</summary>
    public static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new(-4);

    /// <summary>
    /// Sets the per-monitor DPI awareness context for the calling thread.
    /// Returns TRUE on success. (Backup to the app.manifest declaration.)
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetProcessDpiAwarenessContext(IntPtr value);
}
