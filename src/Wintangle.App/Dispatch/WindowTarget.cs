using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using Wintangle.App.Interop;

namespace Wintangle.App.Dispatch;

/// <summary>
/// A validated target window for tiling. <see cref="TryCreate"/> returns null
/// for windows that must never be moved: our own process, shell surfaces,
/// invisible/tool/cloaked windows, and windows smaller than 60x60.
/// </summary>
internal sealed class WindowTarget
{
    private const int MinSize = 60;

    private WindowTarget(IntPtr hwnd, uint processId, string processName)
    {
        Hwnd = hwnd;
        ProcessId = processId;
        ProcessName = processName;
    }

    public IntPtr Hwnd { get; }

    public uint ProcessId { get; }

    /// <summary>Process name without extension (e.g. "notepad"); empty if it couldn't be resolved.</summary>
    public string ProcessName { get; }

    public static WindowTarget? TryCreate(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        // Our own process windows (host window, hidden helpers) are never targets.
        if (WindowApi.GetWindowThreadProcessId(hwnd, out var pid) == 0)
        {
            return null;
        }

        if (pid == (uint)Environment.ProcessId)
        {
            return null;
        }

        var className = new StringBuilder(256);
        if (WindowApi.GetClassNameW(hwnd, className, className.Capacity) == 0)
        {
            return null;
        }

        var cls = className.ToString();
        if (cls is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "Progman" or "WorkerW")
        {
            return null;
        }

        if (!WindowApi.IsWindowVisible(hwnd))
        {
            return null;
        }

        if ((WindowApi.GetWindowLongPtrW(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64() & NativeMethods.WS_EX_TOOLWINDOW) != 0)
        {
            return null;
        }

        if (WindowApi.DwmGetWindowAttribute(hwnd, WindowApi.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0
            && cloaked != 0)
        {
            return null;
        }

        var bounds = GetWindowBounds(hwnd);
        if (bounds.Width < MinSize || bounds.Height < MinSize)
        {
            return null;
        }

        string processName = string.Empty;
        try
        {
            processName = Process.GetProcessById((int)pid).ProcessName;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception)
        {
            // Elevated/other session processes may refuse the query; leave name empty.
        }

        return new WindowTarget(hwnd, pid, processName);
    }

    /// <summary>DWM extended frame bounds (true visual rect), falling back to GetWindowRect.</summary>
    public static Rectangle GetWindowBounds(IntPtr hwnd)
    {
        var rect = new RECT();
        int hr = WindowApi.DwmGetWindowAttribute(hwnd, WindowApi.DWMWA_EXTENDED_FRAME_BOUNDS, out rect, Marshal.SizeOf<RECT>());
        if (hr != 0 || rect.Width <= 0 || rect.Height <= 0)
        {
            WindowApi.GetWindowRect(hwnd, out rect);
        }

        return new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height);
    }
}
