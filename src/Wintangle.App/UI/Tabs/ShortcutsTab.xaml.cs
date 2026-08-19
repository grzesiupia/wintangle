using System.Windows;
using System.Windows.Controls;
using Wintangle.App.Hooks;
using Wintangle.App.Services;
using Wintangle.App.UI.Controls;
using Wintangle.Core.Hotkeys;

namespace Wintangle.App.UI.Tabs;

/// <summary>
/// Keyboard Shortcuts tab: one row per action with an inline recorder.
/// Rows are rebuilt from the config via <see cref="Rebuild"/> (tab activation,
/// defaults restore). Closing the host must call <see cref="Teardown"/> so the
/// hook is never left in RecordingMode and recorders are detached.
/// </summary>
public partial class ShortcutsTab : UserControl
{
    private readonly ConfigService _config;
    private readonly KeyboardHook _hook;

    private readonly List<ShortcutRow> _rows = new();

    internal ShortcutsTab(ConfigService config, KeyboardHook hook)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _hook = hook ?? throw new ArgumentNullException(nameof(hook));

        InitializeComponent();
        Rebuild();
    }

    /// <summary>
    /// Rebuilds all rows from the current effective bindings. Detaches old
    /// recorders from the hook first: the hook is app-lifetime and holds a
    /// reference per subscription, so leaving them attached would keep stale
    /// windows (and their recorders) alive.
    /// </summary>
    public void Rebuild()
    {
        // Cancel first so a recording in progress never leaves the shared
        // flag armed.
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

    /// <summary>
    /// Window teardown: cancels any in-flight recording (the hook must never
    /// stay armed) and detaches every recorder from the app-lifetime hook so
    /// the window can be GC'd.
    /// </summary>
    public void Teardown()
    {
        foreach (var row in _rows)
        {
            row.Recorder.CancelRecording();
            row.Recorder.Hook = null;
        }

        _hook.RecordingMode = false; // belt and braces — never leave the hook armed
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
