using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Wintangle.App.Services;
using Wintangle.App.UI.Controls;
using Wintangle.Core.Hotkeys;

namespace Wintangle.App.UI.Tabs;

/// <summary>
/// Window Layouts tab (Phase 2): the 16 slot preset cards (with live shortcut
/// chips) and a live desktop mock on the left; the active windows list with
/// refresh on the right. Clicking a slot applies it to the window that owned
/// the foreground before settings opened (via the shell's apply callback) and
/// marks it selected. The desktop mock previews the last applied slot on the
/// currently selected window. Theme swaps repaint the previews through
/// <see cref="RefreshPreviews"/>; the shell calls <see cref="RefreshShortcuts"/>,
/// <see cref="RefreshActiveWindows"/> and <see cref="UpdatePreview"/> on tab
/// activation, and <see cref="Teardown"/> when the window closes.
/// </summary>
public partial class LayoutsTab : UserControl
{
    private readonly ConfigService _config;
    private readonly Action<HotkeyAction>? _applyPreset;

    private readonly List<SlotCard> _cards = new();
    private readonly List<Button> _windowRows = new();
    private readonly List<ActiveWindowInfo> _windows = new();

    private HotkeyAction? _selectedAction;
    private int _selectedWindow = -1;

    internal LayoutsTab(ConfigService config, Action<HotkeyAction>? applyPreset)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _applyPreset = applyPreset;

        InitializeComponent();
        BuildCards();

