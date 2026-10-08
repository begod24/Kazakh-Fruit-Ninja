"""
Menu art for the new UI, built from the user's mockup pieces in Assets/_Game/UI/New UI.

- Mode cards: the baked English/Kazakh captions are painted out (the game draws them in the chosen language).
- Mode thumbnails: the cards' pictures, with rounded corners, for the records panel.
- Round buttons and the heart: redrawn at 4x in the same style (the originals are 82 px circles, soft on a phone).
- Ornaments, frames and small icons: white masks, tinted in Unity.

Run from anywhere:  python3 ArtSource/UI/build_menu_art.py
Writes to Assets/_Game/UI/Sprites/Menu.
"""
import math
import os

import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "Assets/_Game/UI/New UI")
OUT = os.path.join(ROOT, "Assets/_Game/UI/Sprites/Menu")

GOLD = (232, 199, 120)
ICON_LINE = (190, 163, 102)
BUTTON_FILL = (18, 21, 14)
HEART = (200, 50, 48)


# ---------- SDF helpers (pixels, y down, negative inside) ----------

def grid(w, h):
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float64)
    return xs + 0.5, ys + 0.5


def cover(d):
    """Anti-aliased coverage of the inside of a distance field."""
    return np.clip(0.5 - d, 0.0, 1.0)


def circle(x, y, cx, cy, r):
    return np.hypot(x - cx, y - cy) - r


def segment(x, y, a, b):
    ax, ay = a
    bx, by = b
    abx, aby = bx - ax, by - ay
    t = np.clip(((x - ax) * abx + (y - ay) * aby) / max(abx * abx + aby * aby, 1e-9), 0.0, 1.0)
    return np.hypot(x - (ax + abx * t), y - (ay + aby * t))


def polyline(x, y, points):
    d = np.full(x.shape, 1e9)
    for a, b in zip(points, points[1:]):
        d = np.minimum(d, segment(x, y, a, b))
    return d


def box(x, y, cx, cy, hw, hh, r):
    qx = np.abs(x - cx) - hw + r
    qy = np.abs(y - cy) - hh + r
    outside = np.hypot(np.maximum(qx, 0), np.maximum(qy, 0))
    return outside + np.minimum(np.maximum(qx, qy), 0) - r


def spiral(cx, cy, radius, start, direction, turns=6.0, shrink=0.26):
    pts = []
    t = 0.0
    while t <= turns:
        r = radius * math.exp(-shrink * t)
        a = start + direction * t
        pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
        t += 0.05
    return pts


def save_mask(name, alpha):
    img = np.zeros(alpha.shape + (4,), np.uint8)
    img[..., :3] = 255
    img[..., 3] = np.round(np.clip(alpha, 0, 1) * 255).astype(np.uint8)
    Image.fromarray(img).save(os.path.join(OUT, name))


def save_rgba(name, layers, shape):
    """Composites (colour, alpha) layers bottom-up, premultiplied maths, straight-alpha output."""
    h, w = shape
    rgb = np.zeros((h, w, 3))
    a = np.zeros((h, w))
    for color, alpha in layers:
        c = np.array(color[:3], np.float64) / 255.0
        la = np.clip(alpha, 0, 1) * (color[3] / 255.0 if len(color) > 3 else 1.0)
        rgb = c[None, None, :] * la[..., None] + rgb * (1 - la[..., None])
        a = la + a * (1 - la)
    out = np.zeros((h, w, 4), np.uint8)
    safe = np.where(a > 1e-6, a, 1.0)
    out[..., :3] = np.round(np.clip(rgb / safe[..., None], 0, 1) * 255).astype(np.uint8)
    out[..., 3] = np.round(np.clip(a, 0, 1) * 255).astype(np.uint8)
    Image.fromarray(out).save(os.path.join(OUT, name))


# ---------- Pieces taken from the mockup ----------

