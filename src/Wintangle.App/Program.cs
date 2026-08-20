using System.Windows;
using System.Windows.Interop;
using Wintangle.App.Dispatch;
using Wintangle.App.Hooks;
using Wintangle.App.Interop;
using Wintangle.App.Services;
using Wintangle.App.Tray;
using Wintangle.App.UI;
using Wintangle.Core.Config;
using Wintangle.Core.Hotkeys;

namespace Wintangle.App;

/// <summary>
/// Entry point. Windows-only: on non-Windows the app exits immediately with a
/// console message (the net8.0-windows apphost itself refuses to start on
/// Linux anyway — this guard is defense in depth).
/// </summary>
public static class Program
{
    // Rooted for the app's lifetime: the HwndSource owns the native host
    // window, and if either got GC'd the tray messages would stop arriving.
    private static Window? s_hostWindow;
    private static HwndSource? s_hostSource;
    private static IntPtr s_hostHwnd;

    private static RuntimeState? s_state;
    private static WindowDispatcher? s_dispatcher;
    private static KeyboardHook? s_hook;
    private static ConfigService? s_config;
    private static TrayIcon? s_tray;
    private static TrayMenu? s_menu;

    /// <summary>
    /// Ensures Shutdown()/cleanup runs exactly once: the tray menu Quit path
    /// shuts down and then exits the dispatcher loop, which calls Shutdown()
    /// again from Main().
    /// </summary>
    private static bool s_shutdownDone;

    [STAThread]
    public static void Main()
    {
        Log.Init();
        Log.Info("wintangle starting");

        if (!OperatingSystem.IsWindows())
        {
            Console.WriteLine("wintangle requires Windows 10/11 — exiting.");
            Log.Warn("wintangle requires Windows 10/11 — exiting");
            return;
        }

        // Single-instance guard runs before anything else: a second launch
        // signals the running instance to show settings, then exits.
        if (!SingleInstance.TryAcquire())
        {
            Log.Info("another instance is running — signaled it to show settings; exiting");
            return;
        }

        try
        {
            Dpi.SetProcessDpiAwarenessContext(Dpi.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        }
        catch (Exception ex)
        {
            Log.Warn($"DPI init failed (manifest already declares PerMonitorV2): {ex.Message}");
        }

        var app = new App();

        s_state = new RuntimeState();

        s_hostWindow = CreateHostWindow();
        s_hostHwnd = new WindowInteropHelper(s_hostWindow).EnsureHandle();
        s_hostSource = HwndSource.FromHwnd(s_hostHwnd);
        s_hostSource?.AddHook(WndProc);

        s_tray = new TrayIcon(s_hostHwnd);
        s_tray.Add();

        s_dispatcher = new WindowDispatcher(s_state, (title, text) => s_tray?.ShowBalloon(title, text));

        // Hook first: ConfigService swaps the hotkey table into it (and the
        // tray menu needs the config service for the autostart/ignore items).
        s_hook = new KeyboardHook(DefaultHotkeys.Create());
        s_hook.ActionMatched += action => s_dispatcher.Apply(action, s_state.Gaps);

        s_config = new ConfigService(s_state, s_hook, AppPaths.ConfigPath);

        // Apply theme changes live. ThemeChanged can fire on the watcher
        // thread (external config edits), so marshal to the UI dispatcher
        // before touching Application resources.
        s_config.ThemeChanged += theme =>
        {
            if (Application.Current is not App app)
            {
                return;
            }

            if (app.Dispatcher.CheckAccess())
            {
                app.ApplyTheme(theme);
            }
            else
            {
                app.Dispatcher.BeginInvoke(() => app.ApplyTheme(theme));
            }
        };

        s_menu = new TrayMenu(s_hostHwnd, s_state, s_config, (action, gaps) => s_dispatcher.Apply(action, gaps), Quit, ShowSettingsWindow);

        bool hookInstalled = s_hook.Start();
        if (hookInstalled)
        {
            Log.Info("keyboard hook installed");
        }
        else
        {
            Log.Warn("keyboard hook NOT installed; tray menu still available");
        }

        // Apply persisted config (hotkeys, gaps, ignored apps) and sync the
        // registry autostart to the config's AutoStart flag.
        s_config.Load();
        s_config.ReconcileAutoStart();

        // Apply the persisted theme (Dark by default). ThemeChanged already
        // fired during Load; this direct call covers the case where the
        // dispatcher queue has not been pumped yet, and is a no-op afterwards.
        (Application.Current as App)?.ApplyTheme(s_config.Current.Theme);

        // Runs the WPF dispatcher message loop. No window is shown — the
        // hidden host window routes tray notifications; hotkeys dispatch on
        // the dedicated hook thread.
        app.Run();

        // Application.Shutdown path (tray menu → Quit) unwinds through here
        // after the dispatcher loop exits.
        Shutdown();
        Log.Info("wintangle exiting");
    }

    /// <summary>
    /// Hidden message-routing window. Its title doubles as the single-instance
    /// lookup key (the second instance finds it via FindWindowW by title).
    /// </summary>
    private static Window CreateHostWindow() => new()
    {
        Title = SingleInstance.HostWindowTitle,
        Visibility = Visibility.Hidden,
        ShowInTaskbar = false,
        WindowStyle = WindowStyle.None,
        ResizeMode = ResizeMode.NoResize,
        ShowActivated = false,
        Width = 0,
        Height = 0,
        Left = int.MinValue,
        Top = int.MinValue,
    };

    private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == SingleInstance.WmShowSettings)
        {
            ShowSettingsWindow();
            handled = true;
            return IntPtr.Zero;
        }

        if (s_tray is { } tray && tray.IsCallbackMessage((uint)msg, wParam))
        {
            var mouseMsg = (uint)lParam.ToInt64();
            if (mouseMsg is NativeMethods.WM_CONTEXTMENU or NativeMethods.WM_RBUTTONUP)
            {
                s_menu?.Show();
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Opens (or activates) the settings window. Runs on the UI thread via the
    /// tray menu and via WM_APP+1 from a second instance. Dropped silently
    /// while the config/hook are still being wired up at startup.
    /// </summary>
    /// <remarks>
    /// ShowOrActivate rethrows after resetting its singleton, so a failure here
    /// is logged (Debug builds) and surfaced as a tray balloon (Release builds
    /// only — Debug.WriteLine is compiled out there, and the balloon gives the
    /// user something to report).
    /// </remarks>
    private static void ShowSettingsWindow()
    {
        if (s_config == null || s_hook == null)
        {
            return;
        }

        try
        {
            SettingsWindow.ShowOrActivate(s_config, s_hook);
        }
        catch (Exception ex)
        {
            Log.Error("Settings failed to open", ex);
#if !DEBUG
            s_tray?.ShowBalloon("wintangle", $"Settings failed to open: {ex.Message}");
#endif
        }
    }

    private static void Quit()
    {
        try
        {
            Shutdown();
        }
        finally
        {
            Application.Current.Shutdown();
        }
    }

    private static void Shutdown()
    {
        if (s_shutdownDone)
        {
            return;
        }

        s_shutdownDone = true;

        if (s_hook != null)
        {
            // Never leave the hook in recording mode (settings window closed
            // mid-recording, or a stray second-instance race).
            s_hook.RecordingMode = false;
            s_hook.Dispose();
            s_hook = null;
        }

        s_tray?.Dispose();
        s_tray = null;

        if (s_hostSource != null)
        {
            s_hostSource.RemoveHook(WndProc);
            s_hostSource.Dispose();
            s_hostSource = null;
        }

        s_config?.Dispose();
        SingleInstance.Release();
    }
}
