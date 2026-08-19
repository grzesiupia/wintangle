namespace Wintangle.Core.Hotkeys;

/// <summary>
/// Pure hotkey matching decision. No state, no Win32 — fully unit-testable.
/// </summary>
public static class HotkeyMatcherCore
{
    /// <summary>Auto-repeat window: a re-match of the same combo within this many ms is suppressed.</summary>
    public const int RepeatSuppressionMs = 250;

    /// <summary>
    /// Matches <paramref name="vk"/> + <paramref name="mods"/> against
    /// <paramref name="table"/>, suppressing repeats of the same combination
    /// that was handled within the last <see cref="RepeatSuppressionMs"/> ms.
    /// </summary>
    /// <param name="vk">Virtual key code of the pressed key.</param>
    /// <param name="mods">Modifier mask sampled at keypress time.</param>
    /// <param name="table">The hotkey table to match against.</param>
    /// <param name="lastMatch">Last handled combo (default = never matched).</param>
    /// <param name="nowUtc">Current time — supplied by the caller so the behavior is deterministic.</param>
    public static HotkeyMatchResult Match(
        byte vk,
        KeyModifiers mods,
        HotkeyTable table,
        LastHotkeyMatch lastMatch,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (!table.TryMatch(vk, mods, out var action))
        {
            return HotkeyMatchResult.NoMatch;
        }

        if (lastMatch != default
            && lastMatch.VirtualKey == vk
            && lastMatch.Modifiers == mods
            && (nowUtc - lastMatch.Utc).TotalMilliseconds < RepeatSuppressionMs)
        {
            return new HotkeyMatchResult(HotkeyMatchResultKind.RepeatSuppressed, action);
        }

        return new HotkeyMatchResult(HotkeyMatchResultKind.Matched, action);
    }
}
