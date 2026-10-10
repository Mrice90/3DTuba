"""Placeholder coin skin textures for AI-080-COIN-3D-PRESENTATION.

Writes PNGs into <overlay>/Assets/Playtest/Resources/CoinSkins/<Skin>/:
  front.png (seat 0, Zeus)  back.png (seat 1, Poseidon)  rim.png (wraps the edge, u = around).
Colors follow 3DTuba docs/production/FACTION_COLOR_GUIDE.md. The CapitalArtTest skin reuses the
existing capital card art already in the restoration Resources/CardArt folder (no new generation).

usage: python make_coin_textures.py <overlay root> <restoration UnityProof root>
"""
import math, os, sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont

FACE = 1024
RIM_W, RIM_H = 1024, 64
FONT = r"C:\Windows\Fonts\georgiab.ttf"

ZEUS_GOLD, ZEUS_GOLD_HI = (178, 154, 63), (255, 239, 157)
ZEUS_WHITE, ZEUS_BLUE, ZEUS_BOLT = (238, 241, 246), (30, 60, 140), (205, 230, 255)
POS_NAVY, POS_TEAL, POS_CYAN, PEARL = (8, 30, 46), (32, 137, 171), (139, 246, 255), (232, 228, 214)


def radial(size, inner, outer, power=1.0):
    im = Image.new("RGB", (size, size))
    px = im.load(); c = (size - 1) / 2
    for y in range(size):
        for x in range(size):
            t = min(1.0, math.hypot(x - c, y - c) / c) ** power
            px[x, y] = tuple(int(inner[i] + (outer[i] - inner[i]) * t) for i in range(3))
    return im


def ring(draw, c, r0, r1, color):
    draw.ellipse([c - r1, c - r1, c + r1, c + r1], fill=color)
    draw.ellipse([c - r0, c - r0, c + r0, c + r0], fill=None)


def rim_band(im, c, r_in, r_out, base, hi, ticks, tick_color):
    d = ImageDraw.Draw(im)
    d.ellipse([c - r_out, c - r_out, c + r_out, c + r_out], fill=base)
    d.ellipse([c - r_out + 10, c - r_out + 10, c + r_out - 10, c + r_out - 10], outline=hi, width=6)
    for i in range(ticks):
        a = 2 * math.pi * i / ticks
        p0 = (c + math.cos(a) * (r_in + 8), c + math.sin(a) * (r_in + 8))
        p1 = (c + math.cos(a) * (r_out - 22), c + math.sin(a) * (r_out - 22))
        d.line([p0, p1], fill=tick_color, width=5)
    d.ellipse([c - r_in, c - r_in, c + r_in, c + r_in], outline=hi, width=8)


def paste_disc(dst, src, c, r):
    mask = Image.new("L", dst.size, 0)
    ImageDraw.Draw(mask).ellipse([c - r, c - r, c + r, c + r], fill=255)
    dst.paste(src, (0, 0), mask)


def glow(im, shape_draw, color, blur):
    layer = Image.new("RGBA", im.size, (0, 0, 0, 0))
    shape_draw(ImageDraw.Draw(layer), color + (255,))
    layer = layer.filter(ImageFilter.GaussianBlur(blur))
    im.alpha_composite(layer)


def label(im, text, cy, size, fill, outline):
    d = ImageDraw.Draw(im)
    font = ImageFont.truetype(FONT, size)
    w = d.textlength(text, font=font)
    d.text(((FACE - w) / 2, cy - size * .6), text, font=font, fill=fill, stroke_width=6, stroke_fill=outline)


def bolt(cx, cy, s):
    pts = [(.10, -1.0), (-.42, .08), (-.06, .08), (-.22, 1.0), (.46, -.18), (.08, -.18), (.32, -1.0)]
    return [(cx + x * s, cy + y * s) for x, y in pts]


def trident(d, cx, cy, s, color):
    w = int(s * .09)
    d.line([(cx, cy - s * .55), (cx, cy + s * 1.0)], fill=color, width=w)          # shaft
    d.arc([cx - s * .55, cy - s * 1.15, cx + s * .55, cy - s * .05], 0, 180, fill=color, width=w)  # cross-guard
    for x in (-.55, 0, .55):
        tip = cy - s * (1.0 if x == 0 else .78)
        base = cy - s * .55 if x == 0 else cy - s * .6
        d.line([(cx + x * s, base), (cx + x * s, tip)], fill=color, width=w)
        d.polygon([(cx + x * s - w * 1.4, tip + w), (cx + x * s, tip - w * 2.2), (cx + x * s + w * 1.4, tip + w)], fill=color)
    d.ellipse([cx - w * 1.6, cy + s * .2 - w * 1.6, cx + w * 1.6, cy + s * .2 + w * 1.6], fill=color)


