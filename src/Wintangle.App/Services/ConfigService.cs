using System.Collections.Immutable;
using System.IO;
using Wintangle.App.Hooks;
using Wintangle.Core.Config;
using Wintangle.Core.Geometry;
using Wintangle.Core.Hotkeys;

namespace Wintangle.App.Services;

/// <summary>
/// Owns the config lifecycle: loads at startup, watches the config file for
/// external edits (hand-editing), persists tray-driven changes, and applies
/// everything to <see cref="RuntimeState"/> / the hotkey table live — no
/// restart, no hook reinstall.
/// </summary>
/// <remarks>
/// <para>Threading: the watcher event fires on a threadpool thread and is
/// debounced (250ms) before reloading. Reload only swaps immutable snapshots
/// via Interlocked, so it can never throw into the hook thread. All mutations
/// of the in-memory model are serialized under <see cref="_lock"/>.</para>
/// <para>The hook table is swapped through <see cref="KeyboardHook.Table"/>,
/// which does <see cref="Interlocked.Exchange"/> into the exact field the hook
/// proc reads on every keypress (<c>HotkeyMatcher._table</c>).</para>
/// </remarks>
internal sealed class ConfigService : IDisposable
{
    private const int ReloadDebounceMs = 250;

    private readonly RuntimeState _state;
    private readonly KeyboardHook _hook;
    private readonly string _path;
    private readonly FileSystemWatcher? _watcher;
    private readonly object _lock = new();

    private ConfigModel _current = ConfigStore.Default();
    private Timer? _debounceTimer;
    private volatile bool _disposed;
    private volatile string? _appliedTheme;

