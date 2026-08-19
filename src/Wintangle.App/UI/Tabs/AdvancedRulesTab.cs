using System.Windows.Controls;
using Wintangle.App.UI.Controls;

namespace Wintangle.App.UI.Tabs;

/// <summary>
/// Advanced Rules tab — placeholder for the per-app rule engine (later phase).
/// </summary>
public sealed class AdvancedRulesTab : UserControl
{
    public AdvancedRulesTab()
    {
        Content = new PlaceholderPanel(
            "\uE713", // Segoe MDL2 Assets: Settings
            "Advanced Rules",
            "Define custom layout rules per app. The rule engine ships in a later release.",
            "New rule");
    }
}
