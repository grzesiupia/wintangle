using System.Drawing;
using Wintangle.Core.Geometry;

namespace Wintangle.Core.Tests;

/// <summary>
/// Real tiling math — the same per-edge rule as the design preview
/// (<see cref="SlotFraction"/>): boundary-touching edges apply the edge gap,
/// interior edges apply the window gap. So the seam between two adjacent
/// slots is 2·WindowGap and the boundary inset is exactly EdgeGap.
/// </summary>
public class SlotCalculatorTests
{
    private static GapSettings Gaps(int window = 8, int edge = 8) => new(window, edge);

    [Fact]
    public void CenterHalf_IsCenteredHalfWidth_FullHeight()
    {
        var work = new Rectangle(0, 0, 1920, 1080);
        var gaps = Gaps(8, 0); // interior edges get the window gap; boundary edges the edge gap (0)

        var rect = SlotCalculator.ComputeSlot(work, SlotLayout.CenterHalf, gaps);

        // 50% width centered: left = 0.25·W + G = 488, right = 0.75·W − G = 1432.
        Assert.Equal(488, rect.X);
        Assert.Equal(0, rect.Y);
        Assert.Equal(944, rect.Width);   // 0.5·W − 2·G
        Assert.Equal(1080, rect.Height); // full height (top/bottom touch with edge gap 0)
        // Centered: boundary gap identical on both sides.
        Assert.Equal(rect.X - work.X, work.Right - rect.Right);
        Assert.Equal(rect.Y - work.Y, work.Bottom - rect.Bottom);
    }

    [Fact]
    public void CenterHalf_WithEdgeGap_InsertsOnlyAtTopAndBottom()
    {
        var work = new Rectangle(0, 0, 1920, 1080);
        var gaps = Gaps(8, 8);

        var rect = SlotCalculator.ComputeSlot(work, SlotLayout.CenterHalf, gaps);

        Assert.Equal(488, rect.X);   // still the window gap on the interior sides
        Assert.Equal(8, rect.Y);     // boundary edge → edge gap only
        Assert.Equal(944, rect.Width);
        Assert.Equal(1064, rect.Height); // 1080 − 2·8
    }

    [Fact]
    public void Halves_HaveFullGapSeam_AndEdgeGapBoundary()
    {
        var work = new Rectangle(0, 0, 1920, 1080);
        var gaps = Gaps(8, 8);

        var left = SlotCalculator.ComputeSlot(work, SlotLayout.HalfLeft, gaps);
        var right = SlotCalculator.ComputeSlot(work, SlotLayout.HalfRight, gaps);

        // Boundary inset per side = EdgeGap exactly (not EdgeGap + WindowGap).
        Assert.Equal(8, left.X);
        Assert.Equal(8, left.Y);
        Assert.Equal(8, work.Right - right.Right);
        Assert.Equal(8, work.Bottom - right.Bottom);
        // Seam between adjacent slots = 2·WindowGap (each side applies its own).
        Assert.Equal(2 * gaps.WindowGap, right.X - left.Right);
        // Equal halves: [E, W/2 − G] vs [W/2 + G, W − E].
        Assert.Equal(952, left.Right);
        Assert.Equal(968, right.X);
        Assert.Equal(left.Width, right.Width);
        Assert.Equal(944, left.Width);
    }

    [Fact]
    public void Thirds_HaveThreeEqualColumns_WithFullGapSeams()
    {
        var work = new Rectangle(0, 0, 1920, 1080);
        var gaps = Gaps(8, 8);

        var left = SlotCalculator.ComputeSlot(work, SlotLayout.ThirdLeft, gaps);
        var center = SlotCalculator.ComputeSlot(work, SlotLayout.ThirdCenter, gaps);
        var right = SlotCalculator.ComputeSlot(work, SlotLayout.ThirdRight, gaps);

        Assert.Equal(left.Width, center.Width);
        Assert.Equal(center.Width, right.Width);
        Assert.Equal(624, left.Width);
        Assert.Equal(2 * gaps.WindowGap, center.X - left.Right);
        Assert.Equal(2 * gaps.WindowGap, right.X - center.Right);
        Assert.Equal(8, left.X);
        Assert.Equal(8, work.Right - right.Right);
    }

