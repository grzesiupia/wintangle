using System.Windows;
using System.Windows.Controls.Primitives;

namespace Wintangle.App.UI.Controls;

/// <summary>
/// Settings tab-rail button: 17px icon + label. <see cref="IsSelected"/>
/// mirrors <see cref="ToggleButton.IsChecked"/> (both directions) so the shell
/// can drive selection either way; the "TabButton" style paints hover/checked
/// states. Buttons sharing a non-empty <see cref="GroupName"/> are mutually
/// exclusive (only one checked at a time) — WPF's built-in grouping lives on
/// RadioButton, not ToggleButton, so TabButton implements it itself.
/// </summary>
public class TabButton : ToggleButton
{
    /// <summary>Path data for the rail icon (24×24 space), e.g. the layouts 4-rect mark.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(string),
        typeof(TabButton),
        new PropertyMetadata(null));

    /// <summary>Selected state; kept in sync with <see cref="ToggleButton.IsChecked"/>.</summary>
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected),
        typeof(bool),
        typeof(TabButton),
        new FrameworkPropertyMetadata(
            false,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnIsSelectedChanged));

    /// <summary>
    /// Mutual-exclusion group name (mirrors RadioButton.GroupName). Buttons
    /// with the same non-empty name uncheck each other when one is checked.
    /// </summary>
    public static readonly DependencyProperty GroupNameProperty = DependencyProperty.Register(
        nameof(GroupName),
        typeof(string),
        typeof(TabButton),
        new PropertyMetadata(null, OnGroupNameChanged));

    /// <summary>Live group members by name (strong refs; removed on Unloaded).</summary>
    private static readonly Dictionary<string, List<TabButton>> s_groups = new(StringComparer.Ordinal);

    public string? Icon
    {
        get => (string?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public string? GroupName
    {
        get => (string?)GetValue(GroupNameProperty);
        set => SetValue(GroupNameProperty, value);
    }

    public TabButton()
    {
        // A closed settings window must not leak its buttons in the group
        // registry (each open creates a fresh window with fresh buttons).
        Unloaded += OnButtonUnloaded;
    }

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((TabButton)d).IsChecked = (bool)e.NewValue;
    }

    private static void OnGroupNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var button = (TabButton)d;

        if (e.OldValue is string old && old.Length > 0 && s_groups.TryGetValue(old, out var oldList))
        {
            oldList.Remove(button);
            if (oldList.Count == 0)
            {
                s_groups.Remove(old);
            }
        }

        if (e.NewValue is string name && name.Length > 0)
        {
            if (!s_groups.TryGetValue(name, out var list))
            {
                list = new List<TabButton>();
                s_groups[name] = list;
            }

            list.Add(button);
        }
    }

    protected override void OnChecked(RoutedEventArgs e)
    {
        base.OnChecked(e);
        SetCurrentValue(IsSelectedProperty, true);

        if (string.IsNullOrEmpty(GroupName))
        {
            return;
        }

        // Mutually exclusive group: uncheck every sibling sharing the name.
        if (s_groups.TryGetValue(GroupName, out var list))
        {
            foreach (var sibling in list)
            {
                if (!ReferenceEquals(sibling, this) && sibling.IsChecked == true)
                {
                    sibling.SetCurrentValue(IsCheckedProperty, false);
                }
            }
        }
    }

    protected override void OnUnchecked(RoutedEventArgs e)
    {
        base.OnUnchecked(e);
        SetCurrentValue(IsSelectedProperty, false);
    }

    private void OnButtonUnloaded(object sender, RoutedEventArgs e)
    {
        if (GroupName is { Length: > 0 } name && s_groups.TryGetValue(name, out var list))
        {
            list.Remove(this);
            if (list.Count == 0)
            {
                s_groups.Remove(name);
            }
        }
    }
}
