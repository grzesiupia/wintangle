using System.Drawing;
using Wintangle.Core.Geometry;

namespace Wintangle.Core.Tests;

/// <summary>
/// Design-preview math: the 17 fraction table and the gap/edge preview
/// rectangle formula. Both the preview and the real tiling math share the same
/// per-edge rule — <see cref="SlotCalculator"/> delegates to
/// <see cref="SlotFraction"/> — so these tests cover the rule the tiler uses
/// (see also <see cref="SlotCalculatorTests"/>).
/// </summary>
public class SlotPreviewTests
{
    [Fact]
    public void GetFraction_AllSlotLayouts()
    {
        // The 17 f-values from the design's SLOTS table (wintangle-app.html),
        // in enum order so a reorder can never silently re-wire a slot.
        var expected = new Dictionary<SlotLayout, (double L, double T, double R, double B)>
        {
            [SlotLayout.CenterHalf] = (0.25, 0, 0.75, 1),

            [SlotLayout.HalfLeft] = (0, 0, 0.5, 1),
            [SlotLayout.HalfRight] = (0.5, 0, 1, 1),

            [SlotLayout.QuarterTopLeft] = (0, 0, 0.5, 0.5),
            [SlotLayout.QuarterTopRight] = (0.5, 0, 1, 0.5),
            [SlotLayout.QuarterBottomLeft] = (0, 0.5, 0.5, 1),
            [SlotLayout.QuarterBottomRight] = (0.5, 0.5, 1, 1),

            [SlotLayout.ThirdLeft] = (0, 0, 1d / 3, 1),
            [SlotLayout.ThirdCenter] = (1d / 3, 0, 2d / 3, 1),
            [SlotLayout.ThirdRight] = (2d / 3, 0, 1, 1),

            [SlotLayout.SixthTopLeft] = (0, 0, 1d / 3, 0.5),
            [SlotLayout.SixthTopCenter] = (1d / 3, 0, 2d / 3, 0.5),
            [SlotLayout.SixthTopRight] = (2d / 3, 0, 1, 0.5),
            [SlotLayout.SixthBottomLeft] = (0, 0.5, 1d / 3, 1),
            [SlotLayout.SixthBottomCenter] = (1d / 3, 0.5, 2d / 3, 1),
            [SlotLayout.SixthBottomRight] = (2d / 3, 0.5, 1, 1),
            [SlotLayout.Fullscreen] = (0, 0, 1, 1),
        };

        Assert.Equal(17, Enum.GetValues<SlotLayout>().Length);

        foreach (var (layout, fraction) in expected)
        {
            var actual = SlotFraction.GetFraction(layout);
            Assert.Equal(fraction.L, actual.L, 12);
            Assert.Equal(fraction.T, actual.T, 12);
            Assert.Equal(fraction.R, actual.R, 12);
            Assert.Equal(fraction.B, actual.B, 12);
        }
    }

    [Fact]
    public void ComputePreviewRect_Gap8Edge0_HalfLeft()
    {
        // left = 0 + 0 = 0; right = 500 − gap 8 = 492; top = 0; bottom = 800 − 0 = 800.
        var rect = SlotFraction.ComputePreviewRect(1000, 800, SlotLayout.HalfLeft, gap: 8, edge: 0);

        Assert.Equal(0, rect.X);
        Assert.Equal(0, rect.Y);
        Assert.Equal(492, rect.Width);
        Assert.Equal(800, rect.Height);
    }

    [Fact]
    public void ComputePreviewRect_ZeroGap_EdgesFlush()
    {
        var left = SlotFraction.ComputePreviewRect(1000, 800, SlotLayout.HalfLeft, gap: 0, edge: 0);
        var right = SlotFraction.ComputePreviewRect(1000, 800, SlotLayout.HalfRight, gap: 0, edge: 0);

        Assert.Equal(0, left.X);
        Assert.Equal(1000, right.Right);      // edge flush to the mock boundary
        Assert.Equal(right.X, left.Right);    // seam flush, no gap
        Assert.Equal(0, left.Y);
        Assert.Equal(800, left.Bottom);
    }

    [Fact]
    public void ComputePreviewRect_EdgeOnly_HalfLeft()
    {
        // Edge gap 8 on boundary-touching edges; interior edge uses the 0 window gap.
        var rect = SlotFraction.ComputePreviewRect(1000, 800, SlotLayout.HalfLeft, gap: 0, edge: 8);

        Assert.Equal(8, rect.X);
        Assert.Equal(8, rect.Y);
        Assert.Equal(492, rect.Width);   // right = 500 − (window gap 0)
        Assert.Equal(784, rect.Height);  // bottom = 800 − edge 8
    }

    [Fact]
    public void ComputePreviewRect_BothGaps_HalfLeft()
    {
        // Boundary edges take edge 8; the interior edge takes window gap 8.
        var rect = SlotFraction.ComputePreviewRect(1000, 800, SlotLayout.HalfLeft, gap: 8, edge: 8);

        Assert.Equal(8, rect.X);
        Assert.Equal(8, rect.Y);
        Assert.Equal(484, rect.Width);   // right = 500 − window gap 8
        Assert.Equal(784, rect.Height);  // bottom = 800 − edge 8
    }

    [Fact]
    public void ComputePreviewRect_ClampsToMinimum10()
    {
        // Tiny mock (4×4) still yields a 10×10 window, never a sub-10px sliver.
        var rect = SlotFraction.ComputePreviewRect(4, 4, SlotLayout.HalfLeft, gap: 0, edge: 0);

        Assert.True(rect.Width >= 10);
        Assert.True(rect.Height >= 10);
    }

    [Fact]
    public void ComputePreviewRect_DegenerateWidth_DoesNotThrow()
    {
        // Zero-sized mock: no throw, and every slot still returns a usable 10×10.
        foreach (var layout in Enum.GetValues<SlotLayout>())
        {
            var rect = SlotFraction.ComputePreviewRect(0, 0, layout, gap: 0, edge: 0);

            Assert.True(rect.Width >= 10, $"{layout}: width {rect.Width}");
            Assert.True(rect.Height >= 10, $"{layout}: height {rect.Height}");
            Assert.True(rect.X >= 0 && rect.Y >= 0, $"{layout}: origin non-negative");
        }
    }
}
