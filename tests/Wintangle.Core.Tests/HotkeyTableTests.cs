using Wintangle.Core.Hotkeys;

namespace Wintangle.Core.Tests;

public class HotkeyTableTests
{
    private static readonly KeyModifiers CtrlWin = KeyModifiers.Ctrl | KeyModifiers.Win;
    private static readonly KeyModifiers WinAlt = KeyModifiers.Win | KeyModifiers.Alt;

    /// <summary>The exact default specification from the Phase 2 plan.</summary>
    public static readonly TheoryData<byte, KeyModifiers, HotkeyAction> DefaultBindings = new()
    {
        { 0x43, CtrlWin, HotkeyAction.CenterHalf },                        // C
        { 0x25, CtrlWin, HotkeyAction.HalfLeft },                          // Left
        { 0x27, CtrlWin, HotkeyAction.HalfRight },                         // Right
        { 0xDB, CtrlWin, HotkeyAction.QuarterTopLeft },                    // [
        { 0xDD, CtrlWin, HotkeyAction.QuarterTopRight },                   // ]
        { 0xBA, CtrlWin, HotkeyAction.QuarterBottomLeft },                 // ;
        { 0xDE, CtrlWin, HotkeyAction.QuarterBottomRight },                // '
        { 0xBC, CtrlWin, HotkeyAction.ThirdLeft },                         // ,
        { 0xBE, CtrlWin, HotkeyAction.ThirdCenter },                       // .
        { 0xBF, CtrlWin, HotkeyAction.ThirdRight },                        // /
        { 0x49, CtrlWin, HotkeyAction.SixthTopLeft },                      // I
        { 0x4F, CtrlWin, HotkeyAction.SixthTopCenter },                    // O
        { 0x50, CtrlWin, HotkeyAction.SixthTopRight },                     // P
        { 0x4A, CtrlWin, HotkeyAction.SixthBottomLeft },                   // J
        { 0x4B, CtrlWin, HotkeyAction.SixthBottomCenter },                 // K
        { 0x4C, CtrlWin, HotkeyAction.SixthBottomRight },                  // L
        { 0x25, WinAlt, HotkeyAction.PrevMonitor },                        // Win+Alt+Left
        { 0x27, WinAlt, HotkeyAction.NextMonitor },                        // Win+Alt+Right
    };

    [Theory]
    [MemberData(nameof(DefaultBindings))]
    public void DefaultTable_ContainsAllBindings(byte vk, KeyModifiers mods, HotkeyAction expected)
    {
        var table = DefaultHotkeys.Create();

        Assert.True(table.TryMatch(vk, mods, out var action), $"no match for VK 0x{vk:X2} + {mods}");
        Assert.Equal(expected, action);
    }

    [Fact]
    public void DefaultTable_HasExactly18Bindings()
    {
        Assert.Equal(18, DefaultHotkeys.Create().Count);
        Assert.Equal(18, Enum.GetValues<HotkeyAction>().Length);
    }

    [Fact]
    public void DefaultTable_NoDuplicateCombos()
    {
        // Construction itself would throw on duplicates; assert the singleton too.
        var table = DefaultHotkeys.Create();
        Assert.Equal(DefaultHotkeys.Entries.Select(e => e.Key).Distinct().Count(), table.Count);
    }

    [Theory]
    [InlineData(0x43)] // C with wrong modifiers
    [InlineData(0x25)] // Left with wrong modifiers
    [InlineData(0xDB)]
    [InlineData(0x49)]
    public void TryMatch_WrongModifiers_NoMatch(byte vk)
    {
        var table = DefaultHotkeys.Create();

        Assert.False(table.TryMatch(vk, KeyModifiers.None, out _));
        Assert.False(table.TryMatch(vk, KeyModifiers.Ctrl, out _));
        Assert.False(table.TryMatch(vk, KeyModifiers.Win, out _));
        Assert.False(table.TryMatch(vk, KeyModifiers.Ctrl | KeyModifiers.Shift, out _));
        Assert.False(table.TryMatch(vk, KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Win | KeyModifiers.Shift, out _));
    }

    [Theory]
    [InlineData(0x41)] // A
    [InlineData(0x01)] // mouse
    [InlineData(0x20)] // Space
    [InlineData(0x39)] // 9
    public void TryMatch_UnknownKey_NoMatch(byte vk)
    {
        var table = DefaultHotkeys.Create();

        Assert.False(table.TryMatch(vk, CtrlWin, out _));
        Assert.False(table.TryMatch(vk, WinAlt, out _));
    }

    [Fact]
    public void Constructor_RejectsDuplicateCombo()
    {
        var entries = new[]
        {
            new KeyValuePair<Hotkey, HotkeyAction>(new Hotkey(0x43, CtrlWin), HotkeyAction.CenterHalf),
            new KeyValuePair<Hotkey, HotkeyAction>(new Hotkey(0x43, CtrlWin), HotkeyAction.HalfLeft),
        };

        Assert.Throws<ArgumentException>(() => new HotkeyTable(entries));
    }

    [Fact]
    public void SameVirtualKey_DifferentModifiers_IsNotADuplicate()
    {
        // 0x25 with Ctrl+Win (HalfLeft) and Win+Alt (PrevMonitor) both exist.
        var table = DefaultHotkeys.Create();

        Assert.True(table.TryMatch(0x25, CtrlWin, out var left));
        Assert.True(table.TryMatch(0x25, WinAlt, out var prev));
        Assert.Equal(HotkeyAction.HalfLeft, left);
        Assert.Equal(HotkeyAction.PrevMonitor, prev);
    }

