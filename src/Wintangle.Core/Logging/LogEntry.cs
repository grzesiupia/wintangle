namespace Wintangle.Core.Logging;

/// <summary>
/// Immutable log entry captured at the call site.
/// </summary>
public readonly record struct LogEntry(
    DateTimeOffset Timestamp,
    LogLevel Level,
    int ThreadId,
    string Message,
    string? ExceptionText = null)
{
    /// <summary>
    /// Factory method capturing timestamp, thread ID, and formatted exception string at creation time.
    /// </summary>
    public static LogEntry Create(LogLevel level, string message, Exception? exception = null)
    {
        string? exceptionText = null;
        if (exception != null)
        {
            exceptionText = string.IsNullOrEmpty(exception.StackTrace)
                ? exception.Message
                : $"{exception.Message}{Environment.NewLine}{exception.StackTrace}";
        }

        return new LogEntry(
            DateTimeOffset.Now,
            level,
            Environment.CurrentManagedThreadId,
            message,
            exceptionText);
    }
}