def mode_cards():
    for src, dst, thumb in [("Classic Mod.png", "ModeClassic.png", "ThumbClassic.png"),
                            ("Arcade mod.png", "ModeArcade.png", "ThumbArcade.png")]:
        im = np.array(Image.open(os.path.join(SOURCE, src)).convert("RGBA"))
        flat = im[430, 100].copy()
        # The captions sit between the gold divider (rows 412-413) and the bottom border; the band is one flat colour.
        im[436:534, 56:516] = flat
        Image.fromarray(im).save(os.path.join(OUT, dst))

        pic = im[61:397, 48:524].astype(np.float64)
        h, w = pic.shape[:2]
        x, y = grid(w, h)
        mask = cover(box(x, y, w / 2, h / 2, w / 2, h / 2, 18))
        pic[..., 3] = 255 * mask
        Image.fromarray(np.round(pic).astype(np.uint8)).save(os.path.join(OUT, thumb))


# ---------- Round buttons (redrawn at 4x) ----------

S = 320  # canvas; the circle is 300 across, like the mockup's 82 px circle x3.66
C = S / 2
LINE = 2.1  # half width of the icon lines


def round_button(name, icon):
    x, y = grid(S, S)
    d = np.hypot(x - C, y - C)
    fill = cover(d - 149)
    ring = cover(np.abs(d - 146) - 3.8)
    save_rgba(name, [(BUTTON_FILL + (204,), fill), (GOLD, ring), (ICON_LINE, icon(x - C, y - C))], (S, S))


def sword_parts(x, y, flip):
    """One straight sword drawn in outline, tip at the top; returns (lines, blade area)."""
    s2 = math.sqrt(0.5)
    d = (-s2, s2) if flip else (s2, s2)  # from tip towards the hilt
    n = (-d[1], d[0])
    tip = (-d[0] * 72, -d[1] * 72)

    def at(t, side=0.0):
        return (tip[0] + d[0] * t + n[0] * side, tip[1] + d[1] * t + n[1] * side)

    w = 9.0
    lines = []
    lines.append(segment(x, y, at(0), at(16, w)))
    lines.append(segment(x, y, at(0), at(16, -w)))
    lines.append(segment(x, y, at(16, w), at(100, w)))
    lines.append(segment(x, y, at(16, -w), at(100, -w)))
    lines.append(segment(x, y, at(100, -24), at(100, 24)))    # guard
    lines.append(segment(x, y, at(100, -24), at(106, -24)))
    lines.append(segment(x, y, at(100, 24), at(106, 24)))
    lines.append(segment(x, y, at(100), at(134)))               # grip
    lines.append(segment(x, y, at(134, -8), at(134, 8)))        # pommel
    dist = np.minimum.reduce(lines)
    # Blade area (for hiding the sword behind), slightly larger than the outline.
    px, py = x - tip[0], y - tip[1]
    along = px * d[0] + py * d[1]
    across = np.abs(px * n[0] + py * n[1])
    area = (along > -4) & (along < 108) & (across < w + 5)
    return dist, area.astype(np.float64)


def swords_icon(x, y):
    back, _ = sword_parts(x, y, True)
    front, front_area = sword_parts(x, y, False)
    back_cov = cover(back - LINE) * (1 - front_area)
    return np.maximum(back_cov, cover(front - LINE))


def trophy_icon(x, y):
    y = y + 4
    parts = []
    rim = -52
    # Bowl: straight sides that round into a narrow waist.
    left = [(-40, rim)] + [(-40 + 32 * (1 - math.cos(t * math.pi / 2)), rim + 30 + 34 * math.sin(t * math.pi / 2)) for t in np.linspace(0, 1, 24)]
    left = [(-40, rim), (-40, rim + 30)] + left[2:]
    right = [(-px, py) for px, py in left]
    parts.append(polyline(x, y, left))
    parts.append(polyline(x, y, right))
    parts.append(segment(x, y, (-40, rim), (40, rim)))
    # Handles: half rings outside the bowl.
    for side in (-1, 1):
        cx = side * 44
        ring = np.abs(np.hypot(x - cx, y - (rim + 18)) - 14)
        ring = np.where(x * side > 40, ring, 1e9)
        parts.append(ring)
    # Stem: waist, then flaring down to the foot.
    waist = rim + 64
    parts.append(segment(x, y, (-8, waist), (-6, waist + 10)))
    parts.append(segment(x, y, (8, waist), (6, waist + 10)))
    foot = [(-6, waist + 10)] + [(-6 - 20 * t * t, waist + 10 + 26 * t) for t in np.linspace(0, 1, 16)]
    parts.append(polyline(x, y, foot))
    parts.append(polyline(x, y, [(-px, py) for px, py in foot]))
    parts.append(segment(x, y, (-42, waist + 37), (42, waist + 37)))
    return cover(np.minimum.reduce(parts) - LINE)


