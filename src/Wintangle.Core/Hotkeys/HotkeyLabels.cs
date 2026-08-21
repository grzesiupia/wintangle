using System.Text;

namespace Wintangle.Core.Hotkeys;

/// <summary>
/// Human-readable hotkey labels ("Ctrl+Win+C") for menus and settings UI.
/// </summary>
public static class HotkeyLabels
{
    public static string Format(Hotkey hotkey) => Format(hotkey.Modifiers, hotkey.VirtualKey);

    public static string Format(KeyModifiers mods, byte vk)
    {
        var parts = new List<string>(4);
        if ((mods & KeyModifiers.Ctrl) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((mods & KeyModifiers.Win) != 0)
        {
            parts.Add("Win");
        }

        if ((mods & KeyModifiers.Alt) != 0)
        {
            parts.Add("Alt");
        }

        if ((mods & KeyModifiers.Shift) != 0)
        {
            parts.Add("Shift");
        }

        parts.Add(KeyName(vk));
        return string.Join("+", parts);
    }

    /// <summary>
    /// The keycap parts of a hotkey, e.g. ["Ctrl", "Win", "←"] for Ctrl+Win+Left.
    /// Used by the settings UI to render one chip per part. Unlike
    /// <see cref="Format(Hotkey)"/> (which keeps the "Ctrl+Win+C" tray string),
    /// the key uses display glyphs: arrows become "←"/"→"/"↑"/"↓",
    /// PageUp→"PgUp", PageDown→"PgDn", Delete→"Del", Space→"Space", Home/End
    /// stay "Home"/"End", and printable keys are uppercased.
    /// </summary>
    public static IReadOnlyList<string> KeycapParts(Hotkey hotkey) => KeycapParts(hotkey.Modifiers, hotkey.VirtualKey);

    /// <inheritdoc cref="KeycapParts(Hotkey)"/>
    public static IReadOnlyList<string> KeycapParts(KeyModifiers mods, byte vk)
    {
        var parts = new List<string>(4);
        if ((mods & KeyModifiers.Ctrl) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((mods & KeyModifiers.Win) != 0)
        {
            parts.Add("Win");
        }

        if ((mods & KeyModifiers.Alt) != 0)
        {
            parts.Add("Alt");
        }

        if ((mods & KeyModifiers.Shift) != 0)
        {
            parts.Add("Shift");
        }

        parts.Add(KeyGlyph(vk));
        return parts;
    }

    /// <summary>The display glyph for a virtual key in a keycap chip.</summary>
    private static string KeyGlyph(byte vk) => vk switch
    {
        0x21 => "PgUp",   // VK_PRIOR
        0x22 => "PgDn",   // VK_NEXT
        0x23 => "Home",
        0x24 => "End",
        0x25 => "←",      // VK_LEFT
        0x26 => "↑",      // VK_UP
        0x27 => "→",      // VK_RIGHT
        0x28 => "↓",      // VK_DOWN
        0x0D => "Enter",  // VK_RETURN
        0x2E => "Del",    // VK_DELETE
        0x20 => "Space",  // VK_SPACE
        _ => PrintableKey(vk),
    };

    /// <summary>Printable ASCII virtual keys render as their (uppercased) char.</summary>
    private static string PrintableKey(byte vk)
    {
        if (vk is >= 0x20 and <= 0x7E)
        {
            var ch = (char)vk;
            return char.IsLetter(ch) ? char.ToUpperInvariant(ch).ToString() : ch.ToString();
        }

        return KeyName(vk);
    }

    private static string KeyName(byte vk) => vk switch
    {
        (byte)'A' or (byte)'B' or (byte)'C' or (byte)'D' or (byte)'E' or (byte)'F' or (byte)'G' or
        (byte)'H' or (byte)'I' or (byte)'J' or (byte)'K' or (byte)'L' or (byte)'M' or (byte)'N' or
        (byte)'O' or (byte)'P' or (byte)'Q' or (byte)'R' or (byte)'S' or (byte)'T' or (byte)'U' or
        (byte)'V' or (byte)'W' or (byte)'X' or (byte)'Y' or (byte)'Z'
            => ((char)vk).ToString(),

        (byte)'0' or (byte)'1' or (byte)'2' or (byte)'3' or (byte)'4' or (byte)'5' or
        (byte)'6' or (byte)'7' or (byte)'8' or (byte)'9'
            => ((char)vk).ToString(),

        0x25 => "Left",
        0x26 => "Up",
        0x27 => "Right",
        0x28 => "Down",
        0x0D => "Enter",
        0xBA => ";",
        0xBB => "=",
        0xBC => ",",
        0xBD => "-",
        0xBE => ".",
        0xBF => "/",
        0xC0 => "`",
        0xDB => "[",
        0xDC => "\\",
        0xDD => "]",
        0xDE => "'",
        _ => $"VK(0x{vk:X2})",
    };
}