    [Fact]
    public void Quarters_HaveFullGapSeams_OnBothAxes()
    {
        var work = new Rectangle(0, 0, 1920, 1080);
        var gaps = Gaps(8, 8);

        var tl = SlotCalculator.ComputeSlot(work, SlotLayout.QuarterTopLeft, gaps);
        var tr = SlotCalculator.ComputeSlot(work, SlotLayout.QuarterTopRight, gaps);
        var bl = SlotCalculator.ComputeSlot(work, SlotLayout.QuarterBottomLeft, gaps);
        var br = SlotCalculator.ComputeSlot(work, SlotLayout.QuarterBottomRight, gaps);

        Assert.Equal(2 * gaps.WindowGap, tr.X - tl.Right);
        Assert.Equal(2 * gaps.WindowGap, bl.Y - tl.Bottom);
        Assert.Equal(2 * gaps.WindowGap, br.X - bl.Right);
        Assert.Equal(2 * gaps.WindowGap, br.Y - tr.Bottom);
        // Equal cell sizes across both axes.
        Assert.Equal(tl.Width, tr.Width);
        Assert.Equal(bl.Width, br.Width);
        Assert.Equal(tl.Width, bl.Width);
        Assert.Equal(944, tl.Width);
        Assert.Equal(524, tl.Height);
        Assert.Equal(tl.Height, tr.Height);
        Assert.Equal(tl.Height, bl.Height);
    }

    [Fact]
    public void Sixths_HaveFullGapSeams_OnBothAxes()
    {
        var work = new Rectangle(0, 0, 1920, 1080);
        var gaps = Gaps(8, 8);

        var tl = SlotCalculator.ComputeSlot(work, SlotLayout.SixthTopLeft, gaps);
        var tc = SlotCalculator.ComputeSlot(work, SlotLayout.SixthTopCenter, gaps);
        var tr = SlotCalculator.ComputeSlot(work, SlotLayout.SixthTopRight, gaps);
        var bl = SlotCalculator.ComputeSlot(work, SlotLayout.SixthBottomLeft, gaps);
        var bc = SlotCalculator.ComputeSlot(work, SlotLayout.SixthBottomCenter, gaps);
        var br = SlotCalculator.ComputeSlot(work, SlotLayout.SixthBottomRight, gaps);

        // Horizontal seams.
        Assert.Equal(2 * gaps.WindowGap, tc.X - tl.Right);
        Assert.Equal(2 * gaps.WindowGap, tr.X - tc.Right);
        Assert.Equal(2 * gaps.WindowGap, bc.X - bl.Right);
        Assert.Equal(2 * gaps.WindowGap, br.X - bc.Right);
        // Vertical seams.
        Assert.Equal(2 * gaps.WindowGap, bl.Y - tl.Bottom);
        Assert.Equal(2 * gaps.WindowGap, bc.Y - tc.Bottom);
        Assert.Equal(2 * gaps.WindowGap, br.Y - tr.Bottom);
        // Boundary insets = EdgeGap exactly.
        Assert.Equal(8, tl.X);
        Assert.Equal(8, tl.Y);
        Assert.Equal(8, work.Right - tr.Right);
        Assert.Equal(8, work.Bottom - bl.Bottom);
        // Equal cell sizes.
        Assert.Equal(624, tl.Width);
        Assert.Equal(524, tl.Height);
    }

    [Fact]
    public void ZeroGap_AdjacentSlotsTouchAtExactHalf()
    {
        var work = new Rectangle(0, 0, 1000, 800);
        var gaps = Gaps(0, 0);

        var left = SlotCalculator.ComputeSlot(work, SlotLayout.HalfLeft, gaps);
        var right = SlotCalculator.ComputeSlot(work, SlotLayout.HalfRight, gaps);

        Assert.Equal(0, gaps.WindowGap);
        Assert.Equal(work.Width / 2, left.Right); // touch at exactly W/2
        Assert.Equal(right.X, left.Right);        // touch, no gap
        Assert.Equal(0, left.X);
        Assert.Equal(1000, right.Right);
    }

    [Fact]
    public void MaxGap50_UsesEdgeGapBoundary_AndFullGapSeam()
    {
        var work = new Rectangle(0, 0, 1920, 1080);
        var gaps = Gaps(50, 50);

        var left = SlotCalculator.ComputeSlot(work, SlotLayout.HalfLeft, gaps);
        var right = SlotCalculator.ComputeSlot(work, SlotLayout.HalfRight, gaps);

        Assert.Equal(50, left.X);                 // EdgeGap exactly
        Assert.Equal(2 * gaps.WindowGap, right.X - left.Right); // seam 100
        Assert.Equal(50, work.Right - right.Right);
        Assert.Equal(860, left.Width);
    }

