using Wintangle.Core.Config;
using Wintangle.Core.Hotkeys;

namespace Wintangle.Core.Tests;

public class ConfigStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wintangle-tests", Guid.NewGuid().ToString("N"));

    public ConfigStoreTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // best-effort cleanup
        }
    }

    private string ConfigPath() => Path.Combine(_dir, "config.json");

    [Fact]
    public void MissingFile_ReturnsDefaults_AndCreatesFile()
    {
        var path = ConfigPath();

        var model = ConfigStore.Load(path);

        Assert.Equal(ConfigModel.DefaultWindowGap, model.WindowGap);
        Assert.Equal(ConfigModel.DefaultEdgeGap, model.EdgeGap);
        Assert.False(model.AutoStart);
        Assert.Equal(ConfigModel.CurrentVersion, model.Version);
        Assert.Empty(model.Shortcuts);
        Assert.Empty(model.IgnoredApps);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void CorruptJson_ReturnsDefaults_AndRewritesFile()
    {
        var path = ConfigPath();
        File.WriteAllText(path, "{ this is not json !!! ");

        var model = ConfigStore.Load(path);

        Assert.Equal(ConfigModel.DefaultWindowGap, model.WindowGap);
        Assert.Empty(model.Shortcuts);

        // The corrupt file was rewritten to valid defaults.
        var reread = ConfigStore.Load(path);
        Assert.Equal(ConfigModel.DefaultWindowGap, reread.WindowGap);
        Assert.Empty(reread.Shortcuts);
    }

    [Fact]
    public void NullJson_ReturnsDefaults()
    {
        var path = ConfigPath();
        File.WriteAllText(path, "null");

        var model = ConfigStore.Load(path);

        Assert.Equal(ConfigModel.DefaultWindowGap, model.WindowGap);
        Assert.Empty(model.Shortcuts);
    }

    [Fact]
    public void RoundTrip_SaveThenLoad_ReturnsSameConfig()
    {
        var path = ConfigPath();
        var config = new ConfigModel
        {
            WindowGap = 12,
            EdgeGap = 4,
            AutoStart = true,
            Shortcuts = new List<ShortcutBinding>
            {
                new(HotkeyAction.HalfLeft, 0x25, KeyModifiers.Ctrl | KeyModifiers.Win),
                new(HotkeyAction.PrevMonitor, 0x25, KeyModifiers.Win | KeyModifiers.Alt),
                new(HotkeyAction.SixthBottomRight, 0x4C, KeyModifiers.Ctrl | KeyModifiers.Win),
            },
            IgnoredApps = new List<string> { "notepad.exe", "chrome.exe" },
        };

        ConfigStore.Save(path, config);
        var loaded = ConfigStore.Load(path);

        Assert.Equal(12, loaded.WindowGap);
        Assert.Equal(4, loaded.EdgeGap);
        Assert.True(loaded.AutoStart);
        Assert.Equal(3, loaded.Shortcuts.Count);
        Assert.Equal(new ShortcutBinding(HotkeyAction.HalfLeft, 0x25, KeyModifiers.Ctrl | KeyModifiers.Win), loaded.Shortcuts[0]);
        Assert.Equal(new ShortcutBinding(HotkeyAction.PrevMonitor, 0x25, KeyModifiers.Win | KeyModifiers.Alt), loaded.Shortcuts[1]);
        Assert.Equal(new ShortcutBinding(HotkeyAction.SixthBottomRight, 0x4C, KeyModifiers.Ctrl | KeyModifiers.Win), loaded.Shortcuts[2]);
        Assert.Equal(new List<string> { "notepad.exe", "chrome.exe" }, loaded.IgnoredApps);
    }

    [Fact]
    public void Shortcuts_RoundTrip_SpecificActionVkMods()
    {
        var path = ConfigPath();
        ConfigStore.Save(path, new ConfigModel
        {
            Shortcuts = new List<ShortcutBinding>
            {
                new(HotkeyAction.QuarterTopRight, 0xDD, KeyModifiers.Ctrl | KeyModifiers.Win | KeyModifiers.Shift),
            },
        });

        var loaded = ConfigStore.Load(path);

        var shortcut = Assert.Single(loaded.Shortcuts);
        Assert.Equal(HotkeyAction.QuarterTopRight, shortcut.Action);
        Assert.Equal(0xDD, shortcut.VirtualKey);
        Assert.Equal(KeyModifiers.Ctrl | KeyModifiers.Win | KeyModifiers.Shift, shortcut.Modifiers);
    }

    [Fact]
    public void GapOutOfRange_Defaults()
    {
        var path = ConfigPath();
        File.WriteAllText(path, """{ "windowGap": 99, "edgeGap": -3 }""");

        var model = ConfigStore.Load(path);

        Assert.Equal(ConfigModel.DefaultWindowGap, model.WindowGap);
        Assert.Equal(ConfigModel.DefaultEdgeGap, model.EdgeGap);
    }

    [Fact]
    public void GapBoundaryValues_AreKept()
    {
        var path = ConfigPath();
        File.WriteAllText(path, """{ "windowGap": 0, "edgeGap": 50 }""");

        var model = ConfigStore.Load(path);

        Assert.Equal(0, model.WindowGap);
        Assert.Equal(50, model.EdgeGap);
    }

    [Fact]
    public void UnknownAction_Dropped()
    {
        var path = ConfigPath();
        File.WriteAllText(path, """
            {
              "shortcuts": [
                { "action": 999, "virtualKey": 67, "modifiers": 5 },
                { "action": 1, "virtualKey": 67, "modifiers": 5 }
              ]
            }
            """);

        var model = ConfigStore.Load(path);

        var shortcut = Assert.Single(model.Shortcuts);
        Assert.Equal(HotkeyAction.HalfLeft, shortcut.Action);
    }

    [Fact]
    public void DuplicateCombos_Deduped_FirstWins()
    {
        var config = new ConfigModel
        {
            Shortcuts = new List<ShortcutBinding>
            {
                new(HotkeyAction.CenterHalf, 0x43, KeyModifiers.Ctrl | KeyModifiers.Win),
                new(HotkeyAction.HalfLeft, 0x43, KeyModifiers.Ctrl | KeyModifiers.Win),
                new(HotkeyAction.NextMonitor, 0x43, KeyModifiers.Ctrl | KeyModifiers.Win),
            },
        };

        var sanitized = ConfigStore.Sanitize(config);

        var shortcut = Assert.Single(sanitized.Shortcuts);
        Assert.Equal(HotkeyAction.CenterHalf, shortcut.Action);
    }

    [Fact]
    public void DuplicateAction_DifferentCombos_Deduped_FirstWins()
    {
        var config = new ConfigModel
        {
            Shortcuts = new List<ShortcutBinding>
            {
                new(HotkeyAction.CenterHalf, 0x43, KeyModifiers.Ctrl | KeyModifiers.Win),
                new(HotkeyAction.CenterHalf, 0x44, KeyModifiers.Ctrl | KeyModifiers.Win | KeyModifiers.Shift),
            },
        };

        var sanitized = ConfigStore.Sanitize(config);

        var shortcut = Assert.Single(sanitized.Shortcuts);
        Assert.Equal(HotkeyAction.CenterHalf, shortcut.Action);
        Assert.Equal(0x43, shortcut.VirtualKey);
        Assert.Equal(KeyModifiers.Ctrl | KeyModifiers.Win, shortcut.Modifiers);
    }

    [Fact]
    public void SameActionTwice_RoundTrip_KeepsFirstOnly()
    {
        var path = ConfigPath();
        File.WriteAllText(path, """
            {
              "shortcuts": [
                { "action": "CenterHalf", "virtualKey": 67, "modifiers": 5 },
                { "action": "CenterHalf", "virtualKey": 68, "modifiers": 6 }
              ]
            }
            """);

        var model = ConfigStore.Load(path);

        var shortcut = Assert.Single(model.Shortcuts);
        Assert.Equal(HotkeyAction.CenterHalf, shortcut.Action);
        Assert.Equal(67, shortcut.VirtualKey);
    }

    [Fact]
    public void SameKey_DifferentModifiers_NotDeduped()
    {
        var config = new ConfigModel
        {
            Shortcuts = new List<ShortcutBinding>
            {
                new(HotkeyAction.HalfLeft, 0x25, KeyModifiers.Ctrl | KeyModifiers.Win),
                new(HotkeyAction.PrevMonitor, 0x25, KeyModifiers.Win | KeyModifiers.Alt),
            },
        };

        var sanitized = ConfigStore.Sanitize(config);

        Assert.Equal(2, sanitized.Shortcuts.Count);
    }

    [Fact]
    public void Sanitize_Null_ReturnsDefaults()
    {
        var sanitized = ConfigStore.Sanitize(null);

        Assert.Equal(ConfigModel.DefaultWindowGap, sanitized.WindowGap);
        Assert.Equal(ConfigModel.DefaultEdgeGap, sanitized.EdgeGap);
        Assert.Empty(sanitized.Shortcuts);
        Assert.Empty(sanitized.IgnoredApps);
        Assert.False(sanitized.AutoStart);
        Assert.Equal(ConfigModel.DefaultTheme, sanitized.Theme);
    }

    [Fact]
    public void DefaultTheme_IsDark()
    {
        Assert.Equal("Dark", ConfigModel.DefaultTheme);
        Assert.Equal(ConfigModel.ThemeDark, ConfigModel.DefaultTheme);
        Assert.Equal(ConfigModel.ThemeDark, new ConfigModel().Theme);
        Assert.Equal(ConfigModel.ThemeDark, ConfigStore.Default().Theme);
    }

    [Fact]
    public void Theme_RoundTrip_Light()
    {
        var path = ConfigPath();
        ConfigStore.Save(path, new ConfigModel { Theme = "Light" });

        var loaded = ConfigStore.Load(path);

        Assert.Equal("Light", loaded.Theme);
    }

    [Fact]
    public void Theme_RoundTrip_System()
    {
        var path = ConfigPath();
        ConfigStore.Save(path, new ConfigModel { Theme = ConfigModel.ThemeSystem });

        var loaded = ConfigStore.Load(path);

        Assert.Equal(ConfigModel.ThemeSystem, loaded.Theme);
    }

    [Fact]
    public void Theme_Unknown_DefaultsDark()
    {
        var path = ConfigPath();
        File.WriteAllText(path, """{ "theme": "Neon" }""");

        var model = ConfigStore.Load(path);

        Assert.Equal(ConfigModel.ThemeDark, model.Theme);
    }

    [Fact]
    public void Theme_CaseInsensitive_Normalizes()
    {
        var lowerPath = ConfigPath();
        File.WriteAllText(lowerPath, """{ "theme": "dark" }""");
        Assert.Equal(ConfigModel.ThemeDark, ConfigStore.Load(lowerPath).Theme);

        var upperPath = ConfigPath();
        File.WriteAllText(upperPath, """{ "theme": "LIGHT" }""");
        Assert.Equal(ConfigModel.ThemeLight, ConfigStore.Load(upperPath).Theme);

        var systemPath = ConfigPath();
        File.WriteAllText(systemPath, """{ "theme": "system" }""");
        Assert.Equal(ConfigModel.ThemeSystem, ConfigStore.Load(systemPath).Theme);

        var systemUpperPath = ConfigPath();
        File.WriteAllText(systemUpperPath, """{ "theme": "SYSTEM" }""");
        Assert.Equal(ConfigModel.ThemeSystem, ConfigStore.Load(systemUpperPath).Theme);

        var paddedPath = ConfigPath();
        File.WriteAllText(paddedPath, """{ "theme": "  dark  " }""");
        Assert.Equal(ConfigModel.ThemeDark, ConfigStore.Load(paddedPath).Theme);

        var paddedSystemPath = ConfigPath();
        File.WriteAllText(paddedSystemPath, """{ "theme": "  system  " }""");
        Assert.Equal(ConfigModel.ThemeSystem, ConfigStore.Load(paddedSystemPath).Theme);
    }

    [Fact]
    public void Theme_NullOrWrongType_DefaultsDark()
    {
        var nullPath = ConfigPath();
        File.WriteAllText(nullPath, """{ "theme": null }""");
        Assert.Equal(ConfigModel.ThemeDark, ConfigStore.Load(nullPath).Theme);

        var numberPath = ConfigPath();
        File.WriteAllText(numberPath, """{ "theme": 42 }""");
        Assert.Equal(ConfigModel.ThemeDark, ConfigStore.Load(numberPath).Theme);
    }

    [Theory]
    [InlineData(null, ConfigModel.ThemeDark)]
    [InlineData("", ConfigModel.ThemeDark)]
    [InlineData("   ", ConfigModel.ThemeDark)]
    [InlineData("dark", ConfigModel.ThemeDark)]
    [InlineData("DARK", ConfigModel.ThemeDark)]
    [InlineData("Dark", ConfigModel.ThemeDark)]
    [InlineData("light", ConfigModel.ThemeLight)]
    [InlineData("LIGHT", ConfigModel.ThemeLight)]
    [InlineData("Light", ConfigModel.ThemeLight)]
    [InlineData("system", ConfigModel.ThemeSystem)]
    [InlineData("SYSTEM", ConfigModel.ThemeSystem)]
    [InlineData("System", ConfigModel.ThemeSystem)]
    [InlineData("  system  ", ConfigModel.ThemeSystem)]
    [InlineData("unknown", ConfigModel.ThemeDark)]
    [InlineData("random", ConfigModel.ThemeDark)]
    public void NormalizeTheme_ReturnsExpected(string? input, string expected)
    {
        Assert.Equal(expected, ConfigStore.NormalizeTheme(input));
    }

    [Fact]
    public void Sanitize_InvalidModifierBits_Dropped()
    {
        var config = new ConfigModel
        {
            Shortcuts = new List<ShortcutBinding>
            {
                new(HotkeyAction.CenterHalf, 0x43, (KeyModifiers)0x20), // undefined bit
                new(HotkeyAction.HalfLeft, 0x43, KeyModifiers.Ctrl | KeyModifiers.Win),
            },
        };

        var sanitized = ConfigStore.Sanitize(config);

        var shortcut = Assert.Single(sanitized.Shortcuts);
        Assert.Equal(HotkeyAction.HalfLeft, shortcut.Action);
    }

    [Fact]
    public void Sanitize_ZeroVirtualKey_Dropped()
    {
        var config = new ConfigModel
        {
            Shortcuts = new List<ShortcutBinding>
            {
                new(HotkeyAction.CenterHalf, 0, KeyModifiers.Ctrl | KeyModifiers.Win),
                new(HotkeyAction.HalfLeft, 0x43, KeyModifiers.Ctrl | KeyModifiers.Win),
            },
        };

        var sanitized = ConfigStore.Sanitize(config);

        var shortcut = Assert.Single(sanitized.Shortcuts);
        Assert.Equal(HotkeyAction.HalfLeft, shortcut.Action);
    }

    [Fact]
    public void Sanitize_NormalizesIgnoredApps_LowercaseTrimmedDistinct()
    {
        var config = new ConfigModel
        {
            IgnoredApps = new List<string> { "  NOTEPAD.EXE ", "notepad.exe", "chrome.exe", "" },
        };

        var sanitized = ConfigStore.Sanitize(config);

        Assert.Equal(2, sanitized.IgnoredApps.Count);
        Assert.Contains("notepad.exe", sanitized.IgnoredApps);
        Assert.Contains("chrome.exe", sanitized.IgnoredApps);
    }

    [Fact]
    public void Sanitize_ForcesCurrentVersion()
    {
        var config = new ConfigModel { Version = 99, WindowGap = 5 };

        var sanitized = ConfigStore.Sanitize(config);

        Assert.Equal(ConfigModel.CurrentVersion, sanitized.Version);
        Assert.Equal(5, sanitized.WindowGap);
    }

    [Fact]
    public void All19Actions_RoundTrip()
    {
        var path = ConfigPath();
        var actions = Enum.GetValues<HotkeyAction>();
        Assert.Equal(19, actions.Length);

        var config = new ConfigModel
        {
            Shortcuts = actions
                .Select((action, i) => new ShortcutBinding(action, (byte)(0x41 + i), KeyModifiers.Ctrl | KeyModifiers.Win))
                .ToList(),
        };

        ConfigStore.Save(path, config);
        var loaded = ConfigStore.Load(path);

        Assert.Equal(actions.Length, loaded.Shortcuts.Count);
        Assert.Equal(actions, loaded.Shortcuts.Select(s => s.Action).ToArray());
    }

    [Fact]
    public void FullscreenAction_RoundTrip_ViaStringName()
    {
        var path = ConfigPath();
        File.WriteAllText(path, """
            {
              "shortcuts": [
                { "action": "Fullscreen", "virtualKey": 13, "modifiers": 5 }
              ]
            }
            """);

        var model = ConfigStore.Load(path);

        var shortcut = Assert.Single(model.Shortcuts);
        Assert.Equal(new ShortcutBinding(HotkeyAction.Fullscreen, 0x0D, KeyModifiers.Ctrl | KeyModifiers.Win), shortcut);
    }

    [Fact]
    public void TrailingGarbage_ReturnsDefaults_AndRewritesFile()
    {
        var path = ConfigPath();
        File.WriteAllText(path, """{ "windowGap": 12 } trailing garbage !!!""");

        var model = ConfigStore.Load(path);

        Assert.Equal(ConfigModel.DefaultWindowGap, model.WindowGap);
        Assert.Empty(model.Shortcuts);

        // The garbage file was rewritten to valid defaults.
        var reread = ConfigStore.Load(path);
        Assert.Equal(ConfigModel.DefaultWindowGap, reread.WindowGap);
        Assert.Empty(reread.Shortcuts);
    }

    [Fact]
    public void Load_NullPath_ReturnsDefaults()
    {
        var model = ConfigStore.Load(null!);

        Assert.Equal(ConfigModel.DefaultWindowGap, model.WindowGap);
        Assert.Equal(ConfigModel.DefaultEdgeGap, model.EdgeGap);
        Assert.False(model.AutoStart);
        Assert.Empty(model.Shortcuts);
        Assert.Empty(model.IgnoredApps);
    }

    [Fact]
    public void OverflowGapNumber_DefaultsGap_PreservesShortcuts()
    {
        var path = ConfigPath();
        File.WriteAllText(path, """
            {
              "windowGap": 99999999999999999999,
              "edgeGap": 12,
              "shortcuts": [
                { "action": 1, "virtualKey": 67, "modifiers": 5 }
              ]
            }
            """);

        var model = ConfigStore.Load(path);

        Assert.Equal(ConfigModel.DefaultWindowGap, model.WindowGap);
        Assert.Equal(12, model.EdgeGap);
        var shortcut = Assert.Single(model.Shortcuts);
        Assert.Equal(HotkeyAction.HalfLeft, shortcut.Action);
    }

    [Fact]
    public void UnknownActionString_DropsEntry_KeepsOthers()
    {
        var path = ConfigPath();
        File.WriteAllText(path, """
            {
              "shortcuts": [
                { "action": "BogusAction", "virtualKey": 68, "modifiers": 5 },
                { "action": "HalfLeft", "virtualKey": 67, "modifiers": 5 }
              ]
            }
            """);

        var model = ConfigStore.Load(path);

        var shortcut = Assert.Single(model.Shortcuts);
        Assert.Equal(HotkeyAction.HalfLeft, shortcut.Action);
        Assert.Equal(67, shortcut.VirtualKey);
    }

    [Fact]
    public void NullShortcutElement_Dropped_KeepsOthers()
    {
        var path = ConfigPath();
        File.WriteAllText(path, """
            {
              "shortcuts": [
                null,
                { "action": 1, "virtualKey": 67, "modifiers": 5 }
              ]
            }
            """);

        var model = ConfigStore.Load(path);

        var shortcut = Assert.Single(model.Shortcuts);
        Assert.Equal(HotkeyAction.HalfLeft, shortcut.Action);
    }

    [Fact]
    public void WrongTypeField_DefaultsFieldOnly_KeepsRest()
    {
        var path = ConfigPath();
        File.WriteAllText(path, """
            {
              "autoStart": "yes",
              "windowGap": 12,
              "ignoredApps": [ "notepad.exe", 42, null ],
              "shortcuts": [ { "action": 1, "virtualKey": 67, "modifiers": 5 } ]
            }
            """);

        var model = ConfigStore.Load(path);

        Assert.False(model.AutoStart);
        Assert.Equal(12, model.WindowGap);
        var shortcut = Assert.Single(model.Shortcuts);
        Assert.Equal(HotkeyAction.HalfLeft, shortcut.Action);
        Assert.Equal(new List<string> { "notepad.exe" }, model.IgnoredApps);
    }
}
