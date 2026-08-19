using System.Diagnostics;
using Microsoft.Win32;
using Wintangle.Core.Config;

namespace Wintangle.App.Services;

/// <summary>
/// Windows registry autostart (HKCU Run key). The only file that touches
/// <see cref="Microsoft.Win32.Registry"/>; on non-Windows every method is a
/// safe no-op. The config file is the source of truth at startup — this class
/// is just the mechanism that makes "run at logon" happen.
/// </summary>
internal static class AutoStart
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "wintangle";

    /// <summary>True when the Run value is present and matches our executable.</summary>
    public static bool IsEnabled()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            var value = key?.GetValue(ValueName) as string;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return string.Equals(Unquote(value), Unquote(BuildCommand()), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[wintangle] autostart read failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>Creates or removes the Run value for wintangle.</summary>
    public static void SetEnabled(bool enabled)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key == null)
            {
                return;
            }

            if (enabled)
            {
                key.SetValue(ValueName, BuildCommand(), RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[wintangle] autostart write failed: {ex.Message}");
        }
    }

    /// <summary>Quoted path to the running executable, e.g. "\"C:\...\Wintangle.App.exe\"".</summary>
    private static string BuildCommand()
    {
        var exe = AppPaths.GetExePath();
        return string.IsNullOrEmpty(exe) ? string.Empty : $"\"{exe}\"";
    }

    private static string Unquote(string value) =>
        value.Length >= 2 && value[0] == '"' && value[^1] == '"'
            ? value[1..^1]
            : value;
}
