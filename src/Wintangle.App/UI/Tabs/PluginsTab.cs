using System.Windows.Controls;
using Wintangle.App.UI.Controls;

namespace Wintangle.App.UI.Tabs;

/// <summary>
/// Plugins tab — placeholder for the plugin system (later phase).
/// </summary>
public sealed class PluginsTab : UserControl
{
    public PluginsTab()
    {
        Content = new PlaceholderPanel(
            "\uE710", // Segoe MDL2 Assets: Add
            "Plugins",
            "Extend wintangle with community plugins. Shipping in a later release.");
    }
}
