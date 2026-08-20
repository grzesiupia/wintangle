using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Wintangle.App.Services;
using Wintangle.App.UI.Controls;
using Wintangle.Core.Config;
using Wintangle.Core.Geometry;
using Wintangle.Core.Update;

namespace Wintangle.App.UI.Tabs;

/// <summary>
/// Settings tab (Phase 4): spacing sliders + gap boxes, theme cards, autostart
/// switch, ignored-apps manager and restore-defaults with an inline
/// confirmation bar. All changes persist immediately through
/// <see cref="ConfigService"/>. The tab subscribes to
/// <see cref="ConfigService.ThemeChanged"/> so externally edited config
/// re-syncs the theme cards and gaps live (<see cref="Teardown"/> detaches
/// it). Restoring defaults raises <see cref="DefaultsRestored"/> so the shell
/// can re-sync the other tabs.
/// </summary>
public partial class SettingsTab : UserControl
{
    private readonly ConfigService _config;
    private readonly UpdateService _updateService = new();
    private CancellationTokenSource? _updateCts;
    private ReleaseInfo? _availableRelease;
    private UpdateState _updateState = UpdateState.Idle;

    /// <summary>
    /// Suppresses save/theme/autostart events while the tab is being populated
    /// or a control is being re-synced from the config (slider ↔ number box
    /// two-way sync and external reloads).
    /// </summary>
    private bool _syncing;

    /// <summary>Reverts the danger flash on a gap box after an invalid commit.</summary>
    private DispatcherTimer? _invalidFlash;

    /// <summary>Raised after the user confirms "Restore defaults" (config already reset).</summary>
    public event Action? DefaultsRestored;

    internal SettingsTab(ConfigService config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        InitializeComponent();
        LoadSettings();

        _config.ThemeChanged += OnThemeChanged;
    }

    /// <summary>Re-reads the config into all controls (gaps, theme, autostart, ignored).</summary>
    private void LoadSettings()
    {
        _syncing = true;
        try
        {
            WindowGapSlider.Value = _config.Current.WindowGap;
            EdgeGapSlider.Value = _config.Current.EdgeGap;
            WindowGapBox.Text = _config.Current.WindowGap.ToString();
            EdgeGapBox.Text = _config.Current.EdgeGap.ToString();
            WindowGapValue.Text = _config.Current.WindowGap.ToString();
            EdgeGapValue.Text = _config.Current.EdgeGap.ToString();
            AutoStartSwitch.IsChecked = _config.GetAutoStartEnabled();
            SyncThemeRadios(_config.Theme);
            RefreshIgnored();
            CurrentVersionText.Text = UpdateService.CurrentVersion.ToDisplayString();
            SetUpdateState(UpdateState.Idle);
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>
    /// Sets the theme cards to match <paramref name="theme"/> without saving
    /// (used by the shell on theme change / defaults restore).
    /// </summary>
    public void SyncThemeRadios(string theme)
    {
        var isDark = string.Equals(ConfigStore.NormalizeTheme(theme), ConfigModel.ThemeDark, StringComparison.Ordinal);
        DarkThemeCard.IsSelected = isDark;
        LightThemeCard.IsSelected = !isDark;
    }

    /// <summary>Detaches the theme subscription and cancels pending update operations (window teardown).</summary>
    public void Teardown()
    {
        _config.ThemeChanged -= OnThemeChanged;
        _invalidFlash?.Stop();
        try
        {
            _updateCts?.Cancel();
            _updateCts?.Dispose();
            _updateCts = null;
        }
        catch
        {
            // Ignore teardown cancellation errors
        }
    }

    // ---- Gap sliders + number boxes ----

    private void WindowGapSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncing)
        {
            return;
        }

        // Live commit: dragging the slider applies the gap immediately. The
        // value is integral (GapSlider snaps to ticks) — no float drift.
        var value = (int)e.NewValue;
        if (value != _config.Current.WindowGap)
        {
            _config.UpdateGaps(value, _config.Current.EdgeGap);
        }

        SyncGapText(WindowGapBox, WindowGapValue, value);
    }

