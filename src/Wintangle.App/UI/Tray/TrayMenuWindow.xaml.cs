using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Wintangle.App.Interop;
using Wintangle.App.Services;
using Wintangle.Core.Geometry;
using Wintangle.Core.Hotkeys;

namespace Wintangle.App.UI.Tray;

/// <summary>
/// Custom WPF popup tray menu matching the prototype in wintangle-app.html
/// (2-column slot grid, mono keycaps, checkmarks, dismissal on click-outside
/// or Escape, and DPI-aware monitor positioning).
/// </summary>
public partial class TrayMenuWindow : Window
{
    private static readonly HotkeyAction[] s_slotActions = new[]
    {
        HotkeyAction.HalfLeft,
        HotkeyAction.HalfRight,
        HotkeyAction.CenterHalf,
        HotkeyAction.QuarterTopLeft,
        HotkeyAction.QuarterTopRight,
        HotkeyAction.QuarterBottomLeft,
        HotkeyAction.QuarterBottomRight,
        HotkeyAction.ThirdLeft,
        HotkeyAction.ThirdCenter,
        HotkeyAction.ThirdRight,
        HotkeyAction.SixthTopLeft,
        HotkeyAction.SixthTopCenter,
        HotkeyAction.SixthTopRight,
        HotkeyAction.SixthBottomLeft,
        HotkeyAction.SixthBottomCenter,
        HotkeyAction.SixthBottomRight,
    };

    private readonly IntPtr _hostHwnd;
    private readonly RuntimeState _state;
    private readonly ConfigService _config;
    private readonly Action<HotkeyAction, GapSettings> _apply;
    private readonly Action _quit;
    private readonly Action _showSettings;

    private IntPtr _capturedForeground;
    private string? _foregroundProcess;
    private bool _isDismissing;

