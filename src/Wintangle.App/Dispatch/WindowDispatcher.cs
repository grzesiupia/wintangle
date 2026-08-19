using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using Wintangle.App.Interop;
using Wintangle.Core.Geometry;
using Wintangle.Core.Hotkeys;

namespace Wintangle.App.Dispatch;

/// <summary>
/// Applies a <see cref="HotkeyAction"/> to the foreground window. Never
/// throws; every failure is logged via Debug.WriteLine (balloons for the
/// user-visible elevation skips) and the call returns.
/// </summary>
/// <remarks>
/// Win32-only. The constructor and all methods are safe no-ops on non-Windows.
/// </remarks>
internal sealed class WindowDispatcher
{
    private readonly RuntimeState _state;
    private readonly Action<string, string>? _showBalloon;
    private readonly bool _selfElevated;

    public WindowDispatcher(RuntimeState state, Action<string, string>? showBalloon)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _showBalloon = showBalloon;

        // Cached once at startup: our own elevation never changes mid-session.
        _selfElevated = OperatingSystem.IsWindows() && ElevationApi.IsProcessElevated();
    }

    public void Apply(HotkeyAction action, GapSettings gaps)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            ApplyCore(action, gaps);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[wintangle] Apply({action}) failed: {ex}");
        }
    }

    private void ApplyCore(HotkeyAction action, GapSettings gaps)
    {
        var hwnd = WindowApi.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var target = WindowTarget.TryCreate(hwnd);
        if (target == null)
        {
            return;
        }

        if (_state.IsIgnored(target.ProcessName))
        {
            return;
        }

        if (!ElevationPasses(target))
        {
            return;
        }

        if (action is HotkeyAction.PrevMonitor or HotkeyAction.NextMonitor)
        {
            MoveToAdjacentMonitor(hwnd, action == HotkeyAction.NextMonitor ? 1 : -1);
            return;
        }

        var slot = action.ToSlotLayout();
        if (slot == null)
        {
            return; // unreachable: the two monitor actions are handled above
        }

        RestoreIfMaximized(hwnd);

        var monitor = MonitorApi.MonitorFromWindow(hwnd, MonitorApi.MONITOR_DEFAULTTONEAREST);
        if (!TryGetWorkArea(monitor, out var workArea))
        {
            return;
        }

        var rect = SlotCalculator.ComputeSlot(workArea, slot.Value, gaps);
        rect = ApplyMinMaxClamp(hwnd, rect);
        SetWindowPos(hwnd, rect);
    }

    // ---- Elevation guard ----

    /// <summary>
    /// True when the target can be moved from this session: either we are
    /// elevated, or the target is not elevated. Skips with a balloon
    /// otherwise (and on OpenProcess/OpenProcessToken failures).
    /// </summary>
    private bool ElevationPasses(WindowTarget target)
    {
        if (_selfElevated)
        {
            return true; // elevated session can move any window
        }

        IntPtr hProcess = IntPtr.Zero;
        IntPtr hToken = IntPtr.Zero;
        try
        {
            hProcess = ElevationApi.OpenProcess(ElevationApi.PROCESS_QUERY_LIMITED_INFORMATION, false, target.ProcessId);
            if (hProcess == IntPtr.Zero)
            {
                Balloon("wintangle", "Can't inspect that window's process — skipped.");
                return false;
            }

            if (!ElevationApi.OpenProcessToken(hProcess, ElevationApi.TOKEN_QUERY, out hToken) || hToken == IntPtr.Zero)
            {
                Balloon("wintangle", "Can't inspect that window's elevation — skipped.");
                return false;
            }

            if (!ElevationApi.GetTokenInformation(
                    hToken,
                    ElevationApi.TokenElevation,
                    out var elevation,
                    (uint)Marshal.SizeOf<TOKEN_ELEVATION>(),
                    out _))
            {
                Balloon("wintangle", "Can't read that window's elevation — skipped.");
                return false;
            }

            if (elevation.TokenIsElevated != 0)
            {
                Balloon("wintangle", "wintangle can't move elevated windows from a non-elevated session.");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[wintangle] elevation check failed: {ex.Message}");
            return false;
        }
        finally
        {
            if (hToken != IntPtr.Zero)
            {
                ElevationApi.CloseHandle(hToken);
            }

            if (hProcess != IntPtr.Zero)
            {
                ElevationApi.CloseHandle(hProcess);
            }
        }
    }

    // ---- Monitor move (PrevMonitor / NextMonitor) ----

    private void MoveToAdjacentMonitor(IntPtr hwnd, int direction)
    {
        RestoreIfMaximized(hwnd);

        var screens = EnumerateOrderedScreens();
        if (screens.Count == 0)
        {
            return;
        }

        var current = MonitorApi.MonitorFromWindow(hwnd, MonitorApi.MONITOR_DEFAULTTONEAREST);
        int index = screens.FindIndex(s => s.Monitor == current);
        if (index < 0)
        {
            index = 0;
        }

        int targetIndex = Math.Clamp(index + direction, 0, screens.Count - 1);
        if (targetIndex == index)
        {
            return; // single monitor or already at the edge
        }

        if (!TryGetWorkArea(screens[index].Monitor, out var oldWork)
            || !TryGetWorkArea(screens[targetIndex].Monitor, out var newWork))
        {
            return;
        }

        var oldRect = WindowTarget.GetWindowBounds(hwnd);
        var translated = SlotTranslator.TranslateSlot(oldWork, newWork, oldRect);
        var clamped = SlotTranslator.ClampToWorkArea(translated, newWork);
        SetWindowPos(hwnd, ApplyMinMaxClamp(hwnd, clamped));
    }

    /// <summary>
    /// All monitors as <see cref="ScreenInfo"/>, ordered by
    /// <see cref="ScreenLayout.OrderScreens"/> (primary first, then X/Y).
    /// </summary>
    private static List<(IntPtr Monitor, ScreenInfo Info)> EnumerateOrderedScreens()
    {
        var raw = new List<(IntPtr Monitor, ScreenInfo Info)>();

        MonitorApi.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr _, ref RECT _, IntPtr _) =>
        {
            var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (MonitorApi.GetMonitorInfoW(hMonitor, ref mi))
            {
                raw.Add((hMonitor, new ScreenInfo(
                    new Rectangle(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Width, mi.rcMonitor.Height),
                    new Rectangle(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Width, mi.rcWork.Height),
                    (mi.dwFlags & MonitorApi.MONITORINFOF_PRIMARY) != 0,
                    mi.szDevice)));
            }

            return true;
        }, IntPtr.Zero);

        var ordered = ScreenLayout.OrderScreens(raw.Select(r => r.Info)).ToList();

        // Re-attach the native monitor handle using the ScreenInfo value
        // equality (DeviceName makes entries unique).
        var result = new List<(IntPtr Monitor, ScreenInfo Info)>(raw.Count);
        foreach (var info in ordered)
        {
            result.Add((raw.First(r => r.Info == info).Monitor, info));
        }

        return result;
    }

    // ---- Shared helpers ----

    private static bool TryGetWorkArea(IntPtr monitor, out Rectangle workArea)
    {
        var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
        if (monitor == IntPtr.Zero || !MonitorApi.GetMonitorInfoW(monitor, ref mi))
        {
            workArea = default;
            return false;
        }

        workArea = new Rectangle(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.Width, mi.rcWork.Height);
        return true;
    }

    private static void RestoreIfMaximized(IntPtr hwnd)
    {
        var placement = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
        if (WindowApi.GetWindowPlacement(hwnd, ref placement) && placement.showCmd == NativeMethods.SW_SHOWMAXIMIZED)
        {
            WindowApi.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
        }
    }

    /// <summary>
    /// Clamps the rect to the window's min/max track sizes (WM_GETMINMAXINFO,
    /// aborts if the window is hung). Windows that don't answer are moved
    /// best-effort without clamping.
    /// </summary>
    private static Rectangle ApplyMinMaxClamp(IntPtr hwnd, Rectangle rect)
    {
        var mmi = new MINMAXINFO();
        var sent = WindowApi.SendMessageTimeoutW(
            hwnd,
            NativeMethods.WM_GETMINMAXINFO,
            IntPtr.Zero,
            ref mmi,
            NativeMethods.SMTO_ABORTIFHUNG,
            NativeMethods.SendMessageTimeoutDefaultTimeout,
            out _);

        if (sent == IntPtr.Zero)
        {
            return rect; // hung/non-responder: proceed best-effort
        }

        var minTrack = new Size(mmi.ptMinTrackSize.X, mmi.ptMinTrackSize.Y);
        var maxTrack = new Size(mmi.ptMaxTrackSize.X, mmi.ptMaxTrackSize.Y);
        return SlotTranslator.ApplyMinMax(rect, minTrack, maxTrack);
    }

    private static void SetWindowPos(IntPtr hwnd, Rectangle rect)
    {
        WindowApi.SetWindowPos(
            hwnd,
            IntPtr.Zero,
            rect.X,
            rect.Y,
            rect.Width,
            rect.Height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOOWNERZORDER);
    }

    private void Balloon(string title, string text)
    {
        try
        {
            _showBalloon?.Invoke(title, text);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[wintangle] balloon failed: {ex.Message}");
        }
    }
}