    private void EdgeGapSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_syncing)
        {
            return;
        }

        var value = (int)e.NewValue;
        if (value != _config.Current.EdgeGap)
        {
            _config.UpdateGaps(_config.Current.WindowGap, value);
        }

        SyncGapText(EdgeGapBox, EdgeGapValue, value);
    }

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
        => CommitGap(WindowGapBox, WindowGapSlider, WindowGapValue, _config.Current.WindowGap, v => _config.UpdateGaps(v, _config.Current.EdgeGap));

    private void CommitEdgeGap()
        => CommitGap(EdgeGapBox, EdgeGapSlider, EdgeGapValue, _config.Current.EdgeGap, v => _config.UpdateGaps(_config.Current.WindowGap, v));

    /// <summary>
    /// Applies a gap box value when it is a valid integer in [0, MaxGap] and
    /// differs from the committed value; invalid or empty input reverts the
    /// box to the last committed value and flashes a danger border for 700 ms
    /// before restoring the normal chrome. Saves only on Enter or LostFocus —
    /// never per keystroke, so partial input is never committed and the config
    /// isn't spammed.
    /// </summary>
    private void CommitGap(TextBox box, Slider slider, TextBlock valueLabel, int current, Action<int> apply)
    {
        if (TryParseGap(box.Text, out var value))
        {
            if (value != current)
            {
                apply(value);
            }

            box.Text = value.ToString(); // normalize (e.g. "05" → "5")
            SetGapBoxState(box, invalid: false);
            SyncGapSlider(slider, valueLabel, value);
            return;
        }

        box.Text = current.ToString(); // revert to last valid
        SetGapBoxState(box, invalid: true);
    }

    private static bool TryParseGap(string? text, out int value)
        => int.TryParse(text?.Trim(), out value) && value is >= 0 and <= GapSettings.MaxGap;

    /// <summary>
    /// Moves the slider to match a committed number-box value. Guarded by
    /// <see cref="_syncing"/> so the resulting ValueChanged doesn't re-commit.
    /// </summary>
    private void SyncGapSlider(Slider slider, TextBlock valueLabel, int value)
    {
        _syncing = true;
        try
        {
            slider.Value = value;
        }
        finally
        {
            _syncing = false;
        }

        valueLabel.Text = value.ToString();
    }

    /// <summary>Mirrors a live slider value into the number box and the value label.</summary>
    private void SyncGapText(TextBox box, TextBlock valueLabel, int value)
    {
        box.Text = value.ToString();
        valueLabel.Text = value.ToString();
    }

    private void SetGapBoxState(TextBox box, bool invalid)
    {
        if (invalid)
        {
            // Themed danger brush (fallback red if the resource is missing).
            box.BorderBrush = box.TryFindResource("Brush.Danger") as Brush ?? Brushes.Red;
            box.BorderThickness = new Thickness(1.5);
            ScheduleInvalidFlash();
            return;
        }

        box.ClearValue(TextBox.BorderBrushProperty);
        box.ClearValue(TextBox.BorderThicknessProperty);
    }

    /// <summary>
    /// Flashes the danger border for 700 ms (matching the design's invalid
    /// state), then reverts the box chrome.
    /// </summary>
    private void ScheduleInvalidFlash()
    {
        if (_invalidFlash == null)
        {
            _invalidFlash = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            _invalidFlash.Tick += (_, _) =>
            {
                _invalidFlash.Stop();
                SetGapBoxState(WindowGapBox, invalid: false);
                SetGapBoxState(EdgeGapBox, invalid: false);
            };
        }

        _invalidFlash.Stop();
        _invalidFlash.Start();
    }

    // ---- Theme ----

    private void ThemeCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ThemeCard card || card.Theme == _config.Theme)
        {
            return;
        }

        _config.SetTheme(card.Theme);
    }

    private void OnThemeChanged(string theme)
    {
        // ThemeChanged may fire on the watcher thread — marshal to the UI thread.
        if (Dispatcher.CheckAccess())
        {
            ApplyThemeSync(theme);
            return;
        }

        try
        {
            Dispatcher.BeginInvoke(() => ApplyThemeSync(theme));
        }
        catch (InvalidOperationException)
        {
            // Dispatcher rejected work at shutdown; the window is going away.
        }
    }

    /// <summary>
    /// Re-syncs the theme cards and gap controls from the config. Runs on any
    /// applied theme change — including external edits, where the gaps may
    /// have changed alongside the theme.
    /// </summary>
    private void ApplyThemeSync(string theme)
    {
        SyncThemeRadios(theme);

        _syncing = true;
        try
        {
            WindowGapSlider.Value = _config.Current.WindowGap;
            EdgeGapSlider.Value = _config.Current.EdgeGap;
            WindowGapBox.Text = _config.Current.WindowGap.ToString();
            EdgeGapBox.Text = _config.Current.EdgeGap.ToString();
            WindowGapValue.Text = _config.Current.WindowGap.ToString();
            EdgeGapValue.Text = _config.Current.EdgeGap.ToString();
        }
        finally
        {
            _syncing = false;
        }
    }

    // ---- Autostart ----

    private void AutoStartSwitch_Checked(object sender, RoutedEventArgs e)
    {
        if (!_syncing)
        {
            _config.SetAutoStart(true);
        }
    }

    private void AutoStartSwitch_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!_syncing)
        {
            _config.SetAutoStart(false);
        }
    }

    // ---- Ignored apps ----

    public void RefreshIgnored()
    {
        IgnoredList.Children.Clear();

        var names = _config.Current.IgnoredApps;
        if (names.Count == 0)
        {
            IgnoredList.Children.Add(CreateEmptyNote());
        }
        else
        {
            foreach (var name in names)
            {
                IgnoredList.Children.Add(CreateIgnoredRow(name));
            }
        }

        RefreshRunningApps();
    }

    private void RefreshRunningApps()
    {
        RunningAppsPanel.Children.Clear();

        var ignoredSet = new HashSet<string>(
            _config.Current.IgnoredApps.Select(RuntimeState.NormalizeProcessName),
            StringComparer.OrdinalIgnoreCase);

        var runningWindows = ActiveWindows.Enumerate();
        var candidateProcs = runningWindows
            .Select(w => w.ProcessName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Where(name =>
            {
                var norm = RuntimeState.NormalizeProcessName(name);
                return norm.Length > 0
                    && !norm.Equals("wintangle", StringComparison.OrdinalIgnoreCase)
                    && !ignoredSet.Contains(norm);
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidateProcs.Count == 0)
        {
            var emptyLabel = new TextBlock
            {
                Text = "No other running apps found.",
                FontSize = 11.5,
                Margin = new Thickness(0, 2, 0, 0),
            };
            emptyLabel.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Muted");
            RunningAppsPanel.Children.Add(emptyLabel);
            return;
        }

        foreach (var procName in candidateProcs)
        {
            var btn = new Button
            {
                Content = $"+ {procName}",
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 12,
                ToolTip = $"Click to ignore {procName}",
            };
            btn.SetResourceReference(Button.StyleProperty, "GhostButton");
            btn.SetResourceReference(Control.FontFamilyProperty, "Font.Mono");
            btn.Click += (_, _) =>
            {
                _config.AddIgnored(procName);
                RefreshIgnored();
            };
            RunningAppsPanel.Children.Add(btn);
        }
    }

    /// <summary>Dashed-border empty state (the design's .empty-note).</summary>
    private static Grid CreateEmptyNote()
    {
        var text = new TextBlock
        {
            Text = "No ignored apps. Windows from ignored apps are never tiled or moved.",
            FontSize = 12.5,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(14),
        };
        text.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Muted");

        // WPF Border can't dash its outline — a Rectangle stroke with
        // StrokeDashArray fills the same role behind the text.
        var dash = new Rectangle
        {
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 3, 3 },
            RadiusX = 9,
            RadiusY = 9,
        };
        dash.SetResourceReference(Shape.StrokeProperty, "Brush.Border");

        var frame = new Grid();
        frame.Children.Add(dash);
        frame.Children.Add(text);
        return frame;
    }

    private Border CreateIgnoredRow(string name)
    {
        var proc = new TextBlock { Text = name, FontSize = 13, FontWeight = FontWeights.SemiBold };
        proc.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Fg");
        proc.SetResourceReference(TextBlock.FontFamilyProperty, "Font.Mono");

        var state = new TextBlock { Text = "· ignored", FontSize = 11, Margin = new Thickness(2, 0, 0, 0) };
        state.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Muted");

        var textStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        textStack.Children.Add(proc);
        textStack.Children.Add(state);

        var remove = new Button
        {
            Content = CreateRemoveIcon(),
            ToolTip = $"Remove {name}",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0),
        };
        remove.SetResourceReference(Button.StyleProperty, "IgnoreRemoveButton");
        remove.Click += (_, _) => RemoveIgnored(name);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(textStack, 0);
        Grid.SetColumn(remove, 1);
        grid.Children.Add(textStack);
        grid.Children.Add(remove);

        var row = new Border
        {
            Child = grid,
            Padding = new Thickness(14, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 6),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
        };
        row.SetResourceReference(Border.BackgroundProperty, "Brush.Bg");
        row.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");
        return row;
    }

    /// <summary>The 13px X glyph; its stroke follows the button foreground (Muted → Danger on hover).</summary>
    private static Path CreateRemoveIcon()
    {
        var icon = new Path
        {
            Data = Geometry.Parse("M5 5l14 14M19 5L5 19"),
            Width = 13,
            Height = 13,
            Stretch = Stretch.Uniform,
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        };
        icon.SetBinding(
            Path.StrokeProperty,
            new Binding(nameof(Button.Foreground))
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Button), 1),
            });
        return icon;
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

    private void RemoveIgnored(string name)
    {
        _config.RemoveIgnored(name);
        RefreshIgnored();
    }

    // ---- Updates ----

    private void SetUpdateState(UpdateState state, string? message = null)
    {
        _updateState = state;
        switch (state)
        {
            case UpdateState.Idle:
                CheckUpdatesButton.IsEnabled = true;
                CheckUpdatesButton.Content = "Check for updates";
                InstallUpdateButton.Visibility = Visibility.Collapsed;
                ReleaseNotesButton.Visibility = _availableRelease != null ? Visibility.Visible : Visibility.Collapsed;
                UpdateProgressBar.Visibility = Visibility.Collapsed;
                UpdateStatusText.Text = message ?? "Check GitHub for the latest wintangle releases and improvements.";
                break;

            case UpdateState.Checking:
                CheckUpdatesButton.IsEnabled = false;
                CheckUpdatesButton.Content = "Checking…";
                InstallUpdateButton.Visibility = Visibility.Collapsed;
                ReleaseNotesButton.Visibility = Visibility.Collapsed;
                UpdateProgressBar.Visibility = Visibility.Collapsed;
                UpdateStatusText.Text = "Checking for updates…";
                break;

            case UpdateState.UpToDate:
                CheckUpdatesButton.IsEnabled = true;
                CheckUpdatesButton.Content = "Check for updates";
                InstallUpdateButton.Visibility = Visibility.Collapsed;
                ReleaseNotesButton.Visibility = Visibility.Collapsed;
                UpdateProgressBar.Visibility = Visibility.Collapsed;
                UpdateStatusText.Text = message ?? $"wintangle {UpdateService.CurrentVersion.ToDisplayString()} is the latest version.";
                break;

            case UpdateState.UpdateAvailable:
                CheckUpdatesButton.IsEnabled = true;
                CheckUpdatesButton.Content = "Check again";
                InstallUpdateButton.Visibility = !string.IsNullOrEmpty(_availableRelease?.AssetUrl) ? Visibility.Visible : Visibility.Collapsed;
                InstallUpdateButton.IsEnabled = true;
                InstallUpdateButton.Content = "Download and Install";
                ReleaseNotesButton.Visibility = !string.IsNullOrEmpty(_availableRelease?.HtmlUrl) ? Visibility.Visible : Visibility.Collapsed;
                UpdateProgressBar.Visibility = Visibility.Collapsed;
                UpdateStatusText.Text = message ?? $"Version {_availableRelease?.Version.ToDisplayString()} is available.";
                break;

            case UpdateState.Downloading:
                CheckUpdatesButton.IsEnabled = false;
                InstallUpdateButton.IsEnabled = false;
                InstallUpdateButton.Content = "Downloading…";
                ReleaseNotesButton.Visibility = !string.IsNullOrEmpty(_availableRelease?.HtmlUrl) ? Visibility.Visible : Visibility.Collapsed;
                UpdateProgressBar.Visibility = Visibility.Visible;
                UpdateProgressBar.Value = 0;
                UpdateStatusText.Text = message ?? "Downloading update…";
                break;

            case UpdateState.Installing:
                CheckUpdatesButton.IsEnabled = false;
                InstallUpdateButton.IsEnabled = false;
                InstallUpdateButton.Content = "Installing…";
                ReleaseNotesButton.Visibility = Visibility.Collapsed;
                UpdateProgressBar.Visibility = Visibility.Visible;
                UpdateProgressBar.Value = 100;
                UpdateStatusText.Text = "Launching installer and restarting wintangle…";
                break;

            case UpdateState.Error:
                CheckUpdatesButton.IsEnabled = true;
                CheckUpdatesButton.Content = "Check for updates";
                InstallUpdateButton.Visibility = _availableRelease != null && !string.IsNullOrEmpty(_availableRelease.AssetUrl) ? Visibility.Visible : Visibility.Collapsed;
                InstallUpdateButton.IsEnabled = true;
                ReleaseNotesButton.Visibility = _availableRelease != null ? Visibility.Visible : Visibility.Collapsed;
                UpdateProgressBar.Visibility = Visibility.Collapsed;
                UpdateStatusText.Text = message ?? "An error occurred during update.";
                break;
        }
    }

    private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        _updateCts?.Cancel();
        _updateCts?.Dispose();
        _updateCts = new CancellationTokenSource();
        var ct = _updateCts.Token;

        SetUpdateState(UpdateState.Checking);

        try
        {
            var result = await _updateService.CheckAsync(ct);
            if (!result.Success)
            {
                SetUpdateState(UpdateState.Error, $"Update check failed: {result.ErrorMessage}");
                return;
            }

            if (result.IsUpdateAvailable && result.Release != null)
            {
                _availableRelease = result.Release;
                SetUpdateState(UpdateState.UpdateAvailable, $"Version {result.Release.Version.ToDisplayString()} is available.");
            }
            else
            {
                _availableRelease = null;
                SetUpdateState(UpdateState.UpToDate, $"wintangle {UpdateService.CurrentVersion.ToDisplayString()} is up to date.");
            }
        }
        catch (OperationCanceledException)
        {
            SetUpdateState(UpdateState.Idle);
        }
        catch (Exception ex)
        {
            SetUpdateState(UpdateState.Error, $"Update check failed: {ex.Message}");
        }
    }

    private async void InstallUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_availableRelease == null)
        {
            return;
        }

        _updateCts?.Cancel();
        _updateCts?.Dispose();
        _updateCts = new CancellationTokenSource();
        var ct = _updateCts.Token;

        SetUpdateState(UpdateState.Downloading, "Downloading update… 0%");

        var progress = new Progress<double>(p =>
        {
            UpdateProgressBar.Value = p * 100;
            UpdateStatusText.Text = $"Downloading update… {(int)(p * 100)}%";
        });

        try
        {
            string installerPath = await _updateService.DownloadAsync(_availableRelease, progress, ct);
            SetUpdateState(UpdateState.Installing);
            UpdateService.LaunchInstallerAndQuit(installerPath);
        }
        catch (OperationCanceledException)
        {
            SetUpdateState(UpdateState.UpdateAvailable);
        }
        catch (Exception ex)
        {
            SetUpdateState(UpdateState.Error, $"Download failed: {ex.Message}");
        }
    }

    private void ReleaseNotesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_availableRelease == null || string.IsNullOrWhiteSpace(_availableRelease.HtmlUrl))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_availableRelease.HtmlUrl) { UseShellExecute = true });
        }
        catch
        {
            // Ignore failure to launch browser
        }
    }

    // ---- Restore defaults ----

    private void RestoreDefaultsButton_Click(object sender, RoutedEventArgs e)
        => RestoreConfirmBar.Visibility = Visibility.Visible;

    private void RestoreCancel_Click(object sender, RoutedEventArgs e)
        => RestoreConfirmBar.Visibility = Visibility.Collapsed;

    private void RestoreGo_Click(object sender, RoutedEventArgs e)
    {
        _config.RestoreDefaults();
        LoadSettings();
        RestoreConfirmBar.Visibility = Visibility.Collapsed;
        DefaultsRestored?.Invoke();
    }
}
