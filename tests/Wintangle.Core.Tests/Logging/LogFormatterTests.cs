using System.Globalization;
using Wintangle.Core.Logging;

namespace Wintangle.Core.Tests.Logging;

public class LogFormatterTests
{
    [Fact]
    public void Format_BasicInfoEntry_FormatsCorrectly()
    {
        var timestamp = new DateTimeOffset(2026, 8, 20, 14, 30, 15, 123, TimeSpan.Zero);
        var entry = new LogEntry(timestamp, LogLevel.Info, 42, "Application started");

        var formatted = LogFormatter.Format(in entry);

        Assert.Equal("2026-08-20 14:30:15.123 [INFO] [42] Application started", formatted);
    }

    [Fact]
    public void Format_AllLogLevels_FormatsCorrectLevelTag()
    {
        var timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, 0, TimeSpan.Zero);

        var infoEntry = new LogEntry(timestamp, LogLevel.Info, 1, "msg");
        var warnEntry = new LogEntry(timestamp, LogLevel.Warn, 2, "msg");
        var errorEntry = new LogEntry(timestamp, LogLevel.Error, 3, "msg");

        Assert.Equal("2026-01-01 00:00:00.000 [INFO] [1] msg", LogFormatter.Format(in infoEntry));
        Assert.Equal("2026-01-01 00:00:00.000 [WARN] [2] msg", LogFormatter.Format(in warnEntry));
        Assert.Equal("2026-01-01 00:00:00.000 [ERROR] [3] msg", LogFormatter.Format(in errorEntry));
    }

    [Fact]
    public void Format_WithExceptionText_AppendsOnNewline()
    {
        var timestamp = new DateTimeOffset(2026, 8, 20, 10, 0, 0, 500, TimeSpan.Zero);
        var exText = "System.InvalidOperationException: Boom!" + Environment.NewLine + "   at Foo.Bar()";
        var entry = new LogEntry(timestamp, LogLevel.Error, 10, "Operation failed", exText);

        var formatted = LogFormatter.Format(in entry);

        var expected = $"2026-08-20 10:00:00.500 [ERROR] [10] Operation failed{Environment.NewLine}{exText}";
        Assert.Equal(expected, formatted);
    }

    [Fact]
    public void Format_WithCultureOverride_AlwaysUsesInvariantCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            // Set a culture that uses non-standard date separators (e.g. German uses '.' for date)
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var timestamp = new DateTimeOffset(2026, 12, 25, 18, 5, 9, 7, TimeSpan.Zero);
            var entry = new LogEntry(timestamp, LogLevel.Info, 7, "Festive message");

            var formatted = LogFormatter.Format(in entry);

            Assert.Equal("2026-12-25 18:05:09.007 [INFO] [7] Festive message", formatted);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void LogEntry_Create_CapturesExceptionDetailsAndThread()
    {
        var currentThreadId = Environment.CurrentManagedThreadId;
        var ex = new InvalidOperationException("Test exception message");

        var entry = LogEntry.Create(LogLevel.Error, "Something failed", ex);

        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("Something failed", entry.Message);
        Assert.Equal(currentThreadId, entry.ThreadId);
        Assert.NotNull(entry.ExceptionText);
        Assert.Contains("Test exception message", entry.ExceptionText);
    }
}
