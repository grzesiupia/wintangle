using System.Drawing;

namespace Wintangle.Core.Geometry;

/// <summary>
/// Immutable description of a single display.
/// All rectangles use screen (virtual-desktop) coordinates and may be
/// negative on monitors positioned left/above the primary display.
/// </summary>
public sealed record ScreenInfo(
    Rectangle Bounds,
    Rectangle WorkArea,
    bool IsPrimary,
    string DeviceName);
