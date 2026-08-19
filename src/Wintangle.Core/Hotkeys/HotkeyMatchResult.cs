namespace Wintangle.Core.Hotkeys;

/// <summary>Outcome of a hotkey match attempt.</summary>
public enum HotkeyMatchResultKind
{
    /// <summary>The key combination is not in the table.</summary>
    NoMatch,

    /// <summary>The combination matched and should be dispatched.</summary>
    Matched,

    /// <summary>
    /// The combination matched but the same combo was handled moments ago
    /// (keyboard auto-repeat / held key) — swallow without re-dispatching.
    /// </summary>
    RepeatSuppressed,
}

/// <summary>Result of <see cref="HotkeyMatcherCore.Match"/>.</summary>
public readonly record struct HotkeyMatchResult(HotkeyMatchResultKind Kind, HotkeyAction Action)
{
    public static HotkeyMatchResult NoMatch { get; } = new(HotkeyMatchResultKind.NoMatch, default);

    /// <summary>True when the key should be swallowed (matched or suppressed).</summary>
    public bool Handled => Kind != HotkeyMatchResultKind.NoMatch;
}

/// <summary>
/// The previous handled match, used to suppress keyboard auto-repeat.
/// A <see cref="DateTime"/> of <see cref="DateTime.MinValue"/> (default)
/// means "never matched".
/// </summary>
public readonly record struct LastHotkeyMatch(DateTime Utc, byte VirtualKey, KeyModifiers Modifiers);
