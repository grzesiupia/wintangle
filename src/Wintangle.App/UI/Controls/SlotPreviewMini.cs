using System.Windows;
using System.Windows.Media;
using Wintangle.Core.Geometry;

namespace Wintangle.App.UI.Controls;

/// <summary>
/// Small slot thumbnail used inside a <see cref="SlotCard"/>: a rounded
/// Bg2 backdrop with the slot's fractional cell drawn over it (mirroring the
/// design's <c>.slot-prev</c>). The selected state swaps the cell to the
/// accent treatment. Brushes are looked up from the theme resources at render
/// time, so a theme toggle only needs <see cref="InvalidateVisual"/> to
/// repaint in the new colors (the owning card wires that up).
/// </summary>
internal sealed class SlotPreviewMini : FrameworkElement
{
    /// <summary>The slot layout this preview renders.</summary>
    public static readonly DependencyProperty LayoutProperty = DependencyProperty.Register(
        nameof(Layout),
        typeof(SlotLayout),
        typeof(SlotPreviewMini),
        new FrameworkPropertyMetadata(
            SlotLayout.CenterHalf,
            FrameworkPropertyMetadataOptions.AffectsRender));

    private bool _isSelected;

    public SlotLayout Layout
    {
        get => (SlotLayout)GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    /// <summary>
    /// Selected rendering (AccentSoft cell + 1.5px Accent stroke). Not a DP —
    /// the owning <see cref="SlotCard"/> drives it from its own IsSelected.
    /// </summary>
    internal bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            InvalidateVisual();
        }
    }

    /// <summary>Backdrop corner radius (design: 5px).</summary>
    private const double BackdropRadius = 5;

    /// <summary>Cell corner radius (slight rounding so adjacent cells read as slots).</summary>
    private const double CellRadius = 2;

    public SlotPreviewMini()
    {
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

        var bg = FindBrush("Brush.Bg2", Color.FromArgb(0xFF, 0x0D, 0x12, 0x17));
        var border = FindBrush("Brush.Border", Color.FromArgb(0xFF, 0x28, 0x2F, 0x36));
        var cellFill = FindBrush(_isSelected ? "Brush.AccentSoft" : "Brush.FgSoft",
            _isSelected ? Color.FromArgb(0x2E, 0x6B, 0xAD, 0xFF) : Color.FromArgb(0x12, 0xE9, 0xEB, 0xEE));
        var cellStroke = FindBrush(_isSelected ? "Brush.Accent" : "Brush.Border",
            _isSelected ? Color.FromArgb(0xFF, 0x6B, 0xAD, 0xFF) : Color.FromArgb(0xFF, 0x28, 0x2F, 0x36));

        // Rounded backdrop.
        var backdrop = new Rect(0, 0, width, height);
        dc.DrawRoundedRectangle(bg, new Pen(border, 1), backdrop, BackdropRadius, BackdropRadius);

        // Fractional cell (design's per-edge rule is not needed here — the
        // thumbnail just paints the fraction, matching .slot-prev i).
        (double l, double t, double r, double b) = SlotFraction.GetFraction(Layout);
        var cell = new Rect(l * width, t * height, (r - l) * width, (b - t) * height);
        if (cell.Width < 1 || cell.Height < 1)
        {
            return;
        }

        double stroke = _isSelected ? 1.5 : 1;
        dc.DrawRoundedRectangle(cellFill, new Pen(cellStroke, stroke), cell, CellRadius, CellRadius);
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
