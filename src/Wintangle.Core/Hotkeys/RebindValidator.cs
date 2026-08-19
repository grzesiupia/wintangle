namespace Wintangle.Core.Hotkeys;

/// <summary>
/// Pure validation for newly recorded hotkey combinations (used by the
/// settings window's inline recorder). No Win32 — fully testable anywhere.
/// </summary>
/// <remarks>
/// Policy: a binding needs at least one modifier (Ctrl/Alt/Win/Shift) — bare
/// keys are rejected so a recorded combo can never swallow typing globally.
/// A bare Escape is special: it is not a binding, it cancels recording
/// (<see cref="IsCancel"/>). Combos that conflict with OS-reserved keys
/// (Win+L etc.) cannot be detected reliably and are allowed.
/// </remarks>
public static class RebindValidator
{
    public const byte VK_ESCAPE = 0x1B;

    /// <summary>
    /// Returns an error message when <paramref name="hotkey"/> cannot be bound,
    /// or null when it is a valid combination. Escape with no modifiers returns
    /// null (it is the cancel signal, handled by the caller before binding).
    /// </summary>
    public static string? Validate(Hotkey hotkey)
    {
        if (hotkey.VirtualKey == 0)
        {
            return "No key selected.";
        }

        if (((int)hotkey.Modifiers & ~(int)(KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Win | KeyModifiers.Shift)) != 0)
        {
            return "Invalid modifier combination.";
        }

        if (hotkey.Modifiers == KeyModifiers.None && hotkey.VirtualKey != VK_ESCAPE)
        {
            return "Requires at least one modifier (Ctrl, Alt, Win, or Shift).";
        }

        return null;
    }

    /// <summary>True when the combo is the recording cancel signal (bare Escape).</summary>
    public static bool IsCancel(Hotkey hotkey) =>
        hotkey.Modifiers == KeyModifiers.None && hotkey.VirtualKey == VK_ESCAPE;

    /// <summary>True when the virtual key is a bare modifier key (never a hotkey key itself).</summary>
    public static bool IsModifierKey(byte vk) => vk is
        0x10 or 0x11 or 0x12 or 0x5B or 0x5C          // Shift, Ctrl, Alt, LWin, RWin
        or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5; // LShift, RShift, LCtrl, RCtrl, LAlt, RAlt
}
