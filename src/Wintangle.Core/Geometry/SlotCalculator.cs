using System.Drawing;

namespace Wintangle.Core.Geometry;

/// <summary>
/// Computes the pixel rectangle a window should occupy for a given slot
/// layout inside a work area.
/// </summary>
/// <remarks>
/// Per-axis math (origin-relative, works with negative work-area origins):
/// <code>
/// inset   = EdgeGap + WindowGap                      // boundary inset per side
/// usable  = Size − 2·inset − (N−1)·WindowGap          // space for N columns/rows
/// cell    = usable / N                                // integer division
/// offset_i = inset + i·(cell + WindowGap)             // start of column/row i
/// </code>
/// All cells in an axis share the same size, so the seam gap between two
/// adjacent cells is exactly <see cref="GapSettings.WindowGap"/>: each cell
/// contributes WindowGap/2 at the seam (leftover integer-division pixels are
/// dropped at the far edge, keeping seams exact).
/// </remarks>
public static class SlotCalculator
{
    /// <summary>
    /// Computes the slot rectangle for <paramref name="layout"/> inside
    /// <paramref name="workArea"/>.
    /// </summary>
    /// <remarks>
    /// Never throws: on degenerate work areas (e.g. smaller than 2·(E+G)) a
    /// best-effort 1×1 rectangle pinned inside the work area is returned.
    /// </remarks>
    public static Rectangle ComputeSlot(Rectangle workArea, SlotLayout layout, GapSettings gaps)
    {
        ArgumentNullException.ThrowIfNull(gaps);

        (int columns, int rows, int column, int row) = GetGrid(layout);

        int inset = gaps.EdgeGap + gaps.WindowGap;

        int usableWidth = workArea.Width - (2 * inset) - ((columns - 1) * gaps.WindowGap);
        int usableHeight = workArea.Height - (2 * inset) - ((rows - 1) * gaps.WindowGap);

        int cellWidth = usableWidth / columns;
        int cellHeight = usableHeight / rows;

        if (cellWidth < 1)
        {
            cellWidth = 1;
        }

        if (cellHeight < 1)
        {
            cellHeight = 1;
        }

        int x = workArea.X + inset + (column * (cellWidth + gaps.WindowGap));
        int y = workArea.Y + inset + (row * (cellHeight + gaps.WindowGap));

        return ClampToWorkArea(new Rectangle(x, y, cellWidth, cellHeight), workArea);
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

    private static (int Columns, int Rows, int Column, int Row) GetGrid(SlotLayout layout) =>
        layout switch
        {
            SlotLayout.CenterHalf => (1, 1, 0, 0),

            SlotLayout.HalfLeft => (2, 1, 0, 0),
            SlotLayout.HalfRight => (2, 1, 1, 0),

            SlotLayout.QuarterTopLeft => (2, 2, 0, 0),
            SlotLayout.QuarterTopRight => (2, 2, 1, 0),
            SlotLayout.QuarterBottomLeft => (2, 2, 0, 1),
            SlotLayout.QuarterBottomRight => (2, 2, 1, 1),

            SlotLayout.ThirdLeft => (3, 1, 0, 0),
            SlotLayout.ThirdCenter => (3, 1, 1, 0),
            SlotLayout.ThirdRight => (3, 1, 2, 0),

            SlotLayout.SixthTopLeft => (3, 2, 0, 0),
            SlotLayout.SixthTopCenter => (3, 2, 1, 0),
            SlotLayout.SixthTopRight => (3, 2, 2, 0),
            SlotLayout.SixthBottomLeft => (3, 2, 0, 1),
            SlotLayout.SixthBottomCenter => (3, 2, 1, 1),
            SlotLayout.SixthBottomRight => (3, 2, 2, 1),

            _ => throw new ArgumentOutOfRangeException(nameof(layout), layout, "Unknown slot layout."),
        };
}
