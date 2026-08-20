using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Wintangle.App.Interop;

namespace Wintangle.App.UI.Tray;

/// <summary>
/// Custom toast balloon notification matching the wintangle design's .balloon
/// (320px width, 11px radius, status dot, smooth slide-up/fade animation,
/// positioned in the bottom-right corner of the active work area).
/// </summary>
public partial class ToastWindow : Window
{
    private static ToastWindow? s_instance;
    private readonly DispatcherTimer _dismissTimer;
    private bool _isClosing;

    public ToastWindow()
    {
        InitializeComponent();

        _dismissTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(3400),
        };
        _dismissTimer.Tick += (_, _) => Dismiss();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var hwnd = new WindowInteropHelper(this).EnsureHandle();

        // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW: never steal focus from the user's active window
        const int GWL_EXSTYLE = -20;
        const long WS_EX_NOACTIVATE = 0x08000000L;
        const long WS_EX_TOOLWINDOW = 0x00000080L;

        IntPtr currentExStyle = WindowApi.GetWindowLongPtrW(hwnd, GWL_EXSTYLE);
        IntPtr newExStyle = new IntPtr(currentExStyle.ToInt64() | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);
        WindowApi.SetWindowLongPtrW(hwnd, GWL_EXSTYLE, newExStyle);
    }

    /// <summary>
    /// Shows a toast notification. Thread-safe: can be called from any thread.
    /// </summary>
    public static void ShowToast(string title, string message, bool isError = false)
    {
        var app = Application.Current;
        if (app == null)
        {
            return;
        }

        if (app.Dispatcher.CheckAccess())
        {
            ShowToastCore(title, message, isError);
            return;
        }

        try
        {
            app.Dispatcher.BeginInvoke(() => ShowToastCore(title, message, isError));
        }
        catch (InvalidOperationException)
        {
            // App is shutting down
        }
    }

    private static void ShowToastCore(string title, string message, bool isError)
    {
        s_instance ??= new ToastWindow();
        s_instance.Display(title, message, isError);
    }

    private void Display(string title, string message, bool isError)
    {
        _dismissTimer.Stop();
        _isClosing = false;

        TitleText.Text = string.IsNullOrEmpty(title) ? "Wintangle" : title;
        MessageText.Text = message ?? string.Empty;

        StatusDot.SetResourceReference(
            Border.BackgroundProperty,
            isError ? "Brush.Danger" : "Brush.Accent");

        PositionNearTaskbar();

        Show();

        // Animate in: Opacity 0 -> 1, TranslateY 12 -> 0
        Opacity = 0;
        ToastTransform.Y = 12;

        var duration = TimeSpan.FromMilliseconds(180);
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };

        var opacityAnim = new DoubleAnimation(0, 1, duration) { EasingFunction = ease };
        var transAnim = new DoubleAnimation(12, 0, duration) { EasingFunction = ease };

        BeginAnimation(OpacityProperty, opacityAnim);
        ToastTransform.BeginAnimation(TranslateTransform.YProperty, transAnim);

        _dismissTimer.Start();
    }

    private void PositionNearTaskbar()
    {
        // Position at the bottom-right of the current monitor's work area
        double workRight = SystemParameters.WorkArea.Right;
        double workBottom = SystemParameters.WorkArea.Bottom;

        if (OperatingSystem.IsWindows())
        {
            var foreground = WindowApi.GetForegroundWindow();
            var monitor = MonitorApi.MonitorFromWindow(foreground != IntPtr.Zero ? foreground : IntPtr.Zero, MonitorApi.MONITOR_DEFAULTTOPRIMARY);
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (monitor != IntPtr.Zero && MonitorApi.GetMonitorInfoW(monitor, ref info))
            {
                // Convert screen physical pixels to WPF DIPs
                var source = PresentationSource.FromVisual(this);
                double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

                workRight = info.rcWork.Right / dpiX;
                workBottom = info.rcWork.Bottom / dpiY;
            }
        }

        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double targetWidth = 320 + 32; // width + margin
        double targetHeight = DesiredSize.Height > 0 ? DesiredSize.Height : 90;

        Left = workRight - targetWidth - 12;
        Top = workBottom - targetHeight - 12;
    }

    private void Dismiss()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        _dismissTimer.Stop();

        var duration = TimeSpan.FromMilliseconds(180);
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseIn };

        var opacityAnim = new DoubleAnimation(1, 0, duration) { EasingFunction = ease };
        var transAnim = new DoubleAnimation(0, 12, duration) { EasingFunction = ease };

        opacityAnim.Completed += (_, _) =>
        {
            Hide();
            _isClosing = false;
        };

        BeginAnimation(OpacityProperty, opacityAnim);
        ToastTransform.BeginAnimation(TranslateTransform.YProperty, transAnim);
    }

    private void OnToastClick(object sender, MouseButtonEventArgs e)
    {
        Dismiss();
    }
}
