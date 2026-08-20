using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
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
/// Settings dialog: two themed tabs (keyboard shortcuts, settings) in a
/// custom-chrome window (WindowChrome caption + DWM rounded corners). At most
/// one instance exists at a time (<see cref="ShowOrActivate"/>); each open is a
/// fresh window populated from the current config — no stale state. All changes
/// persist immediately through <see cref="ConfigService"/>.
/// </summary>
/// <remarks>
/// The window draws its own titlebar (no system caption), follows
/// <see cref="ConfigService.ThemeChanged"/> live (theme swaps re-sync the
/// settings theme cards), and clamps WM_GETMINMAXINFO so maximized fills the
/// monitor's work area instead of covering the taskbar. Closing the window
/// tears down the shortcut recorders so the keyboard hook is never left in
/// RecordingMode.
/// </remarks>
public partial class SettingsWindow : Window
{
    /// <summary>The single live settings window, or null (fresh create per open).</summary>
    private static SettingsWindow? s_openWindow;

    private readonly ConfigService _config;
    private readonly KeyboardHook _hook;

    private IntPtr _hwnd;
    private HwndSource? _source;

    private ShortcutsTab? _shortcutsPanel;
    private SettingsTab? _settingsPanel;

    private string _activeTab = "shortcuts";

    internal SettingsWindow(ConfigService config, KeyboardHook hook)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _hook = hook ?? throw new ArgumentNullException(nameof(hook));

        InitializeComponent();
        LoadHeaderIcon();

        _shortcutsPanel = new ShortcutsTab(config, hook);
        _settingsPanel = new SettingsTab(config);
        _settingsPanel.DefaultsRestored += OnDefaultsRestored;

        ShortcutsHost.Content = _shortcutsPanel;
        SettingsHost.Content = _settingsPanel;

        _config.ThemeChanged += OnThemeChanged;
        StateChanged += OnWindowStateChanged;

        ShowTab("shortcuts");

