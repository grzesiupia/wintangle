using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wintangle.App.Services;
using Wintangle.Core.Config;
using Wintangle.Core.Geometry;

namespace Wintangle.App.UI.Tabs;

/// <summary>
/// Settings tab: gap boxes, theme radios, autostart, ignored apps, and
/// restore-defaults. All changes persist immediately through
/// <see cref="ConfigService"/>. Restoring defaults raises
/// <see cref="DefaultsRestored"/> so the shell can re-sync the rest of the UI.
/// </summary>
public partial class SettingsTab : UserControl
{
    private readonly ConfigService _config;

    /// <summary>Suppresses save/autostart/theme events while the tab is being populated.</summary>
    private bool _initializing = true;

    /// <summary>Suppresses the Checked handlers while the shell re-syncs the radios.</summary>
    private bool _syncingRadios;

    /// <summary>Raised after the user confirms "Restore defaults" (config already reset).</summary>
    public event Action? DefaultsRestored;

    internal SettingsTab(ConfigService config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        InitializeComponent();
        LoadSettings();

        _initializing = false;
    }

    /// <summary>Re-reads the config into all controls (gaps, theme, autostart, ignored).</summary>
    private void LoadSettings()
    {
        WindowGapBox.Text = _config.Current.WindowGap.ToString();
        EdgeGapBox.Text = _config.Current.EdgeGap.ToString();
        AutostartCheckBox.IsChecked = _config.GetAutoStartEnabled();
        SyncThemeRadios(_config.Theme);
        RefreshIgnored();
    }

    /// <summary>
    /// Sets the theme radios to match <paramref name="theme"/> without saving
    /// (used by the shell on theme change / defaults restore).
    /// </summary>
    public void SyncThemeRadios(string theme)
    {
        var isDark = string.Equals(ConfigStore.NormalizeTheme(theme), ConfigModel.ThemeDark, StringComparison.Ordinal);

        _syncingRadios = true;
        try
        {
            DarkThemeRadio.IsChecked = isDark;
            LightThemeRadio.IsChecked = !isDark;
        }
        finally
        {
            _syncingRadios = false;
        }
    }

    // ---- Gap boxes ----

    private void WindowGapBox_LostFocus(object sender, RoutedEventArgs e) => CommitWindowGap();

    private void EdgeGapBox_LostFocus(object sender, RoutedEventArgs e) => CommitEdgeGap();

    private void GapBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (ReferenceEquals(sender, WindowGapBox))
        {
            CommitWindowGap();
        }
        else if (ReferenceEquals(sender, EdgeGapBox))
        {
            CommitEdgeGap();
        }

        e.Handled = true;
    }

    private void CommitWindowGap()
        => CommitGap(WindowGapBox, _config.Current.WindowGap, v => _config.UpdateGaps(v, _config.Current.EdgeGap));

    private void CommitEdgeGap()
        => CommitGap(EdgeGapBox, _config.Current.EdgeGap, v => _config.UpdateGaps(_config.Current.WindowGap, v));

    /// <summary>
    /// Applies a gap box value when it is a valid integer in [0, MaxGap] and
    /// differs from the committed value; invalid or empty input reverts the box
    /// to the last committed value and marks it with a red border. Saves only on
    /// Enter or LostFocus — never per keystroke, so partial input is never
    /// committed and the config isn't spammed.
    /// </summary>
    private static void CommitGap(TextBox box, int current, Action<int> apply)
    {
        if (TryParseGap(box.Text, out var value))
        {
            if (value != current)
            {
                apply(value);
            }

            box.Text = value.ToString(); // normalize (e.g. "05" → "5")
            SetGapBoxState(box, invalid: false);
            return;
        }

        box.Text = current.ToString(); // revert to last valid
        SetGapBoxState(box, invalid: true);
    }

    private static bool TryParseGap(string? text, out int value)
        => int.TryParse(text?.Trim(), out value) && value is >= 0 and <= GapSettings.MaxGap;

    private static void SetGapBoxState(TextBox box, bool invalid)
    {
        if (invalid)
        {
            // Themed danger brush (fallback red if the resource is missing).
            box.BorderBrush = box.TryFindResource("Brush.Danger") as Brush ?? Brushes.Red;
            box.BorderThickness = new Thickness(1.5);
            return;
        }

        box.ClearValue(TextBox.BorderBrushProperty);
        box.ClearValue(TextBox.BorderThicknessProperty);
    }

    // ---- Theme ----

    private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (_initializing || _syncingRadios)
        {
            return;
        }

        if (ReferenceEquals(sender, DarkThemeRadio) && _config.Theme != ConfigModel.ThemeDark)
        {
            _config.SetTheme(ConfigModel.ThemeDark);
        }
        else if (ReferenceEquals(sender, LightThemeRadio) && _config.Theme != ConfigModel.ThemeLight)
        {
            _config.SetTheme(ConfigModel.ThemeLight);
        }
    }

    // ---- Autostart ----

    private void AutostartCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (!_initializing)
        {
            _config.SetAutoStart(true);
        }
    }

    private void AutostartCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!_initializing)
        {
            _config.SetAutoStart(false);
        }
    }

    // ---- Ignored apps ----

    private void RefreshIgnored()
    {
        IgnoredList.Items.Clear();
        foreach (var name in _config.Current.IgnoredApps)
        {
            IgnoredList.Items.Add(name);
        }
    }

    private void IgnoreAddButton_Click(object sender, RoutedEventArgs e) => AddIgnoredFromBox();

    private void IgnoreAddBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AddIgnoredFromBox();
            e.Handled = true;
        }
    }

    private void AddIgnoredFromBox()
    {
        var name = IgnoreAddBox.Text?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return;
        }

        _config.AddIgnored(name); // normalizes, lowercases, appends .exe
        RefreshIgnored();
        IgnoreAddBox.Clear();
    }

    private void IgnoreRemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (IgnoredList.SelectedItem is not string name)
        {
            return;
        }

        _config.RemoveIgnored(name);
        RefreshIgnored();
    }

    // ---- Restore defaults ----

    private void RestoreDefaultsButton_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var result = MessageBox.Show(
            owner,
            "Reset all settings to defaults?\n\nThis restores the default gaps and hotkeys, turns autostart off, and clears the ignored-apps list.",
            "Restore defaults",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK)
        {
            return;
        }

        _config.RestoreDefaults();
        LoadSettings();
        DefaultsRestored?.Invoke();
    }
}
