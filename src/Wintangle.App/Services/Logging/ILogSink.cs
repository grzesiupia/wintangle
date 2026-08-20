using Wintangle.Core.Logging;

namespace Wintangle.App.Services.Logging;

/// <summary>
/// Defines a target sink for formatted log entries.
/// </summary>
internal interface ILogSink : IDisposable
{
    /// <summary>Emits a formatted log entry to the sink.</summary>
    void Emit(in LogEntry entry, string formatted);

    /// <summary>Flushes buffered entries to underlying storage.</summary>
    void Flush();
}
