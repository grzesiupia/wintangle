using System.Drawing;

namespace Wintangle.Core.Geometry;

/// <summary>
/// Deterministic ordering of screens: primary first, then X ascending,
/// then Y ascending, with a stable tie-break preserving enumeration order
/// for fully identical coordinates.
/// </summary>
public static class ScreenLayout
{
    public static IReadOnlyList<ScreenInfo> OrderScreens(IEnumerable<ScreenInfo> screens)
    {
        ArgumentNullException.ThrowIfNull(screens);

        return screens
            .Select((screen, index) => (Screen: screen, Index: index))
            .OrderByDescending(item => item.Screen.IsPrimary)
            .ThenBy(item => item.Screen.Bounds.X)
            .ThenBy(item => item.Screen.Bounds.Y)
            .ThenBy(item => item.Index)
            .Select(item => item.Screen)
            .ToList();
    }
}
