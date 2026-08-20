using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Wintangle.App.Services;
using Wintangle.Core.Config;

namespace Wintangle.App
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>Theme currently configured ("Dark"/"Light"/"System").</summary>
        public string? CurrentTheme { get; private set; }

        /// <summary>Effective theme dictionary loaded ("Dark"/"Light").</summary>
        private string? _effectiveTheme;

        public App()
        {
            // Best-effort crash capture: both handlers only log (the dispatcher
            // one deliberately keeps e.Handled=false so WPF's default
            // crash behavior is unchanged). One crash can raise both handlers
            // (dispatcher first, then the process-level one), so a shared guard
            // keeps it to a single "Unhandled" log entry.
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;

            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                Log.Error("App InitializeComponent failed", ex);
            }

            EnsureFallbackResources();
        }

        private static int s_unhandledLogged;

        /// <summary>Atomically claims the single crash log slot. Returns true only for the first caller.</summary>
        private static bool TryMarkUnhandledLogged() => Interlocked.Exchange(ref s_unhandledLogged, 1) == 0;

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (TryMarkUnhandledLogged())
            {
                Log.Error("Unhandled", e.Exception);
                Log.Flush();
            }
            // Deliberately NOT setting e.Handled — the default behavior (crash)
            // stays as-is; the log entry is the only change.
        }

        private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (TryMarkUnhandledLogged())
            {
                Log.Error("Unhandled", e.ExceptionObject as Exception);
                Log.Flush();
            }
        }

        /// <summary>
        /// Reads the Windows theme setting from HKCU registry (1 = Light, 0 = Dark).
        /// Defaults to Dark (false) if non-Windows or unreadable.
        /// </summary>
        public static bool IsSystemLightTheme()
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("AppsUseLightTheme") is int val)
                {
                    return val == 1;
                }
            }
            catch
            {
                // Fallback to dark if registry cannot be read
            }

            return false;
        }

        /// <summary>
        /// Ensures critical font family resources exist even if theme loading fails.
        /// </summary>
        private void EnsureFallbackResources()
        {
            if (TryFindResource("Font.Mono") == null)
            {
                Resources["Font.Mono"] = new FontFamily("JetBrains Mono, Cascadia Code, Cascadia Mono, Consolas, Lucida Console, Courier New");
            }
            if (TryFindResource("Font.Body") == null)
            {
                Resources["Font.Body"] = new FontFamily("Segoe UI Variable Text, Segoe UI, Tahoma, Arial");
            }
            if (TryFindResource("Font.Display") == null)
            {
                Resources["Font.Display"] = new FontFamily("Segoe UI Variable Display, Segoe UI Semibold, Segoe UI, Tahoma, Arial");
            }
        }

        /// <summary>
        /// Swaps the merged theme dictionary to <paramref name="theme"/>. Normalizes unknown values to
        /// the default and no-ops when the theme is already applied. When configured to "System",
        /// resolves to the effective system theme (Light or Dark). Never throws — a failed swap
        /// leaves the previous theme in place and ensures fallback resources.
        /// </summary>
        public void ApplyTheme(string theme)
        {
            var normalized = ConfigStore.NormalizeTheme(theme);
            var effectiveTheme = string.Equals(normalized, ConfigModel.ThemeSystem, StringComparison.Ordinal)
                ? (IsSystemLightTheme() ? ConfigModel.ThemeLight : ConfigModel.ThemeDark)
                : normalized;

            if (string.Equals(CurrentTheme, normalized, StringComparison.Ordinal) &&
                string.Equals(_effectiveTheme, effectiveTheme, StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                var themeUri = new Uri($"pack://application:,,,/Wintangle.App;component/Themes/{effectiveTheme}.xaml", UriKind.Absolute);
                var dictionary = new ResourceDictionary
                {
                    Source = themeUri,
                };

                // Locate existing theme dictionary by checking Source (ends with Dark.xaml or Light.xaml or matching theme URI)
                int themeIndex = -1;
                for (int i = 0; i < Resources.MergedDictionaries.Count; i++)
                {
                    var src = Resources.MergedDictionaries[i].Source?.ToString();
                    if (src != null && (src.EndsWith("Dark.xaml", StringComparison.OrdinalIgnoreCase) ||
                                        src.EndsWith("Light.xaml", StringComparison.OrdinalIgnoreCase) ||
                                        src.Contains("/Themes/", StringComparison.OrdinalIgnoreCase)))
                    {
                        themeIndex = i;
                        break;
                    }
                }

                if (themeIndex >= 0)
                {
                    Resources.MergedDictionaries[themeIndex] = dictionary;
                }
                else
                {
                    Resources.MergedDictionaries.Insert(0, dictionary);
                }

                // Ensure Controls.xaml is always present in MergedDictionaries
                bool hasControls = false;
                for (int i = 0; i < Resources.MergedDictionaries.Count; i++)
                {
                    var src = Resources.MergedDictionaries[i].Source?.ToString();
                    if (src != null && src.EndsWith("Controls.xaml", StringComparison.OrdinalIgnoreCase))
                    {
                        hasControls = true;
                        break;
                    }
                }

                if (!hasControls)
                {
                    var controlsUri = new Uri("pack://application:,,,/Wintangle.App;component/Themes/Controls.xaml", UriKind.Absolute);
                    Resources.MergedDictionaries.Add(new ResourceDictionary { Source = controlsUri });
                }

                CurrentTheme = normalized;
                _effectiveTheme = effectiveTheme;
            }
            catch (Exception ex)
            {
                Log.Warn($"theme apply failed for '{normalized}' (effective: '{effectiveTheme}'): {ex.Message}");
                EnsureFallbackResources();
            }
        }
    }
}
