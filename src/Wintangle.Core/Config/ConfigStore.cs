using System.Text.Json;
using Wintangle.Core.Geometry;
using Wintangle.Core.Hotkeys;

namespace Wintangle.Core.Config;

/// <summary>
/// Loads and saves <see cref="ConfigModel"/> as camelCase, indented JSON.
/// Pure file I/O — no Win32 — so it is fully testable on any platform.
/// </summary>
/// <remarks>
/// <para>Load never throws: a missing or corrupt file yields the defaults and
/// the file is (re)written so the app always keeps a valid config on disk.</para>
/// <para>Save is atomic-ish: write <c>path + ".tmp"</c> then rename over the
/// target, so a crash mid-write never leaves a half-written config.</para>
/// </remarks>
public static class ConfigStore
{
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary>The default configuration (first run / corrupt config).</summary>
    public static ConfigModel Default() => new();

    /// <summary>
    /// Loads the config at <paramref name="path"/>. Never throws.
    /// Missing or corrupt file → defaults and the file is created/rewritten.
    /// A null path is treated as "no config" → defaults.
    /// </summary>
    /// <remarks>
    /// Fields are sanitized individually: a single bad field (overflow number,
    /// unknown enum string, null element, wrong type) defaults or drops that
    /// field only — it never resets the rest of the config. Only file-level
    /// invalid JSON syntax falls back to full defaults.
    /// </remarks>
    public static ConfigModel Load(string path)
    {
        // "Never throws" contract: a null path means there is no config to
        // load, so the defaults apply.
        if (path is null)
        {
            return Default();
        }

        ConfigModel result;
        bool rewrite = false;

        try
        {
            if (!File.Exists(path))
            {
                result = Default();
                rewrite = true;
            }
            else
            {
                var json = File.ReadAllText(path);
                result = DeserializeLenient(json);
            }
        }
        catch (Exception)
        {
            // File-level corrupt (invalid JSON syntax) or an unreadable file:
            // fall back to defaults and rewrite so the app keeps a valid
            // config. Bad individual fields never reach this point.
            result = Default();
            rewrite = true;
        }

        if (rewrite)
        {
            try
            {
                Save(path, result);
            }
            catch (Exception)
            {
                // Never throw out of Load; a failed rewrite is logged by the
                // caller and the in-memory defaults still apply.
            }
        }

        return result;
    }

    /// <summary>
    /// Parses config JSON tolerantly. Each field is read best-effort: an
    /// overflow number, unknown enum string, null element, or wrong type
    /// defaults/skips that field only. A non-object root or invalid JSON
    /// syntax throws (caller falls back to full defaults).
    /// </summary>
    private static ConfigModel DeserializeLenient(string json)
    {
        using var doc = JsonDocument.Parse(json);

        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Config root must be a JSON object.");
        }

        var windowGap = ConfigModel.DefaultWindowGap;
        if (TryReadIntProperty(root, "windowGap", out var parsedWindowGap))
        {
            windowGap = parsedWindowGap;
        }

        var edgeGap = ConfigModel.DefaultEdgeGap;
        if (TryReadIntProperty(root, "edgeGap", out var parsedEdgeGap))
        {
            edgeGap = parsedEdgeGap;
        }

        var autoStart = TryGetPropertyCaseInsensitive(root, "autoStart", out var autoStartEl)
            && autoStartEl.ValueKind == JsonValueKind.True;

        // Theme: only a string is read; null/wrong-type is ignored → default.
        // Normalized here so Load output is always normalized (Sanitize's
        // NormalizeTheme stays as an idempotent safety net).
        var theme = ConfigModel.DefaultTheme;
        if (TryGetPropertyCaseInsensitive(root, "theme", out var themeEl)
            && themeEl.ValueKind == JsonValueKind.String
            && themeEl.GetString() is { } themeStr)
        {
            theme = NormalizeTheme(themeStr);
        }

