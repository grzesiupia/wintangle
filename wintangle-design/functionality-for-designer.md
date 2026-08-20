# wintangle — Functionality Spec for the Visual Designer

**Purpose:** This document describes **only what the program can do** — features, actions, options, behaviors. It deliberately contains no colors, no sizes, no styling, no typography, and no visual descriptions. All visual decisions (placements, colors, spacing, look) are left entirely to the visual designer.

**Date:** August 2026 · **App:** wintangle 1.0.0 (WPF / .NET 8, Windows 11)

---

## 1. Product overview

wintangle is a keyboard-only window manager for Windows 11. It runs in the system tray; the user presses a hotkey chord to tile the focused window into one of 16 fixed slots, or moves it to an adjacent monitor with another chord. Configuration happens in a single Settings window or via the tray menu.

---

## 2. What the app can do

### 2.1 Window tiling

- **16 fixed tiling slots** applied to the focused window via hotkey chords (defaults: `Ctrl+Win+…`):
  - Halves — left, right, center
  - Quarters — top-left, top-right, bottom-left, bottom-right
  - Thirds — left, center, right
  - Sixths — top/bottom × left/center/right
- Each slot is a distinct action with its own rebindable shortcut.
- Tiling respects the screen work area (excludes the taskbar) and the configured gaps.
- Maximized windows are restored to normal before tiling.
- Window minimum/maximum sizes are respected — the window is clamped, not resized beyond its limits.
- Tiling can be applied to **any foreground window**, including windows that were foreground before the Settings window was opened.

### 2.2 Monitor movement

- **Move to Previous Monitor** / **Move to Next Monitor** actions (`Win+Alt+Left/Right` by default).
- The window keeps its **relative slot** when moving — a left-half on monitor 1 becomes a left-half on monitor 2.

### 2.3 Gaps

- **Window gap** — space between adjacent windows. Default **8**, range 0–50.
- **Edge gap** — space between windows and screen edges. Default **0**, range 0–50.
- Values are committed when the field loses focus or Enter is pressed; invalid values (out of range) are rejected and not applied.
- Gaps apply live to all subsequent tiling.

### 2.4 Tray menu

- **16 slot items** — one per slot, each labeled with the slot name and its current key binding; selecting one tiles the focused window into that slot.
- **Move to Previous/Next Monitor** items.
- **Ignore app** — toggles tiling off for the currently focused app; checkable when the app is already ignored.
- **Autostart** toggle.
- **Settings…** — opens the Settings window.
- **Quit** — exits the app.
- The menu is rebuilt on every open, so shortcut labels are always up to date.
- **Balloon notifications** are shown only when an action is blocked (e.g. elevated/admin windows that cannot be moved). Not used for success feedback.

### 2.5 Ignoring apps

- Ignored apps' windows are **never tiled or moved**.
- Manageable from two places:
  - Tray menu → "Ignore {app name}" toggles the focused app.
  - Settings → ignored apps list: add a process name, remove an existing one.
- The ignored-app list persists in config.

### 2.6 Keyboard shortcut rebinding

- **18 actions** in total: the 16 slots + Previous Monitor + Next Monitor.
- Every action can be **rebound to any combo** by recording it: press "Record", then press the desired key combination; the new binding is committed automatically.
- Recording can be cancelled (Esc / Cancel button); nothing is changed.
- **Validation rules:**
  - A combo must include at least one modifier key.
  - Duplicate combos are rejected — the conflict is reported (which action already uses that combo) and the binding is not applied.
  - Bindings are swapped into the live hotkey hook atomically — no half-applied state.
- Each action has a **Restore default** option that resets only that action to its factory binding.
- Shortcut changes apply immediately; tray menu and Settings labels show the current live bindings.

### 2.7 Themes

- Two themes: **Dark** (default) and **Light**.
- Switchable live in Settings — the change applies instantly, no restart.
- The native title bar of the Settings window follows the theme.

### 2.8 Autostart

- "Start wintangle when I log in" — toggled from the tray menu or Settings.
- Implemented as a **per-user registry entry** (HKCU Run key) — no administrator rights required.