    [Fact]
    public void EmptyTable_NeverMatches()
    {
        Assert.False(HotkeyTable.Empty.TryMatch(0x43, CtrlWin, out _));
        Assert.Equal(0, HotkeyTable.Empty.Count);
    }

    [Fact]
    public void Hotkey_ValueEquality()
    {
        var a = new Hotkey(0x43, CtrlWin);
        var b = new Hotkey(0x43, CtrlWin);
        var c = new Hotkey(0x43, KeyModifiers.Ctrl);
        var d = new Hotkey(0x44, CtrlWin);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.NotEqual(a, d);
        Assert.True(a == b);
        Assert.True(a != c);
    }

    [Fact]
    public void Hotkey_HashCode_MatchesValueEquality()
    {
        var a = new Hotkey(0x43, CtrlWin);
        var b = new Hotkey(0x43, CtrlWin);

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Hotkey_UsableAsDictionaryKey()
    {
        var dict = new Dictionary<Hotkey, string>
        {
            [new Hotkey(0x43, CtrlWin)] = "center",
        };

        Assert.Equal("center", dict[new Hotkey(0x43, CtrlWin)]);
    }

    [Fact]
    public void FindHotkey_ReverseLookup_ReturnsDefaultBinding()
    {
        Assert.Equal(new Hotkey(0x43, CtrlWin), DefaultHotkeys.FindHotkey(HotkeyAction.CenterHalf));
        Assert.Equal(new Hotkey(0x25, WinAlt), DefaultHotkeys.FindHotkey(HotkeyAction.PrevMonitor));
        Assert.Equal(new Hotkey(0x27, WinAlt), DefaultHotkeys.FindHotkey(HotkeyAction.NextMonitor));
        Assert.Equal(new Hotkey(0x4C, CtrlWin), DefaultHotkeys.FindHotkey(HotkeyAction.SixthBottomRight));
    }

    [Fact]
    public void Labels_AreHumanReadable()
    {
        Assert.Equal("Ctrl+Win+C", DefaultHotkeys.Format(HotkeyAction.CenterHalf));
        Assert.Equal("Win+Alt+Left", DefaultHotkeys.Format(HotkeyAction.PrevMonitor));
        Assert.Equal("Ctrl+Win+[", DefaultHotkeys.Format(HotkeyAction.QuarterTopLeft));
        Assert.Equal("Ctrl+Win+;", DefaultHotkeys.Format(HotkeyAction.QuarterBottomLeft));
        Assert.Equal("Ctrl+Win+Right", DefaultHotkeys.Format(HotkeyAction.HalfRight));
    }

    [Fact]
    public void MatcherCore_ExactMatch_ReturnsMatched()
    {
        var result = HotkeyMatcherCore.Match(
            0x43, CtrlWin, DefaultHotkeys.Create(), default, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(HotkeyMatchResultKind.Matched, result.Kind);
        Assert.Equal(HotkeyAction.CenterHalf, result.Action);
        Assert.True(result.Handled);
    }

    [Fact]
    public void MatcherCore_NoMatch_ReturnsNoMatch()
    {
        var result = HotkeyMatcherCore.Match(
            0x41, CtrlWin, DefaultHotkeys.Create(), default, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(HotkeyMatchResultKind.NoMatch, result.Kind);
        Assert.False(result.Handled);
    }

    [Fact]
    public void MatcherCore_SameComboWithinWindow_IsSuppressed()
    {
        var table = DefaultHotkeys.Create();
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var last = new LastHotkeyMatch(t0, 0x43, CtrlWin);

        var result = HotkeyMatcherCore.Match(0x43, CtrlWin, table, last, t0.AddMilliseconds(100));

        Assert.Equal(HotkeyMatchResultKind.RepeatSuppressed, result.Kind);
        Assert.True(result.Handled);
    }

    [Fact]
    public void MatcherCore_AfterWindowExpires_MatchesAgain()
    {
        var table = DefaultHotkeys.Create();
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var last = new LastHotkeyMatch(t0, 0x43, CtrlWin);

        var result = HotkeyMatcherCore.Match(0x43, CtrlWin, table, last, t0.AddMilliseconds(251));

        Assert.Equal(HotkeyMatchResultKind.Matched, result.Kind);
    }

    [Fact]
    public void MatcherCore_DifferentCombo_IsNotSuppressedByPrevious()
    {
        var table = DefaultHotkeys.Create();
        var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var last = new LastHotkeyMatch(t0, 0x43, CtrlWin); // previous: Ctrl+Win+C

        // Different combo moments later must still dispatch (combo-insensitive suppression).
        var result = HotkeyMatcherCore.Match(0x49, CtrlWin, table, last, t0.AddMilliseconds(50));

        Assert.Equal(HotkeyMatchResultKind.Matched, result.Kind);
    }

    [Fact]
    public void SlotMapping_CoversAll16Slots_AndExcludesMonitorMoves()
    {
        foreach (var action in Enum.GetValues<HotkeyAction>())
        {
            if (action is HotkeyAction.PrevMonitor or HotkeyAction.NextMonitor)
            {
                Assert.Null(action.ToSlotLayout());
            }
            else
            {
                Assert.NotNull(action.ToSlotLayout());
            }
        }
    }
}
