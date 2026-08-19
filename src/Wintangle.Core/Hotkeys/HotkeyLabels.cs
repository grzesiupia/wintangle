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
