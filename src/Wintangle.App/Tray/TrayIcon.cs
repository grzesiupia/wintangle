using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using Wintangle.App.Interop;

namespace Wintangle.App.Tray;

/// <summary>
/// System-tray icon (Shell_NotifyIconW) with balloon notifications.
/// </summary>
/// <remarks>
/// Messages from the shell arrive at the host window as the registered
/// callback message (uCallbackMessage); the host WndProc checks
/// <see cref="IsCallbackMessage"/> and reacts to the mouse message in lParam.
/// </remarks>
internal sealed class TrayIcon : IDisposable
{
    private const uint IconId = 0;
    private const string CallbackMessageName = "Wintangle.TrayCallback";

    private readonly IntPtr _hwnd;
    private readonly uint _callbackMessage;
    private readonly IntPtr _hIcon;

    // All access to _nid is confined to the UI thread: Add/Remove run on the
    // STA main thread and ShowBalloon marshals to it (see ShowBalloon). The
    // flag is volatile because ShowBalloon's early-out reads it from the hook
    // thread before marshaling.
    private TrayApi.NOTIFYICONDATA _nid;
    private volatile bool _added;

    public TrayIcon(IntPtr hwnd)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("TrayIcon requires Windows.");
        }

        _hwnd = hwnd;
        _callbackMessage = TrayApi.RegisterWindowMessageW(CallbackMessageName);
        _hIcon = ExtractAppIcon();
    }

    /// <summary>The registered callback message id for tray notifications.</summary>
    public uint CallbackMessage => _callbackMessage;

    /// <summary>
    /// True when <paramref name="msg"/> is the tray callback for our icon
    /// (<paramref name="wParam"/> carries the icon id).
    /// </summary>
    public bool IsCallbackMessage(uint msg, IntPtr wParam) =>
        msg == _callbackMessage && (uint)wParam.ToInt64() == IconId;

    /// <summary>Adds the icon to the tray. Returns true on success.</summary>
    public bool Add()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            _nid = new TrayApi.NOTIFYICONDATA
            {
                cbSize = (uint)Marshal.SizeOf<TrayApi.NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = IconId,
                uFlags = TrayApi.NIF_MESSAGE | TrayApi.NIF_ICON | TrayApi.NIF_TIP,
                uCallbackMessage = _callbackMessage,
                hIcon = _hIcon,
                szTip = "wintangle",
            };

            _added = TrayApi.Shell_NotifyIconW(TrayApi.NIM_ADD, ref _nid);
            if (!_added)
            {
                Debug.WriteLine($"[wintangle] Shell_NotifyIconW(NIM_ADD) failed: {Marshal.GetLastWin32Error()}");
            }

            return _added;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[wintangle] tray icon add failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>Shows an info balloon; silently no-ops if the icon isn't up.</summary>
    public void ShowBalloon(string title, string text)
    {
        if (!_added || !OperatingSystem.IsWindows())
        {
            return;
        }

        // Marshal to the STA UI thread: this can be invoked from the keyboard
        // hook thread via WindowDispatcher (elevation-skip balloons). All
        // access to _nid is confined to the UI thread (Add/Remove already run
        // there; the balloon path is marshaled here), so no lock is needed.
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            return; // no dispatcher yet, or shutting down — nothing to notify
        }

        if (dispatcher.CheckAccess())
        {
            ShowBalloonCore(title, text);
            return;
        }

        try
        {
            dispatcher.BeginInvoke(() => ShowBalloonCore(title, text));
        }
        catch (Exception ex)
        {
            // Dispatcher can reject work once it has started shutting down.
            Debug.WriteLine($"[wintangle] tray balloon marshal failed: {ex.Message}");
        }
    }

    private void ShowBalloonCore(string title, string text)
    {
        if (!_added || !OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            _nid.uFlags |= TrayApi.NIF_INFO;
            _nid.szInfo = text ?? string.Empty;
            _nid.szInfoTitle = title ?? string.Empty;
            _nid.dwInfoFlags = TrayApi.NIIF_INFO;
            TrayApi.Shell_NotifyIconW(TrayApi.NIM_MODIFY, ref _nid);
            // Reset for subsequent operations.
            _nid.uFlags = TrayApi.NIF_MESSAGE | TrayApi.NIF_ICON | TrayApi.NIF_TIP;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[wintangle] tray balloon failed: {ex.Message}");
        }
    }

    /// <summary>Removes the icon from the tray.</summary>
    public void Remove()
    {
        if (!_added || !OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            TrayApi.Shell_NotifyIconW(TrayApi.NIM_DELETE, ref _nid);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[wintangle] tray icon remove failed: {ex.Message}");
        }
        finally
        {
            _added = false;
        }
    }

    public void Dispose()
    {
        Remove();
        if (_hIcon != IntPtr.Zero && OperatingSystem.IsWindows())
        {
            TrayApi.DestroyIcon(_hIcon);
        }
    }

    /// <summary>
    /// Extracts the small icon from our own executable — the same
    /// Assets/wintangle.ico that is embedded as the ApplicationIcon.
    /// </summary>
    private static IntPtr ExtractAppIcon()
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            return IntPtr.Zero;
        }

        uint extracted = TrayApi.ExtractIconExW(exePath, 0, out var large, out var small, 1);
        if (extracted == 0)
        {
            Debug.WriteLine($"[wintangle] ExtractIconExW failed: {Marshal.GetLastWin32Error()}");
            return IntPtr.Zero;
        }

        if (small != IntPtr.Zero)
        {
            if (large != IntPtr.Zero)
            {
                TrayApi.DestroyIcon(large);
            }

            return small;
        }

        return large;
    }
}
