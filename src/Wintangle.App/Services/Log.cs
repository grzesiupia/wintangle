using System.IO;

namespace Wintangle.App.Services;

/// <summary>
/// Minimal plain-text file logger. Writes to <c>wintangle.log</c> next to the
/// app executable (<see cref="AppContext.BaseDirectory"/>). Logging is best
/// effort only: <see cref="Init"/> and every write are wrapped so the logger
/// never throws and never takes the app down.
/// </summary>
/// <remarks>
/// All levels are kept in Release builds (the user wants to track issues), so
/// callers can rely on the file being written in a shipped build. Set
/// <see cref="Enabled"/> to false to disable logging at runtime if it ever
/// needs to be switched off without a rebuild.
/// </remarks>
internal static class Log
{
    private static readonly object s_lock = new();
    private static string? s_path;

    /// <summary>Master switch — false silences every write. Default true.</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>
    /// Resolves the log file path (next to the executable) once. No-op when
    /// already initialized; never throws — a failed resolve leaves logging
    /// silently disabled.
    /// </summary>
    public static void Init()
    {
        lock (s_lock)
        {
            if (s_path != null)
            {
                return;
            }

            try
            {
                s_path = Path.Combine(AppContext.BaseDirectory, "wintangle.log");
            }
            catch
            {
                s_path = null; // cannot determine a path — disable logging
            }
        }
    }

    /// <summary>Writes an INFO entry.</summary>
    public static void Info(string msg) => Write("INFO", msg, null);

    /// <summary>Writes a WARN entry.</summary>
    public static void Warn(string msg) => Write("WARN", msg, null);

    /// <summary>Writes an ERROR entry, optionally with the exception (Message + StackTrace).</summary>
    public static void Error(string msg, Exception? ex = null) => Write("ERROR", msg, ex);

    private static void Write(string level, string msg, Exception? ex)
    {
        if (!Enabled)
        {
            return;
        }

        lock (s_lock)
        {
            if (s_path == null)
            {
                return;
            }

            try
            {
                var entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  [{level}]  {msg}";
                if (ex != null)
                {
                    entry += Environment.NewLine + ex.Message;
                    if (!string.IsNullOrEmpty(ex.StackTrace))
                    {
                        entry += Environment.NewLine + ex.StackTrace;
                    }
                }

                File.AppendAllText(s_path, entry + Environment.NewLine);
            }
            catch
            {
                // Logging must never throw — swallow everything.
            }
        }
    }
}