def zeus_face():
    c = FACE / 2
    im = radial(FACE, ZEUS_GOLD_HI, ZEUS_GOLD, 1.6).convert("RGBA")
    rim_band(im, c, c * .80, c, ZEUS_GOLD, ZEUS_GOLD_HI, 48, (120, 98, 30))
    field = radial(FACE, (255, 255, 255), (196, 206, 224), 1.4)
    paste_disc(im, field, c, c * .78)
    d = ImageDraw.Draw(im)
    d.ellipse([c - c * .78, c - c * .78, c + c * .78, c + c * .78], outline=ZEUS_BLUE, width=10)
    glow(im, lambda dd, col: dd.polygon(bolt(c, c * .86, c * .46), fill=col), (90, 160, 255), 22)
    d = ImageDraw.Draw(im)
    d.polygon(bolt(c, c * .86, c * .46), fill=ZEUS_BLUE, outline=ZEUS_BOLT)
    d.line(bolt(c, c * .86, c * .46) + [bolt(c, c * .86, c * .46)[0]], fill=ZEUS_GOLD, width=6)
    label(im, "ZEUS", c * 1.52, 150, ZEUS_BLUE, ZEUS_GOLD_HI)
    return im.convert("RGB")


def poseidon_face():
    c = FACE / 2
    im = radial(FACE, PEARL, (176, 160, 110), 1.6).convert("RGBA")
    rim_band(im, c, c * .80, c, (200, 190, 160), PEARL, 48, ZEUS_GOLD)
    field = radial(FACE, POS_TEAL, POS_NAVY, .9)
    paste_disc(im, field, c, c * .78)
    d = ImageDraw.Draw(im)
    d.ellipse([c - c * .78, c - c * .78, c + c * .78, c + c * .78], outline=POS_CYAN, width=10)
    glow(im, lambda dd, col: trident(dd, c, c * .88, c * .44, col), POS_CYAN, 18)
    trident(ImageDraw.Draw(im), c, c * .88, c * .44, (220, 252, 255))
    label(im, "POSEIDON", c * 1.52, 118, PEARL, POS_NAVY)
    return im.convert("RGB")


def art_face(art_path, ring_base, ring_hi, ticks, name, text_fill, text_outline):
    c = FACE / 2
    im = radial(FACE, ring_hi, ring_base, 1.6).convert("RGBA")
    rim_band(im, c, c * .80, c, ring_base, ring_hi, 32, text_outline)
    art = Image.open(art_path).convert("RGB")
    side = min(art.size)
    art = art.crop(((art.width - side) // 2, (art.height - side) // 2, (art.width + side) // 2, (art.height + side) // 2))
    art = art.resize((int(c * 1.56),) * 2, Image.LANCZOS)
    holder = Image.new("RGB", (FACE, FACE)); holder.paste(art, (int(c - c * .78),) * 2)
    paste_disc(im, holder, c, c * .78)
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([c - 330, c * 1.40, c + 330, c * 1.66], radius=40, fill=(0, 0, 0, 170))
    label(im, name, c * 1.53, 104, text_fill, text_outline)
    return im.convert("RGB")


def rim_tex(base, hi, lo, stripes):
    im = Image.new("RGB", (RIM_W, RIM_H), base)
    d = ImageDraw.Draw(im)
    step = RIM_W / stripes
    for i in range(stripes):
        x = i * step
        d.rectangle([x, 6, x + step * .35, RIM_H - 7], fill=hi)
        d.rectangle([x + step * .55, 6, x + step * .75, RIM_H - 7], fill=lo)
    d.rectangle([0, 0, RIM_W, 4], fill=hi); d.rectangle([0, RIM_H - 5, RIM_W, RIM_H], fill=hi)
    return im


def main(overlay, proof):
    root = os.path.join(overlay, "Assets", "Playtest", "Resources", "CoinSkins")
    art = os.path.join(proof, "Assets", "Playtest", "Resources", "CardArt")
    out = {
        "Default": {"front": zeus_face(), "back": poseidon_face(),
                    "rim": rim_tex(ZEUS_GOLD, ZEUS_GOLD_HI, (110, 90, 32), 96)},
        "CapitalArtTest": {
            "front": art_face(os.path.join(art, "zeus_capital_olympus_citadel.jpg"), ZEUS_GOLD, ZEUS_GOLD_HI, 32, "ZEUS", ZEUS_WHITE, ZEUS_BLUE),
            "back": art_face(os.path.join(art, "poseidon_capital_trident_bastion.jpg"), POS_TEAL, POS_CYAN, 32, "POSEIDON", PEARL, POS_NAVY),
            "rim": rim_tex((150, 170, 185), (225, 245, 255), (40, 90, 110), 64)},
    }
    for skin, faces in out.items():
        os.makedirs(os.path.join(root, skin), exist_ok=True)
        for name, im in faces.items():
            p = os.path.join(root, skin, name + ".png"); im.save(p); print(p, im.size)


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
