namespace Wintangle.Core.Hotkeys;

/// <summary>
/// The tiling action a hotkey triggers. The first 16 values map 1:1 to the
/// <c>SlotLayout</c> slots (same names); the last two move the window to an
/// adjacent monitor and do not map to a slot.
/// </summary>
public enum HotkeyAction
{
    CenterHalf,

    HalfLeft,
    HalfRight,

    QuarterTopLeft,
    QuarterTopRight,
    QuarterBottomLeft,
    QuarterBottomRight,

    ThirdLeft,
    ThirdCenter,
    ThirdRight,

    SixthTopLeft,
    SixthTopCenter,
    SixthTopRight,
    SixthBottomLeft,
    SixthBottomCenter,
    SixthBottomRight,

    PrevMonitor,
    NextMonitor,
}