    public ConfigService(RuntimeState state, KeyboardHook hook, string path)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _hook = hook ?? throw new ArgumentNullException(nameof(hook));
        _path = path ?? throw new ArgumentNullException(nameof(path));

        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(directory))
        {
            directory = ".";
        }

        try
        {
            Directory.CreateDirectory(directory);
            _watcher = new FileSystemWatcher(directory, "config.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += OnConfigFileChanged;
            _watcher.Created += OnConfigFileChanged;
            _watcher.Renamed += OnConfigFileChanged;
            _watcher.Deleted += OnConfigFileChanged;
        }
        catch (Exception ex)
        {
            Log.Warn($"config watcher unavailable: {ex.Message}");
        }
    }

    /// <summary>Current in-memory config snapshot (immutable record).</summary>
    public ConfigModel Current
    {
        get
        {
            lock (_lock)
            {
                return _current;
            }
        }
    }

    /// <summary>Currently configured theme key ("Dark" or "Light").</summary>
    public string Theme
    {
        get
        {
            lock (_lock)
            {
                return _current.Theme;
            }
        }
    }

    /// <summary>
    /// Raised whenever the applied theme changes (including the initial load,
    /// when the persisted theme differs from the previous run's). May fire on
    /// the watcher thread — consumers must marshal to the UI thread.
    /// </summary>
    public event Action<string>? ThemeChanged;

    /// <summary>
    /// Startup load: reads the config (defaults + file creation when missing/
    /// corrupt) and applies it to the runtime — hook table, gaps, ignored set.
    /// </summary>
    public ConfigModel Load()
    {
        var model = ConfigStore.Load(_path);
        lock (_lock)
        {
            _current = model;
        }

        ApplyToRuntime(model);
        return model;
    }

    /// <summary>
    /// Explicit reload from disk (used by the settings UI later; the watcher
    /// fires for external edits too — both paths converge here).
    /// Never throws.
    /// </summary>
    public void Reload()
    {
        try
        {
            ReloadFromDisk();
        }
        catch (Exception ex)
        {
            Log.Warn($"config reload failed: {ex.Message}");
        }
    }

    // ---- Ignored apps (tray toggle → persist + swap) ----

    public void AddIgnored(string processName)
    {
        var key = RuntimeState.NormalizeProcessName(processName);
        if (key.Length == 0)
        {
            return;
        }

        ConfigModel? updated = null;
        lock (_lock)
        {
            if (!_current.IgnoredApps.Any(a => RuntimeState.NormalizeProcessName(a) == key))
            {
                var list = _current.IgnoredApps.Append(key + ".exe").ToList();
                updated = _current with { IgnoredApps = list };
            }
        }

        if (updated != null)
        {
            SaveAndApply(updated);
        }
    }

    public void RemoveIgnored(string processName)
    {
        var key = RuntimeState.NormalizeProcessName(processName);
        if (key.Length == 0)
        {
            return;
        }

        ConfigModel updated;
        lock (_lock)
        {
            var list = _current.IgnoredApps
                .Where(a => RuntimeState.NormalizeProcessName(a) != key)
                .ToList();
            updated = _current with { IgnoredApps = list };
        }

        SaveAndApply(updated);
    }

    // ---- Autostart (config = source of truth; registry = mechanism) ----

    /// <summary>True when the registry Run value is present for wintangle.</summary>
    public bool GetAutoStartEnabled() => AutoStart.IsEnabled();

    /// <summary>Persists the autostart flag in the config and mirrors it to the registry.</summary>
    public void SetAutoStart(bool enabled)
    {
        ConfigModel updated;
        lock (_lock)
        {
            updated = _current with { AutoStart = enabled };
        }

        SaveAndApply(updated);

        if (OperatingSystem.IsWindows())
        {
            try
            {
                AutoStart.SetEnabled(enabled);
            }
            catch (Exception ex)
            {
                Log.Warn($"autostart registry update failed: {ex.Message}");
            }
        }
    }

    public void ToggleAutoStart() => SetAutoStart(!GetAutoStartEnabled());

    // ---- Settings UI (gaps, shortcuts, defaults) ----

    /// <summary>
    /// Effective binding for an action — exactly what the live hook table
    /// uses (a custom override when it survived conflict resolution, else the
    /// default). Null only for an unknown action.
    /// </summary>
    public Hotkey? GetShortcut(HotkeyAction action)
    {
        lock (_lock)
        {
            return FindEffectiveBinding(action);
        }
    }

    /// <summary>True when the action's effective binding differs from its default (i.e. a custom binding is live).</summary>
    public bool IsCustomShortcut(HotkeyAction action)
    {
        lock (_lock)
        {
            var effective = FindEffectiveBinding(action);
            if (effective == null)
            {
                return false;
            }

            var def = DefaultHotkeys.FindHotkey(action);
            return def != null && effective != def;
        }
    }

    /// <summary>Binds <paramref name="hotkey"/> to <paramref name="action"/>, replacing any existing binding for that action.</summary>
    public void SetShortcut(HotkeyAction action, Hotkey hotkey)
    {
        ConfigModel updated;
        lock (_lock)
        {
            var shortcuts = _current.Shortcuts.Where(s => s.Action != action).ToList();
            shortcuts.Add(new ShortcutBinding(action, hotkey.VirtualKey, hotkey.Modifiers));
            updated = _current with { Shortcuts = shortcuts };
        }

        SaveAndApply(updated);
    }

    /// <summary>Removes a custom binding so the action falls back to its default.</summary>
    public void RestoreShortcut(HotkeyAction action)
    {
        ConfigModel updated;
        lock (_lock)
        {
            var shortcuts = _current.Shortcuts.Where(s => s.Action != action).ToList();
            updated = _current with { Shortcuts = shortcuts };
        }

        SaveAndApply(updated);
    }

    /// <summary>
    /// Returns the action currently bound to <paramref name="hotkey"/> in the
    /// effective table (defaults + custom overrides), or null when unused.
    /// Used by the recorder's duplicate-combo validation.
    /// </summary>
    public HotkeyAction? FindActionForHotkey(Hotkey hotkey)
    {
        lock (_lock)
        {
            var table = BuildTable(_current.Shortcuts);
            return table.TryMatch(hotkey.VirtualKey, hotkey.Modifiers, out var action) ? action : null;
        }
    }

    /// <summary>Applies new gap values live (used by the settings sliders).</summary>
    public void UpdateGaps(int windowGap, int edgeGap)
    {
        ConfigModel updated;
        lock (_lock)
        {
            updated = _current with { WindowGap = windowGap, EdgeGap = edgeGap };
        }

        SaveAndApply(updated);
    }

    /// <summary>
    /// Persists a new theme ("Dark"/"Light"). Unknown values normalize to the
    /// default; <see cref="ThemeChanged"/> fires when the applied theme differs.
    /// </summary>
    public void SetTheme(string theme)
    {
        var normalized = ConfigStore.NormalizeTheme(theme);
        ConfigModel updated;
        lock (_lock)
        {
            updated = _current with { Theme = normalized };
        }

        SaveAndApply(updated);
    }

    /// <summary>
    /// Resets everything to factory defaults: gaps, autostart off, default
    /// shortcuts, empty ignored-apps list. Mirrors the autostart change to the
    /// registry (config is the source of truth, but the registry is the
    /// mechanism — reconcile it right away, not only at next startup).
    /// </summary>
    public void RestoreDefaults()
    {
        SaveAndApply(ConfigStore.Default());

        if (OperatingSystem.IsWindows())
        {
            try
            {
                AutoStart.SetEnabled(false);
            }
            catch (Exception ex)
            {
                Log.Warn($"autostart registry reset failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Startup reconciliation: the config is the source of truth, so make the
    /// registry match it (repairs manual tampering and covers first-run).
    /// </summary>
    public void ReconcileAutoStart()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            bool configEnabled;
            lock (_lock)
            {
                configEnabled = _current.AutoStart;
            }

            if (configEnabled != AutoStart.IsEnabled())
            {
                AutoStart.SetEnabled(configEnabled);
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"autostart reconcile failed: {ex.Message}");
        }
    }

    // ---- Internal ----

    /// <summary>
    /// Watcher callback — debounces file-system events before reloading. Runs
    /// on a threadpool thread and must never throw unhandled (an exception
    /// here would crash the pool thread at shutdown). <see cref="Dispose"/>
    /// can race an in-flight event, so the disposed state is re-checked under
    /// the lock before touching the timer: a timer is never created or
    /// re-armed after dispose.
    /// </summary>
    private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            if (_disposed)
            {
                return;
            }

            // Debounce: every file-system event resets the timer; the reload
            // fires 250ms after the last event (covers save + tmp-rename
            // double events and same-process saves).
            lock (_lock)
            {
                // Dispose may have completed between the volatile check above
                // and acquiring the lock — never resurrect a timer after
                // dispose (a leaked timer would keep firing post-shutdown).
                if (_disposed)
                {
                    return;
                }

                _debounceTimer ??= new Timer(_ => Reload(), null, Timeout.Infinite, Timeout.Infinite);
                _debounceTimer.Change(ReloadDebounceMs, Timeout.Infinite);
            }
        }
        catch (Exception ex)
        {
            // Timer creation/Change and the FileSystemWatcher are guarded by
            // the lock, but keep the callback exception-proof anyway: an
            // unhandled exception from a watcher event is unrecoverable.
            Log.Warn($"config watcher callback failed: {ex.Message}");
        }
    }

    private void ReloadFromDisk()
    {
        var model = ConfigStore.Load(_path);
        lock (_lock)
        {
            _current = model;
        }

        ApplyToRuntime(model);
    }

    /// <summary>
    /// Persists <paramref name="model"/> and applies it immediately. The
    /// watcher then fires and reloads from disk — idempotent, and the debounce
    /// absorbs the double reload.
    /// </summary>
    private void SaveAndApply(ConfigModel model)
    {
        lock (_lock)
        {
            _current = model;
        }

        try
        {
            ConfigStore.Save(_path, model);
        }
        catch (Exception ex)
        {
            Log.Warn($"config save failed: {ex.Message}");
        }

        ApplyToRuntime(model);
    }

    /// <summary>
    /// Swaps the three runtime snapshots: hook table, gaps, ignored set.
    /// Every swap is an Interlocked exchange — safe to call from any thread
    /// and never throws (inputs come from the sanitized config).
    /// </summary>
    private void ApplyToRuntime(ConfigModel model)
    {
        _hook.Table = BuildTable(model.Shortcuts);
        _state.UpdateGaps(new GapSettings(model.WindowGap, model.EdgeGap));
        _state.UpdateIgnored(BuildIgnoredSet(model.IgnoredApps));

        // Theme swap + notification. Raised strictly outside _lock.
        string? themeToNotify = null;
        if (model.Theme != _appliedTheme)
        {
            _appliedTheme = model.Theme;
            themeToNotify = model.Theme;
        }

        if (themeToNotify != null)
        {
            ThemeChanged?.Invoke(themeToNotify);
        }
    }

    /// <summary>
    /// Builds the live hotkey table from the effective entries (defaults +
    /// conflict-resolved custom overrides).
    /// </summary>
    private static HotkeyTable BuildTable(IReadOnlyList<ShortcutBinding>? shortcuts)
    {
        try
        {
            return new HotkeyTable(BuildEffectiveEntries(shortcuts));
        }
        catch (ArgumentException)
        {
            // Should be unreachable (deduped above), but guard anyway: a
            // duplicate combination would otherwise take down the reload.
            Log.Warn("BuildTable: duplicate hotkey combination detected — falling back to default table");
            return DefaultHotkeys.Create();
        }
    }

    /// <summary>
    /// Builds the effective binding list: the default table with per-action
    /// custom overrides applied on top. A custom binding replaces the default
    /// combo for its own action; an override whose combo is already taken by
    /// another action's effective binding is dropped (keeps the table
    /// conflict-free, mirroring <see cref="ConfigStore.Sanitize"/>). This is
    /// the single source of truth for what the hook table actually matches,
    /// so settings/tray display and the hotkey matcher always agree.
    /// </summary>
    private static List<KeyValuePair<Hotkey, HotkeyAction>> BuildEffectiveEntries(IReadOnlyList<ShortcutBinding>? shortcuts)
    {
        if (shortcuts == null || shortcuts.Count == 0)
        {
            return new List<KeyValuePair<Hotkey, HotkeyAction>>(DefaultHotkeys.Entries);
        }

        var effective = new List<KeyValuePair<Hotkey, HotkeyAction>>(DefaultHotkeys.Entries);

        var byAction = new Dictionary<HotkeyAction, Hotkey>();
        foreach (var shortcut in shortcuts)
        {
            if (shortcut == null || shortcut.VirtualKey == 0 || !Enum.IsDefined(shortcut.Action))
            {
                continue;
            }

            byAction[shortcut.Action] = new Hotkey(shortcut.VirtualKey, shortcut.Modifiers);
        }

        var used = new HashSet<Hotkey>(effective.Count);
        foreach (var entry in effective)
        {
            used.Add(entry.Key);
        }

        for (var i = 0; i < effective.Count; i++)
        {
            var action = effective[i].Value;
            if (!byAction.TryGetValue(action, out var custom))
            {
                continue;
            }

            var currentKey = effective[i].Key;
            if (custom == currentKey)
            {
                continue; // same as default — no-op
            }

            if (used.Contains(custom))
            {
                Log.Warn($"shortcut for {action} ({HotkeyLabels.Format(custom)}) conflicts with another binding — keeping the existing one.");
                continue;
            }

            used.Remove(currentKey);
            used.Add(custom);
            effective[i] = new KeyValuePair<Hotkey, HotkeyAction>(custom, action);
        }

        return effective;
    }

    /// <summary>The effective binding for <paramref name="action"/>, or null for an unknown action.</summary>
    private Hotkey? FindEffectiveBinding(HotkeyAction action)
    {
        foreach (var entry in BuildEffectiveEntries(_current.Shortcuts))
        {
            if (entry.Value == action)
            {
                return entry.Key;
            }
        }

        return null;
    }

    private static ImmutableHashSet<string> BuildIgnoredSet(IEnumerable<string>? names)
    {
        var set = ImmutableHashSet<string>.Empty.WithComparer(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names ?? Enumerable.Empty<string>())
        {
            var key = RuntimeState.NormalizeProcessName(name);
            if (key.Length > 0)
            {
                set = set.Add(key);
            }
        }

        return set;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_watcher != null)
        {
            try
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
            }
            catch (Exception ex)
            {
                Log.Warn($"config watcher dispose failed: {ex.Message}");
            }
        }

        lock (_lock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }
    }
}
