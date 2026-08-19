using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Wintangle.App.Hooks;
using Wintangle.App.Interop;
using Wintangle.App.Services;
using Wintangle.App.UI.Tabs;
using Wintangle.Core.Config;
using Wintangle.Core.Hotkeys;

namespace Wintangle.App.UI;

/// <summary>
/// Settings dialog: seven themed tabs (layouts, advanced rules, shortcuts,
/// mouse actions, plugins, workspaces, settings). At most one instance exists
/// at a time (<see cref="ShowOrActivate"/>); each open is a fresh window
/// populated from the current config — no stale state. All changes persist
/// immediately through <see cref="ConfigService"/>.
/// </summary>
/// <remarks>
/// The window applies the dark/light title bar via DWM and follows
/// <see cref="ConfigService.ThemeChanged"/> live: theme swaps repaint the
/// layout previews and re-sync the theme radios. Closing the window tears down
/// the shortcut recorders so the keyboard hook is never left in RecordingMode.
/// </remarks>
public partial class SettingsWindow : Window
{
    /// <summary>The single live settings window, or null (fresh create per open).</summary>
    private static SettingsWindow? s_openWindow;

    private readonly ConfigService _config;
    private readonly KeyboardHook _hook;

    private IntPtr _hwnd;

    /// <summary>Suppresses tab-activation refresh while the window is being built.</summary>
    private bool _initializing = true;

    private LayoutsTab? _layoutsPanel;
    private AdvancedRulesTab? _advancedRulesPanel;
    private ShortcutsTab? _shortcutsPanel;
    private MouseActionsTab? _mouseActionsPanel;
    private PluginsTab? _pluginsPanel;
    private WorkspacesTab? _workspacesPanel;
    private SettingsTab? _settingsPanel;

    internal SettingsWindow(ConfigService config, KeyboardHook hook, Action<HotkeyAction>? applyPreset = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _hook = hook ?? throw new ArgumentNullException(nameof(hook));

        InitializeComponent();
        LoadHeaderIcon();

        _layoutsPanel = new LayoutsTab(config, applyPreset);
        _advancedRulesPanel = new AdvancedRulesTab();
        _shortcutsPanel = new ShortcutsTab(config, hook);
        _mouseActionsPanel = new MouseActionsTab();
        _pluginsPanel = new PluginsTab();
        _workspacesPanel = new WorkspacesTab();
        _settingsPanel = new SettingsTab(config);
        _settingsPanel.DefaultsRestored += OnDefaultsRestored;

        ContentHost.Children.Add(_layoutsPanel);
        ContentHost.Children.Add(_advancedRulesPanel);
        ContentHost.Children.Add(_shortcutsPanel);
        ContentHost.Children.Add(_mouseActionsPanel);
        ContentHost.Children.Add(_pluginsPanel);
        ContentHost.Children.Add(_workspacesPanel);
        ContentHost.Children.Add(_settingsPanel);

        _config.ThemeChanged += OnThemeChanged;

        _initializing = false;
        ShowTab(_layoutsPanel);

        Closed += (_, _) => Teardown();
    }

    /// <summary>
    /// Shows the settings window, activating the existing instance if one is
    /// already open (tray menu and WM_APP+1 both route through here).
    /// </summary>
    internal static void ShowOrActivate(ConfigService config, KeyboardHook hook, Action<HotkeyAction>? applyPreset = null)
    {
        if (s_openWindow != null)
        {
            s_openWindow.Activate();
            return;
        }

        s_openWindow = new SettingsWindow(config, hook, applyPreset);
        s_openWindow.Closed += (_, _) => s_openWindow = null;
        s_openWindow.Show();
        s_openWindow.Activate();
    }

