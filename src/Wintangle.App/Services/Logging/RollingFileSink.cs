using System.IO;
using System.Text;
using Wintangle.Core.Config;
using Wintangle.Core.Logging;

namespace Wintangle.App.Services.Logging;

/// <summary>
/// Writes log entries to daily files in <see cref="AppPaths.LogDirectory"/>,
/// rolls files when they exceed 5 MB, and applies <see cref="LogRetentionPolicy"/>
/// on startup and file rollover. Never throws exceptions.
/// </summary>
internal sealed class RollingFileSink : ILogSink
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

    private readonly object _lock = new();
    private readonly string _logDirectory;
    private DateTime _currentDate;
    private int _currentSequence;
    private string? _currentFilePath;
    private StreamWriter? _writer;
    private long _currentFileSize;
    private bool _isDisposed;

    public RollingFileSink(string? logDirectory = null)
    {
        _logDirectory = logDirectory ?? AppPaths.LogDirectory;

        try
        {
            Directory.CreateDirectory(_logDirectory);
            ApplyRetentionPolicy();
            OpenFileForDate(DateTime.UtcNow.Date);
        }
        catch
        {
            // Silently swallow initialization errors
        }
    }

    public void Emit(in LogEntry entry, string formatted)
    {
        if (_isDisposed)
        {
            return;
        }

        lock (_lock)
        {
            try
            {
                var todayUtc = DateTime.UtcNow.Date;
                if (todayUtc != _currentDate || _writer == null)
                {
                    OpenFileForDate(todayUtc);
                }

                if (_writer == null)
                {
                    return;
                }

                int byteCount = Utf8WithoutBom.GetByteCount(formatted) + Utf8WithoutBom.GetByteCount(Environment.NewLine);

                if (_currentFileSize + byteCount > MaxFileSizeBytes)
                {
                    RollFile();
                }

                if (_writer == null)
                {
                    return;
                }

                _writer.WriteLine(formatted);
                _writer.Flush();
                _currentFileSize += byteCount;
            }
            catch
            {
                // Silently swallow write failures
            }
        }
    }

    public void Flush()
    {
        lock (_lock)
        {
            try
            {
                _writer?.Flush();
            }
            catch
            {
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            try
            {
                _writer?.Flush();
                _writer?.Dispose();
                _writer = null;
            }
            catch
            {
            }
        }
    }

    private void OpenFileForDate(DateTime dateUtc)
    {
        try
        {
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;

            _currentDate = dateUtc;
            _currentSequence = FindNextAvailableSequence(dateUtc);
            _currentFilePath = GetFilePathForSequence(dateUtc, _currentSequence);

            var fileStream = new FileStream(
                _currentFilePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite);

            _currentFileSize = fileStream.Length;
            _writer = new StreamWriter(fileStream, Utf8WithoutBom);
        }
        catch
        {
            _writer = null;
        }
    }

    private void RollFile()
    {
        try
        {
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;

            _currentSequence++;
            _currentFilePath = GetFilePathForSequence(_currentDate, _currentSequence);

            ApplyRetentionPolicy();

            var fileStream = new FileStream(
                _currentFilePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite);

            _currentFileSize = fileStream.Length;
            _writer = new StreamWriter(fileStream, Utf8WithoutBom);
        }
        catch
        {
            _writer = null;
        }
    }

    private int FindNextAvailableSequence(DateTime dateUtc)
    {
        int sequence = 0;
        while (true)
        {
            string path = GetFilePathForSequence(dateUtc, sequence);
            if (!File.Exists(path))
            {
                return sequence;
            }

            try
            {
                var length = new FileInfo(path).Length;
                if (length < MaxFileSizeBytes)
                {
                    return sequence;
                }
            }
            catch
            {
                return sequence;
            }

            sequence++;
        }
    }

    private string GetFilePathForSequence(DateTime dateUtc, int sequence)
    {
        string baseName = AppPaths.LogFileName(dateUtc); // e.g. "wintangle-20260820.log"
        if (sequence == 0)
        {
            return Path.Combine(_logDirectory, baseName);
        }

        string prefix = Path.GetFileNameWithoutExtension(baseName);
        return Path.Combine(_logDirectory, $"{prefix}.{sequence}.log");
    }

    private void ApplyRetentionPolicy()
    {
        try
        {
            if (!Directory.Exists(_logDirectory))
            {
                return;
            }

            var logFiles = Directory.GetFiles(_logDirectory, "wintangle-*.log");
            if (logFiles.Length == 0)
            {
                return;
            }

            var fileInfos = new List<(string FilePath, DateTime LastWriteUtc)>(logFiles.Length);
            foreach (var file in logFiles)
            {
                try
                {
                    fileInfos.Add((file, File.GetLastWriteTimeUtc(file)));
                }
                catch
                {
                }
            }

            var toDelete = LogRetentionPolicy.GetFilesToDelete(fileInfos, DateTime.UtcNow);
            foreach (var file in toDelete)
            {
                if (string.Equals(file, _currentFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    File.Delete(file);
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }
}
