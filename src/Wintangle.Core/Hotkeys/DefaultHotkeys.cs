namespace Wintangle.Core.Hotkeys;

/// <summary>
/// The default hotkey table — this is the specification for the default
/// keybindings. All 17 slot actions use Ctrl+Win; monitor moves use Win+Alt.
/// </summary>
public static class DefaultHotkeys
{
    /// <summary>The default table (a singleton instance; immutable).</summary>
    public static HotkeyTable Table { get; } = Create();

    /// <summary>All default entries, in menu/display order.</summary>
    public static IReadOnlyList<KeyValuePair<Hotkey, HotkeyAction>> Entries { get; } = BuildEntries();

    private static readonly IReadOnlyDictionary<HotkeyAction, Hotkey> s_byAction = BuildActionLookup();

    /// <summary>Builds a fresh default table (used by tests).</summary>
    public static HotkeyTable Create() => new(BuildEntries());

    /// <summary>Reverse lookup: the hotkey bound to <paramref name="action"/>, if any.</summary>
    public static Hotkey? FindHotkey(HotkeyAction action) =>
        s_byAction.TryGetValue(action, out var hotkey) ? hotkey : null;

    /// <summary>Human-readable combo label, e.g. "Ctrl+Win+C".</summary>
    public static string Format(Hotkey hotkey) => HotkeyLabels.Format(hotkey);

    /// <summary>Human-readable combo label for an action's default binding.</summary>
    public static string Format(HotkeyAction action)
    {
        var hotkey = FindHotkey(action);
        return hotkey is { } h ? HotkeyLabels.Format(h) : string.Empty;
    }

    private static IReadOnlyDictionary<HotkeyAction, Hotkey> BuildActionLookup()
    {
        var map = new Dictionary<HotkeyAction, Hotkey>();
        foreach (var entry in Entries)
        {
            map[entry.Value] = entry.Key;
        }

        return map;
    }

    private static List<KeyValuePair<Hotkey, HotkeyAction>> BuildEntries()
    {
        var ctrlWin = KeyModifiers.Ctrl | KeyModifiers.Win;
        var winAlt = KeyModifiers.Win | KeyModifiers.Alt;

        return new List<KeyValuePair<Hotkey, HotkeyAction>>
        {
            new(new Hotkey(VirtualKey.VK_C, ctrlWin), HotkeyAction.CenterHalf),

            new(new Hotkey(VirtualKey.VK_LEFT, ctrlWin), HotkeyAction.HalfLeft),
            new(new Hotkey(VirtualKey.VK_RIGHT, ctrlWin), HotkeyAction.HalfRight),

            new(new Hotkey(VirtualKey.VK_OEM_4, ctrlWin), HotkeyAction.QuarterTopLeft),
            new(new Hotkey(VirtualKey.VK_OEM_6, ctrlWin), HotkeyAction.QuarterTopRight),
            new(new Hotkey(VirtualKey.VK_OEM_1, ctrlWin), HotkeyAction.QuarterBottomLeft),
            new(new Hotkey(VirtualKey.VK_OEM_7, ctrlWin), HotkeyAction.QuarterBottomRight),

            new(new Hotkey(VirtualKey.VK_OEM_COMMA, ctrlWin), HotkeyAction.ThirdLeft),
            new(new Hotkey(VirtualKey.VK_OEM_PERIOD, ctrlWin), HotkeyAction.ThirdCenter),
            new(new Hotkey(VirtualKey.VK_OEM_2, ctrlWin), HotkeyAction.ThirdRight),

            new(new Hotkey(VirtualKey.VK_I, ctrlWin), HotkeyAction.SixthTopLeft),
            new(new Hotkey(VirtualKey.VK_O, ctrlWin), HotkeyAction.SixthTopCenter),
            new(new Hotkey(VirtualKey.VK_P, ctrlWin), HotkeyAction.SixthTopRight),

            new(new Hotkey(VirtualKey.VK_J, ctrlWin), HotkeyAction.SixthBottomLeft),
            new(new Hotkey(VirtualKey.VK_K, ctrlWin), HotkeyAction.SixthBottomCenter),
            new(new Hotkey(VirtualKey.VK_L, ctrlWin), HotkeyAction.SixthBottomRight),

            new(new Hotkey(VirtualKey.VK_RETURN, ctrlWin), HotkeyAction.Fullscreen),

            new(new Hotkey(VirtualKey.VK_LEFT, winAlt), HotkeyAction.PrevMonitor),
            new(new Hotkey(VirtualKey.VK_RIGHT, winAlt), HotkeyAction.NextMonitor),
        };
    }
}
