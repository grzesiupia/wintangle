using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using Wintangle.App.Hooks;
using Wintangle.App.Services;
using Wintangle.App.UI.Controls;
using Wintangle.Core.Hotkeys;

namespace Wintangle.App.UI.Tabs;

/// <summary>
/// Keyboard Shortcuts tab (Phase 3): one row per action (19 rows in
/// <see cref="DefaultHotkeys.Entries"/> order) — name + group label, an inline
/// recorder (keycap chips + Record/Cancel), and a restore-default button. Rows
/// are rebuilt from the config via <see cref="Rebuild"/> (tab activation,
/// defaults restore, after commit/reset). Closing the host must call
/// <see cref="Teardown"/> so the hook is never left in RecordingMode and
/// recorders are detached. No success balloon — the binding is committed
/// silently per the functionality spec.
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

    /// <summary>Rebuilds the rows after a commit/reset (chips reflect the live bindings).</summary>
    public void RefreshShortcuts() => Rebuild();

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
        // sc-name — 13.5px Medium + mono 10.5px group label.
        var name = new TextBlock
        {
            Text = UiLabels.ActionName(action),
            FontSize = 13.5,
            FontWeight = FontWeights.Medium,
            VerticalAlignment = VerticalAlignment.Center,
        };
        name.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Fg");

        var group = new TextBlock
        {
            Text = UiLabels.GroupFor(action),
            FontSize = 10.5,
            Margin = new Thickness(0, 1, 0, 0),
        };
        group.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Muted");
        group.SetResourceReference(TextBlock.FontFamilyProperty, "Font.Mono");

        var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        nameStack.Children.Add(name);
        nameStack.Children.Add(group);

        // sc-bind — the inline recorder.
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
            // custom entry and don't re-enable the "Default" button.
            if (_config.GetShortcut(action) == combo)
            {
                return;
            }

            _config.SetShortcut(action, combo);
            RefreshShortcuts();
        };

        // Reset button — restore-default icon.
        var reset = new Button
        {
            IsEnabled = _config.IsCustomShortcut(action),
            VerticalAlignment = VerticalAlignment.Center,
            Content = CreateResetIcon(),
            ToolTip = $"Restore default for {UiLabels.ActionName(action)}",
        };
        reset.SetResourceReference(Button.StyleProperty, "ResetButton");
        reset.Click += (_, _) =>
        {
            _config.RestoreShortcut(action);
            RefreshShortcuts();
        };

        // Row — 1fr auto auto, 14px gaps, vertical padding 12, bottom border.
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        nameStack.Margin = new Thickness(0, 0, 14, 0);
        recorder.Margin = new Thickness(0, 0, 14, 0);
        Grid.SetColumn(nameStack, 0);
        Grid.SetColumn(recorder, 1);
        Grid.SetColumn(reset, 2);
        grid.Children.Add(nameStack);
        grid.Children.Add(recorder);
        grid.Children.Add(reset);

        var root = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 12, 0, 12),
            Child = grid,
        };
        root.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");

        return new ShortcutRow(action, recorder, root);
    }

    private static Path CreateResetIcon()
    {
        var icon = new Path
        {
            Data = Geometry.Parse("M3 12a9 9 0 1 0 3-6.7M3 4v4h4"),
            Width = 14,
            Height = 14,
            Stretch = Stretch.Uniform,
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
        };
        // The icon follows the button's foreground (Muted → Fg on hover).
        icon.SetBinding(
            Path.StrokeProperty,
            new Binding(nameof(Button.Foreground))
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Button), 1),
            });
        return icon;
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
            return $"That chord is already assigned to \u201C{UiLabels.ActionName(other)}\u201D.";
        }

        return null;
    }

    /// <summary>One shortcuts-tab row (action label + recorder + restore button).</summary>
    private sealed class ShortcutRow
    {
        public ShortcutRow(HotkeyAction action, RebindRecorder recorder, Border root)
        {
            Action = action;
            Recorder = recorder;
            Root = root;
        }

        public HotkeyAction Action { get; }

        public RebindRecorder Recorder { get; }

        public Border Root { get; }
    }
}
