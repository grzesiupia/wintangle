using Wintangle.Core.Hotkeys;

namespace Wintangle.App.UI;

/// <summary>
/// Friendly display names shared by the tray menu and the settings window.
/// </summary>
internal static class UiLabels
{
    public static string ActionName(HotkeyAction action) => action switch
    {
        HotkeyAction.CenterHalf => "Center Half",
        HotkeyAction.HalfLeft => "Left Half",
        HotkeyAction.HalfRight => "Right Half",
        HotkeyAction.QuarterTopLeft => "Top-Left Quarter",
        HotkeyAction.QuarterTopRight => "Top-Right Quarter",
        HotkeyAction.QuarterBottomLeft => "Bottom-Left Quarter",
        HotkeyAction.QuarterBottomRight => "Bottom-Right Quarter",
        HotkeyAction.ThirdLeft => "Left Third",
        HotkeyAction.ThirdCenter => "Center Third",
        HotkeyAction.ThirdRight => "Right Third",
        HotkeyAction.SixthTopLeft => "Top-Left Sixth",
        HotkeyAction.SixthTopCenter => "Top-Center Sixth",
        HotkeyAction.SixthTopRight => "Top-Right Sixth",
        HotkeyAction.SixthBottomLeft => "Bottom-Left Sixth",
        HotkeyAction.SixthBottomCenter => "Bottom-Center Sixth",
        HotkeyAction.SixthBottomRight => "Bottom-Right Sixth",
        _ => action.ToString(),
    };
}
