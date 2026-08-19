namespace Wintangle.Core.Hotkeys;

/// <summary>
/// A single hotkey: a virtual key code plus a modifier mask.
/// Value-typed equality (record struct) so it can be used as a dictionary key.
/// </summary>
public readonly record struct Hotkey(byte VirtualKey, KeyModifiers Modifiers);
