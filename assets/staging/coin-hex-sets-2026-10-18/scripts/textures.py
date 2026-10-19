"""Paints the texture maps for the three coin/hex cosmetic sets (numpy + Pillow, no Blender).

  python3 -I textures.py <out_dir> [size]

For every set it writes, per surface (coin heads, coin tails, hex tile):
  <set>_<surface>_BaseColor.png         sRGB albedo (cavity shading folded in)
  <set>_<surface>_Emission.png          sRGB glow colour, black where nothing glows
  <set>_<surface>_Normal.png            OpenGL (+Y up) tangent normal, Unity and Blender convention
  <set>_<surface>_Height.png            16-bit relief, used by build.py to displace the mesh
  <set>_<surface>_MetallicSmoothness.png Unity Standard/URP: metal in R, smoothness in A
  <set>_<surface>_MetallicRoughness.png glTF: roughness in G, metal in B
and two drop-in CoinSkin faces, <set>_heads.png and <set>_tails.png, with the relief and glow
baked in so they also read on the existing flat coin (UnityProof Resources/Coin/Skins).

Coordinates: each image is drawn in world units of its surface, +Y up.
  coin faces: the square spans the face disc (radius RF = 0.45 of the 1.0-across coin),
              the same planar UVs as docs/production/coin/coin.py, tails drawn as seen.
  hex tile:   pointy-top hex 2.0 across the flats; the square spans the corner-to-corner
              circle (radius 2/sqrt(3)). The strip at x < -1 (outside the hex) holds the
              side-wall band and the patch at the top-left corner holds the underside.
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

SQ3 = math.sqrt(3.0)
HEX_RC = 2.0 / SQ3


def gblur(a, sigma_px):
    """Separable Gaussian blur of a float array (edges clamped)."""
    if sigma_px < 0.3:
        return a
    r = int(math.ceil(sigma_px * 3))
    k = np.exp(-0.5 * (np.arange(-r, r + 1) / sigma_px) ** 2).astype(np.float32)
    k /= k.sum()
    out = a.astype(np.float32)
    for axis in (0, 1):
        pad = [(0, 0)] * out.ndim
        pad[axis] = (r, r)
        p = np.pad(out, pad, mode="edge")
        acc = np.zeros_like(out)
        for i, w in enumerate(k):
            sl = [slice(None)] * out.ndim
            sl[axis] = slice(i, i + out.shape[axis])
            acc += w * p[tuple(sl)]
        out = acc
    return out


def lerp(a, b, t):
    return a + (b - a) * t


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def srgb(c):
    """Hex colour or 0..1 tuple in sRGB -> linear-ish working values (we paint in sRGB, keep simple)."""
    if isinstance(c, str):
        c = tuple(int(c[i:i + 2], 16) / 255.0 for i in (1, 3, 5))
    return np.array(c, dtype=np.float32)


class Canvas:
    """A square of world space [-ext, ext]^2 with height, colour, glow, metal and roughness layers."""

    def __init__(self, size, ext, base, metal=0.0, rough=0.5):
        self.n, self.ext = size, ext
        ax = (np.arange(size, dtype=np.float32) + 0.5) / size * 2 - 1
        self.X = (ax * ext)[None, :].repeat(size, 0)
        self.Y = (-ax * ext)[:, None].repeat(size, 1)
        self.R = np.hypot(self.X, self.Y)
        self.T = np.arctan2(self.Y, self.X)
        self.H = np.zeros((size, size), np.float32)
        self.C = np.ones((size, size, 3), np.float32) * srgb(base)
        self.E = np.zeros((size, size, 3), np.float32)
        self.M = np.full((size, size), metal, np.float32)
        self.Ro = np.full((size, size), rough, np.float32)
        self.px = size / (2 * ext)   # pixels per world unit

    # ---------------------------------------------------------------- masks
    def ring(self, r0, r1, soft=None):
        s = soft if soft is not None else 1.5 / self.px
        return smooth(r0 - s, r0 + s, self.R) * (1 - smooth(r1 - s, r1 + s, self.R))

    def disc(self, r, soft=None, cx=0.0, cy=0.0):
        s = soft if soft is not None else 1.5 / self.px
        return 1 - smooth(r - s, r + s, np.hypot(self.X - cx, self.Y - cy))

    def hexd(self, scale=1.0):
        """Pointy-top hex distance: 1.0 on the edge of a hex `scale` across the flats... /2."""
        ax, ay = np.abs(self.X), np.abs(self.Y)
        return np.maximum(ax, 0.5 * ax + SQ3 / 2 * ay) / scale

    def band(self, d, d0, d1, soft=None):
        s = soft if soft is not None else 1.5 / self.px
        return smooth(d0 - s, d0 + s, d) * (1 - smooth(d1 - s, d1 + s, d))

    def _to_px(self, p):
        return ((p[0] / self.ext * 0.5 + 0.5) * self.n * 2, (0.5 - p[1] / self.ext * 0.5) * self.n * 2)

    def strokes(self, lines, width, blur=0.0, fill_polys=()):
        """Anti-aliased mask of polylines (world units) at 2x supersample. width in world units."""
        img = Image.new("L", (self.n * 2, self.n * 2), 0)
        dr = ImageDraw.Draw(img)
        w = max(1, int(round(width * self.px * 2)))
        for line in lines:
            pts = [self._to_px(p) for p in line]
            if len(pts) > 1:
                dr.line(pts, fill=255, width=w, joint="curve")
            r = w / 2
            for x, y in (pts[0], pts[-1]):
                dr.ellipse((x - r, y - r, x + r, y + r), fill=255)
        for poly in fill_polys:
            dr.polygon([self._to_px(p) for p in poly], fill=255)
        img = img.resize((self.n, self.n), Image.LANCZOS)
        return gblur(np.asarray(img, np.float32) / 255.0, blur * self.px)

    # ---------------------------------------------------------------- paint
    def paint(self, mask, color=None, metal=None, rough=None, emit=None, height=None, hmode="add"):
        m = mask[..., None]
        if color is not None:
            c = srgb(color) if not isinstance(color, np.ndarray) or color.ndim == 1 else color
            self.C = self.C * (1 - m) + c * m
        if metal is not None:
            self.M = self.M * (1 - mask) + metal * mask
        if rough is not None:
            self.Ro = self.Ro * (1 - mask) + rough * mask
        if emit is not None:
            e = srgb(emit) if not isinstance(emit, np.ndarray) or emit.ndim == 1 else emit
            self.E = np.maximum(self.E, e * m)
        if height is not None:
            if hmode == "add":
                self.H = self.H + height * mask
            elif hmode == "set":
                self.H = self.H * (1 - mask) + height * mask
            elif hmode == "max":
                self.H = np.maximum(self.H, height * mask)

    def blur_height(self, sigma_world):
        self.H = gblur(self.H, sigma_world * self.px)

    # ---------------------------------------------------------------- output
    def normal(self, strength):
        gy, gx = np.gradient(self.H * self.px)  # rows go down = -Y
        nx, ny = -gx * strength, gy * strength
        nz = np.ones_like(nx)
        l = np.sqrt(nx * nx + ny * ny + nz * nz)
        return np.stack([nx / l, ny / l, nz / l], -1)

    def cavity(self, sigma_world=0.01):
        return self.H - gblur(self.H, sigma_world * self.px)

    def save(self, out, name, hscale, nstrength):
        h = self.H
        cav = self.cavity()
        shade = np.clip(1 + cav / max(hscale, 1e-6) * 2.5, 0.55, 1.15)[..., None]
        base = np.clip(self.C * shade, 0, 1)
        nrm = self.normal(nstrength)
        smooth_ = 1 - self.Ro

        def u8(a):
            return Image.fromarray((np.clip(a, 0, 1) * 255 + 0.5).astype(np.uint8))

        u8(base).save(os.path.join(out, f"{name}_BaseColor.png"))
        u8(self.E).save(os.path.join(out, f"{name}_Emission.png"))
        u8(nrm * 0.5 + 0.5).save(os.path.join(out, f"{name}_Normal.png"))
        hh = np.clip(h / hscale, 0, 1)
        Image.fromarray((hh * 65535 + 0.5).astype(np.uint16)).save(os.path.join(out, f"{name}_Height.png"))
        ms = np.dstack([self.M, self.M, self.M, smooth_])
        Image.fromarray((np.clip(ms, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA").save(os.path.join(out, f"{name}_MetallicSmoothness.png"))
        mr = np.dstack([np.zeros_like(self.M), self.Ro, self.M])
        u8(mr).save(os.path.join(out, f"{name}_MetallicRoughness.png"))
        return base, nrm

    def flat_skin(self, base, nrm, path, mask):
        """Bake a key light over the relief and add the glow, for the existing flat coin."""
        L = np.array([-0.45, 0.55, 0.70], np.float32)
        L /= np.linalg.norm(L)
        ndl = np.clip((nrm * L).sum(-1), 0, 1)[..., None]
        V = np.array([0, 0, 1], np.float32)
        Hh = (L + V) / np.linalg.norm(L + V)
        spec = np.clip((nrm * Hh).sum(-1), 0, 1) ** (8 + 120 * (1 - self.Ro)) * (0.25 + 0.75 * self.M)
        lit = base * (0.35 + 0.85 * ndl) + spec[..., None] * lerp(np.ones(3), base, self.M[..., None]) * 0.9
        lit = lit + self.E * 1.1
        lit = np.clip(lit, 0, 1) * mask[..., None]
        Image.fromarray((lit * 255 + 0.5).astype(np.uint8)).save(path)


# ==================================================================== shape generators
def bolt(p0, p1, rng, depth=5, jitter=0.18):
    pts = [np.array(p0, float), np.array(p1, float)]
    for _ in range(depth):
        nxt = [pts[0]]
        for a, b in zip(pts, pts[1:]):
            d = b - a
            n = np.array([-d[1], d[0]])
            m = (a + b) / 2 + n * rng.uniform(-jitter, jitter)
            nxt += [m, b]
        pts = nxt
        jitter *= 0.55
    return [tuple(p) for p in pts]


def branched(p0, p1, rng, branches=4, depth=5, jitter=0.2, blen=0.45):
    main = bolt(p0, p1, rng, depth, jitter)
    out = [main]
    for _ in range(branches):
        i = rng.integers(len(main) // 5, len(main) * 4 // 5)
        a = np.array(main[i])
        d = np.array(p1) - np.array(p0)
        ang = math.atan2(d[1], d[0]) + rng.choice([-1, 1]) * rng.uniform(0.4, 0.9)
        L = np.linalg.norm(d) * blen * rng.uniform(0.5, 1.0)
        b = a + L * np.array([math.cos(ang), math.sin(ang)])
        out.append(bolt(a, b, rng, depth - 1, jitter * 0.8))
    return out


def polar_line(pts, r0, r1, t0, t1):
    """Map unit band coords (s along 0..1 -> angle t0..t1, h 0..1 -> radius r0..r1) to xy."""
    out = []
    for s, h in pts:
        t = lerp(t0, t1, s)
        r = lerp(r0, r1, h)
        out.append((r * math.cos(t), r * math.sin(t)))
    return out


def densify(pts, n=6):
    out = []
    for a, b in zip(pts, pts[1:]):
        for i in range(n):
            out.append((lerp(a[0], b[0], i / n), lerp(a[1], b[1], i / n)))
    out.append(pts[-1])
    return out


def meander_band(count, r0, r1, phase=0.0):
    """Greek-key band around a circle as polylines."""
    unit = [(0.0, 0.0), (0.0, 0.85), (0.62, 0.85), (0.62, 0.32), (0.3, 0.32), (0.3, 0.58)]
    lines = []
    for k in range(count):
        t0 = phase + 2 * math.pi * k / count
        t1 = phase + 2 * math.pi * (k + 1) / count
        lines.append(polar_line(densify(unit, 8), r0, r1, t0, t1))
    base = [(i / 256, 0.0) for i in range(257)]
    lines.append(polar_line(base, r0, r1, phase, phase + 2 * math.pi))
    return lines


def rune(rng, cx, cy, s, rot=0.0):
    """A procedural rune: a vertical stave plus two or three strokes on a 3x3 grid."""
    g = [(x, y) for y in (-1, 0, 1) for x in (-0.6, 0, 0.6)]
    strokes = [[(0, -1), (0, 1)]]
    pool = [((0, 1), (0.6, 0)), ((0, 1), (-0.6, 0)), ((0, 0), (0.6, -1)), ((0, 0), (-0.6, -1)), ((0, -1), (0.6, 0)),
            ((0, 0), (0.6, 1)), ((-0.6, 1), (0.6, -1)), ((0, 0.4), (0.6, 0.4)), ((0, -0.3), (-0.6, -0.3)),
            ((0, 1), (0.6, 0.55)), ((0.6, 0.55), (0, 0.1)), ((-0.6, 0), (0.6, 0))]
    for i in rng.choice(len(pool), rng.integers(2, 4), replace=False):
        strokes.append(list(pool[i]))
    c, sn = math.cos(rot), math.sin(rot)
    del g
    return [[(cx + s * (x * c - y * sn), cy + s * (x * sn + y * c)) for x, y in st] for st in strokes]


def spread(rng, n, lo, hi, min_d):
    """Up to n random points in a square with no two closer than min_d (keeps cracks from pooling)."""
    pts = []
    for _ in range(n * 40):
        p = (rng.uniform(lo, hi), rng.uniform(lo, hi))
        if all((p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2 >= min_d * min_d for q in pts):
            pts.append(p)
            if len(pts) == n:
                break
    return pts


def voronoi_f2f1(cv, pts):
    best = np.full(cv.X.shape, 1e9, np.float32)
    second = best.copy()
    for px, py in pts:
        d = np.hypot(cv.X - px, cv.Y - py)
        second = np.minimum(second, np.maximum(best, d))
        best = np.minimum(best, d)
    return second - best


def laurel(cv, r, count, leaf_len, leaf_w, t_start, t_end, side=1):
    polys = []
    for i in range(count):
        t = lerp(t_start, t_end, (i + 0.5) / count)
        for k in (-1, 1):
            ang = t + side * math.pi / 2 * 0.0
            # leaf points outward/inward from the stem at 35 degrees, along the stem direction
            tang = t + side * math.pi / 2
            lean = tang - side * k * math.radians(38)
            cx, cy = r * math.cos(t), r * math.sin(t)
            pts = []
            for j in range(13):
                u = j / 12
                along = u * leaf_len
                wid = math.sin(math.pi * u) * leaf_w * (1 if j < 13 else 0)
                pts.append((along, wid))
            pts += [(a, -w) for a, w in reversed(pts[1:-1])]
            ca, sa = math.cos(lean), math.sin(lean)
            off = (k * 0.35 * leaf_w * math.cos(t), k * 0.35 * leaf_w * math.sin(t))
            polys.append([(cx + off[0] + a * ca - w * sa, cy + off[1] + a * sa + w * ca) for a, w in pts])
            del ang
    return polys


# ==================================================================== sets
PAL = {
    "celestial": dict(white="#E9E6DF", marble2="#CFCBC2", gold="#D9A03A", gold_dark="#8A5E1C", royal="#13256E",
                      royal_deep="#0A1440", glow="#9CC4FF", glow_core="#EAF3FF"),
    "abyssal": dict(abyss="#07131F", navy="#0B2236", teal="#0F5560", deepteal="#063040", pearl="#E4EAE6",
                    pearl_pink="#F1DCE0", oldgold="#A8843D", cyan="#3FE0F5", cyan_soft="#1A8FA8"),
    "obsidian": dict(obsidian="#0B0A0D", obsidian2="#1A171D", iron="#3A3532", bronze="#7A4E2A", ember="#FF5A1A",
                     ember_hot="#FFC04A", ember_deep="#B3200A"),
}


def coin_canvas(size, base, metal, rough):
    return Canvas(size, 0.45, base, metal, rough)


# -------------------------------------------------------------- celestial lightning (Zeus)
def celestial_heads(size, rng):
    P = PAL["celestial"]
    cv = coin_canvas(size, P["gold"], 1.0, 0.28)
    r = cv.R / 0.45  # normalised face radius
    # outer gold band with greek key
    cv.paint(cv.ring(0.0, 0.47 * 0.45), P["royal"], 0.0, 0.32, height=0.0)
    key = cv.strokes(meander_band(20, 0.80 * 0.45, 0.955 * 0.45, 0.05), 0.012, blur=0.002)
    cv.paint(cv.ring(0.78 * 0.45, 1.01 * 0.45), P["gold"], 1.0, 0.30, height=0.004)
    cv.paint(key, P["gold"], 1.0, 0.22, height=0.0035)
    cv.paint(cv.ring(0.775 * 0.45, 0.80 * 0.45), P["gold_dark"], 1.0, 0.35, height=-0.0005)
    # white marble ring with twelve gold star studs and a fine glow channel
    cv.paint(cv.ring(0.58 * 0.45, 0.775 * 0.45), P["white"], 0.0, 0.42, height=0.0025)
    veins = cv.strokes([bolt((0.58 * 0.45 * math.cos(a), 0.58 * 0.45 * math.sin(a)),
                             (0.775 * 0.45 * math.cos(a + 0.4), 0.775 * 0.45 * math.sin(a + 0.4)), rng, 4, 0.25)
                        for a in rng.uniform(0, 2 * math.pi, 16)], 0.0015, blur=0.001)
    cv.paint(veins * cv.ring(0.58 * 0.45, 0.775 * 0.45) * 0.45, P["marble2"])
    for k in range(12):
        a = 2 * math.pi * k / 12 + math.pi / 12
        cx, cy = 0.675 * 0.45 * math.cos(a), 0.675 * 0.45 * math.sin(a)
        star = []
        for j in range(8):
            rr = 0.026 if j % 2 == 0 else 0.010
            aa = a + j * math.pi / 4
            star.append((cx + rr * math.cos(aa), cy + rr * math.sin(aa)))
        m = cv.strokes([], 0.0, fill_polys=[star])
        cv.paint(m, P["gold"], 1.0, 0.2, height=0.003)
    cv.paint(cv.ring(0.555 * 0.45, 0.585 * 0.45), P["gold"], 1.0, 0.25, height=0.0042)
    chan = cv.ring(0.535 * 0.45, 0.553 * 0.45)
    cv.paint(chan, P["royal_deep"], 0.0, 0.2, emit=P["glow"], height=-0.0008)
    # royal blue field with fractal lightning tendrils
    field = cv.disc(0.535 * 0.45)
    cv.paint(field, P["royal"], 0.15, 0.35, height=0.0)
    tend = []
    for k in range(9):
        a = 2 * math.pi * k / 9 + rng.uniform(-0.2, 0.2)
        tend += branched((0.05 * math.cos(a), 0.05 * math.sin(a)), (0.235 * math.cos(a), 0.235 * math.sin(a)), rng, 2, 5, 0.3, 0.35)
    tm = cv.strokes(tend, 0.0022, blur=0.0015) * field
    cv.paint(tm, P["glow"], 0.0, 0.2, emit=P["glow"], height=0.0004)
    halo = cv.strokes(tend, 0.010, blur=0.008) * field
    cv.paint(halo * 0.35, P["glow"], emit=np.array(srgb(P["glow"]) * 0.35))
    # the bolt: gold-trimmed, white-hot core
    B = [(-0.035, 0.205), (0.075, 0.205), (0.022, 0.045), (0.095, 0.045), (-0.065, -0.215), (-0.012, -0.03), (-0.085, -0.03)]
    Bs = [(x * 0.95, y * 0.95) for x, y in B]
    trim = cv.strokes([Bs + [Bs[0]]], 0.016, fill_polys=[Bs])
    core = cv.strokes([], 0, fill_polys=[[(x * 0.80 - 0.001, y * 0.80) for x, y in B]])
    core = gblur(core, 0.002 * cv.px)
    cv.paint(trim, P["gold"], 1.0, 0.18, height=0.0075, hmode="max")
    cv.paint(core, P["glow_core"], 0.0, 0.15, emit=P["glow_core"], height=0.0085, hmode="max")
    glowB = cv.strokes([], 0, fill_polys=[Bs])
    glowB = gblur(glowB, 0.02 * cv.px)
    cv.paint(glowB * field * (1 - trim) * 0.6, None, emit=np.array(srgb(P["glow"]) * 0.6))
    cv.blur_height(0.0008)
    return cv, r <= 1.0


def celestial_tails(size, rng):
    P = PAL["celestial"]
    cv = coin_canvas(size, P["white"], 0.0, 0.42)
    r = cv.R / 0.45
    cv.paint(cv.ring(0.86 * 0.45, 1.01 * 0.45), P["gold"], 1.0, 0.28, height=0.004)
    # beaded border
    for k in range(48):
        a = 2 * math.pi * k / 48
        d = cv.disc(0.0075, cx=0.93 * 0.45 * math.cos(a), cy=0.93 * 0.45 * math.sin(a))
        cv.paint(d, P["gold"], 1.0, 0.2, height=0.0055, hmode="max")
    cv.paint(cv.ring(0.84 * 0.45, 0.865 * 0.45), P["royal_deep"], 0.0, 0.2, emit=P["glow"], height=-0.0006)
    # royal field
    cv.paint(cv.disc(0.84 * 0.45), P["royal"], 0.15, 0.35, height=0.0)
    # gold laurel wreath, open at the top
    polys = laurel(cv, 0.69 * 0.45, 13, 0.055, 0.016, math.radians(100), math.radians(260), side=1)
    polys += laurel(cv, 0.69 * 0.45, 13, 0.055, 0.016, math.radians(80), math.radians(-80), side=-1)
    stem = [[(0.69 * 0.45 * math.cos(t), 0.69 * 0.45 * math.sin(t)) for t in np.linspace(math.radians(100), math.radians(260), 60)],
            [(0.69 * 0.45 * math.cos(t), 0.69 * 0.45 * math.sin(t)) for t in np.linspace(math.radians(80), math.radians(-80), 60)]]
    lm = cv.strokes(stem, 0.005, fill_polys=polys, blur=0.0015)
    cv.paint(lm, P["gold"], 1.0, 0.22, height=0.006, hmode="max")
    # storm eye: concentric white ring, eight-ray sunburst of lightning, glowing orb
    cv.paint(cv.ring(0.40 * 0.45, 0.47 * 0.45), P["white"], 0.0, 0.4, height=0.003)
    cv.paint(cv.ring(0.385 * 0.45, 0.405 * 0.45), P["gold"], 1.0, 0.2, height=0.0045)
    rays = []
    for k in range(8):
        a = 2 * math.pi * k / 8 + math.pi / 8
        rays.append(bolt((0.07 * math.cos(a), 0.07 * math.sin(a)), (0.17 * math.cos(a), 0.17 * math.sin(a)), rng, 4, 0.22))
    rm = cv.strokes(rays, 0.0035, blur=0.0015) * cv.disc(0.385 * 0.45)
    cv.paint(rm, P["glow"], 0.0, 0.2, emit=P["glow"], height=0.0006)
    cv.paint(cv.strokes(rays, 0.012, blur=0.008) * cv.disc(0.385 * 0.45) * 0.4, None, emit=np.array(srgb(P["glow"]) * 0.4))
    orb_h = np.sqrt(np.clip(1 - (cv.R / 0.062) ** 2, 0, 1))
    cv.paint(cv.disc(0.062), P["glow_core"], 0.0, 0.1, emit=P["glow_core"])
    cv.paint(cv.disc(0.062), None, height=orb_h * 0.008, hmode="max")
    cv.paint(cv.ring(0.062, 0.072), P["gold"], 1.0, 0.18, height=0.0065, hmode="max")
    # two small stars at the wreath opening (Player 2)
    for sx in (-0.03, 0.03):
        cx, cy = sx, 0.36
        star = [(cx + (0.016 if j % 2 == 0 else 0.0065) * math.cos(math.pi / 2 + j * math.pi / 5),
                 cy + (0.016 if j % 2 == 0 else 0.0065) * math.sin(math.pi / 2 + j * math.pi / 5)) for j in range(10)]
        cv.paint(cv.strokes([], 0, fill_polys=[star]), P["glow_core"], 0, 0.2, emit=P["glow"], height=0.004, hmode="max")
    cv.blur_height(0.0008)
    return cv, r <= 1.0


def celestial_tile(size, rng):
    P = PAL["celestial"]
    cv = Canvas(size, HEX_RC, P["white"], 0.0, 0.45)
    d = cv.hexd()
    # side-wall strip (x < -1): gold band between royal lines, and the underside patch
    strip = (cv.X < -1.0).astype(np.float32)
    u = (cv.X + HEX_RC) / (HEX_RC - 1.0)  # 0 at the bottom edge of the wall .. 1 at the top
    cv.paint(strip, P["royal_deep"], 0.0, 0.5)
    cv.paint(strip * cv.band(u, 0.55, 0.88), P["gold"], 1.0, 0.3)
    cv.paint(strip * cv.band(u, 0.66, 0.74), P["royal_deep"], 0.0, 0.2, emit=P["glow"])
    # top: chamfer gold edge, marble field, gold double inlay, royal inner hex
    cv.paint(cv.band(d, 0.955, 1.2), P["gold"], 1.0, 0.25, height=0.006)
    cv.paint(cv.band(d, 0.90, 0.955), P["white"], 0.0, 0.4, height=0.008)
    veins = cv.strokes([bolt((rng.uniform(-1, 1), rng.uniform(-1, 1)), (rng.uniform(-1, 1), rng.uniform(-1, 1)), rng, 6, 0.3)
                        for _ in range(26)], 0.002, blur=0.0015)
    cv.paint(veins * (d < 0.955) * 0.35, P["marble2"])
    cv.paint(cv.band(d, 0.86, 0.90), P["gold"], 1.0, 0.22, height=0.010)
    cv.paint(cv.band(d, 0.835, 0.86), P["royal_deep"], 0.0, 0.2, emit=P["glow"], height=0.004)
    cv.paint(cv.band(d, 0.80, 0.835), P["gold"], 1.0, 0.25, height=0.009)
    # corner rivets with lightning-arc spokes between them along the inlay
    corners = [(HEX_RC * math.cos(math.radians(30 + 60 * i)), HEX_RC * math.sin(math.radians(30 + 60 * i))) for i in range(6)]
    for i, (x, y) in enumerate(corners):
        cx, cy = x * 0.75, y * 0.75
        cv.paint(cv.disc(0.045, cx=cx, cy=cy), P["gold"], 1.0, 0.2, height=0.012, hmode="max")
        cv.paint(cv.disc(0.018, cx=cx, cy=cy), P["glow_core"], 0.0, 0.15, emit=P["glow"], height=0.013, hmode="max")
    # royal field inside, low glow circuitry of lightning paths from rivets toward the centre ring
    field = (d < 0.80).astype(np.float32)
    field = cv.band(d, -1, 0.80)
    cv.paint(field, P["royal"], 0.15, 0.4, height=0.004)
    paths = []
    for (x, y) in corners:
        paths += branched((x * 0.70, y * 0.70), (x * 0.32, y * 0.32), rng, 2, 5, 0.25, 0.5)
    pm = cv.strokes(paths, 0.006, blur=0.003) * field
    cv.paint(pm, P["glow"], 0.0, 0.2, emit=np.array(srgb(P["glow"]) * 0.8), height=0.0015)
    # centre: calm marble medallion in gold ring (units stand here)
    cv.paint(cv.disc(0.36), P["gold"], 1.0, 0.22, height=0.008, hmode="max")
    cv.paint(cv.disc(0.335), P["white"], 0.0, 0.38, height=0.007, hmode="set")
    cv.paint(cv.ring(0.24, 0.252), P["gold"], 1.0, 0.25, height=0.0085, hmode="max")
    cv.paint(veins * cv.disc(0.335) * 0.3, P["marble2"])
    cv.paint(strip * 0 + (cv.X < -1.0) * (cv.Y > 0.9), P["royal_deep"], 0.0, 0.6)
    cv.blur_height(0.002)
    return cv, None


# -------------------------------------------------------------- abyssal pearl and runes (Poseidon)
def pearl(cv, cx, cy, r, P, glow=False):
    d = np.hypot(cv.X - cx, cv.Y - cy)
    m = 1 - smooth(r - 1.5 / cv.px, r + 1.5 / cv.px, d)
    h = np.sqrt(np.clip(1 - (d / r) ** 2, 0, 1))
    # nacre: pearl white with a pink/teal sheen that shifts with the angle
    sheen = 0.5 + 0.5 * np.sin(np.arctan2(cv.Y - cy, cv.X - cx) * 2 + d / r * 3)
    col = lerp(srgb(P["pearl"]), srgb(P["pearl_pink"]), sheen[..., None] * 0.6)
    col = lerp(col, srgb("#CFE9EA"), (1 - h)[..., None] * 0.5)
    cv.paint(m, col, 0.15, 0.18)
    cv.paint(m, None, height=h * r * 0.55, hmode="max")
    if glow:
        cv.paint(m * h, None, emit=np.array(srgb(P["cyan"]) * 0.35))


def abyssal_heads(size, rng):
    P = PAL["abyssal"]
    cv = coin_canvas(size, P["deepteal"], 0.6, 0.35)
    r = cv.R / 0.45
    cv.paint(cv.ring(0.83 * 0.45, 1.01 * 0.45), P["teal"], 0.7, 0.3, height=0.003)
    for k in range(28):
        a = 2 * math.pi * k / 28
        pearl(cv, 0.915 * 0.45 * math.cos(a), 0.915 * 0.45 * math.sin(a), 0.0125, P)
    cv.paint(cv.ring(0.81 * 0.45, 0.83 * 0.45), P["oldgold"], 1.0, 0.3, height=0.004)
    # rune band, glowing cyan, set into navy
    cv.paint(cv.ring(0.62 * 0.45, 0.81 * 0.45), P["navy"], 0.2, 0.4, height=0.001)
    runes = []
    for k in range(18):
        a = 2 * math.pi * k / 18
        runes += rune(rng, 0.715 * 0.45 * math.cos(a), 0.715 * 0.45 * math.sin(a), 0.022, a - math.pi / 2)
    rm = cv.strokes(runes, 0.0045, blur=0.001)
    cv.paint(rm, P["cyan"], 0.0, 0.2, emit=P["cyan"], height=-0.0012)
    cv.paint(cv.strokes(runes, 0.016, blur=0.01) * 0.35 * cv.ring(0.62 * 0.45, 0.81 * 0.45), None, emit=np.array(srgb(P["cyan_soft"]) * 0.6))
    cv.paint(cv.ring(0.60 * 0.45, 0.62 * 0.45), P["oldgold"], 1.0, 0.3, height=0.004)
    # abyss field with wave scrolls
    field = cv.disc(0.60 * 0.45)
    cv.paint(field, P["abyss"], 0.3, 0.45, height=0.0)
    waves = []
    for row in range(-5, 6):
        y0 = row * 0.045
        pts = [(x, y0 + 0.012 * math.sin(x * 40 + row)) for x in np.linspace(-0.3, 0.3, 120)]
        waves.append(pts)
    wm = cv.strokes(waves, 0.0018, blur=0.001) * field
    cv.paint(wm * 0.7, P["deepteal"], height=0.0008)
    caust = cv.strokes(waves, 0.010, blur=0.007) * field
    cv.paint(caust * 0.18, None, emit=np.array(srgb(P["cyan_soft"]) * 0.5))
    # trident in pearl with glowing outline
    S = 1.0
    shaft = [(0, -0.22 * S), (0, 0.12 * S)]
    cross = [(-0.085, 0.05), (0.085, 0.05)]
    left = [(-0.085, 0.05), (-0.085, 0.16)]
    right = [(0.085, 0.05), (0.085, 0.16)]
    mid = [(0, 0.12), (0, 0.205)]
    heads_ = [[(-0.085, 0.19), (-0.105, 0.15), (-0.065, 0.15)], [(0.085, 0.19), (0.065, 0.15), (0.105, 0.15)],
              [(0, 0.235), (-0.022, 0.19), (0.022, 0.19)]]
    tri_lines = [shaft, cross, left, right, mid]
    out = cv.strokes(tri_lines, 0.034, fill_polys=[[(x * 1.25 - (0 if True else 0), y) for x, y in h] for h in heads_], blur=0.002)
    body = cv.strokes(tri_lines, 0.018, fill_polys=heads_, blur=0.001)
    cv.paint(out * field, P["cyan"], 0.0, 0.2, emit=P["cyan"], height=0.002, hmode="max")
    pc = lerp(srgb(P["pearl"]), srgb(P["pearl_pink"]), (0.5 + 0.5 * np.sin(cv.Y * 60))[..., None] * 0.5)
    cv.paint(body, pc, 0.15, 0.18, height=0.008, hmode="max")
    pearl(cv, 0, -0.03, 0.03, P, glow=True)
    pearl(cv, 0, -0.235, 0.022, P)
    cv.blur_height(0.0007)
    return cv, r <= 1.0


def abyssal_tails(size, rng):
    P = PAL["abyssal"]
    cv = coin_canvas(size, P["deepteal"], 0.6, 0.35)
    r = cv.R / 0.45
    cv.paint(cv.ring(0.85 * 0.45, 1.01 * 0.45), P["oldgold"], 1.0, 0.32, height=0.003)
    # wave crest border (scallops)
    sc = []
    for k in range(24):
        t0, t1 = 2 * math.pi * k / 24, 2 * math.pi * (k + 1) / 24
        sc.append([(0.90 * 0.45 * math.cos(lerp(t0, t1, s)) + 0.0 * s, 0.90 * 0.45 * math.sin(lerp(t0, t1, s))) for s in np.linspace(0, 1, 2)])
        pts = []
        for s in np.linspace(0, 1, 16):
            t = lerp(t0, t1, s)
            rr = (0.88 + 0.06 * math.sin(math.pi * s)) * 0.45
            pts.append((rr * math.cos(t), rr * math.sin(t)))
        sc.append(pts)
    cv.paint(cv.strokes(sc, 0.005, blur=0.001), P["pearl"], 0.2, 0.2, height=0.0045, hmode="max")
    cv.paint(cv.ring(0.82 * 0.45, 0.85 * 0.45), P["abyss"], 0.0, 0.2, emit=P["cyan_soft"], height=-0.0006)
    field = cv.disc(0.82 * 0.45)
    cv.paint(field, P["abyss"], 0.3, 0.45)
    # glowing rune circle
    runes = []
    for k in range(14):
        a = 2 * math.pi * k / 14 + 0.1
        runes += rune(rng, 0.70 * 0.45 * math.cos(a), 0.70 * 0.45 * math.sin(a), 0.019, a - math.pi / 2)
    cv.paint(cv.strokes(runes, 0.004, blur=0.001), P["cyan"], 0.0, 0.2, emit=P["cyan"], height=-0.001)
    cv.paint(cv.ring(0.6 * 0.45, 0.615 * 0.45), P["oldgold"], 1.0, 0.3, height=0.0035)
    # nautilus: whorls banded by spiral phase, each whorl bulging, a glowing cyan suture line
    a_, b_ = 0.010, 0.205
    shell = cv.disc(0.235)
    rr = np.maximum(cv.R, 1e-4)
    phase = (np.log(rr / a_) / b_ - cv.T) / (2 * math.pi)
    f = phase - np.floor(phase)                      # 0..1 across one whorl
    whorl = np.sin(math.pi * f)
    col = lerp(srgb(P["pearl_pink"]), srgb(P["pearl"]), whorl[..., None])
    col = lerp(col, srgb(P["teal"]), (1 - whorl)[..., None] ** 3 * 0.6)
    # chamber septa: faint ridges every 24 degrees of the curve
    sept = 0.5 + 0.5 * np.cos(cv.T * 15 + np.log(rr / a_) * 1.5)
    col = lerp(col, srgb("#BFA978"), (sept[..., None] > 0.97) * 0.5)
    cv.paint(shell, col, 0.2, 0.18)
    cv.paint(shell, None, height=0.003 + 0.0035 * whorl, hmode="set")
    suture = (1 - smooth(0.0, 0.06, np.minimum(f, 1 - f))) * shell * (cv.R > 0.012)
    cv.paint(suture, P["oldgold"], 1.0, 0.25, emit=np.array(srgb(P["cyan"]) * 0.9), height=0.0)
    cv.paint(cv.ring(0.235, 0.25), P["oldgold"], 1.0, 0.25, height=0.006, hmode="max")
    pearl(cv, 0.0, 0.0, 0.024, P, glow=True)
    # two pearls at the top (Player 2)
    for sx in (-0.03, 0.03):
        pearl(cv, sx, 0.355, 0.014, P, glow=True)
    cv.blur_height(0.0007)
    return cv, r <= 1.0


def abyssal_tile(size, rng):
    P = PAL["abyssal"]
    cv = Canvas(size, HEX_RC, P["abyss"], 0.2, 0.55)
    d = cv.hexd()
    strip = (cv.X < -1.0).astype(np.float32)
    u = (cv.X + HEX_RC) / (HEX_RC - 1.0)
    cv.paint(strip, P["navy"], 0.3, 0.5)
    cv.paint(strip * cv.band(u, 0.6, 0.86), P["teal"], 0.7, 0.3)
    cv.paint(strip * cv.band(u, 0.70, 0.76), P["abyss"], 0.0, 0.2, emit=P["cyan"])
    cv.paint(cv.band(d, 0.955, 1.2), P["oldgold"], 1.0, 0.3, height=0.006)
    cv.paint(cv.band(d, 0.82, 0.955), P["teal"], 0.6, 0.32, height=0.008)
    # rune band along the border
    runes = []
    for i in range(6):
        a0 = math.radians(30 + 60 * i)
        a1 = math.radians(30 + 60 * (i + 1))
        p0 = np.array([math.cos(a0), math.sin(a0)]) * HEX_RC * 0.885
        p1 = np.array([math.cos(a1), math.sin(a1)]) * HEX_RC * 0.885
        edge = p1 - p0
        rot = math.atan2(edge[1], edge[0])
        for k in range(1, 6):
            p = p0 + edge * k / 6
            runes += rune(rng, p[0], p[1], 0.032, rot)
    rm = cv.strokes(runes, 0.0085, blur=0.002) * cv.band(d, 0.82, 0.955)
    cv.paint(rm, P["cyan"], 0.0, 0.2, emit=P["cyan"], height=-0.003)
    cv.paint(cv.strokes(runes, 0.03, blur=0.02) * cv.band(d, 0.82, 0.955) * 0.3, None, emit=np.array(srgb(P["cyan_soft"]) * 0.6))
    cv.paint(cv.band(d, 0.795, 0.82), P["oldgold"], 1.0, 0.28, height=0.009)
    # pearls at the corners
    for i in range(6):
        a = math.radians(30 + 60 * i)
        pearl(cv, HEX_RC * 0.885 * math.cos(a), HEX_RC * 0.885 * math.sin(a), 0.05, P, glow=True)
    # abyss field with soft caustic lines (low glow)
    field = cv.band(d, -1, 0.795)
    cv.paint(field, P["navy"], 0.25, 0.5, height=0.003)
    pts = [(rng.uniform(-1.2, 1.2), rng.uniform(-1.2, 1.2)) for _ in range(30)]
    f = voronoi_f2f1(cv, pts)
    caust = (1 - smooth(0.0, 0.03, f)) * field
    cv.paint(caust * 0.55, P["deepteal"], height=0.0012)
    cv.paint(caust * 0.25 * (1 - cv.disc(0.40)), None, emit=np.array(srgb(P["cyan_soft"]) * 0.7))
    # centre: old-gold ring around a smooth navy pool (units stand here)
    cv.paint(cv.ring(0.36, 0.385), P["oldgold"], 1.0, 0.28, height=0.008, hmode="max")
    cv.paint(cv.disc(0.36), P["deepteal"], 0.5, 0.3, height=0.005, hmode="set")
    cv.paint((cv.X < -1.0) * (cv.Y > 0.9), P["abyss"], 0.0, 0.7)
    cv.blur_height(0.002)
    return cv, None


# -------------------------------------------------------------- obsidian ember cracks (neutral)
def ember_cracks(cv, pts, width, mask, P, strength=1.0, fine=True, rng=None):
    f = voronoi_f2f1(cv, pts)
    crack = 1 - smooth(0.0, width, f)
    core = 1 - smooth(0.0, width * 0.35, f)
    if fine and rng is not None:
        pts2 = spread(rng, len(pts) * 3, -cv.ext, cv.ext, cv.ext / math.sqrt(len(pts) * 3) * 0.7)
        f2 = voronoi_f2f1(cv, pts2)
        crack = np.maximum(crack, (1 - smooth(0.0, width * 0.45, f2)) * 0.6)
        core = np.maximum(core, (1 - smooth(0.0, width * 0.15, f2)) * 0.5)
    crack *= mask
    core *= mask
    col = lerp(srgb(P["ember_deep"]), srgb(P["ember"]), crack[..., None])
    col = lerp(col, srgb(P["ember_hot"]), core[..., None])
    cv.paint(crack, col, 0.0, 0.6, height=-0.004 * strength)
    emit = (lerp(srgb(P["ember_deep"]), srgb(P["ember"]), crack[..., None]) * crack[..., None]
            + srgb(P["ember_hot"]) * core[..., None] * 0.8) * strength
    cv.E = np.maximum(cv.E, emit)
    return crack


def obsidian_heads(size, rng):
    P = PAL["obsidian"]
    cv = coin_canvas(size, P["obsidian"], 0.0, 0.08)
    r = cv.R / 0.45
    cv.paint(cv.ring(0.84 * 0.45, 1.01 * 0.45), P["iron"], 0.9, 0.5, height=0.003)
    # notched iron band with bronze studs
    notches = []
    for k in range(36):
        a = 2 * math.pi * k / 36
        notches.append([(0.86 * 0.45 * math.cos(a), 0.86 * 0.45 * math.sin(a)), (0.99 * 0.45 * math.cos(a), 0.99 * 0.45 * math.sin(a))])
    cv.paint(cv.strokes(notches, 0.004, blur=0.001), P["obsidian2"], 0.5, 0.6, height=-0.0025)
    for k in range(6):
        a = 2 * math.pi * k / 6 + math.pi / 6
        cv.paint(cv.disc(0.016, cx=0.925 * 0.45 * math.cos(a), cy=0.925 * 0.45 * math.sin(a)), P["bronze"], 1.0, 0.3, height=0.006, hmode="max")
    cv.paint(cv.ring(0.82 * 0.45, 0.84 * 0.45), P["bronze"], 1.0, 0.3, height=0.004)
    face = cv.disc(0.82 * 0.45)
    cv.paint(face, P["obsidian"], 0.0, 0.07, height=0.002)
    # glassy faceting: subtle angular facets in height
    fpts = [(rng.uniform(-0.4, 0.4), rng.uniform(-0.4, 0.4)) for _ in range(22)]
    fac = voronoi_f2f1(cv, fpts)
    cv.paint(face * (np.clip(fac * 8, 0, 1) * 0.002), None, height=1.0)
    pts = spread(rng, 26, -0.4, 0.4, 0.07)
    ember_cracks(cv, pts, 0.006, face * (1 - cv.disc(0.20)), P, 1.0, True, rng)
    # central raised hex sigil in bronze, its own cracks brighter
    hd = np.maximum(np.abs(cv.X), 0.5 * np.abs(cv.X) + SQ3 / 2 * np.abs(cv.Y))
    cv.paint(cv.band(hd, -1, 0.175), P["bronze"], 1.0, 0.28, height=0.007, hmode="max")
    inner = cv.band(hd, -1, 0.15)
    cv.paint(inner, P["obsidian2"], 0.0, 0.06, height=0.0065, hmode="set")
    pts2 = spread(rng, 9, -0.15, 0.15, 0.05)
    ember_cracks(cv, pts2, 0.006, inner, P, 1.3, False)
    cv.paint(cv.disc(0.03), P["ember_hot"], 0.0, 0.3, emit=P["ember_hot"], height=0.004)
    cv.blur_height(0.0006)
    return cv, r <= 1.0


def obsidian_tails(size, rng):
    P = PAL["obsidian"]
    cv = coin_canvas(size, P["obsidian"], 0.0, 0.08)
    r = cv.R / 0.45
    cv.paint(cv.ring(0.84 * 0.45, 1.01 * 0.45), P["iron"], 0.9, 0.5, height=0.003)
    cv.paint(cv.ring(0.82 * 0.45, 0.84 * 0.45), P["bronze"], 1.0, 0.3, height=0.004)
    face = cv.disc(0.82 * 0.45)
    # concentric fractured rings: three obsidian terraces split by glowing seams
    for r0, r1, h in ((0.58, 0.82, 0.0015), (0.33, 0.56, 0.003), (0.0, 0.31, 0.0045)):
        cv.paint(cv.ring(r0 * 0.45, r1 * 0.45), P["obsidian"], 0.0, 0.07, height=h)
    seams = cv.ring(0.56 * 0.45, 0.58 * 0.45) + cv.ring(0.31 * 0.45, 0.33 * 0.45)
    cv.paint(seams, P["ember"], 0.0, 0.5, emit=P["ember"], height=-0.002)
    radial = []
    for k in range(10):
        a = 2 * math.pi * k / 10 + rng.uniform(-0.1, 0.1)
        radial.append(bolt((0.05 * math.cos(a), 0.05 * math.sin(a)), (0.36 * math.cos(a), 0.36 * math.sin(a)), rng, 5, 0.15))
    rmask = cv.strokes(radial, 0.004, blur=0.0015) * face
    hot = cv.strokes(radial, 0.0015) * face
    col = lerp(srgb(P["ember"]), srgb(P["ember_hot"]), hot[..., None])
    cv.paint(rmask, col, 0.0, 0.5, height=-0.003)
    cv.E = np.maximum(cv.E, srgb(P["ember"]) * rmask[..., None] + srgb(P["ember_hot"]) * hot[..., None] * 0.7)
    cv.paint(cv.strokes(radial, 0.014, blur=0.01) * face * 0.3, None, emit=np.array(srgb(P["ember_deep"]) * 0.8))
    # molten core
    core_h = np.sqrt(np.clip(1 - (cv.R / 0.05) ** 2, 0, 1))
    cv.paint(cv.disc(0.05), P["ember_hot"], 0.0, 0.3, emit=P["ember_hot"])
    cv.paint(cv.disc(0.05), None, height=core_h * 0.006 + 0.0045, hmode="max")
    cv.paint(cv.ring(0.05, 0.062), P["bronze"], 1.0, 0.25, height=0.0065, hmode="max")
    for sx in (-0.03, 0.03):
        cv.paint(cv.disc(0.012, cx=sx, cy=0.30), P["ember_hot"], 0.0, 0.3, emit=P["ember_hot"], height=0.005, hmode="max")
    cv.blur_height(0.0006)
    return cv, r <= 1.0


def obsidian_tile(size, rng):
    P = PAL["obsidian"]
    cv = Canvas(size, HEX_RC, P["obsidian"], 0.0, 0.1)
    d = cv.hexd()
    strip = (cv.X < -1.0).astype(np.float32)
    u = (cv.X + HEX_RC) / (HEX_RC - 1.0)
    cv.paint(strip, P["iron"], 0.9, 0.5)
    cv.paint(strip * cv.band(u, 0.62, 0.82), P["bronze"], 1.0, 0.3)
    cv.paint(strip * cv.band(u, 0.20, 0.26), P["obsidian"], 0.0, 0.5, emit=P["ember"])
    cv.paint(cv.band(d, 0.94, 1.2), P["iron"], 0.9, 0.45, height=0.006)
    cv.paint(cv.band(d, 0.905, 0.94), P["bronze"], 1.0, 0.3, height=0.009)
    face = cv.band(d, -1, 0.905)
    cv.paint(face, P["obsidian"], 0.0, 0.08, height=0.007)
    fpts = [(rng.uniform(-1.1, 1.1), rng.uniform(-1.1, 1.1)) for _ in range(28)]
    fac = voronoi_f2f1(cv, fpts)
    cv.paint(face * np.clip(fac * 3, 0, 1) * 0.004, None, height=1.0)
    pts = spread(rng, 30, -1.1, 1.1, 0.22)
    # cracks stay out of the calm centre so units read cleanly
    ember_cracks(cv, pts, 0.012, face * (1 - cv.disc(0.42, soft=0.08)), P, 1.0, True, rng)
    for i in range(6):
        a = math.radians(30 + 60 * i)
        cx, cy = HEX_RC * 0.92 * math.cos(a), HEX_RC * 0.92 * math.sin(a)
        cv.paint(cv.disc(0.04, cx=cx, cy=cy), P["bronze"], 1.0, 0.28, height=0.012, hmode="max")
    cv.paint(cv.ring(0.34, 0.36), P["bronze"], 1.0, 0.3, height=0.009, hmode="max")
    cv.paint((cv.X < -1.0) * (cv.Y > 0.9), P["iron"], 0.8, 0.6)
    cv.blur_height(0.002)
    return cv, None


SETS = {
    "celestial": (celestial_heads, celestial_tails, celestial_tile),
    "abyssal": (abyssal_heads, abyssal_tails, abyssal_tile),
    "obsidian": (obsidian_heads, obsidian_tails, obsidian_tile),
}
COIN_H = 0.012   # height map 0..1 covers 0..12 mm on the 1.0 coin
TILE_H = 0.03


def main(out, size):
    os.makedirs(out, exist_ok=True)
    for i, (sid, (fh, ft, ftile)) in enumerate(SETS.items()):
        for surf, fn in (("heads", fh), ("tails", ft), ("tile", ftile)):
            rng = np.random.default_rng(1000 * i + {"heads": 1, "tails": 2, "tile": 3}[surf])
            cv, inside = fn(size if surf != "tile" else size, rng)
            cv.H = np.clip(cv.H, 0, None)
            hs = COIN_H if surf != "tile" else TILE_H
            base, nrm = cv.save(out, f"{sid}_{surf}", hs, 1.0)
            if inside is not None:
                cv.flat_skin(base, nrm, os.path.join(out, f"{sid}_{surf}.png"), (cv.R <= 0.45 + 2 / cv.px).astype(np.float32))
            print("TEX", sid, surf, "hmax %.4f" % cv.H.max(), "glow px %.1f%%" % (100 * (cv.E.max(-1) > 0.05).mean()))


if __name__ == "__main__":
    main(sys.argv[1], int(sys.argv[2]) if len(sys.argv) > 2 else 1024)
