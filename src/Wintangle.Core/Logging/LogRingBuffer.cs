namespace Wintangle.Core.Logging;

/// <summary>
/// Thread-safe circular buffer retaining the last N log entries (default 200).
/// </summary>
public sealed class LogRingBuffer
{
    private readonly object _lock = new();
    private readonly string[] _buffer;
    private readonly int _capacity;
    private int _head;
    private int _count;

    /// <summary>
    /// Initializes a new instance of <see cref="LogRingBuffer"/> with the specified capacity.
    /// </summary>
    /// <param name="capacity">Maximum number of entries to retain. Defaults to 200.</param>
    public LogRingBuffer(int capacity = 200)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be greater than zero.");
        }

        _capacity = capacity;
        _buffer = new string[capacity];
    }

    /// <summary>
    /// Maximum capacity of the ring buffer.
    /// </summary>
    public int Capacity => _capacity;

    /// <summary>
    /// Current number of entries stored.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _count;
            }
        }
    }

    /// <summary>
    /// Appends a new entry to the circular buffer, evicting the oldest if at capacity.
    /// </summary>
    public void Add(string entry)
    {
        lock (_lock)
        {
            _buffer[_head] = entry;
            _head = (_head + 1) % _capacity;
            if (_count < _capacity)
            {
                _count++;
            }
        }
    }

    /// <summary>
    /// Returns a point-in-time snapshot of the buffer's contents in chronological order (oldest to newest).
    /// </summary>
    public IReadOnlyList<string> Snapshot()
    {
        lock (_lock)
        {
            if (_count == 0)
            {
                return Array.Empty<string>();
            }

            var result = new string[_count];
            int start = (_head - _count + _capacity) % _capacity;
            for (int i = 0; i < _count; i++)
            {
                result[i] = _buffer[(start + i) % _capacity];
            }

            return result;
        }
    }

    /// <summary>
    /// Clears all entries from the buffer.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _head = 0;
            _count = 0;
            Array.Clear(_buffer, 0, _buffer.Length);
        }
    }
}
