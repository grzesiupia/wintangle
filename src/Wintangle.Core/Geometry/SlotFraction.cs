using System.Drawing;

namespace Wintangle.Core.Geometry;

/// <summary>
/// Fractional slot geometry shared by the settings window's DESIGN preview and
/// the real tiling math (<see cref="SlotCalculator"/> delegates to
/// <see cref="GetFraction"/>). The 16 f-values map 1:1 to the exported design
/// (wintangle-app.html SLOTS array) and drive the live preview rectangle drawn
/// on the desktop mock.
/// </summary>
/// <remarks>
/// The per-edge rule — an edge touches the boundary with the edge gap, an
/// interior edge with the window gap — is the design contract; the real
/// <see cref="SlotCalculator.ComputeSlot"/> applies exactly the same rule, so
/// the mockup reads like the tiling.
/// </remarks>
public static class SlotFraction
{
    /// <summary>
    /// The slot's fractional bounds (L, T, R, B) in the design's 0..1 space.
    /// </summary>
    public static (double L, double T, double R, double B) GetFraction(SlotLayout layout) => layout switch
    {
        SlotLayout.CenterHalf => (0.25, 0, 0.75, 1),

        SlotLayout.HalfLeft => (0, 0, 0.5, 1),
        SlotLayout.HalfRight => (0.5, 0, 1, 1),

        SlotLayout.QuarterTopLeft => (0, 0, 0.5, 0.5),
        SlotLayout.QuarterTopRight => (0.5, 0, 1, 0.5),
        SlotLayout.QuarterBottomLeft => (0, 0.5, 0.5, 1),
        SlotLayout.QuarterBottomRight => (0.5, 0.5, 1, 1),

        SlotLayout.ThirdLeft => (0, 0, 1d / 3, 1),
        SlotLayout.ThirdCenter => (1d / 3, 0, 2d / 3, 1),
        SlotLayout.ThirdRight => (2d / 3, 0, 1, 1),

        SlotLayout.SixthTopLeft => (0, 0, 1d / 3, 0.5),
        SlotLayout.SixthTopCenter => (1d / 3, 0, 2d / 3, 0.5),
        SlotLayout.SixthTopRight => (2d / 3, 0, 1, 0.5),
        SlotLayout.SixthBottomLeft => (0, 0.5, 1d / 3, 1),
        SlotLayout.SixthBottomCenter => (1d / 3, 0.5, 2d / 3, 1),
        SlotLayout.SixthBottomRight => (2d / 3, 0.5, 1, 1),

        _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, "Unknown slot layout."),
    };

    /// <summary>
    /// Computes the DESIGN preview rectangle for <paramref name="layout"/>
    /// inside a <paramref name="width"/> × <paramref name="height"/> desktop
    /// mock. Mirrors the exported JS math exactly:
    /// <code>
    /// left   = f0·W + (f0 == 0 ? edge : gap)
    /// right  = f2·W − (f2 == 1 ? edge : gap)
    /// top    = f1·H + (f1 == 0 ? edge : gap)
    /// bottom = f3·H − (f3 == 1 ? edge : gap)
    /// </code>
    /// with the result clamped to at least 10×10 (the design never renders a
    /// sub-10px window). Degenerate (zero) inputs never throw.
    /// </summary>
    public static Rectangle ComputePreviewRect(double width, double height, SlotLayout layout, int gap, int edge)
    {
        double w = Math.Max(10, width);
        double h = Math.Max(10, height);

        (double l, double t, double r, double b) = GetFraction(layout);

        double left = (l * w) + (l == 0 ? edge : gap);
        double right = (r * w) - (r == 1 ? edge : gap);
        double top = (t * h) + (t == 0 ? edge : gap);
        double bottom = (b * h) - (b == 1 ? edge : gap);

        int x = (int)Math.Round(left);
        int y = (int)Math.Round(top);
        int rectWidth = (int)Math.Max(10, Math.Round(right - left));
        int rectHeight = (int)Math.Max(10, Math.Round(bottom - top));

        return new Rectangle(x, y, rectWidth, rectHeight);
    }
}
