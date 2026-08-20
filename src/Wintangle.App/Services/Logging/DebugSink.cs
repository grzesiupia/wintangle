using System.Diagnostics;
using System.Runtime.InteropServices;
using Wintangle.Core.Logging;

namespace Wintangle.App.Services.Logging;

/// <summary>
/// Emits log entries to <see cref="Debug.WriteLine(string?)"/> and Windows <c>OutputDebugStringW</c>.
/// </summary>
internal sealed class DebugSink : ILogSink
{
    [DllImport("kernel32.dll", EntryPoint = "OutputDebugStringW", CharSet = CharSet.Unicode, SetLastError = false)]
    private static extern void OutputDebugString(string lpOutputString);

    public void Emit(in LogEntry entry, string formatted)
    {
        try
        {
            Debug.WriteLine(formatted);

            if (OperatingSystem.IsWindows())
            {
                OutputDebugString(formatted + "\n");
            }
        }
        catch
        {
            // Silently swallow errors
        }
    }

    public void Flush()
    {
    }

    public void Dispose()
    {
    }
}