        var shortcuts = new List<ShortcutBinding>();
        if (TryGetPropertyCaseInsensitive(root, "shortcuts", out var shortcutsEl)
            && shortcutsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in shortcutsEl.EnumerateArray())
            {
                if (TryReadShortcut(element, out var shortcut))
                {
                    shortcuts.Add(shortcut);
                }
            }
        }

        var ignoredApps = new List<string>();
        if (TryGetPropertyCaseInsensitive(root, "ignoredApps", out var ignoredEl)
            && ignoredEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in ignoredEl.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String && element.GetString() is { } name)
                {
                    ignoredApps.Add(name);
                }
            }
        }

        return Sanitize(new ConfigModel
        {
            WindowGap = windowGap,
            EdgeGap = edgeGap,
            AutoStart = autoStart,
            Theme = theme,
            Shortcuts = shortcuts,
            IgnoredApps = ignoredApps,
        });
    }

    /// <summary>
    /// Normalizes a theme key: case-insensitive "Dark"/"Light"; anything else
    /// (null, whitespace, unknown values) maps to the default dark theme.
    /// </summary>
    public static string NormalizeTheme(string? theme)
    {
        if (string.IsNullOrWhiteSpace(theme))
        {
            return ConfigModel.ThemeDark;
        }

        return string.Equals(theme.Trim(), ConfigModel.ThemeLight, StringComparison.OrdinalIgnoreCase)
            ? ConfigModel.ThemeLight
            : ConfigModel.ThemeDark;
    }

    /// <summary>Case-insensitive property lookup (mirrors the old serializer's <c>PropertyNameCaseInsensitive</c>).</summary>
    private static bool TryGetPropertyCaseInsensitive(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Reads an int property. Overflowing numbers (e.g. an int-precision
    /// overflow), non-integers, and wrong-typed values report failure so the
    /// caller can fall back to the field default.
    /// </summary>
    private static bool TryReadIntProperty(JsonElement root, string propertyName, out int value)
    {
        value = default;
        return TryGetPropertyCaseInsensitive(root, propertyName, out var element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetInt32(out value);
    }

    /// <summary>
    /// Parses one shortcut array element. Missing properties keep the
    /// serializer-equivalent defaults (action 0, key 0, modifiers None); a
    /// malformed property (unknown action string, overflow number, wrong type,
    /// null element) drops the entry.
    /// </summary>
    private static bool TryReadShortcut(JsonElement element, out ShortcutBinding shortcut)
    {
        shortcut = null!; // caller only uses it when this returns true
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false; // null / non-object element → drop entry
        }

        var action = HotkeyAction.CenterHalf;
        if (TryGetPropertyCaseInsensitive(element, "action", out var actionEl)
            && !TryReadAction(actionEl, out action))
        {
            return false; // unknown action string / overflow number → drop entry
        }

        var virtualKey = (byte)0;
        if (TryGetPropertyCaseInsensitive(element, "virtualKey", out var vkEl)
            && !TryReadByte(vkEl, out virtualKey))
        {
            return false; // malformed virtual key → drop entry
        }

        var modifiers = KeyModifiers.None;
        if (TryGetPropertyCaseInsensitive(element, "modifiers", out var modEl)
            && !TryReadModifiers(modEl, out modifiers))
        {
            return false; // malformed modifiers → drop entry
        }

        shortcut = new ShortcutBinding(action, virtualKey, modifiers);
        return true;
    }

    private static bool TryReadAction(JsonElement element, out HotkeyAction action)
    {
        action = default;
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                // Undefined numbers (e.g. 999) still parse and are dropped
                // later by Sanitize (Enum.IsDefined); only non-integer or
                // overflowing numbers fail here.
                if (!element.TryGetInt32(out var numeric))
                {
                    return false;
                }
                action = (HotkeyAction)numeric;
                return true;

            case JsonValueKind.String:
                return Enum.TryParse(element.GetString(), ignoreCase: true, out action);

            default:
                return false;
        }
    }

    private static bool TryReadByte(JsonElement element, out byte value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Number && element.TryGetByte(out value);
    }

    private static bool TryReadModifiers(JsonElement element, out KeyModifiers modifiers)
    {
        modifiers = KeyModifiers.None;
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                // Undefined flag bits are dropped later by Sanitize; only
                // non-integer or overflowing numbers fail here.
                if (!element.TryGetInt32(out var numeric))
                {
                    return false;
                }
                modifiers = (KeyModifiers)numeric;
                return true;

            case JsonValueKind.String:
                return Enum.TryParse(element.GetString(), ignoreCase: true, out modifiers);

            default:
                return false;
        }
    }

    /// <summary>
    /// Validates and normalizes a parsed config: gap values out of [0, 50]
    /// fall back to the default; unknown actions, zero virtual keys, and
    /// invalid modifier bits are dropped; duplicate combos keep the first
    /// entry and a single binding per action is enforced (first wins);
    /// ignored apps are trimmed, lowercased, and de-duplicated.
    /// </summary>
    public static ConfigModel Sanitize(ConfigModel? raw)
    {
        if (raw == null)
        {
            return Default();
        }

        var windowGap = raw.WindowGap is >= 0 and <= GapSettings.MaxGap
            ? raw.WindowGap
            : ConfigModel.DefaultWindowGap;

        var edgeGap = raw.EdgeGap is >= 0 and <= GapSettings.MaxGap
            ? raw.EdgeGap
            : ConfigModel.DefaultEdgeGap;

        var shortcuts = new List<ShortcutBinding>();
        var seen = new HashSet<Hotkey>();
        var seenActions = new HashSet<HotkeyAction>();
        foreach (var shortcut in raw.Shortcuts ?? Enumerable.Empty<ShortcutBinding>())
        {
            if (shortcut == null)
            {
                continue;
            }

            if (!Enum.IsDefined(shortcut.Action))
            {
                continue;
            }

            if (shortcut.VirtualKey == 0)
            {
                continue;
            }

            // Modifiers must be a subset of None|Ctrl|Alt|Win|Shift.
            if (((int)shortcut.Modifiers & ~(int)(KeyModifiers.Ctrl | KeyModifiers.Alt | KeyModifiers.Win | KeyModifiers.Shift)) != 0)
            {
                continue;
            }

            if (!seenActions.Add(shortcut.Action))
            {
                continue; // duplicate action — first wins
            }

            if (!seen.Add(new Hotkey(shortcut.VirtualKey, shortcut.Modifiers)))
            {
                continue; // duplicate combo — first wins
            }

            shortcuts.Add(shortcut);
        }

        var ignored = (raw.IgnoredApps ?? Enumerable.Empty<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return raw with
        {
            Version = ConfigModel.CurrentVersion,
            WindowGap = windowGap,
            EdgeGap = edgeGap,
            Theme = NormalizeTheme(raw.Theme),
            Shortcuts = shortcuts,
            IgnoredApps = ignored,
        };
    }

    /// <summary>
    /// Saves <paramref name="config"/> to <paramref name="path"/> atomically-ish:
    /// writes <c>path + ".tmp"</c>, then renames over the target.
    /// </summary>
    public static void Save(string path, ConfigModel config)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(config);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(config, s_options));
        File.Move(tmp, path, overwrite: true);
    }
}