    [Fact]
    public void EdgeGapOnly_Versus_WindowGapOnly_UseTheirOwnEdges()
    {
        var work = new Rectangle(0, 0, 1920, 1080);

        var edgeGapOnly = SlotCalculator.ComputeSlot(work, SlotLayout.HalfLeft, Gaps(0, 8));
        var windowGapOnly = SlotCalculator.ComputeSlot(work, SlotLayout.HalfLeft, Gaps(8, 0));

        // Boundary edges differ: edge gap applies at the boundary, window gap does not.
        Assert.Equal(8, edgeGapOnly.X);
        Assert.Equal(0, windowGapOnly.X);
        // Interior edges differ the other way: window gap applies at the seam.
        Assert.Equal(960, edgeGapOnly.Right);      // right = W/2 − windowGap 0
        Assert.Equal(952, windowGapOnly.Right);    // right = W/2 − windowGap 8
        // Seam between halves: 0 with only edge gap, 2·G with only window gap.
        var edgeGapRight = SlotCalculator.ComputeSlot(work, SlotLayout.HalfRight, Gaps(0, 8));
        var windowGapRight = SlotCalculator.ComputeSlot(work, SlotLayout.HalfRight, Gaps(8, 0));
        Assert.Equal(0, edgeGapRight.X - edgeGapOnly.Right);
        Assert.Equal(16, windowGapRight.X - windowGapOnly.Right);
    }

    [Fact]
    public void NegativeOriginWorkArea_ComputesCorrectly()
    {
        // Monitor left of the primary: origin at (-1920, 0).
        var work = new Rectangle(-1920, 0, 1920, 1080);
        var gaps = Gaps(8, 8);

        var center = SlotCalculator.ComputeSlot(work, SlotLayout.CenterHalf, gaps);
        Assert.Equal(-1432, center.X);
        Assert.Equal(944, center.Width);
        Assert.True(center.Left >= work.Left && center.Right <= work.Right);

        var left = SlotCalculator.ComputeSlot(work, SlotLayout.HalfLeft, gaps);
        var right = SlotCalculator.ComputeSlot(work, SlotLayout.HalfRight, gaps);
        Assert.Equal(-1912, left.X);
        Assert.Equal(2 * gaps.WindowGap, right.X - left.Right);
        Assert.Equal(-8, work.Left - left.Left);       // boundary inset 8 (EdgeGap)
        Assert.Equal(8, work.Right - right.Right);
    }

    [Fact]
    public void PortraitWorkArea_LaysOutVertically()
    {
        var work = new Rectangle(0, 0, 1080, 1920);
        var gaps = Gaps(8, 8);

        var center = SlotCalculator.ComputeSlot(work, SlotLayout.CenterHalf, gaps);
        Assert.Equal(524, center.Width);
        Assert.Equal(1904, center.Height);
        Assert.True(center.Width < center.Height);

        var tl = SlotCalculator.ComputeSlot(work, SlotLayout.SixthTopLeft, gaps);
        var bl = SlotCalculator.ComputeSlot(work, SlotLayout.SixthBottomLeft, gaps);
        Assert.Equal(344, tl.Width);
        Assert.Equal(944, tl.Height);
        Assert.Equal(2 * gaps.WindowGap, bl.Y - tl.Bottom);
    }

    [Fact]
    public void Fullscreen_FillsWorkArea_WithEdgeGapOnAllFourSides()
    {
        var work = new Rectangle(0, 0, 1920, 1080);
        var gaps = Gaps(8, 8);

        var rect = SlotCalculator.ComputeSlot(work, SlotLayout.Fullscreen, gaps);

        Assert.Equal(new Rectangle(8, 8, 1904, 1064), rect);
    }

    [Fact]
    public void Fullscreen_ZeroEdgeGap_FillsWorkAreaExactly()
    {
        var work = new Rectangle(0, 0, 1920, 1080);
        var gaps = Gaps(8, 0);

        var rect = SlotCalculator.ComputeSlot(work, SlotLayout.Fullscreen, gaps);

        Assert.Equal(work, rect);
    }

    [Fact]
    public void DegenerateSmallWorkArea_DoesNotThrow_ReturnsAtLeast1Px()
    {
        var work = new Rectangle(0, 0, 10, 10);
        var gaps = Gaps(50, 50); // gaps exceed the work area

        foreach (var layout in Enum.GetValues<SlotLayout>())
        {
            var rect = SlotCalculator.ComputeSlot(work, layout, gaps);

            Assert.True(rect.Width >= 1, $"{layout}: width {rect.Width}");
            Assert.True(rect.Height >= 1, $"{layout}: height {rect.Height}");
            Assert.True(rect.Left >= work.Left && rect.Top >= work.Top, $"{layout}: origin in work area");
            Assert.True(rect.Right <= work.Right && rect.Bottom <= work.Bottom, $"{layout}: inside work area");
        }
    }
}
