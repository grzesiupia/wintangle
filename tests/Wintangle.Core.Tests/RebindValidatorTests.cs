using Wintangle.Core.Hotkeys;

namespace Wintangle.Core.Tests;

public class RebindValidatorTests
{
    private static readonly KeyModifiers CtrlWin = KeyModifiers.Ctrl | KeyModifiers.Win;

    [Fact]
    public void EmptyCombo_IsInvalid()
    {
        Assert.NotNull(RebindValidator.Validate(new Hotkey(0, KeyModifiers.None)));
    }

    [Fact]
    public void EmptyKey_WithModifiers_IsInvalid()
    {
        Assert.NotNull(RebindValidator.Validate(new Hotkey(0, CtrlWin)));
    }

    [Fact]
    public void BareKey_WithoutModifier_IsInvalid()
    {
        // A with no modifiers — would swallow typing globally if bound.
        Assert.NotNull(RebindValidator.Validate(new Hotkey(0x41, KeyModifiers.None)));
    }

    [Fact]
    public void Escape_WithoutModifiers_IsAllowedAsCancelSignal()
    {
        Assert.Null(RebindValidator.Validate(new Hotkey(RebindValidator.VK_ESCAPE, KeyModifiers.None)));
        Assert.True(RebindValidator.IsCancel(new Hotkey(RebindValidator.VK_ESCAPE, KeyModifiers.None)));
    }

    [Fact]
    public void ModifiedKey_IsValid()
    {
        Assert.Null(RebindValidator.Validate(new Hotkey(0x43, CtrlWin))); // Ctrl+Win+C
        Assert.Null(RebindValidator.Validate(new Hotkey(0x25, KeyModifiers.Win | KeyModifiers.Alt))); // Win+Alt+Left
        Assert.Null(RebindValidator.Validate(new Hotkey(0x41, KeyModifiers.Shift))); // Shift+A
    }

    [Fact]
    public void InvalidModifierBits_AreRejected()
    {
        Assert.NotNull(RebindValidator.Validate(new Hotkey(0x43, (KeyModifiers)0x10)));
        Assert.NotNull(RebindValidator.Validate(new Hotkey(0x43, (KeyModifiers)0x100)));
    }

    [Theory]
    [InlineData(0x10)] // Shift
    [InlineData(0x11)] // Ctrl
    [InlineData(0x12)] // Alt
    [InlineData(0x5B)] // LWin
    [InlineData(0x5C)] // RWin
    [InlineData(0xA0)] // LShift
    [InlineData(0xA1)] // RShift
    [InlineData(0xA2)] // LCtrl
    [InlineData(0xA3)] // RCtrl
    [InlineData(0xA4)] // LAlt
    [InlineData(0xA5)] // RAlt
    public void ModifierKeys_AreDetected(byte vk)
    {
        Assert.True(RebindValidator.IsModifierKey(vk));
    }

    [Theory]
    [InlineData(0x41)] // A
    [InlineData(0x43)] // C
    [InlineData(0x25)] // Left
    [InlineData(0x1B)] // Escape
    public void NonModifierKeys_AreNotModifierKeys(byte vk)
    {
        Assert.False(RebindValidator.IsModifierKey(vk));
    }

    [Fact]
    public void CtrlEscape_IsNotCancel()
    {
        Assert.False(RebindValidator.IsCancel(new Hotkey(RebindValidator.VK_ESCAPE, KeyModifiers.Ctrl)));
        Assert.Null(RebindValidator.Validate(new Hotkey(RebindValidator.VK_ESCAPE, KeyModifiers.Ctrl)));
    }
}
