using System.Windows.Controls;
using Wintangle.App.UI.Controls;

namespace Wintangle.App.UI.Tabs;

/// <summary>
/// Mouse Actions tab — placeholder for mouse-button tiling actions (later phase).
/// </summary>
public sealed class MouseActionsTab : UserControl
{
    public MouseActionsTab()
    {
        Content = new PlaceholderPanel(
            "\uE72D", // Segoe MDL2 Assets: Share
            "Mouse Actions",
            "Tile and snap windows with mouse gestures. Shipping in a later release.");
    }
}
