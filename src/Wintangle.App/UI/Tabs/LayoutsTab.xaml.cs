using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Wintangle.App.Services;
using Wintangle.App.UI.Controls;
using Wintangle.Core.Geometry;
using Wintangle.Core.Hotkeys;

namespace Wintangle.App.UI.Tabs;

/// <summary>
/// Window Layouts tab: a clickable grid of the 16 slot presets (with their
/// live shortcut chips) on the left, and the active windows list plus the
/// (placeholder) rule builder on the right. Clicking a preset applies it to
/// the window that owned the foreground before settings opened.
/// </summary>
public partial class LayoutsTab : UserControl
{
    private readonly ConfigService _config;
    private readonly Action<HotkeyAction>? _applyPreset;

    private readonly List<(HotkeyAction Action, TextBlock ChipText)> _chips = new();
    private readonly List<LayoutPreview> _previews = new();

    internal LayoutsTab(ConfigService config, Action<HotkeyAction>? applyPreset)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _applyPreset = applyPreset;

        InitializeComponent();

        BuildCards();
        FillRuleBuilderSamples();
        RefreshActiveWindows();
        RefreshShortcuts();
    }

    /// <summary>
    /// Re-reads the effective shortcut bindings and updates every card's chip.
    /// Called when the tab is activated and after defaults are restored.
    /// </summary>
    public void RefreshShortcuts()
    {
        foreach (var (action, chipText) in _chips)
        {
            chipText.Text = FormatShortcut(action);
        }
    }

    /// <summary>Re-enumerates the active windows list.</summary>
    public void RefreshActiveWindows()
    {
        WindowsList.ItemsSource = ActiveWindows.Enumerate();
    }

    /// <summary>
    /// Repaints every preview from the current theme resources. Called on a
    /// theme change (the brushes are resolved at render time).
    /// </summary>
    public void RefreshPreviews()
    {
        foreach (var preview in _previews)
        {
            preview.InvalidateVisual();
        }
    }

    // ---- Card building ----

    /// <summary>
    /// The 16 slot actions in the exact display order used by the preset grid
    /// and the rule-builder dropdowns. Explicit (not enum order) so reordering
    /// either enum can never silently re-wire a card to the wrong slot.
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
            CardsGrid.Children.Add(CreateCard(action, action.ToSlotLayout()!.Value));
        }
    }

    private Border CreateCard(HotkeyAction action, SlotLayout layout)
    {
        var preview = new LayoutPreview
        {
            Layout = layout,
            Width = 72,
            Height = 44,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _previews.Add(preview);

        var label = new TextBlock
        {
            Text = UiLabels.ActionName(action),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0),
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "Brush.TextSecondary");

        var chipText = new TextBlock
        {
            FontSize = 11,
        };
        chipText.SetResourceReference(TextBlock.ForegroundProperty, "Brush.ShortcutChipFg");
        _chips.Add((action, chipText));

        var chip = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(0, 6, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = chipText,
        };
        chip.SetResourceReference(Border.BackgroundProperty, "Brush.ShortcutChipBg");

        var stack = new StackPanel();
        stack.Children.Add(preview);
        stack.Children.Add(label);
        stack.Children.Add(chip);

        var card = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8),
            Margin = new Thickness(0, 0, 8, 8),
            Cursor = Cursors.Hand,
            Child = stack,
        };
        card.SetResourceReference(Border.BackgroundProperty, "Brush.Surface");
        card.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");
        card.MouseLeftButtonUp += (_, _) => _applyPreset?.Invoke(action);

        return card;
    }

    private void FillRuleBuilderSamples()
    {
        // Placeholder rule builder (approved out of scope): a sample app and
        // the layout dropdowns are populated but disabled.
        RuleAppCombo.Items.Add("notepad.exe");
        RuleAppCombo.SelectedIndex = 0;

        foreach (var action in s_slotActions)
        {
            var name = UiLabels.ActionName(action);
            RuleLayoutCombo.Items.Add(name);
            RuleElseCombo.Items.Add(name);
        }

        RuleLayoutCombo.SelectedIndex = 0;
        RuleElseCombo.SelectedIndex = 0;
    }

    private string FormatShortcut(HotkeyAction action)
    {
        var hotkey = _config.GetShortcut(action) ?? DefaultHotkeys.FindHotkey(action);
        return hotkey is { } h ? HotkeyLabels.Format(h) : string.Empty;
    }

    private void RefreshWindowsButton_Click(object sender, RoutedEventArgs e) => RefreshActiveWindows();
}
