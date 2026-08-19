using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Wintangle.App.Hooks;
using Wintangle.App.Services;
using Wintangle.App.UI.Controls;
using Wintangle.Core.Hotkeys;

namespace Wintangle.App.UI;

/// <summary>
/// Settings dialog: gaps, hotkey bindings, and ignored apps. At most one
/// instance exists at a time (<see cref="ShowOrActivate"/>); each open is a
/// fresh window populated from the current config — no stale state. All
/// changes persist immediately through <see cref="ConfigService"/>.
/// </summary>
/// <remarks>
/// Closing the window cancels any in-flight recording so the keyboard hook is
/// never left in RecordingMode.
/// </remarks>
public partial class SettingsWindow : Window
{
    /// <summary>The single live settings window, or null (fresh create per open).</summary>
    private static SettingsWindow? s_openWindow;
    private readonly ConfigService _config;
    private readonly KeyboardHook _hook;

    /// <summary>300 ms debounce so slider drags save once at the end, not per tick.</summary>
    private readonly DispatcherTimer _gapDebounce = new() { Interval = TimeSpan.FromMilliseconds(300) };

    /// <summary>Suppresses save/autostart events while the tab is being populated.</summary>
    private bool _initializing = true;

    private readonly List<ShortcutRow> _rows = new();

    internal SettingsWindow(ConfigService config, KeyboardHook hook)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _hook = hook ?? throw new ArgumentNullException(nameof(hook));

        _gapDebounce.Tick += (_, _) =>
        {
            _gapDebounce.Stop();
            SaveGaps();
        };

        InitializeComponent();

        LoadGeneralTab();
        BuildShortcutsTab();
        RefreshIgnoredTab();

        _initializing = false;

