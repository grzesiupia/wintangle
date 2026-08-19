using System.Diagnostics;
using System.Threading;
using Wintangle.App.Interop;

namespace Wintangle.App.Services;

/// <summary>
/// Named-mutex single-instance guard plus a minimal "show settings" signal.
/// The second instance never owns the mutex: it finds the first instance's
/// hidden host window (by title, verified to belong to another process),
/// posts WM_APP+1 ("show settings"), and exits.
/// </summary>
/// <remarks>
/// Win32-only; pure interop, deliberately minimal (no testable logic here —
/// the interesting behavior lives in <see cref="Program"/> and the host
/// window's WndProc).
/// </remarks>
internal static class SingleInstance
{
    private const string MutexName = @"Local\Wintangle.SingleInstance";

    /// <summary>Title of the first instance's hidden host window.</summary>
    public const string HostWindowTitle = "wintangle";

    /// <summary>Message posted to the first instance to show the settings window.</summary>
    public const int WmShowSettings = (int)NativeMethods.WM_APP_SHOW_SETTINGS;

    private static Mutex? s_mutex;

    /// <summary>
    /// Tries to become the single instance. Returns true when this process
    /// acquired the mutex (first instance). When another instance already
    /// holds it, signals that instance to show settings and returns false.
    /// </summary>
    public static bool TryAcquire()
    {
        s_mutex = new Mutex(initiallyOwned: false, MutexName);

        bool acquired;
        try
        {
            acquired = s_mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            // The previous owner died without releasing; the mutex is ours now.
            acquired = true;
        }

        if (acquired)
        {
            return true;
        }

        SignalExistingInstance();
        return false;
    }

    /// <summary>Releases the mutex (call on the owning thread at shutdown).</summary>
    public static void Release()
    {
        try
        {
            s_mutex?.ReleaseMutex();
        }
        catch (Exception ex) when (ex is ApplicationException or ObjectDisposedException or ThreadStateException)
        {
            Debug.WriteLine($"[wintangle] single-instance mutex release failed: {ex.Message}");
        }
        finally
        {
            s_mutex = null;
        }
    }

    private static void SignalExistingInstance()
    {
        var hwnd = FindHostWindow();
        if (hwnd == IntPtr.Zero)
        {
            // The first instance may be mid-shutdown — retry once, then exit
            // anyway (the caller exits either way).
            Thread.Sleep(500);
            hwnd = FindHostWindow();
        }

        if (hwnd != IntPtr.Zero)
        {
            TrayApi.PostMessageW(hwnd, (uint)WmShowSettings, IntPtr.Zero, IntPtr.Zero);
        }
    }

    /// <summary>
    /// Locates the first instance's hidden host window by title and verifies
    /// it belongs to a different process (guards against a stray window that
    /// merely shares the title).
    /// </summary>
    private static IntPtr FindHostWindow()
    {
        var hwnd = WindowApi.FindWindowW(null, HostWindowTitle);
        if (hwnd == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        WindowApi.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid != 0 && pid != (uint)Environment.ProcessId)
        {
            return hwnd;
        }

        // Own process or unknown — not the first instance's host window.
        return IntPtr.Zero;
    }
}
