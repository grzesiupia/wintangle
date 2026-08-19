using System.Diagnostics;
using Wintangle.App.Interop;
using Wintangle.App.Services;
using Wintangle.App.UI;
using Wintangle.Core.Geometry;
using Wintangle.Core.Hotkeys;

namespace Wintangle.App.Tray;

/// <summary>
/// Right-click context menu for the tray icon. Rebuilt from scratch on every
/// right-click; command ids are returned synchronously via TPM_RETURNCMD and
/// dispatched immediately (no WM_COMMAND plumbing needed).
/// </summary>
internal sealed class TrayMenu
{
    private const uint IdBase = 1;

    private const uint IdPrevMonitor = IdBase + 16;
    private const uint IdNextMonitor = IdBase + 17;
    private const uint IdIgnoreApp = IdBase + 18;
    private const uint IdAutostart = IdBase + 19;
    private const uint IdSettings = IdBase + 20;
    private const uint IdQuit = IdBase + 21;

    /// <summary>The 16 slot actions, in default-table order.</summary>
    private static readonly HotkeyAction[] s_slotActions =
    {
        HotkeyAction.CenterHalf,
        HotkeyAction.HalfLeft,
        HotkeyAction.HalfRight,
        HotkeyAction.QuarterTopLeft,
        HotkeyAction.QuarterTopRight,
        HotkeyAction.QuarterBottomLeft,
        HotkeyAction.QuarterBottomRight,
        HotkeyAction.ThirdLeft,
        HotkeyAction.ThirdCenter,
        HotkeyAction.ThirdRight,
        HotkeyAction.SixthTopLeft,
        HotkeyAction.SixthTopCenter,
        HotkeyAction.SixthTopRight,
        HotkeyAction.SixthBottomLeft,
        HotkeyAction.SixthBottomCenter,
        HotkeyAction.SixthBottomRight,
    };

    private readonly IntPtr _hostHwnd;
    private readonly RuntimeState _state;
    private readonly ConfigService _config;
    private readonly Action<HotkeyAction, GapSettings> _apply;
    private readonly Action _quit;
    private readonly Action _showSettings;

    public TrayMenu(
        IntPtr hostHwnd,
        RuntimeState state,
        ConfigService config,
        Action<HotkeyAction, GapSettings> apply,
        Action quit,
        Action showSettings)
    {
        _hostHwnd = hostHwnd;
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _quit = quit ?? throw new ArgumentNullException(nameof(quit));
        _showSettings = showSettings ?? throw new ArgumentNullException(nameof(showSettings));
    }

    public void Show()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var menu = TrayApi.CreatePopupMenu();
        if (menu == IntPtr.Zero)
        {
            return;
        }

        try
        {
            Build(menu);
            TrayApi.GetCursorPos(out var pt);

            // Classic tray-menu dance: the menu must not steal activation from
            // the calling window, and the WM_NULL message dismisses the menu
            // immediately after a selection.
            TrayApi.SetForegroundWindow(_hostHwnd);
            uint command = TrayApi.TrackPopupMenu(
                menu,
                TrayApi.TPM_RETURNCMD | TrayApi.TPM_LEFTALIGN | TrayApi.TPM_BOTTOMALIGN,
                pt.X,
                pt.Y,
                0,
                _hostHwnd,
                IntPtr.Zero);
            TrayApi.PostMessageW(_hostHwnd, NativeMethods.WM_NULL, IntPtr.Zero, IntPtr.Zero);

            HandleCommand(command);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[wintangle] tray menu failed: {ex.Message}");
        }
        finally
        {
            TrayApi.DestroyMenu(menu);
        }
    }

    private void Build(IntPtr menu)
    {
        uint id = IdBase;
        foreach (var action in s_slotActions)
        {
            TrayApi.AppendMenuW(menu, TrayApi.MF_STRING, new UIntPtr(id++), LabelFor(action));
        }

        TrayApi.AppendMenuW(menu, TrayApi.MF_SEPARATOR, UIntPtr.Zero, null);
        TrayApi.AppendMenuW(menu, TrayApi.MF_STRING, new UIntPtr(IdPrevMonitor), "Move to Previous Monitor");
        TrayApi.AppendMenuW(menu, TrayApi.MF_STRING, new UIntPtr(IdNextMonitor), "Move to Next Monitor");

        TrayApi.AppendMenuW(menu, TrayApi.MF_SEPARATOR, UIntPtr.Zero, null);

        var ignoreFlags = TrayApi.MF_STRING;
        var ignoreLabel = "Ignore this app";
        var foreground = GetForegroundProcessName();
        if (foreground != null)
        {
            ignoreLabel = $"Ignore {foreground}";
            if (_state.IsIgnored(foreground))
            {
                ignoreFlags |= TrayApi.MF_CHECKED;
            }
        }

        TrayApi.AppendMenuW(menu, ignoreFlags, new UIntPtr(IdIgnoreApp), ignoreLabel);

        var autostartFlags = TrayApi.MF_STRING;
        if (_config.GetAutoStartEnabled())
        {
            autostartFlags |= TrayApi.MF_CHECKED;
        }

        TrayApi.AppendMenuW(menu, autostartFlags, new UIntPtr(IdAutostart), "Autostart");

        TrayApi.AppendMenuW(menu, TrayApi.MF_SEPARATOR, UIntPtr.Zero, null);
        TrayApi.AppendMenuW(menu, TrayApi.MF_STRING, new UIntPtr(IdSettings), "Settings…");
        TrayApi.AppendMenuW(menu, TrayApi.MF_STRING, new UIntPtr(IdQuit), "Quit");
    }

    private void HandleCommand(uint command)
    {
        if (command >= IdBase && command < IdBase + s_slotActions.Length)
        {
            _apply(s_slotActions[command - IdBase], _state.Gaps);
            return;
        }

        switch (command)
        {
            case IdPrevMonitor:
                _apply(HotkeyAction.PrevMonitor, _state.Gaps);
                break;

            case IdNextMonitor:
                _apply(HotkeyAction.NextMonitor, _state.Gaps);
                break;

            case IdIgnoreApp:
                ToggleIgnoreCurrentApp();
                break;

            case IdAutostart:
                _config.ToggleAutoStart();
                break;

            case IdSettings:
                _showSettings();
                break;

            case IdQuit:
                _quit();
                break;
        }
    }

    private string LabelFor(HotkeyAction action)
    {
        // Show the effective binding (custom override or default) so the tray
        // label stays in sync after a rebind. Fall back to the default label
        // for an unknown action.
        var hotkey = _config.GetShortcut(action);
        var label = hotkey is { } effective ? HotkeyLabels.Format(effective) : DefaultHotkeys.Format(action);
        return $"{UiLabels.ActionName(action)} — {label}";
    }

    private void ToggleIgnoreCurrentApp()
    {
        var name = GetForegroundProcessName();
        if (name == null)
        {
            return;
        }

        if (_state.IsIgnored(name))
        {
            _config.RemoveIgnored(name);
        }
        else
        {
            _config.AddIgnored(name);
        }
    }

    private static string? GetForegroundProcessName()
    {
        var hwnd = WindowApi.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        if (WindowApi.GetWindowThreadProcessId(hwnd, out var pid) == 0 || pid == 0)
        {
            return null;
        }

        if (pid == (uint)Environment.ProcessId)
        {
            return null;
        }

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
