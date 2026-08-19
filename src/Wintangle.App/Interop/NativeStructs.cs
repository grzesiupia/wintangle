using System.Runtime.InteropServices;

namespace Wintangle.App.Interop;

/// <summary>Win32 RECT (LONG left/top/right/bottom, exclusive right/bottom).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public RECT(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

/// <summary>Win32 POINT (LONG x/y).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
    public int X;
    public int Y;
}

/// <summary>
/// Win32 MONITORINFOEX — must be zeroed and <see cref="cbSize"/> set to
/// <see cref="Marshal.SizeOf{T}"/> before passing to GetMonitorInfoW.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct MONITORINFOEX
{
    public int cbSize;
    public RECT rcMonitor;
    public RECT rcWork;
    public int dwFlags;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string szDevice;
}

/// <summary>
/// Win32 WINDOWPLACEMENT. <see cref="length"/> must be set to the struct
/// size before calling GetWindowPlacement. <see cref="rcDevice"/> is written
/// only when the caller allocates the extended struct (Win10 1809+).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct WINDOWPLACEMENT
{
    public int length;
    public int flags;
    public int showCmd;
    public POINT ptMinPosition;
    public POINT ptMaxPosition;
    public RECT rcNormalPosition;
    public RECT rcDevice;
}

/// <summary>Win32 MINMAXINFO (used by WM_GETMINMAXINFO).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MINMAXINFO
{
    public POINT ptReserved;
    public POINT ptMaxSize;
    public POINT ptMaxPosition;
    public POINT ptMinTrackSize;
    public POINT ptMaxTrackSize;
}

/// <summary>Win32 KBDLLHOOKSTRUCT (low-level keyboard hook, used in later phases).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KBDLLHOOKSTRUCT
{
    public uint vkCode;
    public uint scanCode;
    public uint flags;
    public uint time;
    public IntPtr dwExtraInfo;
}

/// <summary>Win32 MSG (message-queue structure used by the GetMessageW loop).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MSG
{
    public IntPtr hwnd;
    public uint message;
    public IntPtr wParam;
    public IntPtr lParam;
    public uint time;
    public POINT pt;
}

/// <summary>Win32 TOKEN_ELEVATION (used with TokenElevation=20).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct TOKEN_ELEVATION
{
    public int TokenIsElevated;
}