    internal TrayMenuWindow(
        IntPtr hostHwnd,
        RuntimeState state,
        ConfigService config,
        Action<HotkeyAction, GapSettings> apply,
        Action quit,
        Action showSettings)
    {
        _hostHwnd = hostHwnd;
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _quit = quit ?? throw new ArgumentNullException(nameof(quit));
        _showSettings = showSettings ?? throw new ArgumentNullException(nameof(showSettings));

        InitializeComponent();

        Deactivated += (_, _) => Dismiss();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Dismiss();
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    /// <summary>
    /// Rebuilds items, positions the menu near the tray cursor, and displays it.
    /// </summary>
    public void ShowMenu()
    {
        _isDismissing = false;

        // Capture foreground window before this menu takes activation
        _capturedForeground = WindowApi.GetForegroundWindow();
        if (_capturedForeground == _hostHwnd)
        {
            _capturedForeground = IntPtr.Zero;
        }

        _foregroundProcess = GetForegroundProcessName(_capturedForeground);

        RebuildContent();
        PositionNearCursor();

        Show();
        Activate();
        Focus();
    }

    private void RebuildContent()
    {
        // 1. Build 16 slot buttons
        SlotGrid.Children.Clear();
        foreach (var action in s_slotActions)
        {
            var btn = CreateSlotButton(action);
            SlotGrid.Children.Add(btn);
        }

        // 2. Update monitor bindings
        var prevHotkey = _config.GetShortcut(HotkeyAction.PrevMonitor);
        PrevMonBindingText.Text = prevHotkey is { } p ? HotkeyLabels.Format(p) : DefaultHotkeys.Format(HotkeyAction.PrevMonitor);

        var nextHotkey = _config.GetShortcut(HotkeyAction.NextMonitor);
        NextMonBindingText.Text = nextHotkey is { } n ? HotkeyLabels.Format(n) : DefaultHotkeys.Format(HotkeyAction.NextMonitor);

        // 3. Update Ignore app item
        if (_foregroundProcess != null)
        {
            IgnoreAppLabel.Text = $"Ignore {_foregroundProcess}";
            bool isIgnored = _state.IsIgnored(_foregroundProcess);
            SetCheckbox(IgnoreAppCheck, IgnoreAppCheckIcon, isIgnored);
            IgnoreAppButton.Visibility = Visibility.Visible;
        }
        else
        {
            IgnoreAppLabel.Text = "Ignore this app";
            SetCheckbox(IgnoreAppCheck, IgnoreAppCheckIcon, false);
            IgnoreAppButton.Visibility = Visibility.Visible;
        }

        // 4. Update Autostart item
        bool autostart = _config.GetAutoStartEnabled();
        SetCheckbox(AutostartCheck, AutostartCheckIcon, autostart);
    }

    private Button CreateSlotButton(HotkeyAction action)
    {
        var btn = new Button
        {
            Style = (Style)FindResource("TraySlotButton"),
            Margin = new Thickness(1),
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var nameText = new TextBlock
        {
            Text = UiLabels.ActionName(action),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        nameText.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Fg");

        var hotkey = _config.GetShortcut(action);
        var chordText = hotkey is { } hk ? HotkeyLabels.Format(hk) : DefaultHotkeys.Format(action);

        var chordBlock = new TextBlock
        {
            Text = chordText,
            FontSize = 9.5,
            FontFamily = (FontFamily)FindResource("Font.Mono"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0),
        };
        chordBlock.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Muted");

        Grid.SetColumn(nameText, 0);
        Grid.SetColumn(chordBlock, 1);
        grid.Children.Add(nameText);
        grid.Children.Add(chordBlock);

        btn.Content = grid;
        btn.Click += (_, _) =>
        {
            Dismiss();
            _apply(action, _state.Gaps);
        };

        return btn;
    }

    private static void SetCheckbox(Border checkBorder, Path checkIcon, bool isChecked)
    {
        if (isChecked)
        {
            checkBorder.SetResourceReference(Border.BackgroundProperty, "Brush.Accent");
            checkBorder.SetResourceReference(Border.BorderBrushProperty, "Brush.Accent");
            checkIcon.Visibility = Visibility.Visible;
        }
        else
        {
            checkBorder.Background = Brushes.Transparent;
            checkBorder.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");
            checkIcon.Visibility = Visibility.Collapsed;
        }
    }

    private void PositionNearCursor()
    {
        TrayApi.GetCursorPos(out var pt);

        double cursorX = pt.X;
        double cursorY = pt.Y;
        double workLeft = SystemParameters.WorkArea.Left;
        double workTop = SystemParameters.WorkArea.Top;
        double workRight = SystemParameters.WorkArea.Right;
        double workBottom = SystemParameters.WorkArea.Bottom;

        if (OperatingSystem.IsWindows())
        {
            var ptNative = new POINT { X = pt.X, Y = pt.Y };
            var monitor = MonitorApi.MonitorFromPoint(ptNative, MonitorApi.MONITOR_DEFAULTTONEAREST);
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (monitor != IntPtr.Zero && MonitorApi.GetMonitorInfoW(monitor, ref info))
            {
                var source = PresentationSource.FromVisual(this);
                double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

                cursorX = pt.X / dpiX;
                cursorY = pt.Y / dpiY;
                workLeft = info.rcWork.Left / dpiX;
                workTop = info.rcWork.Top / dpiY;
                workRight = info.rcWork.Right / dpiX;
                workBottom = info.rcWork.Bottom / dpiY;
            }
        }

        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double menuWidth = 330 + 36; // 330 + margin
        double menuHeight = DesiredSize.Height > 0 ? DesiredSize.Height : 440;

        // Position: if cursor is closer to bottom edge, place above cursor; otherwise below
        double posX = cursorX - (menuWidth / 2);
        double posY = (cursorY > (workTop + workBottom) / 2)
            ? cursorY - menuHeight - 10
            : cursorY + 10;

        // Clamp inside work area
        posX = Math.Max(workLeft + 8, Math.Min(posX, workRight - menuWidth - 8));
        posY = Math.Max(workTop + 8, Math.Min(posY, workBottom - menuHeight - 8));

        Left = posX;
        Top = posY;
    }

    private void Dismiss()
    {
        if (_isDismissing)
        {
            return;
        }

        _isDismissing = true;
        Hide();
        RestoreForeground(_capturedForeground);
    }

    private void PrevMonButton_Click(object sender, RoutedEventArgs e)
    {
        Dismiss();
        _apply(HotkeyAction.PrevMonitor, _state.Gaps);
    }

    private void NextMonButton_Click(object sender, RoutedEventArgs e)
    {
        Dismiss();
        _apply(HotkeyAction.NextMonitor, _state.Gaps);
    }

    private void IgnoreAppButton_Click(object sender, RoutedEventArgs e)
    {
        if (_foregroundProcess != null)
        {
            if (_state.IsIgnored(_foregroundProcess))
            {
                _config.RemoveIgnored(_foregroundProcess);
            }
            else
            {
                _config.AddIgnored(_foregroundProcess);
            }
        }

        Dismiss();
    }

    private void AutostartButton_Click(object sender, RoutedEventArgs e)
    {
        _config.ToggleAutoStart();
        Dismiss();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        Dismiss();
        _showSettings();
    }

    private void QuitButton_Click(object sender, RoutedEventArgs e)
    {
        Dismiss();
        _quit();
    }

    private void RestoreForeground(IntPtr previous)
    {
        if (previous == IntPtr.Zero || previous == _hostHwnd)
        {
            return;
        }

        if (TrayApi.SetForegroundWindow(previous))
        {
            return;
        }

        var fgThread = WindowApi.GetWindowThreadProcessId(WindowApi.GetForegroundWindow(), out _);
        var currentThread = HookApi.GetCurrentThreadId();
        var attached = fgThread != 0 && fgThread != currentThread
            && WindowApi.AttachThreadInput(currentThread, fgThread, true);
        if (attached)
        {
            try
            {
                if (TrayApi.SetForegroundWindow(previous))
                {
                    return;
                }
            }
            finally
            {
                WindowApi.AttachThreadInput(currentThread, fgThread, false);
            }
        }

        WindowApi.BringWindowToTop(previous);
    }

    private static string? GetForegroundProcessName(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        if (WindowApi.GetWindowThreadProcessId(hwnd, out var pid) == 0 || pid == 0)
        {
            return null;
        }

        if (pid == (uint)Environment.ProcessId)
        {
            return null;
        }

        try
        {
            return Process.GetProcessById((int)pid).ProcessName;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}