        Closed += (_, _) => Teardown();
    }

    /// <summary>
    /// Shows the settings window, activating the existing instance if one is
    /// already open (tray menu and WM_APP+1 both route through here).
    /// </summary>
    /// <remarks>
    /// The whole open sequence is guarded: if construction/Show/Activate throws
    /// (the tray menu's broad catch swallows it, and Debug.WriteLine is
    /// invisible in Release), the static singleton is reset so a later open can
    /// retry instead of no-oping forever against a stale non-null reference.
    /// </remarks>
    internal static void ShowOrActivate(ConfigService config, KeyboardHook hook)
    {
        if (s_openWindow != null)
        {
            try
            {
                s_openWindow.Activate();
            }
            catch (Exception ex)
            {
                // Symmetric guard for the existing-window path. The window is
                // alive and the singleton stays set — nothing to reset.
                Debug.WriteLine($"[wintangle] settings window activate failed: {ex.Message}");
            }
            return;
        }

        SettingsWindow? window = null;
        try
        {
            window = new SettingsWindow(config, hook);
            s_openWindow = window;
            window.Closed += (_, _) => s_openWindow = null;
            window.Show();
            window.Activate();
        }
        catch (Exception ex)
        {
            // Never leave s_openWindow set: the tray menu swallows this
            // exception, and a stale non-null reference would make every later
            // open a no-op — Settings would never open again.
            //
            // If the ctor already ran, the window subscribed to ThemeChanged
            // and would otherwise root the config delegate graph forever
            // (repeated failures leak windows), so unsubscribe and close it
            // before discarding the reference.
            if (window != null)
            {
                window._config.ThemeChanged -= window.OnThemeChanged;
                try
                {
                    window.Close();
                }
                catch (Exception closeEx)
                {
                    Debug.WriteLine($"[wintangle] settings window close after open failure: {closeEx.Message}");
                }
            }
            s_openWindow = null;
            Debug.WriteLine($"[wintangle] settings window failed to open: {ex.Message}");
            throw;
        }
    }

    // ---- Chrome ----

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        try
        {
            _hwnd = new WindowInteropHelper(this).EnsureHandle();
            _source = HwndSource.FromHwnd(_hwnd);
            _source?.AddHook(WndProc);

            // Windows 11 rounded corners (custom chrome has no system caption).
            WindowApi.ApplyWindowCorners(_hwnd, WindowApi.DWMWCP_ROUND);
        }
        catch (Exception ex)
        {
            // Chrome niceties must not kill the window: a failed corner
            // attribute or hook leaves the window usable without them.
            Debug.WriteLine($"[wintangle] settings window chrome init failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Alt+F4 with custom chrome (WindowStyle=None): the DefWindowProc path may
    /// not translate the chord into WM_CLOSE, so handle it directly.
    /// </summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.F4 && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            e.Handled = true;
            Close();
            return;
        }

        base.OnPreviewKeyDown(e);
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

    /// <summary>
    /// WM_GETMINMAXINFO: clamps the maximized size/position to the monitor's
    /// work area. With custom chrome (WindowStyle=None) WPF would otherwise
    /// maximize over the full monitor, covering the taskbar. Only the
    /// ptMaxSize/ptMaxPosition fields are overwritten with the rcWork values;
    /// the incoming struct's ptReserved, ptMinTrackSize (WPF's MinWidth/
    /// MinHeight) and ptMaxTrackSize are passed through untouched.
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == (int)NativeMethods.WM_GETMINMAXINFO)
        {
            ClampMaximizeToWorkArea(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static void ClampMaximizeToWorkArea(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        var monitor = MonitorApi.MonitorFromWindow(hwnd, MonitorApi.MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
        if (monitor != IntPtr.Zero && MonitorApi.GetMonitorInfoW(monitor, ref info))
        {
            // Only touch ptMaxSize/ptMaxPosition — the struct round-trips the
            // rest verbatim (min/max track sizes stay as WPF set them).
            mmi.ptMaxSize.X = info.rcWork.Width;
            mmi.ptMaxSize.Y = info.rcWork.Height;
            mmi.ptMaxPosition.X = info.rcWork.Left;
            mmi.ptMaxPosition.Y = info.rcWork.Top;
            Marshal.StructureToPtr(mmi, lParam, false);
        }
    }

    // ---- Titlebar buttons ----

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(this);
        }
        else
        {
            SystemCommands.MaximizeWindow(this);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        // The maximize button becomes a "restore" glyph while maximized.
        MaximizeButton.IsRestore = WindowState == WindowState.Maximized;
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

    /// <summary>
    /// Theme swap hook: re-syncs the settings tab's theme cards (the tab also
    /// self-subscribes; both paths are idempotent).
    /// </summary>
    private void ApplyThemeUi(string theme)
    {
        _settingsPanel?.SyncThemeRadios(theme);
    }

    // ---- Tab activation ----

    private void ShortcutsTabButton_Click(object sender, RoutedEventArgs e) => ShowTab("shortcuts");

    private void SettingsTabButton_Click(object sender, RoutedEventArgs e) => ShowTab("settings");

    /// <summary>
    /// Swaps the visible pane and runs that tab's refresh hook. The tabs share
    /// the shell's public surface (ShortcutsTab.Rebuild, SettingsTab
    /// auto-syncs via its own ThemeChanged subscription).
    /// </summary>
    private void ShowTab(string name)
    {
        bool shortcuts = name == "shortcuts";
        bool settings = name == "settings";

        // Leaving the shortcuts tab must cancel any in-flight recording so the
        // shared hook flag is never left armed on a hidden tab. Rebuild does
        // exactly that (CancelRecording per row resets RecordingMode).
        if (!shortcuts && _activeTab == "shortcuts")
        {
            _shortcutsPanel?.Rebuild();
        }

        ShortcutsScroll.Visibility = shortcuts ? Visibility.Visible : Visibility.Collapsed;
        SettingsScroll.Visibility = settings ? Visibility.Visible : Visibility.Collapsed;

        if (shortcuts)
        {
            _shortcutsPanel?.Rebuild();
        }
        else if (settings)
        {
            _settingsPanel?.RefreshIgnored();
        }

        _activeTab = name;
    }

    // ---- Defaults restore ----

    private void OnDefaultsRestored()
    {
        // RestoreDefaults already fired ThemeChanged when the theme actually
        // changed; this covers the same-theme case (cards re-sync either way)
        // and rebuilds the shortcut rows.
        _settingsPanel?.SyncThemeRadios(_config.Theme);
        _shortcutsPanel?.Rebuild();
    }

    // ---- Lifecycle ----

    private void Teardown()
    {
        _config.ThemeChanged -= OnThemeChanged;
        _hook.RecordingMode = false;
        _shortcutsPanel?.Teardown();
        _settingsPanel?.Teardown();
    }
}
