using System.Windows;
using System.Windows.Controls;
using Wintangle.Core.Hotkeys;

namespace Wintangle.App.UI.Controls;

/// <summary>
/// One slot preset card in the Window Layouts tab: a <see cref="SlotPreviewMini"/>
/// thumbnail, the slot name, and a keycaps row with the action's current
/// shortcut. Styled by "SlotCard" (hover → FgSoft/FgHoverBorder, selected →
/// AccentSoft/Accent); the keycap chips follow the selected state through
/// "SlotKeycap" / "SlotKeycapText". Clicking the card applies the slot — the
/// host wires the Click handler.
/// </summary>
public class SlotCard : Button
{
    /// <summary>The hotkey action this card applies (drives the name label).</summary>
    public static readonly DependencyProperty ActionProperty = DependencyProperty.Register(
        nameof(Action),
        typeof(HotkeyAction),
        typeof(SlotCard),
        new FrameworkPropertyMetadata(HotkeyAction.CenterHalf, OnActionChanged));

    /// <summary>Selected state — last applied slot; repaints the preview cell.</summary>
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected),
        typeof(bool),
        typeof(SlotCard),
        new FrameworkPropertyMetadata(false, OnIsSelectedChanged));

    private readonly SlotPreviewMini _preview;
    private readonly TextBlock _nameText;
    private readonly WrapPanel _chipsHost;

    public SlotCard()
    {
        _preview = new SlotPreviewMini
        {
            Height = 30,
            Margin = new Thickness(0, 0, 0, 9),
        };

        _nameText = new TextBlock
        {
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Text = UiLabels.ActionName(Action),
        };
        _nameText.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Fg");

        _chipsHost = new WrapPanel
        {
            Margin = new Thickness(0, 6, 0, 0),
        };

        var stack = new StackPanel();
        stack.Children.Add(_preview);
        stack.Children.Add(_nameText);
        stack.Children.Add(_chipsHost);
        Content = stack;
    }

    public HotkeyAction Action
    {
        get => (HotkeyAction)GetValue(ActionProperty);
        set => SetValue(ActionProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>Repaints the thumbnail from the current theme (theme change hook).</summary>
    internal void RefreshVisuals() => _preview.InvalidateVisual();

    /// <summary>Rebuilds the keycap chips for <paramref name="hotkey"/> (null clears the row).</summary>
    public void SetShortcut(Hotkey? hotkey)
    {
        _chipsHost.Children.Clear();
        if (hotkey is not Hotkey effective)
        {
            return;
        }

        foreach (var part in HotkeyLabels.KeycapParts(effective))
        {
            _chipsHost.Children.Add(CreateChip(part));
        }
    }

    private Border CreateChip(string part)
    {
        var text = new TextBlock { Text = part };
        text.SetResourceReference(TextBlock.StyleProperty, "SlotKeycapText");

        var chip = new Border { Child = text };
        chip.SetResourceReference(Border.StyleProperty, "SlotKeycap");
        return chip;
    }

    private static void OnActionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var card = (SlotCard)d;
        card._nameText.Text = UiLabels.ActionName((HotkeyAction)e.NewValue);
    }

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SlotCard)d)._preview.IsSelected = (bool)e.NewValue;
    }
}
