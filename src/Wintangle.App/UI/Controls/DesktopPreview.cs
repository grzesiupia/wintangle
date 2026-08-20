using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Wintangle.Core.Geometry;
using Wintangle.Core.Hotkeys;

namespace Wintangle.App.UI.Controls;

/// <summary>
/// Live desktop mock for the Window Layouts tab: a rounded desktop backdrop
/// with a taskbar strip, a slot label, and an animated "window" that moves/
/// resizes to the selected slot (or the default position when no slot is
/// selected). The window follows the design's preview math via
/// <see cref="SlotFraction.ComputePreviewRect"/> plus the taskbar offset.
/// Aspect is fixed at ~16/9.6 (Height = Width / 1.6667 on every size change).
/// </summary>
/// <remarks>
/// All brushes resolve through DynamicResource at layout time; a theme swap
/// only needs <see cref="Refresh"/> (re-resolves the shadow color and repaints).
/// </remarks>
internal sealed class DesktopPreview : ContentControl
{
    /// <summary>Design aspect ratio: 16 / 9.6.</summary>
    private const double AspectRatio = 16d / 9.6;

    /// <summary>Taskbar strip height as a fraction of the mock height.</summary>
    private const double TaskbarFraction = 0.09;

    /// <summary>Window position/size animation duration (design: 0.26s).</summary>
    private const double AnimationSeconds = 0.26;

    private readonly Canvas _winLayer;
    private readonly Border _win;
    private readonly TextBlock _barTitle;
    private readonly TextBlock _bodyText;
    private readonly TextBlock _slotLabel;
    private readonly Border _taskbar;

    private string _proc = "window";
    private string _title = "…";
    private SlotLayout? _slot;
    private int _gap;
    private int _edge;
    private bool _laidOut;

    public DesktopPreview()
    {
        Focusable = false;

        var root = new Border
        {
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
        };
        root.SetResourceReference(Border.BackgroundProperty, "Brush.Bg2");
        root.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");

        var grid = new Grid();
        root.Child = grid;
        Content = root;

        // Slot label — top-left, mono 10 uppercase.
        _slotLabel = new TextBlock
        {
            FontSize = 10,
            Margin = new Thickness(12, 10, 12, 0),
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Left,
            Text = "NO SLOT SELECTED — PICK A SLOT",
        };
        _slotLabel.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Muted");
        _slotLabel.SetResourceReference(TextBlock.FontFamilyProperty, "Font.Mono");
        grid.Children.Add(_slotLabel);

        // Window layer — Canvas so the desk-win can be absolutely placed.
        _winLayer = new Canvas();
        grid.Children.Add(_winLayer);

        _win = new Border
        {
            CornerRadius = new CornerRadius(7),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Effect = new DropShadowEffect
            {
                BlurRadius = 14,
                ShadowDepth = 6,
                Opacity = 0.25,
                Direction = 270,
            },
        };
        _win.SetResourceReference(Border.BackgroundProperty, "Brush.Surface");
        _win.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");

        var winGrid = new Grid();
        winGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
        winGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // dw-bar — 20px, Bg2, bottom border, 3 dots + mono title.
        var bar = new Border { BorderThickness = new Thickness(0, 0, 0, 1) };
        bar.SetResourceReference(Border.BackgroundProperty, "Brush.Bg2");
        bar.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");
        var barPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(8, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        for (var i = 0; i < 3; i++)
        {
            var dot = new Border
            {
                Width = 6,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 0, 6, 0),
            };
            dot.SetResourceReference(Border.BackgroundProperty, "Brush.Border");
            barPanel.Children.Add(dot);
        }

        _barTitle = new TextBlock
        {
            FontSize = 9,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _barTitle.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Muted");
        _barTitle.SetResourceReference(TextBlock.FontFamilyProperty, "Font.Mono");
        barPanel.Children.Add(_barTitle);
        bar.Child = barPanel;
        Grid.SetRow(bar, 0);
        winGrid.Children.Add(bar);

        // dw-body — mono 9 process name, centered.
        var body = new Grid();
        _bodyText = new TextBlock
        {
            FontSize = 9,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _bodyText.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Muted60");
        _bodyText.SetResourceReference(TextBlock.FontFamilyProperty, "Font.Mono");
        body.Children.Add(_bodyText);
        Grid.SetRow(body, 1);
        winGrid.Children.Add(body);

        _win.Child = winGrid;
        _winLayer.Children.Add(_win);

        // Taskbar — bottom strip, DeskTaskbar bg, top border, two 14×14 icons right-aligned.
        _taskbar = new Border
        {
            Height = 0,
            VerticalAlignment = VerticalAlignment.Bottom,
            BorderThickness = new Thickness(0, 1, 0, 0),
        };
        _taskbar.SetResourceReference(Border.BackgroundProperty, "Brush.DeskTaskbar");
        _taskbar.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");
        var taskPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        for (var i = 0; i < 2; i++)
        {
            var icon = new Border
            {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 6, 0),
            };
            icon.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");
            taskPanel.Children.Add(icon);
        }

        _taskbar.Child = taskPanel;
        grid.Children.Add(_taskbar);

        SizeChanged += OnSizeChanged;
        Loaded += (_, _) => Refresh();
    }

    /// <summary>The window shown on the mock ("Code.exe" etc.).</summary>
    public void SetWindow(string proc, string title)
    {
        _proc = proc;
        _title = title;
    }

    /// <summary>The slot to preview (null = default position), plus the current gaps.</summary>
    public void SetSlot(SlotLayout? layout, int gap, int edge)
    {
        _slot = layout;
        _gap = gap;
        _edge = edge;
    }

    /// <summary>
    /// Repositions/resizes the desk-win (animated) and repaints the chrome.
    /// No slot → default (0.24W, 0.3H, 0.52W, 0.4H) position.
    /// </summary>
    public void Refresh()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }

