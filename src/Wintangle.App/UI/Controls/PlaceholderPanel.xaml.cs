using System.Windows;
using System.Windows.Controls;

namespace Wintangle.App.UI.Controls;

/// <summary>
/// Centered "coming soon" placeholder: an icon glyph, a title, a description,
/// and an optional (disabled) button. Brushes come from the theme resources.
/// </summary>
public partial class PlaceholderPanel : UserControl
{
    public PlaceholderPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Creates a panel pre-filled with <paramref name="iconGlyph"/> (a Segoe
    /// MDL2 Assets glyph character), <paramref name="title"/> and
    /// <paramref name="description"/>. When <paramref name="buttonText"/> is
    /// non-empty the disabled button is shown with that label.
    /// </summary>
    public PlaceholderPanel(string iconGlyph, string title, string description, string? buttonText = null)
        : this()
    {
        IconGlyph.Text = iconGlyph;
        TitleText.Text = title;
        DescriptionText.Text = description;

        if (!string.IsNullOrEmpty(buttonText))
        {
            ActionButton.Content = buttonText;
            ActionButton.Visibility = Visibility.Visible;
        }
    }
}