def gear_icon(x, y):
    r = np.hypot(x, y)
    a = np.arctan2(y, x) + math.pi / 2
    lobes, base, depth = 6, 43.0, 10.0
    wave = 0.5 + 0.5 * np.cos(a * lobes)
    radius = base + depth * wave
    slope = -0.5 * depth * lobes * np.sin(a * lobes)
    outline = np.abs(r - radius) / np.sqrt(1 + (slope / np.maximum(r, 1)) ** 2)
    hole = np.abs(r - 16)
    return cover(np.minimum(outline, hole) - LINE)


# ---------- Heart (the mockup's Life.png at 4x) ----------

def heart():
    w, h = 216, 192
    x, y = grid(w, h)
    # Inigo Quilez's heart: tip at (0,0), about 1.1 tall, y up.
    scale = 150.0
    px = np.abs(x - w / 2) / scale
    py = (h - 22 - y) / scale
    upper = np.hypot(px - 0.25, py - 0.75) - math.sqrt(2) / 4
    m = np.maximum(px + py, 0) * 0.5
    lower = np.sqrt(np.minimum((px) ** 2 + (py - 1.0) ** 2, (px - m) ** 2 + (py - m) ** 2)) * np.sign(px - py)
    d = np.where(py + px > 1.0, upper, lower) * scale
    stroke = cover(np.abs(d + 4) - 5.0)
    inside = cover(d + 4) * 0.22
    # A soft dark halo outside keeps the thin outline readable on the bright steppe and the night sky alike.
    shadow = np.clip(1 - (d + 4) / 16, 0, 1) ** 2 * 0.45 * (d + 4 > 0)
    save_rgba("Heart.png", [((20, 4, 4, 255), shadow), (HEART, inside), (HEART, stroke)], (h, w))


# ---------- White masks ----------

def frames():
    x, y = grid(128, 128)
    d = box(x, y, 64, 64, 63, 63, 40)
    # Same outline as UI/Sprites/RoundedRect.png, so a ring sits exactly on a fill.
    save_mask("RingRect.png", cover(np.maximum(d, -(d + 3.2))))
    d = np.hypot(x - 64, y - 64) - 62
    # Same size as UI/Sprites/Circle.png.
    save_mask("RingCircle.png", cover(np.maximum(d, -(d + 3.2))))
    d = box(x, y, 64, 64, 36, 36, 26)
    t = np.clip((d + 12) / 40, 0, 1)
    save_mask("ShadowRect.png", (1 - t * t * (3 - 2 * t)))


def divider():
    w, h = 640, 24
    x, y = grid(w, h)
    cx, cy = w / 2, h / 2
    fade = np.clip(1 - np.abs(x - cx) / (cx - 4), 0, 1) ** 0.6
    line = cover(np.abs(y - cy) - 0.9) * fade
    diamond = cover((np.abs(x - cx) + np.abs(y - cy)) * 0.7071 - 5.5)
    gap = 1 - cover((np.abs(x - cx) + np.abs(y - cy)) * 0.7071 - 9.5)
    save_mask("Divider.png", np.maximum(line * gap, diamond))
    save_mask("LineFade.png", cover(np.abs(y - cy) - 1.1) * fade)


