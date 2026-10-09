"""AI-094 coin skins: draws the face textures for the reskinnable coin.

Every skin is two square PNGs, <skin>_heads.png and <skin>_tails.png. The coin's
face UVs map the whole square onto the face disc (the inscribed circle), so the
corners are never seen. Image top = coin +Z in Unity when the face is up.

  python3 -I skins.py <out_dir>

Writes the two built-in skins (olympus = Zeus heads / Poseidon tails, bronze =
plain I / II) plus coin_face_template.png, a guide for painting new skins.
"""
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

S = 1024          # drawn at 2x, saved at 512
OUT = 512
C = S / 2
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf"


def rgb(t):
    return tuple(int(round(255 * v)) for v in t)


def radial(inner, outer):
    img = Image.new("RGB", (S, S))
    px = img.load()
    for y in range(S):
        for x in range(S):
            r = min(1.0, math.hypot(x - C, y - C) / C)
            k = r * r
            px[x, y] = tuple(int(inner[i] + (outer[i] - inner[i]) * k) for i in range(3))
    return img


def ring(d, r0, r1, fill):
    d.ellipse([C - r1 * C, C - r1 * C, C + r1 * C, C + r1 * C], fill=fill)
    d.ellipse([C - r0 * C, C - r0 * C, C + r0 * C, C + r0 * C], fill=None)


def band(img, r0, r1, fill):
    """Fills the annulus r0..r1 (fractions of the radius)."""
    m = Image.new("L", (S, S), 0)
    md = ImageDraw.Draw(m)
    md.ellipse([C - r1 * C, C - r1 * C, C + r1 * C, C + r1 * C], fill=255)
    md.ellipse([C - r0 * C, C - r0 * C, C + r0 * C, C + r0 * C], fill=0)
    img.paste(Image.new("RGB", (S, S), fill), (0, 0), m)


def disc(img, r, fill):
    band(img, 0, r, fill)


def ticks(d, n, r0, r1, fill, width):
    for i in range(n):
        a = 2 * math.pi * i / n
        d.line([(C + r0 * C * math.cos(a), C + r0 * C * math.sin(a)),
                (C + r1 * C * math.cos(a), C + r1 * C * math.sin(a))], fill=fill, width=width)


def dots(d, n, r, size, fill, phase=0.0):
    for i in range(n):
        a = 2 * math.pi * (i + phase) / n
        x, y = C + r * C * math.cos(a), C + r * C * math.sin(a)
        d.ellipse([x - size, y - size, x + size, y + size], fill=fill)


def glow(img, shape_fn, color, blur):
    """Draws shape_fn on a mask, adds a blurred halo in color, then the solid shape."""
    m = Image.new("L", (S, S), 0)
    shape_fn(ImageDraw.Draw(m))
    halo = m.filter(ImageFilter.GaussianBlur(blur))
    img.paste(Image.new("RGB", (S, S), color), (0, 0), halo.point(lambda v: int(v * .85)))
    return m


def save(img, path):
    img.resize((OUT, OUT), Image.LANCZOS).save(path, optimize=True)


# Faction palettes from docs/production/FACTION_COLOR_GUIDE.md
MARBLE, GOLD, ROYAL, BOLT = (0.92, 0.91, 0.88), (0.85, 0.62, 0.22), (0.07, 0.14, 0.48), (0.78, 0.88, 1.00)
ABYSS, TEAL, CYAN, PEARL, OLDGOLD = (0.03, 0.09, 0.15), (0.05, 0.33, 0.40), (0.35, 0.90, 1.00), (0.84, 0.88, 0.87), (0.66, 0.52, 0.24)


def zeus_heads(path):
    img = radial(rgb(MARBLE), rgb((0.74, 0.73, 0.70)))
    band(img, .80, 1.0, rgb(GOLD))
    d = ImageDraw.Draw(img)
    ticks(d, 48, .83, .97, rgb((0.55, 0.38, 0.10)), 5)
    band(img, .78, .80, rgb((0.55, 0.38, 0.10)))
    dots(d, 12, .70, 9, rgb(GOLD))
    disc(img, .58, rgb(ROYAL))
    band(img, .56, .60, rgb(GOLD))
    # lightning bolt, top to bottom
    bolt = [(.10, -.48), (-.20, .04), (-.02, .04), (-.12, .48), (.22, -.08), (.03, -.08), (.16, -.48)]
    pts = [(C + x * C, C + y * C) for x, y in bolt]
    m = glow(img, lambda md: md.polygon(pts, fill=255), rgb((0.30, 0.55, 1.0)), 28)
    img.paste(Image.new("RGB", (S, S), rgb(BOLT)), (0, 0), m)
    save(img, path)