        Closed += (_, _) => Teardown();
    }

    /// <summary>
    /// Shows the settings window, activating the existing instance if one is
    /// already open (tray menu and WM_APP+1 both route through here).
    /// </summary>
    internal static void ShowOrActivate(ConfigService config, KeyboardHook hook)
    {
        if (s_openWindow != null)
        {
            s_openWindow.Activate();
            return;
        }

        s_openWindow = new SettingsWindow(config, hook);
        s_openWindow.Closed += (_, _) => s_openWindow = null;
        s_openWindow.Show();
        s_openWindow.Activate();
    }

    // ---- General tab ----

    private void LoadGeneralTab()
    {
        WindowGapSlider.Value = _config.Current.WindowGap;
        EdgeGapSlider.Value = _config.Current.EdgeGap;
        // Set labels explicitly: ValueChanged does not fire when the value
        // already equals the slider's current value (e.g. gap = 0).
        WindowGapValue.Text = $"{_config.Current.WindowGap} px";
        EdgeGapValue.Text = $"{_config.Current.EdgeGap} px";
        AutostartCheckBox.IsChecked = _config.GetAutoStartEnabled();
    }

    private void WindowGapSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        WindowGapValue.Text = $"{e.NewValue:0} px";
        if (!_initializing)
        {
            RestartGapDebounce();
        }
    }

    private void EdgeGapSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        EdgeGapValue.Text = $"{e.NewValue:0} px";
        if (!_initializing)
        {
            RestartGapDebounce();
        }
    }

    private void RestartGapDebounce()
    {
        _gapDebounce.Stop();
        _gapDebounce.Start();
    }

    private void SaveGaps()
    {
        _config.UpdateGaps((int)WindowGapSlider.Value, (int)EdgeGapSlider.Value);
    }

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

    private void RestoreDefaultsButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            this,
            "Reset all settings to defaults?\n\nThis restores the default gaps and hotkeys, turns autostart off, and clears the ignored-apps list.",
            "Restore defaults",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK)
        {
            return;
        }

        _config.RestoreDefaults();
        LoadGeneralTab();
        BuildShortcutsTab();
        RefreshIgnoredTab();
    }

    // ---- Shortcuts tab ----

    private void BuildShortcutsTab()
    {
        // Detach old recorders from the hook first: the hook is app-lifetime
        // and holds a reference per subscription, so leaving them attached
        // would keep stale windows (and their recorders) alive. Cancel first
        // so a recording in progress never leaves the shared flag armed.
        foreach (var row in _rows)
        {
            row.Recorder.CancelRecording();
            row.Recorder.Hook = null;
        }

        ShortcutsHost.Children.Clear();
        _rows.Clear();

        foreach (var entry in DefaultHotkeys.Entries)
        {
            var row = CreateShortcutRow(entry.Value);
            _rows.Add(row);
            ShortcutsHost.Children.Add(row.Root);
        }
    }

    private ShortcutRow CreateShortcutRow(HotkeyAction action)
    {
        var actionName = new TextBlock
        {
            Text = UiLabels.ActionName(action),
            Width = 150,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var restoreButton = new Button
        {
            Content = "Default",
            Margin = new Thickness(6, 0, 0, 0),
            Padding = new Thickness(10, 2, 10, 2),
            IsEnabled = _config.IsCustomShortcut(action),
        };

        var recorder = new RebindRecorder
        {
            Hook = _hook,
            ValidateCombo = combo => ValidateCombo(combo, action),
        };
        recorder.RecordingStarted += (_, _) =>
        {
            // Only one recorder may capture at a time (shared hook flag):
            // cancel every other row's recording before this one arms it.
            foreach (var row in _rows)
            {
                if (!ReferenceEquals(row.Recorder, recorder))
                {
                    row.Recorder.CancelRecording();
                }
            }
        };
        recorder.SetHotkey(_config.GetShortcut(action) ?? new Hotkey(0, KeyModifiers.None));
        recorder.ComboCaptured += (_, combo) =>
        {
            // Capturing the already-effective combo (the default or an
            // identical custom binding) is a no-op: don't persist a redundant
            // custom entry and don't enable the "Default" button.
            if (_config.GetShortcut(action) == combo)
            {
                return;
            }

            _config.SetShortcut(action, combo);
            restoreButton.IsEnabled = _config.IsCustomShortcut(action);
        };

        restoreButton.Click += (_, _) =>
        {
            _config.RestoreShortcut(action);
            recorder.SetHotkey(_config.GetShortcut(action) ?? new Hotkey(0, KeyModifiers.None));
            restoreButton.IsEnabled = false;
        };

        var root = new Grid { Margin = new Thickness(0, 5, 0, 5) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(actionName, 0);
        Grid.SetColumn(recorder, 1);
        Grid.SetColumn(restoreButton, 2);
        root.Children.Add(actionName);
        root.Children.Add(recorder);
        root.Children.Add(restoreButton);

        return new ShortcutRow(action, recorder, root);
    }

    /// <summary>
    /// Duplicate-combo check: rejects a combo already bound to a different
    /// action (effective bindings include defaults). Returns the error text.
    /// </summary>
    private string? ValidateCombo(Hotkey combo, HotkeyAction action)
    {
        var owner = _config.FindActionForHotkey(combo);
        if (owner is { } other && other != action)
        {
            return $"\"{UiLabels.ActionName(other)}\" already uses {HotkeyLabels.Format(combo)}.";
        }

        return null;
    }

    // ---- Ignored apps tab ----

    private void RefreshIgnoredTab()
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
        RefreshIgnoredTab();
        IgnoreAddBox.Clear();
    }

    private void IgnoreRemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (IgnoredList.SelectedItem is not string name)
        {
            return;
        }

        _config.RemoveIgnored(name);
        RefreshIgnoredTab();
    }

    // ---- Lifecycle ----

    /// <summary>
    /// Window teardown: stops the gap debounce, cancels any in-flight
    /// recording (the hook must never stay armed), and detaches every
    /// recorder from the app-lifetime hook so the window can be GC'd.
    /// </summary>
    private void Teardown()
    {
        _gapDebounce.Stop();
        CancelAllRecordings();
        foreach (var row in _rows)
        {
            row.Recorder.Hook = null;
        }
    }

    private void CancelAllRecordings()
    {
        foreach (var row in _rows)
        {
            row.Recorder.CancelRecording();
        }

        _hook.RecordingMode = false; // belt and braces — never leave the hook armed
    }

    /// <summary>One shortcuts-tab row (action label + recorder + restore button).</summary>
    private sealed class ShortcutRow
    {
        public ShortcutRow(HotkeyAction action, RebindRecorder recorder, Grid root)
        {
            Action = action;
            Recorder = recorder;
            Root = root;
        }

        public HotkeyAction Action { get; }

        public RebindRecorder Recorder { get; }

        public Grid Root { get; }
    }
}
