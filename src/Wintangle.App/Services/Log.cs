using Wintangle.App.Services.Logging;
using Wintangle.Core.Logging;

namespace Wintangle.App.Services;

/// <summary>
/// Central static logging facade for wintangle. Dispatches log entries
/// asynchronously via <see cref="LogDispatcher"/> to daily rolling file,
/// debug output, and an in-memory ring buffer.
/// </summary>
internal static class Log
{
    private static readonly object s_lock = new();
    private static LogDispatcher? s_dispatcher;
    private static LogRingBuffer? s_ringBuffer;

    /// <summary>Master switch — false silences every write. Default true.</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>
    /// Initializes logging infrastructure (ring buffer, file sink, debug sink, dispatcher).
    /// Safe to call multiple times (idempotent); never throws.
    /// </summary>
    public static void Init()
    {
        lock (s_lock)
        {
            if (s_dispatcher != null)
            {
                return;
            }

            try
            {
                s_ringBuffer = new LogRingBuffer(200);
                var sinks = new ILogSink[]
                {
                    new RingBufferSink(s_ringBuffer),
                    new RollingFileSink(),
                    new DebugSink(),
                };

                s_dispatcher = new LogDispatcher(sinks);
            }
            catch
            {
                s_dispatcher = null;
                s_ringBuffer = null;
            }
        }
    }

    /// <summary>Writes an INFO entry.</summary>
    public static void Info(string msg) => Write(LogLevel.Info, msg, null);

    /// <summary>Writes a WARN entry.</summary>
    public static void Warn(string msg) => Write(LogLevel.Warn, msg, null);

    /// <summary>Writes an ERROR entry, optionally with the exception.</summary>
    public static void Error(string msg, Exception? ex = null) => Write(LogLevel.Error, msg, ex);

    /// <summary>Returns the most recent log entries from the in-memory ring buffer.</summary>
    public static IReadOnlyList<string> GetRecentEntries() =>
        s_ringBuffer?.Snapshot() ?? Array.Empty<string>();

    /// <summary>Flushes pending entries synchronously.</summary>
    public static void Flush()
    {
        try
        {
            s_dispatcher?.Flush();
        }
        catch
        {
        }
    }

    /// <summary>Flushes and shuts down the logging dispatcher and sinks.</summary>
    public static void Shutdown()
    {
        lock (s_lock)
        {
            if (s_dispatcher == null)
            {
                return;
            }

            try
            {
                s_dispatcher.Shutdown(TimeSpan.FromSeconds(2));
            }
            catch
            {
            }
            finally
            {
                s_dispatcher = null;
            }
        }
    }

    private static void Write(LogLevel level, string msg, Exception? ex)
    {
        if (!Enabled)
        {
            return;
        }

        try
        {
            if (s_dispatcher == null)
            {
                Init();
            }

            var entry = LogEntry.Create(level, msg, ex);
            s_dispatcher?.Enqueue(in entry);
        }
        catch
        {
            // Logging must never throw — swallow everything.
        }
    }
}
