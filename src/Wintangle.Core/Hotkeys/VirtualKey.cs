namespace Wintangle.Core.Hotkeys;

/// <summary>
/// Virtual-key codes used by the default hotkey table. Pure constants (no
/// Win32 interop) so the default configuration lives entirely in the Core.
/// </summary>
public static class VirtualKey
{
    public const byte VK_LEFT = 0x25;
    public const byte VK_RIGHT = 0x27;

    /// <summary>Enter</summary>
    public const byte VK_RETURN = 0x0D;

    public const byte VK_I = 0x49;
    public const byte VK_J = 0x4A;
    public const byte VK_K = 0x4B;
    public const byte VK_L = 0x4C;
    public const byte VK_O = 0x4F;
    public const byte VK_P = 0x50;
    public const byte VK_C = 0x43;

    /// <summary>OEM ';'</summary>
    public const byte VK_OEM_1 = 0xBA;

    /// <summary>OEM ','</summary>
    public const byte VK_OEM_COMMA = 0xBC;

    /// <summary>OEM '.'</summary>
    public const byte VK_OEM_PERIOD = 0xBE;

    /// <summary>OEM '/'</summary>
    public const byte VK_OEM_2 = 0xBF;

    /// <summary>OEM '['</summary>
    public const byte VK_OEM_4 = 0xDB;

    /// <summary>OEM ']'</summary>
    public const byte VK_OEM_6 = 0xDD;

    /// <summary>OEM '''</summary>
    public const byte VK_OEM_7 = 0xDE;
}
