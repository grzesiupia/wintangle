using System.Windows.Controls;
using Wintangle.App.UI.Controls;

namespace Wintangle.App.UI.Tabs;

/// <summary>
/// Workspaces tab — placeholder for named workspace layouts (later phase).
/// </summary>
public sealed class WorkspacesTab : UserControl
{
    public WorkspacesTab()
    {
        Content = new PlaceholderPanel(
            "\uE738", // Segoe MDL2 Assets: Home
            "Workspaces",
            "Save and restore complete window arrangements per project. Shipping in a later release.");
    }
}
