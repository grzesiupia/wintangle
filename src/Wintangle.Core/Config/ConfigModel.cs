using Wintangle.Core.Hotkeys;

namespace Wintangle.Core.Config;

/// <summary>
/// One user-configured hotkey: an action plus the key combination bound to it.
/// Serialized directly to JSON (camelCase), so numeric enum values are
/// persisted for actions and modifiers.
/// </summary>
public sealed record ShortcutBinding(HotkeyAction Action, byte VirtualKey, KeyModifiers Modifiers);

/// <summary>
/// The persisted wintangle configuration. Immutable after construction
/// (records with init-only properties); live changes swap a whole new
/// instance via <c>with</c>.
/// </summary>
/// <remarks>
/// Defaults match the Phase 2 defaults: 8px window gap, no edge gap, no
/// autostart, no custom shortcuts (meaning the default table is used) and no
/// ignored apps.
/// </remarks>
public sealed record ConfigModel
{
    /// <summary>Current config schema version.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Dark theme key (the default).</summary>
    public const string ThemeDark = "Dark";

    /// <summary>Light theme key.</summary>
    public const string ThemeLight = "Light";

    /// <summary>System theme key (follows Windows app theme).</summary>
    public const string ThemeSystem = "System";

    /// <summary>Default theme — dark.</summary>
    public const string DefaultTheme = ThemeDark;

    /// <summary>Default window gap (px), also used when the config value is out of range.</summary>
    public const int DefaultWindowGap = 8;

    /// <summary>Default edge gap (px), also used when the config value is out of range.</summary>
    public const int DefaultEdgeGap = 0;

    public int Version { get; init; } = CurrentVersion;

    public int WindowGap { get; init; } = DefaultWindowGap;

    public int EdgeGap { get; init; } = DefaultEdgeGap;

    public bool AutoStart { get; init; }

    /// <summary>
    /// Theme key ("Dark" or "Light"); unknown values normalize to the default.
    /// </summary>
    public string Theme { get; init; } = DefaultTheme;

    /// <summary>
    /// User hotkey bindings. Empty means "use the default table".
    /// </summary>
    public List<ShortcutBinding> Shortcuts { get; init; } = new();

    /// <summary>
    /// Ignored process names, lowercase with the ".exe" suffix
    /// (e.g. "notepad.exe"). Runtime matching normalizes these.
    /// </summary>
    public List<string> IgnoredApps { get; init; } = new();
}
