#!/usr/bin/env python3
"""Generate src/Wintangle.App/Assets/wintangle.ico (design: "sketch-tile-6").

Design derived from the user's source sketch (Assets/icon-source.jpg, a
656x656 dark tile carrying a bright window-frames glyph) by the approved
pipeline:

1. Corner cut — rounded-rect mask with R = round(W/6) = 109; corner circle
   centers at (R,R), (W-1-R,R), (R,H-1-R), (W-1-R,H-1-R). A pixel is cut
   (transparent) when it lies in a corner quadrant AND dist^2 > R^2, so the
   canvas corners fall away.
2. Edge strip — the outermost 4px on every side is transparent (x<4,
   x>W-5, y<4, y>H-5).
3. Background — clean vertical gradient TOP=45 -> BOT=32 per pixel:
   v = int(45 - 13*y/(H-1)), RGB (v,v,v).
4. Glyph glow — glyph mask = pixels with luminance >= 100 (within the base
   mask); glow = GaussianBlur(glyph mask, radius 8); tile lift =
   int(glow/255 * 16) added to the gradient value.
5. Glyph — the original source pixels where luminance >= 100 (not the
   gradient).

Deterministic: no randomness; fixed Pillow settings.

Outputs the PNG-in-ICO wintangle.ico with 7 sizes [16,24,32,48,64,128,256]
(LANCZOS downscale from the 656px master) plus a 512px dark preview copied to
docs/wintangle-icon.png. Run from anywhere (paths are resolved relative to
this file):

    python3 src/Wintangle.App/Assets/generate_icon.py [--preview-dir DIR]

Requires Pillow (pip install pillow).
"""

import argparse
import hashlib
import io
import os
import struct
import sys

from PIL import Image, ImageFilter

ASSETS_DIR = os.path.dirname(os.path.abspath(__file__))
SOURCE_PATH = os.path.join(ASSETS_DIR, "icon-source.jpg")
OUT_PATH = os.path.join(ASSETS_DIR, "wintangle.ico")
REPO_ROOT = os.path.abspath(os.path.join(ASSETS_DIR, "..", "..", ".."))
DOCS_PREVIEW_PATH = os.path.join(REPO_ROOT, "docs", "wintangle-icon.png")

SIZES = (16, 24, 32, 48, 64, 128, 256)
PREVIEW = 512

# Gradient endpoints (grayscale, the tile background).
GRADIENT_TOP = 45
GRADIENT_BOTTOM = 32
GLOW_RADIUS = 8
GLOW_LIFT = 16
GLYPH_LUM = 100
EDGE_STRIP = 4


def build_master() -> tuple[Image.Image, int]:
    """Renders the 656px master RGBA tile per the sketch-tile-6 pipeline.

    Returns (master, glyph_pixel_count) where glyph_pixel_count is the number
    of source pixels with luminance >= GLYPH_LUM within the base mask.
    """
    src = Image.open(SOURCE_PATH).convert("RGB")
    W, H = src.size
    assert W == H == 656, f"source must be 656x656, got {src.size}"

    R = round(W / 6)  # 109

    lum = src.convert("L")
    lum_px = lum.load()
    src_px = src.load()

    # 1+2. Base mask: rounded-rect corner cut + 4px edge strip.
    base = Image.new("L", (W, H), 255)
    base_px = base.load()
    quadrants = (
        ((0, R), (0, R), (R, R)),                       # TL
        ((W - 1 - R, W - 1), (0, R), (W - 1 - R, R)),   # TR
        ((0, R), (H - 1 - R, H - 1), (R, H - 1 - R)),   # BL
        ((W - 1 - R, W - 1), (H - 1 - R, H - 1), (W - 1 - R, H - 1 - R)),  # BR
    )
    for y in range(H):
        for x in range(W):
            if x < EDGE_STRIP or x > W - 1 - EDGE_STRIP or y < EDGE_STRIP or y > H - 1 - EDGE_STRIP:
                base_px[x, y] = 0
                continue
            for (x0, x1), (y0, y1), (cx, cy) in quadrants:
                if x0 <= x <= x1 and y0 <= y <= y1:
                    dx = x - cx
                    dy = y - cy
                    if dx * dx + dy * dy > R * R:
                        base_px[x, y] = 0
                    break

    # Glyph mask: source luminance >= 100 within the base mask.
    glyph = Image.new("L", (W, H), 0)
    glyph_px = glyph.load()
    glyph_count = 0
    for y in range(H):
        for x in range(W):
            if base_px[x, y] and lum_px[x, y] >= GLYPH_LUM:
                glyph_px[x, y] = 255
                glyph_count += 1

    # Glow: blurred glyph mask; lifts the gradient near the glyph.
    glow = glyph.filter(ImageFilter.GaussianBlur(radius=GLOW_RADIUS))
    glow_px = glow.load()

    # Composite: gradient background (+glow lift) with source pixels for glyph.
    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    out_px = out.load()
    for y in range(H):
        v = int(GRADIENT_TOP - (GRADIENT_TOP - GRADIENT_BOTTOM) * y / (H - 1))
        for x in range(W):
            if not base_px[x, y]:
                continue  # stays transparent
            if glyph_px[x, y]:
                r, g, b = src_px[x, y]
                out_px[x, y] = (r, g, b, 255)
            else:
                lift = int(glow_px[x, y] / 255 * GLOW_LIFT)
                t = v + lift
                out_px[x, y] = (t, t, t, 255)

    return out, glyph_count


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

    master, glyph_count = build_master()
    W, H = master.size

    dark_preview = os.path.join(preview_dir, "wintangle-icon-dark.png")
    preview = master.resize((PREVIEW, PREVIEW), Image.LANCZOS)
    preview.save(dark_preview)

    os.makedirs(os.path.dirname(DOCS_PREVIEW_PATH), exist_ok=True)
    preview.save(DOCS_PREVIEW_PATH)

    images = [(size, master.resize((size, size), Image.LANCZOS)) for size in SIZES]
    ico = pack_png_ico(images)
    with open(OUT_PATH, "wb") as f:
        f.write(ico)

    # Self-validation: parse the ICONDIR table (Pillow's ICO reader only
    # exposes the best-match frame, so the header is checked by hand) and
    # confirm each PNG blob decodes to the declared size.
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

    center_alpha = master.getpixel((W // 2, H // 2))[3]
    assert center_alpha == 255, f"tile-center alpha is {center_alpha}, expected 255"
    corner_tl = master.getpixel((5, 5))[3]
    corner_br = master.getpixel((W - 6, H - 6))[3]
    assert corner_tl == 0, f"corner (5,5) alpha is {corner_tl}, expected 0"
    assert corner_br == 0, f"corner ({W - 6},{H - 6}) alpha is {corner_br}, expected 0"

    md5 = hashlib.md5(ico).hexdigest()

    print(f"wrote {OUT_PATH} ({len(ico)} bytes, sizes {sorted(sizes)}, md5 {md5})")
    print(f"wrote {dark_preview}")
    print(f"wrote {DOCS_PREVIEW_PATH}")
    print(f"validated: {len(sizes)} ico frames, tile-center alpha={center_alpha}")
    print(f"validated: corners transparent (alpha 0 at (5,5) and ({W - 6},{H - 6}))")
    print(f"master: {W}x{H}, glyph pixels (lum>={GLYPH_LUM} within mask): {glyph_count}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