    // ---- Chrome ----

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _hwnd = new WindowInteropHelper(this).EnsureHandle();
        var isDark = string.Equals(ConfigStore.NormalizeTheme(_config.Theme), ConfigModel.ThemeDark, StringComparison.Ordinal);
        WindowApi.ApplyDarkTitleBar(_hwnd, isDark);
    }

    private void LoadHeaderIcon()
    {
        try
        {
            using var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("wintangle.ico");
            if (stream == null)
            {
                return;
            }

            var decoder = new IconBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count > 0)
            {
                HeaderIcon.Source = decoder.Frames[0];
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[wintangle] settings header icon failed: {ex.Message}");
        }
    }

    // ---- Theme ----

    private void OnThemeChanged(string theme)
    {
        // ThemeChanged may fire on the watcher thread — marshal to the UI thread.
        if (Dispatcher.CheckAccess())
        {
            ApplyThemeUi(theme);
            return;
        }

        try
        {
            Dispatcher.BeginInvoke(() => ApplyThemeUi(theme));
        }
        catch (InvalidOperationException)
        {
            // Dispatcher rejected work at shutdown; the window is going away.
        }
    }

    private void ApplyThemeUi(string theme)
    {
        var isDark = string.Equals(ConfigStore.NormalizeTheme(theme), ConfigModel.ThemeDark, StringComparison.Ordinal);
        WindowApi.ApplyDarkTitleBar(_hwnd, isDark);

        // Preview brushes are resolved at render time — repaint them.
        _layoutsPanel?.RefreshPreviews();
        _settingsPanel?.SyncThemeRadios(theme);
    }

    // ---- Tab activation ----

    private void OnTabChecked(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }

        if (ReferenceEquals(sender, LayoutsTabRadio))
        {
            ShowTab(_layoutsPanel);
            _layoutsPanel?.RefreshShortcuts();
            _layoutsPanel?.RefreshActiveWindows();
        }
        else if (ReferenceEquals(sender, AdvancedRulesTabRadio))
        {
            ShowTab(_advancedRulesPanel);
        }
        else if (ReferenceEquals(sender, ShortcutsTabRadio))
        {
            ShowTab(_shortcutsPanel);
            _shortcutsPanel?.Rebuild();
        }
        else if (ReferenceEquals(sender, MouseActionsTabRadio))
        {
            ShowTab(_mouseActionsPanel);
        }
        else if (ReferenceEquals(sender, PluginsTabRadio))
        {
            ShowTab(_pluginsPanel);
        }
        else if (ReferenceEquals(sender, WorkspacesTabRadio))
        {
            ShowTab(_workspacesPanel);
        }
        else if (ReferenceEquals(sender, SettingsTabRadio))
        {
            ShowTab(_settingsPanel);
        }
    }

    private void ShowTab(FrameworkElement? panel)
    {
        SetPanelVisibility(_layoutsPanel, panel);
        SetPanelVisibility(_advancedRulesPanel, panel);
        SetPanelVisibility(_shortcutsPanel, panel);
        SetPanelVisibility(_mouseActionsPanel, panel);
        SetPanelVisibility(_pluginsPanel, panel);
        SetPanelVisibility(_workspacesPanel, panel);
        SetPanelVisibility(_settingsPanel, panel);
    }

    private static void SetPanelVisibility(FrameworkElement? candidate, FrameworkElement? active)
    {
        if (candidate == null)
        {
            return;
        }

        candidate.Visibility = ReferenceEquals(candidate, active) ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---- Defaults restore ----

    private void OnDefaultsRestored()
    {
        // RestoreDefaults already fired ThemeChanged when the theme actually
        // changed; this covers the same-theme case (radios re-sync either way)
        // and rebuilds the shortcut rows + layout chips.
        _settingsPanel?.SyncThemeRadios(_config.Theme);
        _shortcutsPanel?.Rebuild();
        _layoutsPanel?.RefreshShortcuts();
    }

    // ---- Lifecycle ----

    private void Teardown()
    {
        _config.ThemeChanged -= OnThemeChanged;
        _shortcutsPanel?.Teardown();
    }
}
