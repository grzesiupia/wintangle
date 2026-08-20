using System.Windows;
using Wintangle.App.Services;
using Wintangle.App.UI.Tray;
using Wintangle.Core.Geometry;
using Wintangle.Core.Hotkeys;

namespace Wintangle.App.Tray;

/// <summary>
/// Custom WPF tray menu controller. Manages display and dispatch of actions
/// for the custom WPF tray popup menu.
/// </summary>
internal sealed class TrayMenu
{
    private readonly IntPtr _hostHwnd;
    private readonly RuntimeState _state;
    private readonly ConfigService _config;
    private readonly Action<HotkeyAction, GapSettings> _apply;
    private readonly Action _quit;
    private readonly Action _showSettings;

    private TrayMenuWindow? _menuWindow;

    public TrayMenu(
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
    }

    public void Show()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var app = Application.Current;
        if (app == null)
        {
            return;
        }

        if (app.Dispatcher.CheckAccess())
        {
            ShowCore();
            return;
        }

        try
        {
            app.Dispatcher.BeginInvoke(ShowCore);
        }
        catch (InvalidOperationException)
        {
            // Shutting down
        }
    }

    private void ShowCore()
    {
        _menuWindow ??= new TrayMenuWindow(_hostHwnd, _state, _config, _apply, _quit, _showSettings);
        _menuWindow.ShowMenu();
    }
}