        double w = ActualWidth;
        double h = ActualHeight;
        double taskH = Math.Max(1, h * TaskbarFraction);
        double workH = Math.Max(1, h - taskH);
        _taskbar.Height = taskH;

        double x, y, winW, winH;
        if (_slot is { } layout)
        {
            var rect = SlotFraction.ComputePreviewRect(w, workH, layout, _gap, _edge);
            x = rect.X;
            y = rect.Y;
            winW = rect.Width;
            winH = rect.Height;
            // HotkeyAction's first 16 values map 1:1 to SlotLayout (documented
            // invariant in HotkeyAction.cs), so the action name is the slot name.
            _slotLabel.Text = UiLabels.ActionName((HotkeyAction)layout).ToUpperInvariant();
        }
        else
        {
            winW = w * 0.52;
            winH = workH * 0.4;
            x = w * 0.24;
            y = workH * 0.3;
            _slotLabel.Text = "NO SLOT SELECTED — PICK A SLOT";
        }

        // Taskbar offset, then clamp the window into the work area.
        y += taskH;
        x = Math.Max(0, Math.Min(x, Math.Max(0, w - winW)));
        y = Math.Max(taskH, Math.Min(y, Math.Max(taskH, h - winH)));

        _barTitle.Text = $"{_proc} — {_title}";
        _bodyText.Text = _proc.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? _proc[..^4]
            : _proc;

        // The shadow color follows the theme — re-resolve at render time.
        if (_win.Effect is DropShadowEffect shadow && TryFindResource("Brush.Fg") is SolidColorBrush fg)
        {
            shadow.Color = fg.Color;
            shadow.Opacity = 0.25;
        }

        if (!_laidOut)
        {
            // First layout: place directly (no animation from zero).
            Canvas.SetLeft(_win, x);
            Canvas.SetTop(_win, y);
            _win.Width = winW;
            _win.Height = winH;
            _laidOut = true;
            return;
        }

        Animate(Canvas.LeftProperty, _win, x);
        Animate(Canvas.TopProperty, _win, y);
        Animate(FrameworkElement.WidthProperty, _win, winW);
        Animate(FrameworkElement.HeightProperty, _win, winH);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ActualWidth <= 0)
        {
            return;
        }

        // Keep the design aspect ratio (~16/9.6). The guard prevents a loop
        // when the height we set feeds back through SizeChanged.
        var target = ActualWidth / AspectRatio;
        if (Math.Abs(ActualHeight - target) > 0.5)
        {
            Height = target;
        }

        Refresh();
    }

    /// <summary>Animates one geometry property to <paramref name="to"/> over the design duration.</summary>
    private static void Animate(DependencyProperty property, UIElement target, double to)
    {
        var animation = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromSeconds(AnimationSeconds),
        };
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(
            to,
            KeyTime.FromTimeSpan(TimeSpan.FromSeconds(AnimationSeconds)),
            new KeySpline(0.2, 0.7, 0.2, 1.0)));
        target.BeginAnimation(property, animation);
    }
}
