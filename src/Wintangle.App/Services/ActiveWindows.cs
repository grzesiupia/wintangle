using System.Diagnostics;
using Wintangle.App.Interop;

namespace Wintangle.App.Services;

/// <summary>One enumerable top-level window (process label, title, pid, elevation).</summary>
internal readonly record struct ActiveWindowInfo(string ProcessName, string Title, uint ProcessId, bool IsElevated);

/// <summary>
/// Enumerates the user-visible top-level windows for the settings UI's active
/// windows list. Win32-only; returns an empty list on non-Windows.
/// </summary>
/// <remarks>
/// Filters to windows a user could reasonably tile: visible, not cloaked,
/// with a non-empty title, not tool windows, and not owned by this process.
/// Per-window failures are skipped silently — one hostile window must never
/// break the whole enumeration. Elevation is queried once per process id per
/// enumeration (the token query is relatively expensive and several windows
/// often share a pid).
/// </remarks>
internal static class ActiveWindows
{
    /// <summary>
    /// Visible windows in top-down (z-order) enumeration order, de-duplicated
    /// by (process, title). Never throws.
    /// </summary>
    public static List<ActiveWindowInfo> Enumerate()
    {
        var result = new List<ActiveWindowInfo>();
        if (!OperatingSystem.IsWindows())
        {
            return result;
        }

        var ownPid = (uint)Environment.ProcessId;
        var seen = new HashSet<(string Process, string Title)>();
        var elevationCache = new Dictionary<uint, bool>();

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
                    if (seen.Add((fileName, title)))
                    {
                        result.Add(new ActiveWindowInfo(fileName, title, pid, IsElevated(pid, elevationCache)));
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

    /// <summary>
    /// Elevation for <paramref name="pid"/>, cached per enumeration. Query
    /// failures (access denied, exited process) are treated as not elevated.
    /// </summary>
    private static bool IsElevated(uint pid, Dictionary<uint, bool> cache)
    {
        if (cache.TryGetValue(pid, out var known))
        {
            return known;
        }

        bool elevated;
        try
        {
            elevated = ElevationApi.IsProcessElevated(pid);
        }
        catch (Exception)
        {
            elevated = false;
        }

        cache[pid] = elevated;
        return elevated;
    }
}
