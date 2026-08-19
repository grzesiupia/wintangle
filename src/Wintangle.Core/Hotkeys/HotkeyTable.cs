namespace Wintangle.Core.Hotkeys;

/// <summary>
/// Immutable lookup table mapping hotkey combinations to actions.
/// Duplicate combinations are rejected at construction time.
/// </summary>
/// <remarks>
/// Instances are immutable and safe to share across threads; a live
/// reconfiguration swaps the whole instance (the hook reads the current
/// reference per keypress via an interlocked field).
/// </remarks>
public sealed class HotkeyTable
{
    public static readonly HotkeyTable Empty = new(Array.Empty<KeyValuePair<Hotkey, HotkeyAction>>());

    private readonly IReadOnlyDictionary<Hotkey, HotkeyAction> _map;

    public HotkeyTable(IEnumerable<KeyValuePair<Hotkey, HotkeyAction>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var map = new Dictionary<Hotkey, HotkeyAction>();
        foreach (var entry in entries)
        {
            if (!map.TryAdd(entry.Key, entry.Value))
            {
                throw new ArgumentException(
                    $"Duplicate hotkey combination: {entry.Key} (already mapped to {map[entry.Key]}).",
                    nameof(entries));
            }
        }

        _map = map;
    }

    /// <summary>Number of entries in the table.</summary>
    public int Count => _map.Count;

    /// <summary>
    /// Exact match: <paramref name="vk"/> plus <paramref name="mods"/> must
    /// equal an entry's combination bit-for-bit (no partial modifier matching).
    /// </summary>
    public bool TryMatch(byte vk, KeyModifiers mods, out HotkeyAction action) =>
        _map.TryGetValue(new Hotkey(vk, mods), out action);

    /// <summary>True if the table contains <paramref name="hotkey"/>.</summary>
    public bool Contains(Hotkey hotkey) => _map.ContainsKey(hotkey);
}
