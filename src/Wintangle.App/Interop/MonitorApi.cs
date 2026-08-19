using System.Runtime.InteropServices;

namespace Wintangle.App.Interop;

/// <summary>Display enumeration APIs (user32.dll).</summary>
internal static class MonitorApi
{
    public const int MONITOR_DEFAULTTONEAREST = 2;
    public const int MONITOR_DEFAULTTOPRIMARY = 1;
    public const int MONITOR_DEFAULTTONULL = 0;

    /// <summary>MONITORINFOF_PRIMARY.</summary>
    public const int MONITORINFOF_PRIMARY = 0x00000001;

    /// <summary>
    /// Callback signature for <see cref="EnumDisplayMonitors"/>. Return
    /// FALSE to stop enumeration.
    /// </summary>
    internal delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr lprcClip,
        MonitorEnumProc lpfnEnum,
        IntPtr dwData);

    /// <summary>
    /// Fills <paramref name="lpmi"/> (cbSize must be set first). Returns TRUE
    /// on success.
    /// </summary>
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfoW(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    /// <summary>
    /// Returns a handle to the display that has the largest area of
    /// intersection with the given window's bounding rectangle
    /// (dwFlags = MONITOR_DEFAULTTONEAREST).
    /// </summary>
    [DllImport("user32.dll")]
    internal static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
}
