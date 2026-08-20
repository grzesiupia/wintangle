# wintangle — brand spec

**Source:** user's icon (`sketch-tile-6.png`) + user's functionality brief. Direction bound from `modern-minimal` (Linear / Vercel).

**Extracted from icon:** dark tile `#272727` (rgb 39,39,39), monoline strokes `#8f8f8d` (rgb 143,143,141) — a gray-on-gray ribbon "tangle" knot. The mark is monochrome; no accent exists in the brand, so the neutral base carries the brand and the accent comes from the bound direction.

## Tokens (light = bound `modern-minimal`; dark = app default, derived in oklch)

| role | light | dark |
|---|---|---|
| `--bg` | `oklch(99% 0.002 240)` | `oklch(16% 0.012 250)` |
| `--surface` | `oklch(100% 0 0)` | `oklch(21% 0.014 250)` |
| `--fg` | `oklch(18% 0.012 250)` | `oklch(94% 0.004 250)` |
| `--muted` | `oklch(54% 0.012 250)` | `oklch(68% 0.012 250)` |
| `--border` | `oklch(92% 0.005 250)` | `oklch(30% 0.016 250)` |
| `--accent` | `oklch(58% 0.18 255)` | `oklch(74% 0.14 255)` |

- Fonts: display/body = system sans (`-apple-system, 'SF Pro Display', system-ui`); mono = `'JetBrains Mono', ui-monospace, Menlo` for keycaps, ids, numerics.

## Visual language rules

1. **Monochrome first.** The gray-on-gray tangle is the identity; the accent appears only as selection / active / focus, never as decoration.
2. **Quiet, precise, software-native.** Hairline borders only, no card shadows except the app window and dropdowns.
3. **Data-dense but airy.** Tabular numerics, mono keycaps, dense rows with generous whitespace.
4. **Both themes are first-class.** Dark is the default (matches the icon tile); the switch is instant, no restart.
5. **No marketing voice.** Show the product: the 16-slot grid, the 18 chords, the tray.
