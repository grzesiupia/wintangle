using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wintangle.Core.Config;

namespace Wintangle.App.UI.Controls;

/// <summary>
/// One theme card in the Settings tab's Appearance group: a color dot
/// (<c>ThemeDotDark</c> / <c>ThemeDotLight</c> per <see cref="Theme"/>), the
/// theme name and a short note. Styled by "ThemeCard" (hover →
/// FgHoverBorder; selected → Accent border + AccentSoft bg); the host wires
/// the Click handler and drives <see cref="IsSelected"/>.
/// </summary>
public class ThemeCard : Button
{
    /// <summary>Theme key this card applies ("Dark" / "Light").</summary>
    public static readonly DependencyProperty ThemeProperty = DependencyProperty.Register(
        nameof(Theme),
        typeof(string),
        typeof(ThemeCard),
        new PropertyMetadata(ConfigModel.ThemeDark, OnThemeVisualChanged));

    /// <summary>Secondary note under the theme name (e.g. "Default").</summary>
    public static readonly DependencyProperty NoteProperty = DependencyProperty.Register(
        nameof(Note),
        typeof(string),
        typeof(ThemeCard),
        new PropertyMetadata(string.Empty, OnThemeVisualChanged));

    /// <summary>Selected state — the currently applied theme; paints Accent/AccentSoft.</summary>
    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected),
        typeof(bool),
        typeof(ThemeCard),
        new FrameworkPropertyMetadata(false));

    private readonly Border _dot;
    private readonly TextBlock _titleText;
    private readonly TextBlock _noteText;

    public ThemeCard()
    {
        _dot = new Border
        {
            Width = 26,
            Height = 26,
            CornerRadius = new CornerRadius(7),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _dot.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");

        _titleText = new TextBlock
        {
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
        };
        _titleText.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Fg");

        _noteText = new TextBlock
        {
            FontSize = 12,
        };
        _noteText.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Muted");

        var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textStack.Children.Add(_titleText);
        textStack.Children.Add(_noteText);

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(_dot);
        row.Children.Add(textStack);
        Content = row;

        UpdateThemeVisual();
    }

    public string Theme
    {
        get => (string)GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    public string Note
    {
        get => (string)GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>
    /// Repaints the dot and labels from <see cref="Theme"/> / <see cref="Note"/>
    /// (the dot brush and title resolve through the theme resources).
    /// </summary>
    private void UpdateThemeVisual()
    {
        var isDark = string.Equals(ConfigStore.NormalizeTheme(Theme), ConfigModel.ThemeDark, StringComparison.Ordinal);
        _dot.SetResourceReference(Border.BackgroundProperty, isDark ? "Brush.ThemeDotDark" : "Brush.ThemeDotLight");
        _titleText.Text = isDark ? ConfigModel.ThemeDark : ConfigModel.ThemeLight;
        _noteText.Text = Note;
    }

    private static void OnThemeVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((ThemeCard)d).UpdateThemeVisual();
}
