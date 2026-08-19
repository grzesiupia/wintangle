#!/usr/bin/env python3
"""Generate src/Wintangle.App/Assets/wintangle.ico.

Writes a valid 32-bit BMP-in-ICO file with two images (16x16 and 32x32).
The design is a 2x2 window-tiling grid (four colored panes separated by
transparent gaps) on a transparent background.

Run from the repo root (or anywhere; output path is resolved relative to
this file):

    python3 src/Wintangle.App/Assets/generate_icon.py

Pure stdlib: only `struct` and `os` are used (no zlib needed — this is
BMP-in-ICO, not PNG-in-ICO).
"""

import os
import struct

OUT_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "wintangle.ico")

# RGBA colors for the four panes (top-left, top-right, bottom-left, bottom-right).
PANE_COLORS = [
    (0x66, 0x43, 0xF5, 0xFF),  # indigo
    (0x22, 0x96, 0xFA, 0xFF),  # blue
    (0xA8, 0x55, 0xF7, 0xFF),  # purple
    (0xFA, 0xB4, 0x28, 0xFF),  # amber
]

TRANSPARENT = (0, 0, 0, 0)


def draw_pane_icon(width: int, height: int):
    """Returns a 2x2 grid icon as a top-down list of RGBA rows."""
    margin = max(1, width // 8)
    gap = max(1, width // 16)
    cols, rows = 2, 2
    cell_w = (width - 2 * margin - (cols - 1) * gap) // cols
    cell_h = (height - 2 * margin - (rows - 1) * gap) // rows
    corner_r = margin

    def inside_rounded_rect(x: int, y: int) -> bool:
        if x < margin or x >= width - margin or y < margin or y >= height - margin:
            return False
        # rounded corners (only near the four corners of the outer rect)
        if x < margin + corner_r and y < margin + corner_r:
            return (x - margin - corner_r) ** 2 + (y - margin - corner_r) ** 2 <= corner_r ** 2
        if x >= width - margin - corner_r and y < margin + corner_r:
            return (x - (width - margin - corner_r)) ** 2 + (y - margin - corner_r) ** 2 <= corner_r ** 2
        if x < margin + corner_r and y >= height - margin - corner_r:
            return (x - margin - corner_r) ** 2 + (y - (height - margin - corner_r)) ** 2 <= corner_r ** 2
        if x >= width - margin - corner_r and y >= height - margin - corner_r:
            return (x - (width - margin - corner_r)) ** 2 + (y - (height - margin - corner_r)) ** 2 <= corner_r ** 2
        return True

    rows_out = []
    for y in range(height):
        row = []
        for x in range(width):
            if not inside_rounded_rect(x, y):
                row.append(TRANSPARENT)
                continue
            col = (x - margin) // (cell_w + gap)
            r = (y - margin) // (cell_h + gap)
            ox = (x - margin) % (cell_w + gap)
            oy = (y - margin) % (cell_h + gap)
            if col >= cols or r >= rows or ox >= cell_w or oy >= cell_h:
                row.append(TRANSPARENT)  # seam gap between panes
            else:
                row.append(PANE_COLORS[r * cols + col])
        rows_out.append(row)
    return rows_out


def to_bgra_bottom_up(rows):
    """Flattens top-down RGBA rows into bottom-up BGRA pixel bytes."""
    out = bytearray()
    for row in reversed(rows):
        for r, g, b, a in row:
            out += bytes((b, g, r, a))
    return bytes(out)


def and_mask(rows):
    """1bpp AND mask (all zeros — alpha channel already carries transparency)."""
    width = len(rows[0])
    height = len(rows)
    row_bytes = ((width + 31) // 32) * 4
    return b"\x00" * (row_bytes * height)


def build_ico(images):
    """images: list of (width, height, rows) top-down RGBA rows."""
    entries = []
    datas = []
    for width, height, rows in images:
        xor = to_bgra_bottom_up(rows)
        mask = and_mask(rows)
        header = struct.pack(
            "<IiiHHIIiiII",
            40,               # biSize
            width,            # biWidth
            height * 2,       # biHeight (XOR + AND)
            1,                # biPlanes
            32,               # biBitCount
            0,                # biCompression (BI_RGB)
            0,                # biSizeImage (ignored for 32bpp)
            0, 0,             # biXPelsPerMeter / biYPelsPerMeter
            0, 0,             # biClrUsed / biClrImportant
        )
        datas.append(header + xor + mask)

    offset = 6 + 16 * len(images)
    for (width, height, _), data in zip(images, datas):
        entries.append(
            struct.pack(
                "<BBBBHHII",
                width & 0xFF if width < 256 else 0,
                height & 0xFF if height < 256 else 0,
                0,        # color count
                0,        # reserved
                1,        # planes
                32,       # bit count
                len(data),
                offset,
            )
        )
        offset += len(data)

    return struct.pack("<HHH", 0, 1, len(images)) + b"".join(entries) + b"".join(datas)


def main():
    images = [
        (16, 16, draw_pane_icon(16, 16)),
        (32, 32, draw_pane_icon(32, 32)),
    ]
    ico = build_ico(images)
    with open(OUT_PATH, "wb") as f:
        f.write(ico)
    print(f"wrote {OUT_PATH} ({len(ico)} bytes)")


if __name__ == "__main__":
    main()
