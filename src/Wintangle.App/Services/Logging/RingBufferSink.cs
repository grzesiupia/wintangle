using Wintangle.Core.Logging;

namespace Wintangle.App.Services.Logging;

/// <summary>
/// Emits formatted log lines into an in-memory <see cref="LogRingBuffer"/>.
/// </summary>
internal sealed class RingBufferSink : ILogSink
{
    private readonly LogRingBuffer _ringBuffer;

    public RingBufferSink(LogRingBuffer ringBuffer)
    {
        _ringBuffer = ringBuffer ?? throw new ArgumentNullException(nameof(ringBuffer));
    }

    public LogRingBuffer RingBuffer => _ringBuffer;

    public void Emit(in LogEntry entry, string formatted)
    {
        try
        {
            _ringBuffer.Add(formatted);
        }
        catch
        {
        }
    }

    public void Flush()
    {
    }

    public void Dispose()
    {
    }
}
