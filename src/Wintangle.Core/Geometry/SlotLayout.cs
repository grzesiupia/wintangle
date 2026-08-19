namespace Wintangle.Core.Geometry;

/// <summary>
/// Describes a screen slot a window can be tiled to.
/// Values map 1:1 to the action hotkeys (phases 3+): the enum name is the
/// slot a given action moves the foreground window into.
/// </summary>
public enum SlotLayout
{
    /// <summary>Single column, full height, centered horizontally.</summary>
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
}