### 2.9 Restore defaults

- "Restore defaults…" resets everything: gaps (8 / 0), all 18 shortcuts to factory bindings, autostart off, ignored apps cleared.
- Requires explicit confirmation before executing.

### 2.10 Configuration persistence

- All settings (gaps, shortcuts, theme, ignored apps, autostart) are persisted in a **JSON file** (`config.json`, under the per-user app data directory).
- The file is watched: an external edit is **reloaded live** while the app runs.
- Config loading is lenient — unreadable or invalid fields fall back to defaults rather than crashing.

### 2.11 Settings window — 7 tabs

| # | Tab | Status |
|---|---|---|
| 1 | Window Layouts | Implemented |
| 2 | Advanced Rules | Placeholder |
| 3 | Keyboard Shortcuts | Implemented |
| 4 | Mouse Actions | Placeholder |
| 5 | Plugins | Placeholder |
| 6 | Workspaces | Placeholder |
| 7 | Settings | Implemented |

**Tab 1 — Window Layouts:**
- **16 preset cards** (one per slot). Each card shows a preview of the slot geometry and its current shortcut.
- Clicking a card applies that slot to the window that was foreground **before Settings was opened**.
- **Active windows list** with a Refresh button — lists currently open windows (process name + window title).
- **Rule builder** card — disabled, labeled "coming soon" (see §4).

**Tab 2 — Advanced Rules:** placeholder, no functionality.

**Tab 3 — Keyboard Shortcuts:**
- One row per action (18 rows): action name, current binding with a Record control, and a Restore-default button.
- Inline error reporting for duplicate/invalid combos.

**Tab 4 — Mouse Actions:** placeholder ("Tile and snap windows with mouse gestures"), no functionality.

**Tab 5 — Plugins:** placeholder, no functionality.

**Tab 6 — Workspaces:** placeholder ("Save and restore complete window arrangements per project"), no functionality.

**Tab 7 — Settings:**
- Window gap and edge gap inputs (0–50).
- Theme selector (Dark / Light) — applies live.
- Autostart checkbox.
- Ignored apps list with add/remove.
- Restore defaults button (with confirmation).

---

## 3. User flows

1. **Tile a window** — Focus the window → press the slot's chord (or tray → slot item) → the window snaps into that slot with the configured gaps. Clamping to the window's min/max sizes happens automatically.
2. **Move across monitors** — Press `Win+Alt+Left/Right` → the window moves to the adjacent monitor, keeping its relative slot.
3. **Tile from the tray** — Right-click the tray icon → pick a slot item → the focused window tiles into that slot.
4. **Ignore an app** — Either: right-click tray icon → "Ignore {app name}". Or: Settings → ignored apps → add the process name → confirm. The app's windows are never tiled or moved until removed from the list.
5. **Rebind a shortcut** — Settings → Keyboard Shortcuts → Record on the desired action → press the new combo → binding is committed automatically. Esc cancels. Duplicates are reported and rejected. "Restore default" resets that action only.
6. **Apply a preset** — Settings → Window Layouts → click a preset card → the pre-Settings foreground window tiles into that slot.
7. **Switch theme** — Settings → pick Dark or Light → the whole app restyles instantly.
8. **Autostart** — Toggle the tray menu item or the Settings checkbox; the per-user registry entry is created or removed.
9. **Restore defaults** — Settings → Restore defaults… → confirm → gaps, shortcuts, autostart, and ignored apps are all reset.
10. **Quit** — Tray → Quit.

---

## 4. Planned / not yet implemented

These exist as **placeholders only** — they show no functionality today. The designer should not design for them as working features.

- **Rule builder** — automatic layouts per app (app → slot assignment, with a fallback "else" slot). Disabled card in Window Layouts, labeled "coming soon".
- **Advanced Rules** — beyond per-app rules (conditions, priorities). Empty tab.
- **Mouse Actions / gestures** — mouse-driven tiling and snapping. Empty tab.
- **Plugins** — third-party extension surface. Empty tab.
- **Workspaces** — save and restore complete window arrangements per project. Empty tab.
