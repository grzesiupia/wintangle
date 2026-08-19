using System.Windows;
using System.Windows.Media;
using Wintangle.Core.Geometry;

namespace Wintangle.App.UI.Controls;

/// <summary>
/// Small preview of a <see cref="SlotLayout"/>: draws the slot shape inside a
/// rounded outline, mirroring the geometry <see cref="SlotCalculator"/> uses
/// (via <see cref="SlotCalculator.GetGrid"/>), so the thumbnail is faithful to
/// the real tiling result.
/// </summary>
/// <remarks>
/// Brushes are looked up from the theme resources at render time, so a theme
/// toggle only needs <see cref="InvalidateVisual"/> to repaint in the new
/// colors.
/// </remarks>
internal sealed class LayoutPreview : FrameworkElement
{
    /// <summary>The slot layout this preview renders.</summary>
    public static readonly DependencyProperty LayoutProperty = DependencyProperty.Register(
        nameof(Layout),
        typeof(SlotLayout),
        typeof(LayoutPreview),
        new FrameworkPropertyMetadata(
            SlotLayout.CenterHalf,
            FrameworkPropertyMetadataOptions.AffectsRender));

    public SlotLayout Layout
    {
        get => (SlotLayout)GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    /// <summary>Corner radius used for the outline and the slot cell.</summary>
    private const double CornerRadius = 4;

    /// <summary>Space between the preview edge and the outline (the "gap").</summary>
    private const double OutlineInset = 2;

    public LayoutPreview()
    {
        // FrameworkElement has no CornerRadius of its own; the radius lives in
        // the rounded rectangles drawn by OnRender.
        SnapsToDevicePixels = true;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double width = ActualWidth;
        double height = ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var outlineBrush = FindBrush("Brush.Border", Color.FromRgb(0x3A, 0x3A, 0x3A));
        var accentBrush = FindBrush("Brush.Accent", Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
        var selectionBrush = FindBrush("Brush.SelectionBorder", Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF));

        var outline = new Rect(OutlineInset, OutlineInset, width - (2 * OutlineInset), height - (2 * OutlineInset));
        if (outline.Width < 1 || outline.Height < 1)
        {
            return;
        }

        // Outer rounded outline.
        dc.DrawRoundedRectangle(null, new Pen(outlineBrush, 1), outline, CornerRadius, CornerRadius);

        // Slot cell — faithful grid shape via SlotCalculator.GetGrid.
        (int columns, int rows, int column, int row) = SlotCalculator.GetGrid(Layout);

        double cellWidth = outline.Width / columns;
        double cellHeight = outline.Height / rows;
        var cell = new Rect(
            outline.X + (column * cellWidth),
            outline.Y + (row * cellHeight),
            cellWidth,
            cellHeight);

        // Shrink the cell slightly so adjacent cells read as separate slots.
        const double cellInset = 1.5;
        cell = new Rect(
            cell.X + cellInset,
            cell.Y + cellInset,
            Math.Max(1, cell.Width - (2 * cellInset)),
            Math.Max(1, cell.Height - (2 * cellInset)));

        dc.DrawRoundedRectangle(accentBrush, new Pen(selectionBrush, 1.5), cell, CornerRadius - 1, CornerRadius - 1);
    }

    /// <summary>
    /// Theme brush lookup with a hardcoded dark fallback (used when the theme
    /// resource is missing, e.g. before App resources are wired up).
    /// </summary>
    private Brush FindBrush(string key, Color fallbackColor)
    {
        try
        {
            if (TryFindResource(key) is Brush brush)
            {
                return brush;
            }
        }
        catch (Exception)
        {
            // Resource lookup failed — fall through to the fallback.
        }

        return new SolidColorBrush(fallbackColor);
    }
}
