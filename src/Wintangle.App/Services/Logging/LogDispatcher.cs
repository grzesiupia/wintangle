using System.Threading.Channels;
using Wintangle.Core.Logging;

namespace Wintangle.App.Services.Logging;

/// <summary>
/// Asynchronous bounded channel dispatcher that consumes log entries on a single
/// background task and delivers them to all registered sinks.
/// </summary>
internal sealed class LogDispatcher : IDisposable
{
    private readonly Channel<LogEntry> _channel;
    private readonly IReadOnlyList<ILogSink> _sinks;
    private readonly Task _processingTask;
    private bool _isDisposed;

    public LogDispatcher(IEnumerable<ILogSink> sinks)
    {
        _sinks = sinks?.ToList() ?? new List<ILogSink>();

        var options = new BoundedChannelOptions(10_000)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropWrite,
        };

        _channel = Channel.CreateBounded<LogEntry>(options);
        _processingTask = Task.Factory.StartNew(
            ProcessQueueAsync,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();
    }

    /// <summary>
    /// Enqueues a log entry into the channel. Returns false if channel is full or disposed.
    /// </summary>
    public bool Enqueue(in LogEntry entry)
    {
        if (_isDisposed)
        {
            return false;
        }

        return _channel.Writer.TryWrite(entry);
    }

    private async Task ProcessQueueAsync()
    {
        var reader = _channel.Reader;
        try
        {
            while (await reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (reader.TryRead(out var entry))
                {
                    DispatchEntry(in entry);
                }
            }
        }
        catch
        {
            // Channel completed or faulted — swallow silently
        }
    }

    private void DispatchEntry(in LogEntry entry)
    {
        string formatted;
        try
        {
            formatted = LogFormatter.Format(in entry);
        }
        catch
        {
            return;
        }

        for (int i = 0; i < _sinks.Count; i++)
        {
            try
            {
                _sinks[i].Emit(in entry, formatted);
            }
            catch
            {
                // Sink errors never propagate
            }
        }
    }

    /// <summary>
    /// Synchronously drains readable items and flushes all sinks.
    /// </summary>
    public void Flush()
    {
        while (_channel.Reader.TryRead(out var entry))
        {
            DispatchEntry(in entry);
        }

        for (int i = 0; i < _sinks.Count; i++)
        {
            try
            {
                _sinks[i].Flush();
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// Completes the channel, waits up to <paramref name="timeout"/> for processing to finish,
    /// flushes, and disposes all sinks.
    /// </summary>
    public void Shutdown(TimeSpan timeout)
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _channel.Writer.TryComplete();

        try
        {
            _processingTask.Wait(timeout);
        }
        catch
        {
        }

        Flush();

        for (int i = 0; i < _sinks.Count; i++)
        {
            try
            {
                _sinks[i].Dispose();
            }
            catch
            {
            }
        }
    }

    public void Dispose()
    {
        Shutdown(TimeSpan.FromSeconds(2));
    }
}
