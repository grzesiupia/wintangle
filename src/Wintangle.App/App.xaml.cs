using System.Windows;
using System.Windows.Threading;
using Wintangle.App.Services;
using Wintangle.Core.Config;

namespace Wintangle.App
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>Theme currently applied to the app resources ("Dark"/"Light").</summary>
        public string? CurrentTheme { get; private set; }

        public App()
        {
            // Best-effort crash capture: both handlers only log (the dispatcher
            // one deliberately keeps e.Handled=false so WPF's default
            // crash behavior is unchanged). One crash can raise both handlers
            // (dispatcher first, then the process-level one), so a shared guard
            // keeps it to a single "Unhandled" log entry.
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        }

        private static int s_unhandledLogged;

        /// <summary>Atomically claims the single crash log slot. Returns true only for the first caller.</summary>
        private static bool TryMarkUnhandledLogged() => Interlocked.Exchange(ref s_unhandledLogged, 1) == 0;

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (TryMarkUnhandledLogged())
            {
                Log.Error("Unhandled", e.Exception);
            }
            // Deliberately NOT setting e.Handled — the default behavior (crash)
            // stays as-is; the log entry is the only change.
        }

        private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (TryMarkUnhandledLogged())
            {
                Log.Error("Unhandled", e.ExceptionObject as Exception);
            }
        }

        /// <summary>
        /// Swaps the merged theme dictionary (MergedDictionaries[0], seeded by
        /// App.xaml) to <paramref name="theme"/>. Normalizes unknown values to
        /// the default and no-ops when the theme is already applied. Never
        /// throws — a failed swap leaves the previous theme in place.
        /// </summary>
        public void ApplyTheme(string theme)
        {
            var normalized = ConfigStore.NormalizeTheme(theme);
            if (string.Equals(CurrentTheme, normalized, StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                var dictionary = new ResourceDictionary
                {
                    Source = new Uri($"Themes/{normalized}.xaml", UriKind.Relative),
                };
                Resources.MergedDictionaries[0] = dictionary;
                CurrentTheme = normalized;
            }
            catch (Exception ex)
            {
                Log.Warn($"theme apply failed for '{normalized}': {ex.Message}");
            }
        }
    }
}
