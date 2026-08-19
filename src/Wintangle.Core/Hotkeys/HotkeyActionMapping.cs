using Wintangle.Core.Geometry;

namespace Wintangle.Core.Hotkeys;

/// <summary>Maps hotkey actions to the <see cref="SlotLayout"/> slots.</summary>
public static class HotkeyActionMapping
{
    /// <summary>
    /// The slot layout for the 16 slot actions, or null for the monitor-move
    /// actions (<see cref="HotkeyAction.PrevMonitor"/> /
    /// <see cref="HotkeyAction.NextMonitor"/>), which are handled separately.
    /// </summary>
    public static SlotLayout? ToSlotLayout(this HotkeyAction action) => action switch
    {
        HotkeyAction.CenterHalf => SlotLayout.CenterHalf,

        HotkeyAction.HalfLeft => SlotLayout.HalfLeft,
        HotkeyAction.HalfRight => SlotLayout.HalfRight,

        HotkeyAction.QuarterTopLeft => SlotLayout.QuarterTopLeft,
        HotkeyAction.QuarterTopRight => SlotLayout.QuarterTopRight,
        HotkeyAction.QuarterBottomLeft => SlotLayout.QuarterBottomLeft,
        HotkeyAction.QuarterBottomRight => SlotLayout.QuarterBottomRight,

        HotkeyAction.ThirdLeft => SlotLayout.ThirdLeft,
        HotkeyAction.ThirdCenter => SlotLayout.ThirdCenter,
        HotkeyAction.ThirdRight => SlotLayout.ThirdRight,

        HotkeyAction.SixthTopLeft => SlotLayout.SixthTopLeft,
        HotkeyAction.SixthTopCenter => SlotLayout.SixthTopCenter,
        HotkeyAction.SixthTopRight => SlotLayout.SixthTopRight,
        HotkeyAction.SixthBottomLeft => SlotLayout.SixthBottomLeft,
        HotkeyAction.SixthBottomCenter => SlotLayout.SixthBottomCenter,
        HotkeyAction.SixthBottomRight => SlotLayout.SixthBottomRight,

        HotkeyAction.PrevMonitor or HotkeyAction.NextMonitor => null,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown hotkey action."),
    };
}
