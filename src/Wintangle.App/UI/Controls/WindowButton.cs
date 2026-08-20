using System.Windows;
using System.Windows.Controls;

namespace Wintangle.App.UI.Controls;

/// <summary>
/// Title-bar window button (minimize / maximize-restore / close) for the
/// settings window's custom chrome. The glyph comes from the <see cref="Glyph"/>
/// path data; <see cref="IsRestore"/> swaps the maximize rectangle for the
/// overlapping-rect restore glyph; <see cref="IsClose"/> switches the hover
/// treatment to the danger (close) colors. Styled by "TitlebarButton".
/// </summary>
public class WindowButton : Button
{
    /// <summary>Path data for the button glyph (12×12 space), e.g. "M1.5 6h9".</summary>
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph),
        typeof(string),
        typeof(WindowButton),
        new PropertyMetadata(null));

    /// <summary>When true the template shows the "restore" glyph instead of the maximize rect.</summary>
    public static readonly DependencyProperty IsRestoreProperty = DependencyProperty.Register(
        nameof(IsRestore),
        typeof(bool),
        typeof(WindowButton),
        new PropertyMetadata(false));

    /// <summary>When true the hover state uses the danger (close) treatment.</summary>
    public static readonly DependencyProperty IsCloseProperty = DependencyProperty.Register(
        nameof(IsClose),
        typeof(bool),
        typeof(WindowButton),
        new PropertyMetadata(false));

    public string? Glyph
    {
        get => (string?)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public bool IsRestore
    {
        get => (bool)GetValue(IsRestoreProperty);
        set => SetValue(IsRestoreProperty, value);
    }

    public bool IsClose
    {
        get => (bool)GetValue(IsCloseProperty);
        set => SetValue(IsCloseProperty, value);
    }
}
