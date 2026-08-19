#!/usr/bin/env python3
"""Generate src/Wintangle.App/Assets/wintangle.ico (v1.0.0 release icon).

Design: a dark rounded tile (#252525, #3A3A3A 1px border) carrying a
"wintangle" glyph made of two interlocking window frames — a big quartered
frame with a cross divider, and a small overlapping frame that punches through
its bottom band.

Renders a 512x512 master and downscales with LANCZOS to 16/24/32/48/64/128/256,
packing PNG-in-ICO by hand (6-byte ICONDIR + 16-byte ICONDIRENTRY per size;
width/height byte = 0 for the 256 entry). Also writes two 512px previews
(dark + light) for design review and copies the dark one to docs/ for README.

Run from anywhere (paths are resolved relative to this file):

    python3 src/Wintangle.App/Assets/generate_icon.py [--preview-dir DIR]

Requires Pillow (pip install pillow).
"""

import argparse
import io
import os
import struct
import sys

from PIL import Image, ImageDraw

ASSETS_DIR = os.path.dirname(os.path.abspath(__file__))
OUT_PATH = os.path.join(ASSETS_DIR, "wintangle.ico")
REPO_ROOT = os.path.abspath(os.path.join(ASSETS_DIR, "..", "..", ".."))
DOCS_PREVIEW_PATH = os.path.join(REPO_ROOT, "docs", "wintangle-icon.png")

MASTER = 512
SIZES = (16, 24, 32, 48, 64, 128, 256)

# Dark design (the default theme).
TILE_FILL = "#252525"
TILE_OUTLINE = "#3A3A3A"
GLYPH_COLOR = (0xE0, 0xE0, 0xE0, 0xFF)

# Light preview variant: same geometry, inverted palette.
LIGHT_TILE_FILL = "#FFFFFF"
LIGHT_TILE_OUTLINE = "#D6D6D6"
LIGHT_GLYPH_COLOR = (0x1E, 0x1E, 0x1E, 0xFF)


def draw_glyph_mask() -> Image.Image:
    """'L' mask over the master canvas: 255 = glyph, 0 = punch (tile shows through)."""
    mask = Image.new("L", (MASTER, MASTER), 0)
    d = ImageDraw.Draw(mask)

    # Big frame (quarters): outer rounded rect filled, inner punched out —
    # leaves a 34px-thick ring ((272-204)/2 = 34).
    d.rounded_rectangle((128, 112, 400, 384), radius=22, fill=255)
    d.rounded_rectangle((162, 146, 366, 350), radius=10, fill=0)

    # Cross divider: 34px bars with round caps (r=17 at all 4 bar ends) and a
    # center circle at the intersection.
    d.rectangle((162, 231, 366, 265), fill=255)
    d.rectangle((247, 146, 281, 350), fill=255)
    for cx, cy in ((162, 248), (366, 248), (264, 146), (264, 350)):
        d.ellipse((cx - 17, cy - 17, cx + 17, cy + 17), fill=255)
    d.ellipse((264 - 17, 248 - 17, 264 + 17, 248 + 17), fill=255)

    # Small overlapping frame — drawn AFTER the big frame so its inner punch
    # cuts through the big frame's bottom band (interlocking).
    d.rounded_rectangle((224, 288, 416, 448), radius=20, fill=255)
    d.rounded_rectangle((258, 322, 382, 414), radius=10, fill=0)

    return mask


def draw_tile(fill: str, outline: str) -> Image.Image:
    """Full-canvas rounded tile (RGBA; corners outside the radius stay transparent)."""
    tile = Image.new("RGBA", (MASTER, MASTER), (0, 0, 0, 0))
    d = ImageDraw.Draw(tile)
    d.rounded_rectangle(
        (0, 0, MASTER - 1, MASTER - 1),
        radius=118,
        fill=fill,
        outline=outline,
        width=1,
    )
    return tile


