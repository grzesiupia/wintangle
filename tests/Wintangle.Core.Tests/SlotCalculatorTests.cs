using System.Drawing;
using Wintangle.Core.Geometry;

namespace Wintangle.Core.Tests;

public class SlotCalculatorTests
{
    private static GapSettings Gaps(int window = 8, int edge = 8) => new(window, edge);

    [Fact]
    public void CenterHalf_IsCentered_WithWidthWMinusTwiceInset()
    {
        var work = new Rectangle(0, 0, 1920, 1080);
        var gaps = Gaps(8, 8); // inset = 16 per side

        var rect = SlotCalculator.ComputeSlot(work, SlotLayout.CenterHalf, gaps);

        Assert.Equal(16, rect.X);
        Assert.Equal(16, rect.Y);
        Assert.Equal(1920 - 2 * (8 + 8), rect.Width);  // W − 2(E+G)
        Assert.Equal(1080 - 2 * (8 + 8), rect.Height);
        // Centered: boundary gap identical on both sides.
        Assert.Equal(rect.X - work.X, work.Right - rect.Right);
        Assert.Equal(rect.Y - work.Y, work.Bottom - rect.Bottom);
    }

    [Fact]
    public void Halves_HaveExactSeamGap_AndBoundaryInsetEG()
    {
        var work = new Rectangle(0, 0, 1920, 1080);
        var gaps = Gaps(8, 8);

        var left = SlotCalculator.ComputeSlot(work, SlotLayout.HalfLeft, gaps);
        var right = SlotCalculator.ComputeSlot(work, SlotLayout.HalfRight, gaps);

        // Boundary inset per side = E + G = 16.
        Assert.Equal(16, left.X);
        Assert.Equal(16, left.Y);
        Assert.Equal(16, work.Right - right.Right);
        Assert.Equal(16, work.Bottom - right.Bottom);
        // Seam between adjacent slots equals G exactly.
        Assert.Equal(right.X - left.Right, gaps.WindowGap);
        // Equal halves.
        Assert.Equal(left.Width, right.Width);
        Assert.Equal(940, left.Width);
    }

    [Fact]
    public void Thirds_HaveThreeEqualColumns_WithExactSeams()
    {
        var work = new Rectangle(0, 0, 1920, 1080);
        var gaps = Gaps(8, 8);

        var left = SlotCalculator.ComputeSlot(work, SlotLayout.ThirdLeft, gaps);
        var center = SlotCalculator.ComputeSlot(work, SlotLayout.ThirdCenter, gaps);
        var right = SlotCalculator.ComputeSlot(work, SlotLayout.ThirdRight, gaps);

        Assert.Equal(left.Width, center.Width);
        Assert.Equal(center.Width, right.Width);
        Assert.Equal(624, left.Width);
        Assert.Equal(center.X - left.Right, gaps.WindowGap);
        Assert.Equal(right.X - center.Right, gaps.WindowGap);
        Assert.Equal(16, left.X);
        Assert.Equal(16, work.Right - right.Right);
    }

    [Fact]
    public void Quarters_HaveExactSeams_OnBothAxes()
    {
        var work = new Rectangle(0, 0, 1920, 1080);
        var gaps = Gaps(8, 8);

        var tl = SlotCalculator.ComputeSlot(work, SlotLayout.QuarterTopLeft, gaps);
        var tr = SlotCalculator.ComputeSlot(work, SlotLayout.QuarterTopRight, gaps);
        var bl = SlotCalculator.ComputeSlot(work, SlotLayout.QuarterBottomLeft, gaps);
        var br = SlotCalculator.ComputeSlot(work, SlotLayout.QuarterBottomRight, gaps);

        Assert.Equal(tr.X - tl.Right, gaps.WindowGap);
        Assert.Equal(bl.Y - tl.Bottom, gaps.WindowGap);
        Assert.Equal(br.X - bl.Right, gaps.WindowGap);
        Assert.Equal(br.Y - tr.Bottom, gaps.WindowGap);
        // Equal cell sizes across both axes.
        Assert.Equal(tl.Width, tr.Width);
        Assert.Equal(bl.Width, br.Width);
        Assert.Equal(tl.Width, bl.Width);
        Assert.Equal(tl.Height, tr.Height);
        Assert.Equal(tl.Height, bl.Height);
    }

