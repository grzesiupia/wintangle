using System.Diagnostics;
using System.Windows;
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
                Debug.WriteLine($"[wintangle] theme apply failed for '{normalized}': {ex.Message}");
            }
        }
    }
}
