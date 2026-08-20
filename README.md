<div align="center">

<img src="docs/wintangle-icon.png" alt="wintangle icon" width="120" />

# wintangle

**A lightweight, keyboard-first window manager for Windows 11.**  

*Created because Windows lacks a keyboard-first window management tool like [Rectangle](https://github.com/rxhanson/rectangle) — the great macOS window manager.*

[![CI](https://img.shields.io/badge/CI-passing-brightgreen.svg)](https://github.com/wintangle/wintangle/actions)
[![Release](https://img.shields.io/github/v/release/wintangle/wintangle?color=blue&label=release)](https://github.com/wintangle/wintangle/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%2011%20x64-0078d4.svg)](#install)
[![Stack](https://img.shields.io/badge/.NET-8.0%20%7C%20WPF-512bd4.svg)](#build-from-source)

</div>

---

## The Pitch

### The Problem
Traditional window management on Windows forces you into frustrating trade-offs:
- **PowerToys FancyZones** requires dragging windows while holding modifier keys, interruptive zone templates, and per-layout shortcut confusion.
- **Built-in Windows Snap** is limited to basic halves and quadrants with intrusive overlay popups and inconsistent multi-monitor behaviors.
- **Tiling WMs (i3/glaze)** take over your entire desktop workflow with complex config languages and steep learning curves.

### The Solution: wintangle
`wintangle` provides a fast, predictable, keyboard-driven tiling engine that stays out of your way:
- **16 Fixed Tiling Slots**: Instant halves, quarters, thirds, and sixths using intuitive, consistent chords (defaulting to `Ctrl+Win+…`).
- **Multi-Monitor Fluidity**: Move windows across screens with `Win+Alt+Left/Right` while maintaining relative slot placement.
- **Minimalist Footprint**: Lives unobtrusively in your system tray with near-zero idle CPU and memory consumption.
- **True Win32 Precision**: Direct window positioning with configurable window-to-window and screen-edge gaps.


---

## Default Keyboard Shortcuts

All shortcuts can be fully rebound to custom key combinations in the Settings window.

### Halves & Center
| Action | Description | Default Shortcut |
| :--- | :--- | :--- |
| **Half Left** | Left 50% split | `Ctrl+Win+Left` |
| **Half Right** | Right 50% split | `Ctrl+Win+Right` |
| **Center Half** | Centered 50% column | `Ctrl+Win+C` |

### Quarters (Quadrants)
| Action | Description | Default Shortcut |
| :--- | :--- | :--- |
| **Quarter Top-Left** | Top-left quadrant (50% × 50%) | `Ctrl+Win+[` |
| **Quarter Top-Right** | Top-right quadrant (50% × 50%) | `Ctrl+Win+]` |
| **Quarter Bottom-Left** | Bottom-left quadrant (50% × 50%) | `Ctrl+Win+;` |
| **Quarter Bottom-Right** | Bottom-right quadrant (50% × 50%) | `Ctrl+Win+'` |

### Thirds (Columns)
| Action | Description | Default Shortcut |
| :--- | :--- | :--- |
| **Third Left** | Left 33.3% column | `Ctrl+Win+,` |
| **Third Center** | Center 33.3% column | `Ctrl+Win+.` |
| **Third Right** | Right 33.3% column | `Ctrl+Win+/` |

### Sixths (3×2 Grid)
| Action | Description | Default Shortcut |
| :--- | :--- | :--- |
| **Sixth Top-Left** | Top-left 1/6th tile | `Ctrl+Win+I` |
| **Sixth Top-Center** | Top-center 1/6th tile | `Ctrl+Win+O` |
| **Sixth Top-Right** | Top-right 1/6th tile | `Ctrl+Win+P` |
| **Sixth Bottom-Left** | Bottom-left 1/6th tile | `Ctrl+Win+J` |
| **Sixth Bottom-Center** | Bottom-center 1/6th tile | `Ctrl+Win+K` |
| **Sixth Bottom-Right** | Bottom-right 1/6th tile | `Ctrl+Win+L` |

### Multi-Monitor Navigation
| Action | Description | Default Shortcut |
| :--- | :--- | :--- |
| **Previous Monitor** | Move focused window to previous monitor preserving relative slot | `Win+Alt+Left` |
| **Next Monitor** | Move focused window to next monitor preserving relative slot | `Win+Alt+Right` |

---

## Features

- **3-Tab Settings Window**:
  - **Window Layouts**: Visual preview cards for all 16 slots with direct live tiling and a running window inspector.
  - **Keyboard Shortcuts**: Interactive hotkey recorder with instant duplicate detection, modifier validation, and per-action factory reset.
  - **Settings**: Live theme switcher (Dark / Light), precision gap sliders (0–50 px), ignored process manager, and global reset with confirmation.
- **Custom WPF Tray Menu**: Sleek popup menu featuring a 2-column slot matrix, monospaced shortcut indicators, foreground process ignore toggle, and logon autostart toggle.
- **Fluid Toast Notifications**: Non-intrusive animated WPF notifications for system diagnostics and elevation/permission warnings.
- **Dynamic Dark & Light Themes**: Real-time theme switching with customized palettes, translucent window sheens, and robust font fallback chains.
- **Configurable Gaps**: Independent control over **Window Gaps** (spacing between adjacent windows) and **Edge Gaps** (spacing along screen edges).
- **Per-App Ignore List**: Easily exclude full-screen games, media players, or legacy tools from tiling via the tray menu or settings panel.
- **HKCU Logon Autostart**: User-level registry startup (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`) — no administrator rights required.

---

## Install

Download the installer (`wintangle-setup.exe`) from the [Releases](https://github.com/wintangle/wintangle/releases) page.
- **Per-user installation**: No administrator privileges required.
- **Self-contained**: No external .NET runtime installation required.
- **System Requirements**: Windows 11 x64 (Windows 10 64-bit compatible).

> *Note:* The installer is currently unsigned. If Windows SmartScreen displays a warning, select **More info → Run anyway**.

---

## Build from Source

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11 x64 (for building the WPF application)

### Build & Test
```powershell
# Restore and build the solution
dotnet restore Wintangle.sln
dotnet build Wintangle.sln -c Release

# Run test suite
dotnet test tests/Wintangle.Core.Tests -c Release
```

---

## Configuration

Settings are saved automatically in `%APPDATA%\wintangle\config.json`. The file is monitored with a hot reload watcher:

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
| `version` | `number` | Configuration schema version (currently `1`). |
| `windowGap` | `number` | Space between adjacent windows in pixels (`0`–`50`, default `8`). |
| `edgeGap` | `number` | Space between windows and screen edges in pixels (`0`–`50`, default `0`). |
| `autoStart` | `boolean` | Enable launch at Windows user logon. |
| `theme` | `string` | Active theme: `"Dark"` or `"Light"`. |
| `shortcuts` | `array` | Rebound hotkey definitions (`action`, `virtualKey`, `modifiers`). |
| `ignoredApps` | `array` | List of process names to ignore for tiling (e.g. `["game.exe"]`). |

---

## Limitations

- **Elevated / Admin Windows**: Non-elevated processes cannot reposition windows belonging to elevated processes due to Windows User Interface Privilege Isolation (UIPI). wintangle gracefully detects this condition and notifies the user.
- **UWP / Windows Store Apps**: Some modern UWP containers restrict standard Win32 `SetWindowPos` placement.
- **Anti-Cheat Software**: Certain kernel-level anti-cheat engines intercept low-level keyboard hooks (`WH_KEYBOARD_LL`). Add affected games to `ignoredApps`.

---

## License

Distributed under the MIT License. See [`LICENSE`](LICENSE) for details.  
© 2026 wintangle contributors.
