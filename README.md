# wintangle

![wintangle](docs/wintangle-icon.png)

A keyboard-only window manager for Windows 11. Tile the focused window with a
single chord — no dragging, no zone configuration, no per-layout shortcut
guesswork.

Why: PowerToys FancyZones makes you drag windows into pre-drawn zones, the
shortcuts differ for every layout, and the whole thing gets in the way when all
you want is "window to the left, thirds, sixths". wintangle gives you one
consistent, memorable table of 18 shortcuts (mostly `Ctrl+Win+…`) that work on
any screen, any layout, any monitor arrangement.

- 16 fixed tiling slots: halves, quarters, thirds, and sixths — all `Ctrl+Win+…`
- `Win+Alt+Left/Right` moves the window to the previous/next monitor
- Multi-monitor aware: the same slot on another screen keeps the relative
  position (e.g. `Ctrl+Win+[` on monitor 2 puts the window top-left there)
- Small, single tray icon — no dashboard, no onboarding wizard

## Default shortcuts

| Action | Shortcut |
| --- | --- |
| Center half | `Ctrl+Win+C` |
| Half left | `Ctrl+Win+Left` |
| Half right | `Ctrl+Win+Right` |
| Quarter top-left | `Ctrl+Win+[` |
| Quarter top-right | `Ctrl+Win+]` |
| Quarter bottom-left | `Ctrl+Win+;` |
| Quarter bottom-right | `Ctrl+Win+'` |
| Third left | `Ctrl+Win+,` |
| Third center | `Ctrl+Win+.` |
| Third right | `Ctrl+Win+/` |
| Sixth top-left | `Ctrl+Win+I` |
| Sixth top-center | `Ctrl+Win+O` |
| Sixth top-right | `Ctrl+Win+P` |
| Sixth bottom-left | `Ctrl+Win+J` |
| Sixth bottom-center | `Ctrl+Win+K` |
| Sixth bottom-right | `Ctrl+Win+L` |
| Move to previous monitor | `Win+Alt+Left` |
| Move to next monitor | `Win+Alt+Right` |

Every shortcut can be rebound in Settings.

## Install

Download the installer (`wintangle-setup.exe`) from the
[Releases](https://github.com/wintangle/wintangle/releases) page. The setup
installs per-user (no administrator rights needed), adds a Start Menu shortcut,
and optionally launches wintangle when you finish.

The installer is **unsigned**, so SmartScreen will show a warning. Click
**More info → Run anyway**.

Requirements: Windows 11 x64 (Windows 10 64-bit may work but is not tested),
.NET 8 runtime is **not** needed — the published build is self-contained.

## Build from source

Requires the .NET 8 SDK.

```sh
dotnet restore Wintangle.sln
dotnet build Wintangle.sln -c Release
dotnet test tests/Wintangle.Core.Tests -c Release
```

Windows only — the app is WPF (`net8.0-windows`) and cannot be built for other
platforms.

## Configuration

wintangle stores its settings in a JSON file created on first run:

```
%APPDATA%\wintangle\config.json
```

Example:

```json
{
  "version": 1,
  "windowGap": 8,
  "edgeGap": 0,
  "autoStart": false,
  "theme": "Dark",
  "shortcuts": [
    { "action": 1, "virtualKey": 37, "modifiers": 5 }
  ],
  "ignoredApps": [
    "notepad.exe"
  ]
}
```

| Field | Type | Meaning |
| --- | --- | --- |
| `version` | number | Config schema version (currently 1). |
| `windowGap` | number | Gap between adjacent windows, px. Range 0–50. |
| `edgeGap` | number | Gap between a window and the screen edge, px. Range 0–50. |
| `autoStart` | bool | Run at logon (writes the `HKCU\...\Run` key). |
| `theme` | string | "Dark" or "Light", default "Dark". |
| `shortcuts` | array | Rebound hotkeys. Empty array means "use the default table". |
| `ignoredApps` | array | Process names wintangle never tiles (e.g. `"notepad.exe"`, lowercase). |

A missing, corrupt, or out-of-range value falls back to the default; the file is
rewritten so a valid config is always on disk.

### Shortcut values are numeric

`shortcuts` entries are serialized as numbers: `action` is the
`HotkeyAction` enum value (0 = center-half, 1 = half-left, … 17 = next monitor),
`virtualKey` is the Windows virtual-key code (e.g. 37 = `VK_LEFT`), and
`modifiers` is a bitmask (`Ctrl`=1, `Alt`=2, `Win`=4, `Shift`=8). See
`src/Wintangle.Core/Hotkeys/` (`HotkeyAction.cs`, `VirtualKey.cs`,
`KeyModifiers.cs`) for the full tables. Rebinding from the Settings window is
the easy way to get these right.

### Gaps semantics

- **Window gap** — space left between two adjacent windows. Each window
  applies the full window gap on its interior edge, so the seam between two
  adjacent windows is exactly the window gap.
- **Edge gap** — space left between a window and the screen edge. A
  boundary-touching edge applies only the edge gap.

Defaults are 8 px window gap and 0 px edge gap.

## Features

- **Tray icon** — right-click the tray icon to tile the foreground window from
  a menu, ignore the current app, toggle autostart, open Settings, or quit.
- **Settings app** — a 7-tab window: **Window Layouts** shows preset cards
  for every tiling slot, **Keyboard Shortcuts** has an inline **rebind
  recorder** (press Record, hit the combo, done), and **Settings** covers gaps,
  theme, autostart, and ignored apps. Advanced Rules, Mouse Actions, Plugins,
  and Workspaces are placeholders for later releases. Recording rejects invalid
  combos (no modifiers, bare modifiers); combos like Win+L are OS-reserved and
  will not work — the recorder cannot detect these.
- **Per-app ignore** — "Ignore this app" from the tray; ignored windows are
  never tiled. Editable in `ignoredApps`.
- **Autostart** — runs at logon via the `HKCU` Run key (per-user, no admin).
- **Multi-monitor moves** — `Win+Alt+Left/Right` moves the window to the
  adjacent monitor and re-applies the same slot relative to that screen.

## Limitations

- **Elevated windows are skipped.** Windows UIPI blocks a non-elevated process
  from moving windows owned by an elevated process; wintangle detects this and
  skips the move with a tray balloon. Running wintangle elevated lets it move
  anything, but that's not the default (or recommended).
- **UWP (Store) windows may ignore moves** — some modern apps don't respect
  `SetWindowPos` the way classic Win32 windows do.
- **Non-US keyboard layouts** use US key positions. Shortcuts are bound to
  physical virtual-key codes, so `Ctrl+Win+[` on a German layout presses
  whatever key is physically in that position. Rebind affected shortcuts from
  Settings if needed.
- **Anti-cheat games may block low-level hooks.** wintangle uses a
  `WH_KEYBOARD_LL` hook; games with kernel-level anti-cheat can refuse to run
  alongside it (or swallow the hook). Add them to ignored apps or quit wintangle
  before gaming.
- **Taskbar auto-hide edge case** — with the taskbar set to auto-hide, an
  edge-gap of 0 can place a window flush against the auto-hidden taskbar
  reserve area. Bump the edge gap if the taskbar edge misbehaves.

## License

MIT — see [LICENSE](LICENSE). © 2026 wintangle contributors.
