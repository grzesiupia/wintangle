using System.Collections.Immutable;
using System.Threading;
using Wintangle.Core.Config;
using Wintangle.Core.Geometry;

namespace Wintangle.App;

/// <summary>
/// Small shared runtime state used by the tray menu (UI thread), the dispatch
/// pipeline (hook thread) and the config service (watcher thread).
/// Core-independent — no Win32 in here.
/// </summary>
/// <remarks>
/// Both snapshots (<see cref="Gaps"/>, <see cref="Ignored"/>) are immutable
/// and swapped atomically via <see cref="Interlocked"/>: readers take a local
/// snapshot once per action and never hold locks on the hot path.
/// </remarks>
internal sealed class RuntimeState
{
    private GapSettings _gaps = new(ConfigModel.DefaultWindowGap, ConfigModel.DefaultEdgeGap);

    private ImmutableHashSet<string> _ignored = ImmutableHashSet<string>.Empty
        .WithComparer(StringComparer.OrdinalIgnoreCase);

    /// <summary>Current gap snapshot (atomic read).</summary>
    public GapSettings Gaps => Volatile.Read(ref _gaps);

    /// <summary>Current ignored-process set (atomic read, case-insensitive).</summary>
    public ImmutableHashSet<string> Ignored => Volatile.Read(ref _ignored);

    /// <summary>Atomically swaps the gap snapshot.</summary>
    public void UpdateGaps(GapSettings gaps)
    {
        ArgumentNullException.ThrowIfNull(gaps);
        Interlocked.Exchange(ref _gaps, gaps);
    }

    /// <summary>Atomically swaps the ignored-process set.</summary>
    public void UpdateIgnored(ImmutableHashSet<string> ignored)
    {
        ArgumentNullException.ThrowIfNull(ignored);
        Interlocked.Exchange(ref _ignored, ignored);
    }

    /// <summary>
    /// Normalizes a process name for the ignored set: strips a trailing
    /// ".exe" and lowercases ("notepad.exe" → "notepad").
    /// </summary>
    public static string NormalizeProcessName(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return string.Empty;
        }

        return processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4].ToLowerInvariant()
            : processName.ToLowerInvariant();
    }

    public bool IsIgnored(string processName)
    {
        var key = NormalizeProcessName(processName);
        return key.Length > 0 && Volatile.Read(ref _ignored).Contains(key);
    }
}
