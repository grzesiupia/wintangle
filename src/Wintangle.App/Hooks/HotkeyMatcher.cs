using System.Threading;
using Wintangle.Core.Hotkeys;

namespace Wintangle.App.Hooks;

/// <summary>
/// Thread-confined hotkey matcher: holds the current immutable table
/// (swapped atomically for a future live reconfig) and the last-handled
/// combo state (used for auto-repeat suppression).
/// Only touched from the keyboard hook thread.
/// </summary>
internal sealed class HotkeyMatcher
{
    private HotkeyTable _table;
    private LastHotkeyMatch _lastMatch;

    public HotkeyMatcher(HotkeyTable table)
    {
        _table = table ?? throw new ArgumentNullException(nameof(table));
    }

    /// <summary>
    /// Current table. Safe to swap from any thread; the matcher reads the
    /// latest snapshot on every keypress (no locking on the hot path).
    /// </summary>
    public HotkeyTable Table
    {
        get => Volatile.Read(ref _table);
        set => Interlocked.Exchange(ref _table, value ?? HotkeyTable.Empty);
    }

    /// <summary>
    /// Matches a keypress and updates the auto-repeat suppression state.
    /// </summary>
    public HotkeyMatchResult Process(byte vk, KeyModifiers mods)
    {
        var result = HotkeyMatcherCore.Match(vk, mods, Volatile.Read(ref _table), _lastMatch, DateTime.UtcNow);
        if (result.Kind == HotkeyMatchResultKind.Matched)
        {
            _lastMatch = new LastHotkeyMatch(DateTime.UtcNow, vk, mods);
        }

        return result;
    }
}