def render_master(fill: str, outline: str, glyph: tuple[int, int, int, int]) -> Image.Image:
    """Composites the glyph color through the mask onto the tile (RGBA)."""
    mask = draw_glyph_mask()
    tile = draw_tile(fill, outline)

    glyph_rgba = Image.new("RGBA", (MASTER, MASTER), glyph[:3] + (0,))
    glyph_rgba.putalpha(mask)

    return Image.alpha_composite(tile, glyph_rgba)


def pack_png_ico(images: list[tuple[int, Image.Image]]) -> bytes:
    """PNG-in-ICO: 6-byte ICONDIR + one 16-byte ICONDIRENTRY per image + PNG blobs."""
    blobs = []
    for _, image in images:
        buf = io.BytesIO()
        image.save(buf, format="PNG")
        blobs.append(buf.getvalue())

    header = struct.pack("<HHH", 0, 1, len(images))
    offset = 6 + 16 * len(images)
    entries = []
    for (size, _), blob in zip(images, blobs):
        entries.append(
            struct.pack(
                "<BBBBHHII",
                0 if size >= 256 else size,  # width byte (0 means 256)
                0 if size >= 256 else size,  # height byte (0 means 256)
                0,                           # color count
                0,                           # reserved
                1,                           # planes
                32,                          # bit count
                len(blob),
                offset,
            )
        )
        offset += len(blob)

    return header + b"".join(entries) + b"".join(blobs)


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate wintangle.ico + previews")
    parser.add_argument("--preview-dir", default="/tmp/opencode/icon-preview/")
    args = parser.parse_args()

    preview_dir = args.preview_dir
    os.makedirs(preview_dir, exist_ok=True)

    master = render_master(TILE_FILL, TILE_OUTLINE, GLYPH_COLOR)
    dark_preview = os.path.join(preview_dir, "wintangle-icon-dark.png")
    master.save(dark_preview)

    light = render_master(LIGHT_TILE_FILL, LIGHT_TILE_OUTLINE, LIGHT_GLYPH_COLOR)
    light_preview = os.path.join(preview_dir, "wintangle-icon-light.png")
    light.save(light_preview)

    images = [(size, master.resize((size, size), Image.LANCZOS)) for size in SIZES]
    ico = pack_png_ico(images)
    with open(OUT_PATH, "wb") as f:
        f.write(ico)

    # Self-validation: parse the ICONDIR table (Pillow's ICO reader only
    # exposes the best-match frame, so the header is checked by hand) and
    # confirm each PNG blob decodes to the declared size. Also verify the
    # master has an opaque tile center.
    with open(OUT_PATH, "rb") as f:
        raw = f.read()
    reserved, ico_type, count = struct.unpack_from("<HHH", raw, 0)
    assert reserved == 0 and ico_type == 1, f"bad ICONDIR header: {reserved}, {ico_type}"
    assert count == len(SIZES), f"ico frame count mismatch: {count} != {len(SIZES)}"
    sizes = set()
    for i in range(count):
        entry = struct.unpack_from("<BBBBHHII", raw, 6 + 16 * i)
        size = entry[0] if entry[0] != 0 else 256
        sizes.add(size)
        length, offset = entry[6], entry[7]
        blob = raw[offset:offset + length]
        with Image.open(io.BytesIO(blob)) as png:
            assert png.size == (size, size), f"png blob {size} decodes as {png.size}"
    assert sizes == set(SIZES), f"ico sizes mismatch: {sizes} != {set(SIZES)}"

    center_alpha = master.getpixel((MASTER // 2, MASTER // 2))[3]
    assert center_alpha != 0, "tile-center alpha is 0"

    os.makedirs(os.path.dirname(DOCS_PREVIEW_PATH), exist_ok=True)
    master.save(DOCS_PREVIEW_PATH)

    print(f"wrote {OUT_PATH} ({len(ico)} bytes, sizes {sorted(sizes)})")
    print(f"wrote {dark_preview}")
    print(f"wrote {light_preview}")
    print(f"wrote {DOCS_PREVIEW_PATH}")
    print(f"validated: {len(sizes)} frames, tile-center alpha={center_alpha}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
