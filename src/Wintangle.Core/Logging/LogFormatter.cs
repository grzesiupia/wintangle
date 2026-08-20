using System.Globalization;

namespace Wintangle.Core.Logging;

/// <summary>
/// Formats log entries into standard log lines.
/// </summary>
public static class LogFormatter
{
    /// <summary>
    /// Formats a log entry into the standard format:
    /// <c>yyyy-MM-dd HH:mm:ss.fff [LEVEL] [ThreadId] message</c> using invariant culture.
    /// If exception text is present, it is appended on following lines.
    /// </summary>
    public static string Format(in LogEntry entry)
    {
        var timestamp = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var level = entry.Level switch
        {
            LogLevel.Info => "INFO",
            LogLevel.Warn => "WARN",
            LogLevel.Error => "ERROR",
            _ => entry.Level.ToString().ToUpperInvariant(),
        };

        var line = $"{timestamp} [{level}] [{entry.ThreadId}] {entry.Message}";

        if (!string.IsNullOrEmpty(entry.ExceptionText))
        {
            return $"{line}{Environment.NewLine}{entry.ExceptionText}";
        }

        return line;
    }
}