    [Fact]
    public void Sixths_HaveExactSeams_OnBothAxes()
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
        Assert.Equal(tc.X - tl.Right, gaps.WindowGap);
        Assert.Equal(tr.X - tc.Right, gaps.WindowGap);
        Assert.Equal(bc.X - bl.Right, gaps.WindowGap);
        Assert.Equal(br.X - bc.Right, gaps.WindowGap);
        // Vertical seams.
        Assert.Equal(bl.Y - tl.Bottom, gaps.WindowGap);
        Assert.Equal(bc.Y - tc.Bottom, gaps.WindowGap);
        Assert.Equal(br.Y - tr.Bottom, gaps.WindowGap);
        // Boundary insets.
        Assert.Equal(16, tl.X);
        Assert.Equal(16, tl.Y);
        Assert.Equal(16, work.Right - tr.Right);
        Assert.Equal(16, work.Bottom - bl.Bottom);
    }

    [Fact]
    public void ZeroGap_AdjacentSlotsTouch()
    {
        var work = new Rectangle(0, 0, 1000, 800);
        var gaps = Gaps(0, 0);

        var left = SlotCalculator.ComputeSlot(work, SlotLayout.HalfLeft, gaps);
        var right = SlotCalculator.ComputeSlot(work, SlotLayout.HalfRight, gaps);

        Assert.Equal(0, gaps.WindowGap);
        Assert.Equal(right.X, left.Right); // touch, no gap
        Assert.Equal(0, left.X);
        Assert.Equal(1000, right.Right);
    }

    [Fact]
    public void MaxGap50_IsRespected()
    {
        var work = new Rectangle(0, 0, 1920, 1080);
        var gaps = Gaps(50, 50);

        var left = SlotCalculator.ComputeSlot(work, SlotLayout.HalfLeft, gaps);
        var right = SlotCalculator.ComputeSlot(work, SlotLayout.HalfRight, gaps);

        Assert.Equal(100, left.X);                    // E + G
        Assert.Equal(50, right.X - left.Right);       // seam
        Assert.Equal(100, work.Right - right.Right);  // boundary
        Assert.Equal(835, left.Width);
    }

    [Fact]
    public void EdgeGapOnly_Versus_WindowGapOnly_Differ()
    {
        var work = new Rectangle(0, 0, 1920, 1080);
        var edgeGapOnly = SlotCalculator.ComputeSlot(work, SlotLayout.HalfLeft, Gaps(0, 8));
        var windowGapOnly = SlotCalculator.ComputeSlot(work, SlotLayout.HalfLeft, Gaps(8, 0));

        // Same boundary inset (E+G = 8), different seam behavior → different size.
        Assert.Equal(8, edgeGapOnly.X);
        Assert.Equal(8, windowGapOnly.X);
        // The G difference is shared across both halves, so each half differs
        // by G/2 = 4 (interior widths 952 vs 948).
        Assert.Equal(4, edgeGapOnly.Right - windowGapOnly.Right);
        Assert.Equal(0, SlotCalculator.ComputeSlot(work, SlotLayout.HalfRight, Gaps(0, 8)).X
                      - edgeGapOnly.Right); // seam 0 when WindowGap=0
        Assert.Equal(8, SlotCalculator.ComputeSlot(work, SlotLayout.HalfRight, Gaps(8, 0)).X
                      - windowGapOnly.Right); // seam 8 when WindowGap=8
    }

    [Fact]
    public void NegativeOriginWorkArea_ComputesCorrectly()
    {
        // Monitor left of the primary: origin at (-1920, 0).
        var work = new Rectangle(-1920, 0, 1920, 1080);
        var gaps = Gaps(8, 8);

        var center = SlotCalculator.ComputeSlot(work, SlotLayout.CenterHalf, gaps);
        Assert.Equal(-1904, center.X);
        Assert.Equal(1888, center.Width);
        Assert.True(center.Left >= work.Left && center.Right <= work.Right);

        var left = SlotCalculator.ComputeSlot(work, SlotLayout.HalfLeft, gaps);
        var right = SlotCalculator.ComputeSlot(work, SlotLayout.HalfRight, gaps);
        Assert.Equal(-1904, left.X);
        Assert.Equal(right.X - left.Right, gaps.WindowGap);
        Assert.Equal(-16, work.Left - left.Left);       // boundary inset 16
        Assert.Equal(16, work.Right - right.Right);
    }

    [Fact]
    public void PortraitWorkArea_LaysOutVertically()
    {
        var work = new Rectangle(0, 0, 1080, 1920);
        var gaps = Gaps(8, 8);

        var center = SlotCalculator.ComputeSlot(work, SlotLayout.CenterHalf, gaps);
        Assert.Equal(1048, center.Width);
        Assert.Equal(1888, center.Height);
        Assert.True(center.Width < center.Height);

        var tl = SlotCalculator.ComputeSlot(work, SlotLayout.SixthTopLeft, gaps);
        var bl = SlotCalculator.ComputeSlot(work, SlotLayout.SixthBottomLeft, gaps);
        Assert.Equal(344, tl.Width);
        Assert.Equal(940, tl.Height);
        Assert.Equal(bl.Y - tl.Bottom, gaps.WindowGap);
    }

    [Fact]
    public void DegenerateSmallWorkArea_DoesNotThrow_ReturnsAtLeast1Px()
    {
        var work = new Rectangle(0, 0, 10, 10);
        var gaps = Gaps(50, 50); // 2(E+G) = 200 > 10

        foreach (var layout in Enum.GetValues<SlotLayout>())
        {
            var rect = SlotCalculator.ComputeSlot(work, layout, gaps);

            Assert.True(rect.Width >= 1, $"{layout}: width {rect.Width}");
            Assert.True(rect.Height >= 1, $"{layout}: height {rect.Height}");
            Assert.True(rect.Left >= work.Left && rect.Top >= work.Top, $"{layout}: origin in work area");
            Assert.True(rect.Right <= work.Right && rect.Bottom <= work.Bottom, $"{layout}: inside work area");
        }
    }

    [Fact]
    public void GetGrid_AllSlotLayouts()
    {
        // All 16 slot layouts: (columns, rows, column index, row index).
        var expected = new Dictionary<SlotLayout, (int Columns, int Rows, int Column, int Row)>
        {
            [SlotLayout.CenterHalf] = (1, 1, 0, 0),

            [SlotLayout.HalfLeft] = (2, 1, 0, 0),
            [SlotLayout.HalfRight] = (2, 1, 1, 0),

            [SlotLayout.QuarterTopLeft] = (2, 2, 0, 0),
            [SlotLayout.QuarterTopRight] = (2, 2, 1, 0),
            [SlotLayout.QuarterBottomLeft] = (2, 2, 0, 1),
            [SlotLayout.QuarterBottomRight] = (2, 2, 1, 1),

            [SlotLayout.ThirdLeft] = (3, 1, 0, 0),
            [SlotLayout.ThirdCenter] = (3, 1, 1, 0),
            [SlotLayout.ThirdRight] = (3, 1, 2, 0),

            [SlotLayout.SixthTopLeft] = (3, 2, 0, 0),
            [SlotLayout.SixthTopCenter] = (3, 2, 1, 0),
            [SlotLayout.SixthTopRight] = (3, 2, 2, 0),
            [SlotLayout.SixthBottomLeft] = (3, 2, 0, 1),
            [SlotLayout.SixthBottomCenter] = (3, 2, 1, 1),
            [SlotLayout.SixthBottomRight] = (3, 2, 2, 1),
        };

        // The enum must stay in sync with the grid map (16 slots, no monitor moves).
        Assert.Equal(16, Enum.GetValues<SlotLayout>().Length);
        Assert.Equal(expected.Count, Enum.GetValues<SlotLayout>().Length);

        foreach (var (layout, grid) in expected)
        {
            Assert.Equal(grid, SlotCalculator.GetGrid(layout));
        }
    }
}
