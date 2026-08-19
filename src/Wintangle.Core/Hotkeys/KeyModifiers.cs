namespace Wintangle.Core.Hotkeys;

/// <summary>Modifier keys combined into a hotkey combination (bit flags).</summary>
[Flags]
public enum KeyModifiers
{
    None = 0,
    Ctrl = 1,
    Alt = 2,
    Win = 4,
    Shift = 8,
}