def crest():
    """Қошқар мүйіз crest for panel headers: a stem that splits into two ram's horns, a bud on top."""
    w, h = 256, 120
    x, y = grid(w, h)
    cx = w / 2
    parts = [segment(x, y, (cx, 104), (cx, 70))]
    right = [(cx, 70)] + [(cx + 4 + 30 * (1 - math.cos(t * math.pi / 2)), 70 - 30 * math.sin(t * math.pi / 2)) for t in np.linspace(0, 1, 12)]
    parts.append(polyline(x, y, right))
    parts.append(polyline(x, y, [(2 * cx - px, py) for px, py in right]))
    parts.append(polyline(x, y, spiral(cx + 58, 58, 24, -math.pi / 2 - 0.3, 1.0, 5.2)))
    parts.append(polyline(x, y, spiral(cx - 58, 58, 24, -math.pi / 2 + 0.3, -1.0, 5.2)))
    lines = cover(np.minimum.reduce(parts) - 3.0)
    # Bud: a pointed drop above the fork.
    by = 40.0
    bud = np.maximum(np.hypot(x - cx, y - by - 8) - 12, (np.abs(x - cx) * 1.4 + (y - (by - 22)) * -0.45) - 6)
    bud = np.minimum(bud, circle(x, y, cx, by + 8, 11))
    dots = np.minimum(circle(x, y, cx - 100, 88, 5), circle(x, y, cx + 100, 88, 5))
    tail = cover(np.minimum(segment(x, y, (cx - 92, 88), (cx - 30, 104)), segment(x, y, (cx + 92, 88), (cx + 30, 104))) - 1.6)
    save_mask("Crest.png", np.maximum.reduce([lines, cover(bud), cover(dots), tail]))


def flourish():
    """Line with a ram's-horn curl at its right end; mirrored for the other side of a title."""
    w, h = 360, 72
    x, y = grid(w, h)
    cy = h / 2
    fade = np.clip((x - 6) / 180, 0, 1)
    line = cover(np.abs(y - cy) - 1.3) * fade * (x < 300)
    curl_up = spiral(318, cy - 12, 14, math.pi / 2, -1.0, 4.6, 0.3)
    curl_down = spiral(318, cy + 12, 14, -math.pi / 2, 1.0, 4.6, 0.3)
    curls = cover(np.minimum(polyline(x, y, [(292, cy)] + curl_up), polyline(x, y, [(292, cy)] + curl_down)) - 2.4)
    diamond = cover((np.abs(x - 344) + np.abs(y - cy)) * 0.7071 - 4.5)
    save_mask("Flourish.png", np.maximum.reduce([line, curls, diamond]))


def small_icons():
    x, y = grid(128, 128)
    arrow = np.minimum.reduce([segment(x, y, (30, 64), (100, 64)), segment(x, y, (30, 64), (60, 34)), segment(x, y, (30, 64), (60, 94))])
    save_mask("Arrow.png", cover(arrow - 5))
    check = polyline(x, y, [(28, 66), (54, 92), (102, 38)])
    save_mask("Check.png", cover(check - 8))
    shackle = np.where(y < 62, np.abs(np.hypot(x - 64, y - 58) - 24), 1e9)
    shackle = np.minimum(shackle, np.minimum(segment(x, y, (40, 58), (40, 66)), segment(x, y, (88, 58), (88, 66))))
    body = box(x, y, 64, 86, 36, 28, 8)
    keyhole = np.minimum(circle(x, y, 64, 80, 7), box(x, y, 64, 92, 3.5, 10, 2))
    save_mask("Lock.png", np.maximum(np.minimum(cover(shackle - 7) + cover(body), 1), 0) * (1 - cover(keyhole)))


def main():
    os.makedirs(OUT, exist_ok=True)
    mode_cards()
    round_button("ButtonBlades.png", swords_icon)
    round_button("ButtonRecords.png", trophy_icon)
    round_button("ButtonSettings.png", gear_icon)
    heart()
    frames()
    divider()
    crest()
    flourish()
    small_icons()
    print("menu art written to", OUT)


if __name__ == "__main__":
    main()
