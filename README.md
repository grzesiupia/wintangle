<div align="center">

<img src="docs/wintangle-icon.png" alt="wintangle icon" width="120" />

# wintangle

**A keyboard-first window manager for Windows 11.**

Tile the focused window into one of 16 fixed slots with a hotkey chord. No dragging, no zones to configure.

*Created because Windows lacks a keyboard-first window management tool like [Rectangle](https://github.com/rxhanson/rectangle): the great macOS window manager.*

[![CI](https://img.shields.io/badge/CI-passing-brightgreen.svg)](https://github.com/grzesiupia/wintangle/actions)
[![Release](https://img.shields.io/github/v/release/grzesiupia/wintangle?color=blue&label=release)](https://github.com/grzesiupia/wintangle/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%2011%20x64-0078d4.svg)](#install)
[![Stack](https://img.shields.io/badge/.NET-8.0%20%7C%20WPF-512bd4.svg)](#build-from-source)

</div>

---

## Why

Windows has no good keyboard-first option for arranging windows:

- FancyZones wants you to drag windows around with modifiers and maintain zone templates.
- Built-in snap only does halves and quarters, and its overlay popups get in the way.
- i3-style tiling window managers take over the whole desktop and come with a config language to learn.

wintangle sits in the tray and does one thing: press a chord, the focused window moves into a slot. Halves, quarters, thirds and sixths, `Ctrl+Win+…` by default, all rebindable. Windows keep their position when you move them between monitors, and gaps between windows are configurable in pixels.

---

## Default Keyboard Shortcuts

All shortcuts can be rebound in the Settings window.

### Halves & Center
| Action | Description | Default Shortcut |
| :--- | :--- | :--- |
| **Half Left** | Left 50% split | `Ctrl+Win+Left` |
| **Half Right** | Right 50% split | `Ctrl+Win+Right` |
| **Center Half** | Centered 50% column | `Ctrl+Win+C` |

### Quarters
| Action | Description | Default Shortcut |
| :--- | :--- | :--- |
| **Quarter Top-Left** | Top-left quadrant | `Ctrl+Win+[` |
| **Quarter Top-Right** | Top-right quadrant | `Ctrl+Win+]` |
| **Quarter Bottom-Left** | Bottom-left quadrant | `Ctrl+Win+;` |
| **Quarter Bottom-Right** | Bottom-right quadrant | `Ctrl+Win+'` |

### Thirds
| Action | Description | Default Shortcut |
| :--- | :--- | :--- |
| **Third Left** | Left 33.3% column | `Ctrl+Win+,` |
| **Third Center** | Center 33.3% column | `Ctrl+Win+.` |
| **Third Right** | Right 33.3% column | `Ctrl+Win+/` |

### Sixths
| Action | Description | Default Shortcut |
| :--- | :--- | :--- |
| **Sixth Top-Left** | Top-left 1/6th tile | `Ctrl+Win+I` |
| **Sixth Top-Center** | Top-center 1/6th tile | `Ctrl+Win+O` |
| **Sixth Top-Right** | Top-right 1/6th tile | `Ctrl+Win+P` |
| **Sixth Bottom-Left** | Bottom-left 1/6th tile | `Ctrl+Win+J` |
| **Sixth Bottom-Center** | Bottom-center 1/6th tile | `Ctrl+Win+K` |
| **Sixth Bottom-Right** | Bottom-right 1/6th tile | `Ctrl+Win+L` |

### Moving Between Monitors
| Action | Description | Default Shortcut |
| :--- | :--- | :--- |
| **Previous Monitor** | Move focused window to previous monitor, keep relative slot | `Win+Alt+Left` |
| **Next Monitor** | Move focused window to next monitor, keep relative slot | `Win+Alt+Right` |

---

## Features

- **Settings window** with three tabs: layout cards with live previews, a hotkey recorder that rejects duplicate or modifier-less chords, and general settings (theme, gaps, ignored apps, reset to defaults).
- **Custom tray menu**: 2-column slot list with current key bindings, ignore-app toggle, autostart toggle.
- **Toast notifications** for cases where a window can't be moved (elevated processes, UWP restrictions).
- **Dark and light themes**, switchable live.
- **Gaps**: separate pixel values for spacing between windows and from screen edges.
- **Per-app ignore list**: games, media players, anything you don't want touched.
- **Autostart** via HKCU registry entry: no admin rights.

---

## Install

Download `wintangle-setup.exe` from the [Releases](https://github.com/grzesiupia/wintangle/releases) page.

- Per-user install, no admin rights needed.
- Self-contained: no .NET runtime required.
- Requires Windows 11 x64.

> The installer is unsigned for now. If SmartScreen complains, use **More info → Run anyway**.
>
> **Use at your own risk.** This is open-source software provided as-is, without warranty (see the [MIT license](LICENSE)).

---

## Build from Source

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11 x64 (to build the WPF app)

### Build & Test
```powershell
dotnet tool restore   # installs GitVersion CLI (dotnet dotnet-gitversion)
dotnet restore Wintangle.sln
dotnet build Wintangle.sln -c Release
dotnet test tests/Wintangle.Core.Tests -c Release
```

Versions are derived automatically from git tags by GitVersion — no manual bumping.

---

## Configuration

Settings live in `%APPDATA%\wintangle\config.json`. The file is watched, so external edits apply live:

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

| Field | Type | Description |
| :--- | :--- | :--- |
| `version` | `number` | Config schema version (currently `1`). |
| `windowGap` | `number` | Pixels between adjacent windows (`0`-`50`, default `8`). |
| `edgeGap` | `number` | Pixels between windows and screen edges (`0`-`50`, default `0`). |
| `autoStart` | `boolean` | Start at Windows logon. |
| `theme` | `string` | `"Dark"` or `"Light"`. |
| `shortcuts` | `array` | Rebound hotkeys (`action`, `virtualKey`, `modifiers`). |
| `ignoredApps` | `array` | Process names never tiled, e.g. `["game.exe"]`. |

---

## Limitations

- **Elevated / admin windows** can't be moved by a non-elevated process (UIPI). wintangle detects this and notifies instead of failing silently.
- **UWP / Store apps**: some containers ignore `SetWindowPos`.
- **Anti-cheat software**: some kernel-level anti-cheat intercepts low-level keyboard hooks. Add affected games to `ignoredApps`.

---

## License

MIT. See [`LICENSE`](LICENSE).  
© 2026 wintangle contributors.