        _config.ThemeChanged += OnThemeChanged;
        RefreshShortcuts();
        RefreshActiveWindows();
        UpdatePreview();
    }

    // ---- Shell surface ----

    /// <summary>
    /// Re-reads the effective shortcut bindings and updates every card's chips.
    /// Called when the tab is activated and after defaults are restored.
    /// </summary>
    public void RefreshShortcuts()
    {
        foreach (var card in _cards)
        {
            card.SetShortcut(_config.GetShortcut(card.Action) ?? DefaultHotkeys.FindHotkey(card.Action));
        }
    }

    /// <summary>
    /// Re-enumerates the active windows list, keeping the previously selected
    /// window selected when it survives the refresh.
    /// </summary>
    public void RefreshActiveWindows()
    {
        // Snapshot the previously selected window (process + title) BEFORE the
        // list is replaced — the old index no longer means anything once
        // _windows is re-enumerated.
        var previous = _selectedWindow >= 0 && _selectedWindow < _windows.Count
            ? ((string, string)?)(_windows[_selectedWindow].ProcessName, _windows[_selectedWindow].Title)
            : null;
        var previousIndex = _selectedWindow;

        _windows.Clear();
        _windows.AddRange(ActiveWindows.Enumerate());
        RebuildWindowRows(previous, previousIndex);
    }

    /// <summary>Re-renders the live preview (shell hook on tab activation).</summary>
    public void UpdatePreview() => ApplySlotToPreview(_selectedAction);

    /// <summary>
    /// Repaints every card thumbnail and the desktop mock from the current
    /// theme resources (called on a theme change — brushes resolve at render).
    /// </summary>
    public void RefreshPreviews()
    {
        foreach (var card in _cards)
        {
            card.RefreshVisuals();
        }

        Desktop.Refresh();
    }

    /// <summary>Detaches the theme subscription (window teardown).</summary>
    public void Teardown() => _config.ThemeChanged -= OnThemeChanged;

    // ---- Slot cards ----

    /// <summary>
    /// The 16 slot actions in the exact display order used by the preset grid
    /// (center-half first, then halves, quarters, thirds, sixths — matching the
    /// design's SLOTS order). Explicit (not enum order) so reordering either
    /// enum can never silently re-wire a card to the wrong slot.
    /// </summary>
    private static readonly HotkeyAction[] s_slotActions = new[]
    {
        HotkeyAction.CenterHalf,
        HotkeyAction.HalfLeft,
        HotkeyAction.HalfRight,
        HotkeyAction.QuarterTopLeft,
        HotkeyAction.QuarterTopRight,
        HotkeyAction.QuarterBottomLeft,
        HotkeyAction.QuarterBottomRight,
        HotkeyAction.ThirdLeft,
        HotkeyAction.ThirdCenter,
        HotkeyAction.ThirdRight,
        HotkeyAction.SixthTopLeft,
        HotkeyAction.SixthTopCenter,
        HotkeyAction.SixthTopRight,
        HotkeyAction.SixthBottomLeft,
        HotkeyAction.SixthBottomCenter,
        HotkeyAction.SixthBottomRight,
    };

    private void BuildCards()
    {
        foreach (var action in s_slotActions)
        {
            var card = new SlotCard
            {
                Action = action,
                Margin = new Thickness(5), // 10px grid gap
            };
            card.SetResourceReference(Button.StyleProperty, "SlotCard");
            card.Click += (_, _) => OnSlotCardClick(action, card);
            _cards.Add(card);
            SlotGrid.Children.Add(card);
        }
    }

    private void OnSlotCardClick(HotkeyAction action, SlotCard clicked)
    {
        _selectedAction = action;
        foreach (var card in _cards)
        {
            card.IsSelected = ReferenceEquals(card, clicked);
        }

        // Apply to the pre-settings foreground window, then preview it.
        _applyPreset?.Invoke(action);
        ApplySlotToPreview(action);
    }

    // ---- Active windows ----

    private void RebuildWindowRows((string ProcessName, string Title)? previous, int previousIndex)
    {
        WindowsList.Items.Clear();
        _windowRows.Clear();

        if (_windows.Count == 0)
        {
            var empty = new TextBlock
            {
                Text = "No windows found.",
                FontSize = 12.5,
                Margin = new Thickness(4, 2, 0, 0),
            };
            empty.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Muted");
            WindowsList.Items.Add(empty);
            _selectedWindow = -1;
            return;
        }

        _selectedWindow = -1;

        for (var i = 0; i < _windows.Count; i++)
        {
            var info = _windows[i];
            if (_selectedWindow < 0 && previous is { } snap && (info.ProcessName, info.Title) == snap)
            {
                _selectedWindow = i;
            }

            var row = CreateWindowRow(i, info);
            _windowRows.Add(row);
            WindowsList.Items.Add(row);
        }

        if (_selectedWindow < 0)
        {
            // The previously selected window is gone — fall back to the same
            // index (clamped to the new list length).
            _selectedWindow = previousIndex >= 0 && previousIndex < _windows.Count ? previousIndex : 0;
        }

        UpdateRowSelection();
        ApplySlotToPreview(_selectedAction);
    }

    private Button CreateWindowRow(int index, ActiveWindowInfo info)
    {
        var row = new Button
        {
            Margin = new Thickness(0, 0, 0, 6),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        row.SetResourceReference(Button.StyleProperty, "WinRow");

        var proc = new TextBlock { Text = info.ProcessName };
        proc.SetResourceReference(TextBlock.StyleProperty, "WinProcText");
        var title = new TextBlock { Text = info.Title };
        title.SetResourceReference(TextBlock.StyleProperty, "WinTitleText");
        var textStack = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
        textStack.Children.Add(proc);
        textStack.Children.Add(title);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(textStack, 0);
        grid.Children.Add(textStack);

        if (info.IsElevated)
        {
            var badgeText = new TextBlock { Text = "elevated" };
            badgeText.SetResourceReference(TextBlock.StyleProperty, "BadgeText");
            var badge = new Border
            {
                Child = badgeText,
                VerticalAlignment = VerticalAlignment.Center,
            };
            badge.SetResourceReference(Border.StyleProperty, "Badge");
            Grid.SetColumn(badge, 1);
            grid.Children.Add(badge);
        }

        row.Content = grid;
        row.Click += (_, _) => SelectWindow(index);
        return row;
    }

    private void SelectWindow(int index)
    {
        _selectedWindow = index;
        UpdateRowSelection();
        ApplySlotToPreview(_selectedAction);
    }

    private void UpdateRowSelection()
    {
        for (var i = 0; i < _windowRows.Count; i++)
        {
            var row = _windowRows[i];
            if (i == _selectedWindow)
            {
                row.SetResourceReference(Button.BackgroundProperty, "Brush.AccentSoft");
                row.SetResourceReference(Button.BorderBrushProperty, "Brush.Accent");
            }
            else
            {
                row.ClearValue(Button.BackgroundProperty);
                row.ClearValue(Button.BorderBrushProperty);
            }
        }
    }

    // ---- Preview ----

    private void ApplySlotToPreview(HotkeyAction? action)
    {
        var layout = action?.ToSlotLayout();
        Desktop.SetSlot(layout, _config.Current.WindowGap, _config.Current.EdgeGap);

        if (_selectedWindow >= 0 && _selectedWindow < _windows.Count)
        {
            var info = _windows[_selectedWindow];
            Desktop.SetWindow(info.ProcessName, info.Title);
        }

        Desktop.Refresh();
    }

    // ---- Refresh button (rotate the icon 360° on click) ----

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshActiveWindows();
        AnimateRefreshIcon();
    }

    private void AnimateRefreshIcon()
    {
        if (RefreshIcon.RenderTransform is not RotateTransform rotate)
        {
            rotate = new RotateTransform(0);
            RefreshIcon.RenderTransform = rotate;
            RefreshIcon.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        rotate.BeginAnimation(RotateTransform.AngleProperty, null);
        rotate.Angle = 0;
        var animation = new DoubleAnimation(360, TimeSpan.FromSeconds(0.3));
        animation.Completed += (_, _) =>
        {
            rotate.BeginAnimation(RotateTransform.AngleProperty, null);
            rotate.Angle = 0;
        };
        rotate.BeginAnimation(RotateTransform.AngleProperty, animation);
    }

    // ---- Theme ----

    private void OnThemeChanged(string theme)
    {
        // ThemeChanged may fire on the watcher thread — marshal to the UI thread.
        if (Dispatcher.CheckAccess())
        {
            RefreshPreviews();
            return;
        }

        try
        {
            Dispatcher.BeginInvoke(() => RefreshPreviews());
        }
        catch (InvalidOperationException)
        {
            // Dispatcher rejected work at shutdown; the window is going away.
        }
    }
}