def poseidon_tails(path):
    img = radial(rgb((0.06, 0.17, 0.26)), rgb(ABYSS))
    band(img, .80, 1.0, rgb(TEAL))
    d = ImageDraw.Draw(img)
    # wave crests around the band
    for i in range(24):
        a = 2 * math.pi * i / 24
        x, y = C + .90 * C * math.cos(a), C + .90 * C * math.sin(a)
        d.arc([x - 34, y - 34, x + 34, y + 34], math.degrees(a) + 120, math.degrees(a) + 300, fill=rgb(PEARL), width=6)
    band(img, .78, .80, rgb(OLDGOLD))
    dots(d, 12, .70, 8, rgb(PEARL), .5)
    disc(img, .58, rgb((0.02, 0.18, 0.24)))
    band(img, .56, .60, rgb(PEARL))

    def trident(md):
        w = int(.05 * C)
        md.line([(C, C - .34 * C), (C, C + .48 * C)], fill=255, width=w)                         # shaft
        md.line([(C - .26 * C, C - .12 * C), (C + .26 * C, C - .12 * C)], fill=255, width=w)      # crossbar
        for x in (-.26, .26):
            md.line([(C + x * C, C - .12 * C), (C + x * C, C - .40 * C)], fill=255, width=w)     # side tines
        for x, top in ((-.26, -.48), (0, -.50), (.26, -.48)):                                    # barbed tips
            md.polygon([(C + (x - .07) * C, C - .36 * C), (C + x * C, C + top * C), (C + (x + .07) * C, C - .36 * C)], fill=255)
        md.ellipse([C - .07 * C, C + .02 * C, C + .07 * C, C + .16 * C], fill=255)              # grip
    m = glow(img, trident, rgb((0.0, 0.55, 0.80)), 26)
    img.paste(Image.new("RGB", (S, S), rgb(CYAN)), (0, 0), m)
    save(img, path)


def bronze(path, numeral):
    img = radial(rgb((0.78, 0.52, 0.30)), rgb((0.45, 0.27, 0.13)))
    band(img, .82, 1.0, rgb((0.58, 0.36, 0.18)))
    d = ImageDraw.Draw(img)
    ticks(d, 72, .86, .97, rgb((0.36, 0.21, 0.09)), 4)
    band(img, .64, .66, rgb((0.36, 0.21, 0.09)))
    dots(d, 16, .74, 7, rgb((0.92, 0.70, 0.42)))
    font = ImageFont.truetype(FONT, int(.62 * C))
    box = d.textbbox((0, 0), numeral, font=font)
    w, h = box[2] - box[0], box[3] - box[1]
    pos = (C - w / 2 - box[0], C - h / 2 - box[1])
    d.text((pos[0] + 6, pos[1] + 6), numeral, font=font, fill=rgb((0.30, 0.17, 0.07)))
    d.text(pos, numeral, font=font, fill=rgb((0.96, 0.78, 0.50)))
    save(img, path)


def template(path):
    img = Image.new("RGB", (S, S), (40, 40, 40))
    disc(img, 1.0, (200, 200, 200))
    d = ImageDraw.Draw(img)
    for r, col in ((.80, (230, 120, 60)), (.60, (60, 140, 230))):
        d.ellipse([C - r * C, C - r * C, C + r * C, C + r * C], outline=col, width=6)
    d.line([(C, 0), (C, S)], fill=(120, 120, 120), width=2)
    d.line([(0, C), (S, C)], fill=(120, 120, 120), width=2)
    font = ImageFont.truetype(FONT, 44)
    d.text((C - 60, 40), "TOP", font=font, fill=(255, 255, 255))
    d.text((40, C - 120), "outside the circle is never seen", font=ImageFont.truetype(FONT, 26), fill=(255, 255, 255))
    d.text((C - 150, C + .66 * C), "orange: rim band", font=ImageFont.truetype(FONT, 30), fill=(230, 120, 60))
    d.text((C - 150, C + .40 * C), "blue: emblem safe area", font=ImageFont.truetype(FONT, 30), fill=(60, 140, 230))
    save(img, path)


if __name__ == "__main__":
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    zeus_heads(os.path.join(out, "olympus_heads.png"))
    poseidon_tails(os.path.join(out, "olympus_tails.png"))
    bronze(os.path.join(out, "bronze_heads.png"), "I")
    bronze(os.path.join(out, "bronze_tails.png"), "II")
    template(os.path.join(out, "coin_face_template.png"))
    print("skins written to", out)
