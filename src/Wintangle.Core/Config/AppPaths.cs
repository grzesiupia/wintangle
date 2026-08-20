using System.Diagnostics;

namespace Wintangle.Core.Config;

/// <summary>
/// Well-known file locations. Pure — no Win32 — so the config path is
/// resolvable on any platform (Linux resolves %APPDATA% → ~/.config).
/// </summary>
public static class AppPaths
{
    /// <summary>The wintangle config directory under the per-user app data folder.</summary>
    public static string ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "wintangle");

    /// <summary>The config file path (created on first run).</summary>
    public static string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

    /// <summary>The wintangle log directory under the per-user local app data folder.</summary>
    public static string LogDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "wintangle", "logs");

    /// <summary>Generates the standard log file name for a given date.</summary>
    public static string LogFileName(DateTime dt) => $"wintangle-{dt:yyyyMMdd}.log";

    /// <summary>
    /// Absolute path to the running wintangle executable, used for the
    /// registry autostart command. Empty string when it cannot be resolved.
    /// </summary>
    public static string GetExePath() =>
        Environment.ProcessPath
        ?? Process.GetCurrentProcess().MainModule?.FileName
        ?? string.Empty;
}
