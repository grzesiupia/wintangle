using System.Drawing;

namespace Wintangle.Core.Geometry;

/// <summary>
/// Computes the pixel rectangle a window should occupy for a given slot
/// layout inside a work area.
/// </summary>
/// <remarks>
/// Real tiling math — identical to the design's per-edge rule in
/// <see cref="SlotFraction"/> (the settings preview and the actual tiling
/// agree): a slot edge that touches the work-area boundary applies the
/// <see cref="GapSettings.EdgeGap"/>; an interior edge applies the
/// <see cref="GapSettings.WindowGap"/>. Per side:
/// <code>
/// left   = workArea.X + f.L·W + (f.L == 0 ? EdgeGap : WindowGap)
/// right  = workArea.X + f.R·W − (f.R == 1 ? EdgeGap : WindowGap)
/// top    = workArea.Y + f.T·H + (f.T == 0 ? EdgeGap : WindowGap)
/// bottom = workArea.Y + f.B·H − (f.B == 1 ? EdgeGap : WindowGap)
/// </code>
/// Because each window applies the full window gap on its interior edge, the
/// seam between two adjacent slots is exactly 2·WindowGap — the full configured
/// window gap lies between the windows.
/// </remarks>
public static class SlotCalculator
{
    /// <summary>
    /// Computes the slot rectangle for <paramref name="layout"/> inside
    /// <paramref name="workArea"/>.
    /// </summary>
    /// <remarks>
    /// Never throws: on degenerate work areas (e.g. smaller than the gaps) a
    /// best-effort rectangle pinned inside the work area, at least 1×1, is
    /// returned.
    /// </remarks>
    public static Rectangle ComputeSlot(Rectangle workArea, SlotLayout layout, GapSettings gaps)
    {
        ArgumentNullException.ThrowIfNull(gaps);

        (double l, double t, double r, double b) = SlotFraction.GetFraction(layout);

        double left = workArea.X + (l * workArea.Width) + (l == 0 ? gaps.EdgeGap : gaps.WindowGap);
        double right = workArea.X + (r * workArea.Width) - (r == 1 ? gaps.EdgeGap : gaps.WindowGap);
        double top = workArea.Y + (t * workArea.Height) + (t == 0 ? gaps.EdgeGap : gaps.WindowGap);
        double bottom = workArea.Y + (b * workArea.Height) - (b == 1 ? gaps.EdgeGap : gaps.WindowGap);

        int x = (int)Math.Round(left);
        int y = (int)Math.Round(top);
        int width = (int)Math.Max(1, Math.Round(right - left));
        int height = (int)Math.Max(1, Math.Round(bottom - top));

        return ClampToWorkArea(new Rectangle(x, y, width, height), workArea);
    }

    /// <summary>
    /// Best-effort guard: pins <paramref name="rect"/> inside the work area,
    /// never below 1×1, so degenerate work areas never throw.
    /// </summary>
    private static Rectangle ClampToWorkArea(Rectangle rect, Rectangle workArea)
    {
        if (workArea.Width < 1 || workArea.Height < 1)
        {
            return new Rectangle(workArea.X, workArea.Y, 1, 1);
        }

        int width = Math.Min(Math.Max(1, rect.Width), workArea.Width);
        int height = Math.Min(Math.Max(1, rect.Height), workArea.Height);

        int x = Math.Clamp(rect.X, workArea.X, workArea.X + workArea.Width - width);
        int y = Math.Clamp(rect.Y, workArea.Y, workArea.Y + workArea.Height - height);

        return new Rectangle(x, y, width, height);
    }
}
