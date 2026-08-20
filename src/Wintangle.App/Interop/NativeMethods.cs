namespace Wintangle.App.Interop;

/// <summary>
/// Shared Win32 constants and handle values used across the interop layer.
/// P/Invoke declarations live in the per-area classes (Dpi, MonitorApi,
/// WindowApi, ElevationApi, TrayApi).
/// </summary>
internal static class NativeMethods
{
    public const int GWL_EXSTYLE = -20;

    // ---- Extended window styles (WS_EX_*) ----
    public const long WS_EX_TOOLWINDOW = 0x00000080L;

    // ---- SetWindowPos flags (SWP_*) ----
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_NOOWNERZORDER = 0x0200;

    // ---- ShowWindow commands (SW_*) ----
    public const int SW_SHOWMAXIMIZED = 3;
    public const int SW_RESTORE = 9;

    // ---- Windows messages (WM_*) ----
    public const uint WM_NULL = 0x0000;
    public const uint WM_QUIT = 0x0012;
    public const uint WM_SETTINGCHANGE = 0x001A;
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_KEYUP = 0x0101;
    public const uint WM_SYSKEYDOWN = 0x0104;
    public const uint WM_SYSKEYUP = 0x0105;
    public const uint WM_COMMAND = 0x0111;
    public const uint WM_GETMINMAXINFO = 0x0024;
    public const uint WM_CONTEXTMENU = 0x007B;
    public const uint WM_RBUTTONUP = 0x0205;
    public const uint WM_THEMECHANGED = 0x031A;

    // ---- Application-defined messages (WM_APP + n) ----
    public const uint WM_APP = 0x8000;

    /// <summary>Single-instance signal: "show the settings window".</summary>
    public const uint WM_APP_SHOW_SETTINGS = WM_APP + 1;

    // ---- Virtual key codes ----
    public const byte VK_ESCAPE = 0x1B;

    /// <summary>
    /// Dummy key injected between a swallowed Win combo's Win keydown and its
    /// Win keyup, so the shell never sees a "clean" Win release and doesn't
    /// pop the Start menu. No real app binds F24.
    /// </summary>
    public const byte VK_F24 = 0x87;

    // ---- SendMessageTimeout flags ----
    public const uint SMTO_ABORTIFHUNG = 0x0002;
    public const int SendMessageTimeoutDefaultTimeout = 500;

    // ---- Low-level keyboard hook (WH_KEYBOARD_LL) ----
    public const int WH_KEYBOARD_LL = 13;
    public const uint LLKHF_INJECTED = 0x00000010;

    // ---- Modifier virtual key codes ----
    public const byte VK_SHIFT = 0x10;
    public const byte VK_CONTROL = 0x11;
    public const byte VK_MENU = 0x12;
    public const byte VK_LWIN = 0x5B;
    public const byte VK_RWIN = 0x5C;
    public const byte VK_LSHIFT = 0xA0;
    public const byte VK_RSHIFT = 0xA1;
    public const byte VK_LCONTROL = 0xA2;
    public const byte VK_RCONTROL = 0xA3;
    public const byte VK_LMENU = 0xA4;
    public const byte VK_RMENU = 0xA5;
}
