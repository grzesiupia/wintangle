using System.Diagnostics;
using Wintangle.App.Interop;

namespace Wintangle.App.Services;

/// <summary>
/// Enumerates the user-visible top-level windows for the settings UI's active
/// windows list. Win32-only; returns an empty list on non-Windows.
/// </summary>
/// <remarks>
/// Filters to windows a user could reasonably tile: visible, not cloaked,
/// with a non-empty title, not tool windows, and not owned by this process.
/// Per-window failures are skipped silently — one hostile window must never
/// break the whole enumeration.
/// </remarks>
internal static class ActiveWindows
{
    /// <summary>
    /// Visible window labels in "ProcessName.exe — title" form, de-duplicated,
    /// in top-down (z-order) enumeration order. Never throws.
    /// </summary>
    public static List<string> Enumerate()
    {
        var result = new List<string>();
        if (!OperatingSystem.IsWindows())
        {
            return result;
        }

        var ownPid = (uint)Environment.ProcessId;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            WindowApi.EnumWindows((hwnd, _) =>
            {
                try
                {
                    if (!WindowApi.IsWindowVisible(hwnd))
                    {
                        return true;
                    }

                    if (IsCloaked(hwnd))
                    {
                        return true;
                    }

                    var title = GetTitle(hwnd);
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        return true;
                    }

                    var exStyle = WindowApi.GetWindowLongPtrW(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
                    if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0)
                    {
                        return true;
                    }

                    if (WindowApi.GetWindowThreadProcessId(hwnd, out var pid) == 0 || pid == 0)
                    {
                        return true;
                    }

                    if (pid == ownPid)
                    {
                        return true;
                    }

                    var processName = GetProcessName(pid);
                    if (string.IsNullOrWhiteSpace(processName))
                    {
                        return true;
                    }

                    var fileName = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        ? processName
                        : processName + ".exe";
                    var label = $"{fileName} — {title}";
                    if (seen.Add(label))
                    {
                        result.Add(label);
                    }
                }
                catch (Exception)
                {
                    // Skip this window; keep enumerating.
                }

                return true;
            }, IntPtr.Zero);
        }
        catch (Exception)
        {
            // EnumWindows itself failed — return whatever we collected.
        }

        return result;
    }

    /// <summary>True when DWM reports the window as cloaked (virtual desktop / shell-managed).</summary>
    private static bool IsCloaked(IntPtr hwnd)
    {
        try
        {
            return WindowApi.DwmGetWindowAttribute(hwnd, WindowApi.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0
                && cloaked != 0;
        }
        catch (Exception)
        {
            return false; // DWM query failed — treat as visible
        }
    }

    private static string GetTitle(IntPtr hwnd)
    {
        var buffer = new System.Text.StringBuilder(512);
        WindowApi.GetWindowTextW(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static string? GetProcessName(uint pid)
    {
        try
        {
            return Process.GetProcessById((int)pid).ProcessName;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}
