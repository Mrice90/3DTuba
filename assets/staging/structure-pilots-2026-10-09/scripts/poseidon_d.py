"""Poseidon structures, batch D. Each builder only adds parts via kit."""
from kit import box, cyl, sphere, dome, torus, tube, helix, arc
import math

TAU = math.tau
HEX30 = 0.0  # bmesh hexes are already pointy-top (corners at +-Y)
TOP = 0.14   # plinth top


# ---------------------------------------------------------------- shared parts
def plinth(seabed="abyss"):
    cyl("rock", 0.80, 0.87, 0.10, 0.0, seg=6, rot=(0, 0, HEX30), name="rock")
    cyl(seabed, 0.82, 0.80, 0.04, 0.10, seg=6, rot=(0, 0, HEX30), name="seabed")
    cyl("oldgold", 0.823, 0.823, 0.012, 0.115, seg=6, rot=(0, 0, HEX30), name="seabed_trim")
    return TOP


def rock_lumps(spots):
    for (x, y, r) in spots:
        sphere("rock", r, (x, y, TOP - 0.01), seg=8, rings=5, scale=(1.2, 1.0, 0.6), name="lump")


def trident(x, y, z0, h, sw="oldgold", r=0.008, face=0.0):
    """Upright trident; prongs spread along the direction perpendicular to face (rad about Z)."""
    cyl(sw, r, r, h, z0, xy=(x, y), seg=6, name="trident_shaft")
    zt = z0 + h
    ux, uy = math.cos(face), math.sin(face)
    w = 0.045 * h / 0.5
    box(sw, 2 * w + 0.01, 0.012, 0.012, (x, y, zt), rot=(0, 0, face), name="trident_bar")
    for s in (-1, 0, 1):
        px, py = x + s * w * ux, y + s * w * uy
        cyl(sw, r * 1.1, 0.0, 0.10 * h / 0.5 + (0.02 if s == 0 else 0), zt, xy=(px, py), seg=6, name="trident_prong")
    cyl(sw, r * 2.2, r * 2.2, 0.012, zt - 0.03, xy=(x, y), seg=8, name="trident_collar")


def statue(x, y, z0, s=1.0, sw="pearl", trident_side=1, face=-math.pi / 2):
    """Simplified heroic figure facing -Y, holding a trident at its side."""
    cyl("deepteal", 0.085 * s, 0.09 * s, 0.07 * s, z0, xy=(x, y), seg=8, name="statue_plinth")
    cyl("oldgold", 0.092 * s, 0.092 * s, 0.012 * s, z0 + 0.05 * s, xy=(x, y), seg=8, name="statue_plinth_trim")
    zb = z0 + 0.07 * s
    for d in (-1, 1):
        cyl(sw, 0.022 * s, 0.03 * s, 0.17 * s, zb, xy=(x + d * 0.032 * s, y), seg=8, name="leg")
    # robe / kilt
    cyl(sw, 0.07 * s, 0.05 * s, 0.07 * s, zb + 0.10 * s, xy=(x, y), seg=10, name="kilt")
    cyl(sw, 0.05 * s, 0.07 * s, 0.14 * s, zb + 0.17 * s, xy=(x, y), seg=10, name="torso")
    sphere(sw, 0.035 * s, (x - 0.06 * s, y, zb + 0.30 * s), seg=8, rings=5, name="shoulder")
    sphere(sw, 0.035 * s, (x + 0.06 * s, y, zb + 0.30 * s), seg=8, rings=5, name="shoulder")
    cyl(sw, 0.02 * s, 0.02 * s, 0.03 * s, zb + 0.31 * s, xy=(x, y), seg=8, name="neck")
    sphere(sw, 0.036 * s, (x, y, zb + 0.37 * s), seg=10, rings=6, name="head")
    cyl("oldgold", 0.03 * s, 0.038 * s, 0.03 * s, zb + 0.39 * s, xy=(x, y), seg=10, name="crown")
    for k in range(5):
        a = TAU * k / 10 + math.pi / 2 + TAU / 10 * -2
        cyl("oldgold", 0.006 * s, 0.0, 0.04 * s, zb + 0.415 * s, xy=(x + 0.03 * s * math.cos(a + math.pi), y + 0.03 * s * math.sin(a + math.pi)), seg=4, name="crown_spike")
    sphere(sw, 0.03 * s, (x, y - 0.025 * s, zb + 0.335 * s), seg=8, rings=6, scale=(1, 0.6, 1.2), name="beard")
    tube("oldgold", [(x - 0.07 * s, y - 0.01 * s, zb + 0.30 * s), (x + 0.07 * s, y - 0.01 * s, zb + 0.30 * s)], 0.01 * s, sides=6, name="pauldron_chain")
    # trident arm raised to hold the spear, other arm down
    t = trident_side
    hx = x + t * 0.11 * s
    tube(sw, [(x + t * 0.065 * s, y, zb + 0.30 * s), (x + t * 0.10 * s, y - 0.02 * s, zb + 0.24 * s), (hx, y - 0.03 * s, zb + 0.30 * s)], 0.017 * s, 0.014 * s, sides=8, name="arm")
    tube(sw, [(x - t * 0.065 * s, y, zb + 0.30 * s), (x - t * 0.085 * s, y, zb + 0.22 * s), (x - t * 0.08 * s, y - 0.01 * s, zb + 0.15 * s)], 0.017 * s, 0.013 * s, sides=8, name="arm")
    trident(hx, y - 0.03 * s, z0 + 0.07 * s, 0.50 * s, r=0.007 * s, face=0.0)


def banner(x, y, ztop, w=0.10, h=0.32, rz=0.0, sw="deepteal"):
    """Hanging banner whose face points -Y (rotated by rz about Z)."""
    c, s_ = math.cos(rz), math.sin(rz)
    def at(lx, ly, z):
        return (x + lx * c - ly * s_, y + lx * s_ + ly * c, z)
    box(sw, w, 0.010, h, at(0, 0, ztop - h / 2 - 0.01), rot=(0, 0, rz), name="banner")
    box("oldgold", w + 0.03, 0.016, 0.014, at(0, 0, ztop), rot=(0, 0, rz), name="banner_rod")
    box("oldgold", w, 0.013, 0.008, at(0, 0, ztop - h - 0.01), rot=(0, 0, rz), name="banner_hem")
    for side in (-1, 1):
        box("oldgold", 0.006, 0.013, h, at(side * w / 2, 0, ztop - h / 2 - 0.01), rot=(0, 0, rz), name="banner_edge")
    # trident emblem
    ez = ztop - h * 0.55
    box("oldgold", 0.006, 0.014, h * 0.45, at(0, -0.002, ez), rot=(0, 0, rz), name="emblem_shaft")
    box("oldgold", w * 0.5, 0.014, 0.006, at(0, -0.002, ez + h * 0.14), rot=(0, 0, rz), name="emblem_bar")
    for side in (-1, 1):
        box("oldgold", 0.006, 0.014, h * 0.12, at(side * w * 0.25, -0.002, ez + h * 0.2), rot=(0, 0, rz), name="emblem_prong")


def pearl_lamp(x, y, z0, h=0.16, r=0.032):
    cyl("oldgold", 0.008, 0.010, h, z0, xy=(x, y), seg=8, name="lamp_post")
    cyl("oldgold", 0.022, 0.022, 0.012, z0, xy=(x, y), seg=8, name="lamp_foot")
    cyl("oldgold", 0.010, 0.026, 0.025, z0 + h, xy=(x, y), seg=10, name="lamp_cup")
    sphere("pearl", r, (x, y, z0 + h + 0.02 + r * 0.6), seg=12, rings=6, name="lamp_pearl")
    torus("cyan", r * 0.95, 0.004, (x, y, z0 + h + 0.025), seg=12, minor=3, name="lamp_glow")


def horn(base, out_dir, height, reach, r0, sw="pearl", steps=9, curl=0.0):
    """Curved tapering spike rising from base, bending outward along out_dir (x,y)."""
    bx, by, bz = base
    ox, oy = out_dir
    pts = []
    for i in range(steps + 1):
        t = i / steps
        d = reach * math.sin(t * math.pi * 0.5) ** 2 - curl * t ** 3
        pts.append((bx + ox * d, by + oy * d, bz + height * t))
    tube(sw, pts, r0, 0.002, sides=6, name="horn")
    return pts


def scallop(cx, cy, cz, R, n=11, sw_a="pearl", sw_b="white", rim="oldgold", spread=1.0):
    """Upright scallop fan in the XZ plane, hinge at (cx, cy, cz), facing -Y."""
    half = math.pi * spread / 2
    for k in range(n):
        a = math.pi / 2 - half + 2 * half * (k + 0.5) / n
        rr = R * (0.92 + 0.08 * math.sin(math.pi * (k + 0.5) / n))
        end = (cx + rr * math.cos(a), cy - 0.02 * R, cz + rr * math.sin(a))
        mid = (cx + 0.55 * rr * math.cos(a), cy - 0.08 * R, cz + 0.55 * rr * math.sin(a))
        rw = rr * math.sin(half / n) * 1.15
        tube(sw_a if k % 2 == 0 else sw_b, [(cx, cy, cz), mid, end], 0.01, rw, sides=8, name="shell_rib")
    pts = []
    for i in range(n * 3 + 1):
        a = math.pi / 2 - half + 2 * half * i / (n * 3)
        rr = R * (0.92 + 0.08 * math.sin(math.pi * i / (n * 3))) + 0.004
        pts.append((cx + rr * math.cos(a), cy - 0.02 * R, cz + rr * math.sin(a)))
    tube(rim, pts, R * 0.025, sides=6, name="shell_rim")
    # hinge ears
    box(sw_a, R * 0.45, R * 0.12, R * 0.12, (cx, cy - 0.02 * R, cz + R * 0.03), name="shell_hinge")


def clam(x, y, z0, R, open_deg=55, pearl_r=None, rz=0.0):
    """Open clam: bottom valve as a shallow dome, top valve as a tilted scallop fan."""
    dome("pearl", R, z0, xy=(x, y), seg=16, rings=4, height=0.35, name="clam_bottom")
    torus("oldgold", R, 0.008, (x, y, z0 + 0.004), seg=24, minor=4, name="clam_lip")
    # top valve: ribs fanning from the back hinge, tilted open
    n = 9
    hy = y + R * 0.9
    tilt = math.radians(open_deg)
    for k in range(n):
        a = math.pi * (k + 0.5) / n
        lx, ly = R * math.cos(a), -R * math.sin(a) * 1.8
        ex = x + lx
        ey = hy + ly * math.cos(tilt) * 0.5
        ez = z0 + 0.02 + (-ly) * math.sin(tilt) * 0.5
        tube("pearl" if k % 2 == 0 else "white", [(x, hy, z0 + 0.02), (ex, ey, ez)], 0.006, R * 0.19, sides=6, name="clam_rib")
    pr = pearl_r or R * 0.45
    sphere("pearl", pr, (x, y + R * 0.1, z0 + pr * 0.9), seg=12, rings=8, name="clam_pearl")
    torus("cyan", pr * 1.15, 0.006, (x, y + R * 0.1, z0 + pr * 0.3), seg=20, minor=4, name="clam_glow")


def pointed_arch_pts(cx, y, zs, w, h, steps=10):
    """Gothic pointed arch outline (left spring -> apex -> right spring) in the XZ plane."""
    c = (h * h - w * w) / (2 * w)
    R = w + c
    th = math.acos(-c / R)
    left = [(cx + c + R * math.cos(math.pi - (math.pi - th) * i / steps), y, zs + R * math.sin(math.pi - (math.pi - th) * i / steps)) for i in range(steps + 1)]
    right = [(2 * cx - p[0], y, p[2]) for p in reversed(left)]
    return left + right[1:]


def pointed_panel(sw, cx, y, zs, w, h, depth=0.01, z_bottom=None, slices=7):
    """Fill a pointed arch with stacked slabs (rect below spring, stepped point above)."""
    c = (h * h - w * w) / (2 * w)
    R = w + c
    if z_bottom is not None:
        box(sw, 2 * w, depth, zs - z_bottom, (cx, y, (zs + z_bottom) / 2), name="arch_fill")
    for i in range(slices):
        z_lo = zs + h * i / slices
        z_mid = zs + h * (i + 0.5) / slices
        half = math.sqrt(max(R * R - (z_mid - zs) ** 2, 0)) - c
        box(sw, max(2 * half, 0.01), depth, h / slices + 0.002, (cx, y, z_mid), name="arch_fill")


def arcade_arc(cx, cy, cz, r, a_tan, steps=8):
    """Semicircular arch in the vertical plane whose horizontal axis points along a_tan."""
    ux, uy = math.cos(a_tan), math.sin(a_tan)
    return [(cx + r * math.cos(t) * ux, cy + r * math.cos(t) * uy, cz + r * math.sin(t))
            for t in [math.pi * i / steps for i in range(steps + 1)]]


def coral(x, y, z0, h, sw="teal", seed=0):
    tube(sw, [(x, y, z0), (x + 0.01, y, z0 + h * 0.5), (x - 0.01, y, z0 + h)], 0.020, 0.008, sides=6, name="coral")
    for k in range(3):
        a = TAU * (k / 3) + seed
        zb = z0 + h * (0.3 + 0.2 * k)
        tube(sw, [(x, y, zb), (x + 0.04 * math.cos(a), y + 0.04 * math.sin(a), zb + 0.05), (x + 0.05 * math.cos(a), y + 0.05 * math.sin(a), zb + 0.11)], 0.013, 0.006, sides=5, name="coral_branch")
        sphere("cyan", 0.012, (x + 0.05 * math.cos(a), y + 0.05 * math.sin(a), zb + 0.115), seg=6, rings=4, name="coral_tip")


def stairs(x, y_front, z_low, z_high, n, width, depth, sw="pearl", y_dir=1):
    for i in range(n):
        top = z_low + (z_high - z_low) * (i + 1) / n
        yc = y_front + y_dir * depth * (i + 0.5)
        box(sw, width, depth, top - z_low, (x, yc, (top + z_low) / 2), name="step")


# ---------------------------------------------------------------- cards
def poseidon_structure_current_exchange():
    """DT art: round pearl market rotunda with an arcade of lit stalls and deep banners,
    a glowing whirlpool basin on its roof, a soaring pearl crest of curved horns
    cradling a giant pearl, glowing canals with a front bridge, pearl lamp posts."""
    z = plinth()
    # glowing canal ring and outer quay
    cyl("cyan", 0.70, 0.70, 0.012, z, seg=36, name="canal")
    torus("pearl", 0.71, 0.014, (0, 0, z + 0.012), seg=36, minor=4, name="quay_rim")
    cyl("deepteal", 0.50, 0.52, 0.04, z, seg=32, name="island")
    cyl("oldgold", 0.525, 0.525, 0.01, z + 0.02, seg=32, name="island_trim")
    # current swirls on the canal
    for k in range(3):
        a0 = TAU * k / 3
        tube("pearl", [(0.60 * math.cos(a0 + t * 0.9), 0.60 * math.sin(a0 + t * 0.9), z + 0.016) for t in [i / 8 for i in range(9)]], 0.006, 0.002, sides=4, name="current_swirl")
    # rotunda: inner lit drum, arcade of 12 pearl columns with arches
    zf = z + 0.04
    cyl("abyss", 0.36, 0.36, 0.22, zf, seg=24, name="drum")
    for k in range(12):
        a = TAU * k / 12 + TAU / 24
        cyl("seaglass", 0.06, 0.06, 0.006, zf + 0.10, xy=(0.355 * math.cos(a), 0.355 * math.sin(a)), rot=(math.pi / 2, 0, a + math.pi / 2), seg=8, name="stall_glow")
        box("oldgold", 0.06, 0.012, 0.04, (0.37 * math.cos(a), 0.37 * math.sin(a), zf + 0.035), rot=(0, 0, a + math.pi / 2), name="stall_counter")
    R = 0.43
    for k in range(12):
        a = TAU * k / 12
        x, y = R * math.cos(a), R * math.sin(a)
        cyl("pearl", 0.022, 0.022, 0.17, zf, xy=(x, y), seg=6, name="column")
        cyl("oldgold", 0.03, 0.03, 0.012, zf, xy=(x, y), seg=6, name="column_base")
        cyl("oldgold", 0.026, 0.032, 0.014, zf + 0.165, xy=(x, y), seg=6, name="column_cap")
        am = a + TAU / 24
        cr = R * math.cos(TAU / 24)
        tube("pearl", arcade_arc(cr * math.cos(am), cr * math.sin(am), zf + 0.17, R * math.sin(TAU / 24), am + math.pi / 2, steps=6), 0.011, sides=4, name="arcade_arch")
    # entablature with a gold band below the lip, roof deck
    cyl("pearl", 0.46, 0.46, 0.05, zf + 0.22, seg=36, name="entablature")
    cyl("oldgold", 0.465, 0.465, 0.014, zf + 0.235, seg=36, name="entablature_band")
    cyl("pearl", 0.48, 0.47, 0.025, zf + 0.27, seg=36, name="roof_lip")
    cyl("oldgold", 0.485, 0.485, 0.008, zf + 0.275, seg=36, name="roof_lip_trim")
    zr = zf + 0.295
    # banners hanging from the entablature (four around, two at the front)
    for deg in (240, 300, 0, 180):
        a = math.radians(deg)
        banner(0.475 * math.cos(a), 0.475 * math.sin(a), zr - 0.03, w=0.07, h=0.20, rz=a + math.pi / 2)
    # whirlpool basin on the roof
    cyl("teal", 0.36, 0.34, 0.05, zr, seg=32, name="basin")
    cyl("oldgold", 0.365, 0.365, 0.012, zr + 0.025, seg=32, name="basin_band")
    cyl("cyan", 0.32, 0.32, 0.01, zr + 0.045, seg=32, name="whirlpool")
    torus("pearl", 0.34, 0.016, (0, 0, zr + 0.05), seg=40, minor=6, name="basin_rim")
    for k in range(3):
        pts = []
        for i in range(17):
            t = i / 16
            r = 0.30 * (1 - t) + 0.04
            a = TAU * k / 3 + t * 4.0
            pts.append((r * math.cos(a), r * math.sin(a), zr + 0.058 + 0.01 * t))
        tube("pearl", pts, 0.008, 0.003, sides=4, name="vortex_arm")
    for k in range(8):
        a = TAU * k / 8
        sphere("pearl", 0.022, (0.34 * math.cos(a), 0.34 * math.sin(a), zr + 0.075), seg=8, rings=5, name="rim_pearl")
    # fountain jet rising to the pearl
    cyl("cyan", 0.035, 0.02, 0.36, zr + 0.05, seg=12, name="jet")
    cyl("oldgold", 0.05, 0.03, 0.04, zr + 0.05, seg=12, name="jet_nozzle")
    # crest: two great curved horns meeting above, two smaller outer horns
    for s in (-1, 1):
        pts = []
        for i in range(17):
            t = i / 16
            x = s * (0.30 + 0.06 * math.sin(t * math.pi) - 0.29 * t ** 2)
            pts.append((x, 0.0, zr + 0.06 + 0.98 * t))
        tube("pearl", pts, 0.05, 0.012, sides=10, name="crest_horn")
        tube("oldgold", [(p[0] * 0.86, -0.035, p[2]) for p in pts[3:13]], 0.008, sides=5, name="crest_inlay")
        horn((s * 0.40, 0.0, zr + 0.04), (s, 0), 0.48, 0.10, 0.034)
        horn((s * 0.25, 0.22, zr + 0.04), (s * 0.4, 0.9), 0.36, 0.08, 0.026)
        horn((s * 0.25, -0.22, zr + 0.04), (s * 0.4, -0.9), 0.30, 0.07, 0.022)
    # the great pearl in a gold ring cradle
    pz = zr + 0.52
    sphere("pearl", 0.11, (0, 0, pz), seg=20, rings=12, name="great_pearl")
    torus("oldgold", 0.125, 0.012, (0, 0, pz), rot=(math.pi / 2, 0, 0), seg=32, minor=6, name="pearl_ring")
    torus("cyan", 0.14, 0.006, (0, 0, pz), rot=(math.pi / 2, 0, 0), seg=32, minor=4, name="pearl_halo")
    for s in (-1, 1):
        tube("oldgold", [(s * 0.125, 0, pz), (s * 0.19, 0, pz + 0.02), (s * 0.235, 0, pz + 0.05)], 0.01, 0.006, sides=6, name="pearl_strut")
    cyl("oldgold", 0.02, 0.0, 0.10, pz + 0.12, seg=8, name="pearl_crown")
    cyl("oldgold", 0.03, 0.0, 0.08, zr + 1.03, seg=8, name="crest_finial")
    sphere("pearl", 0.022, (0, 0, zr + 1.04), seg=10, rings=6, name="crest_bead")
    # front bridge over the canal with steps
    box("pearl", 0.20, 0.30, 0.03, (0, -0.62, z + 0.045), name="bridge")
    box("oldgold", 0.21, 0.31, 0.008, (0, -0.62, z + 0.035), name="bridge_trim")
    for s in (-1, 1):
        box("pearl", 0.014, 0.30, 0.04, (s * 0.10, -0.62, z + 0.08), name="bridge_rail")
        pearl_lamp(s * 0.10, -0.76, z + 0.06, h=0.12, r=0.024)
    # market stalls on the quay with canopies
    for (x, y) in ((-0.56, -0.22), (0.56, -0.22), (-0.50, 0.38), (0.50, 0.38)):
        box("deepteal", 0.16, 0.10, 0.05, (x, y, z + 0.035), name="stall")
        box("oldgold", 0.17, 0.11, 0.01, (x, y, z + 0.062), name="stall_top")
        for dx in (-0.07, 0.07):
            cyl("oldgold", 0.005, 0.005, 0.11, z + 0.06, xy=(x + dx, y), seg=6, name="canopy_pole")
        box("teal", 0.19, 0.13, 0.012, (x, y, z + 0.175), rot=(0.18, 0, 0), name="canopy")
        sphere("seaglass", 0.018, (x - 0.03, y - 0.02, z + 0.085), seg=8, rings=5, name="wares")
        sphere("pearl", 0.015, (x + 0.03, y - 0.02, z + 0.08), seg=8, rings=5, name="wares")
    # lamp posts around the quay
    for deg in (30, 150, 90):
        a = math.radians(deg)
        pearl_lamp(0.66 * math.cos(a), 0.66 * math.sin(a), z + 0.02, h=0.20)


def poseidon_structure_pearl_infirmary():
    """DT art: pearl healing hall of tall arches crowned by a great scallop shell
    with a pearl, a central waterfall and side falls pouring into tiered glowing
    pools, open clams with pearls on round steps at the front corners, coral."""
    z = plinth()
    # tiered pools at the front
    cyl("pearl", 0.62, 0.62, 0.035, z, seg=36, name="pool_rim_low")
    cyl("oldgold", 0.625, 0.625, 0.01, z + 0.018, seg=36, name="pool_rim_trim")
    cyl("cyan", 0.58, 0.58, 0.01, z + 0.03, seg=36, name="pool_low")
    cyl("pearl", 0.36, 0.36, 0.06, z, xy=(0, 0.06), seg=32, name="pool_rim_mid")
    cyl("oldgold", 0.365, 0.365, 0.01, z + 0.04, xy=(0, 0.06), seg=32, name="pool_mid_trim")
    cyl("cyan", 0.33, 0.33, 0.01, z + 0.055, xy=(0, 0.06), seg=32, name="pool_mid")
    for k in range(10):
        a = math.pi + math.pi * (k + 0.5) / 10
        box("cyan", 0.06, 0.012, 0.035, (0.36 * math.cos(a), 0.06 + 0.36 * math.sin(a), z + 0.04), rot=(0, 0, a + math.pi / 2), name="spill")
    # pool ripple rings
    for r in (0.14, 0.22):
        torus("seaglass", r, 0.004, (0, 0.06, z + 0.067), seg=32, minor=4, name="ripple")
    # back hall: central tall bay plus two lower wings
    hy = 0.36
    box("pearl", 1.10, 0.22, 0.04, (0, hy, z + 0.02), name="hall_floor")
    zb = z + 0.04
    # central bay
    box("pearl", 0.42, 0.20, 0.62, (0, hy, zb + 0.31), bevel=0.01, name="bay")
    box("oldgold", 0.44, 0.22, 0.016, (0, hy, zb + 0.60), name="bay_cornice")
    box("abyss", 0.26, 0.02, 0.36, (0, hy - 0.10, zb + 0.18), name="bay_recess")
    pointed_panel("seaglass", 0, hy - 0.106, zb + 0.36, 0.13, 0.17, z_bottom=zb + 0.0, slices=6)
    tube("pearl", pointed_arch_pts(0, hy - 0.115, zb + 0.36, 0.15, 0.19), 0.02, sides=6, name="bay_arch")
    for s in (-1, 1):
        box("pearl", 0.04, 0.04, 0.36, (s * 0.15, hy - 0.115, zb + 0.18), name="bay_jamb")
    tube("oldgold", pointed_arch_pts(0, hy - 0.13, zb + 0.36, 0.17, 0.21), 0.006, sides=4, name="bay_arch_trim")
    # central waterfall from the shell down into the pools
    box("cyan", 0.10, 0.015, 0.50, (0, hy - 0.135, zb + 0.30), name="waterfall")
    box("seaglass", 0.13, 0.01, 0.06, (0, hy - 0.14, zb + 0.035), name="waterfall_foam")
    # wings with round arches and inner glow
    for s in (-1, 1):
        wx = s * 0.37
        box("pearl", 0.32, 0.18, 0.40, (wx, hy + 0.01, zb + 0.20), bevel=0.008, name="wing")
        box("oldgold", 0.34, 0.20, 0.014, (wx, hy + 0.01, zb + 0.375), name="wing_cornice")
        box("teal", 0.30, 0.16, 0.03, (wx, hy + 0.01, zb + 0.41), name="wing_parapet")
        box("seaglass", 0.16, 0.01, 0.20, (wx, hy - 0.082, zb + 0.13), name="wing_glow")
        tube("pearl", arc((wx, hy - 0.09, zb + 0.23), 0.08, 0, math.pi, steps=10), 0.016, sides=6, name="wing_arch")
        for d in (-1, 1):
            box("pearl", 0.03, 0.03, 0.23, (wx + d * 0.08, hy - 0.09, zb + 0.115), name="wing_jamb")
        # healing bed inside
        box("white", 0.10, 0.05, 0.03, (wx, hy - 0.05, zb + 0.03), bevel=0.006, name="bed")
        box("pearl", 0.02, 0.05, 0.06, (wx - 0.05, hy - 0.05, zb + 0.04), name="bed_head")
        # side waterfall at the wing's outer edge
        box("cyan", 0.04, 0.012, 0.36, (s * 0.53, hy - 0.10, zb + 0.18), name="side_fall")
        # buttress horns at the junctions
        horn((s * 0.215, hy - 0.08, zb + 0.40), (s * 0.3, -0.2), 0.42, 0.06, 0.03)
        horn((s * 0.52, hy, zb + 0.43), (s, 0), 0.22, 0.05, 0.026)
        pearl_lamp(s * 0.52, hy - 0.06, zb + 0.43, h=0.06, r=0.03)
    # great scallop crest with pearl
    scallop(0, hy - 0.04, zb + 0.62, 0.30, n=11)
    sphere("pearl", 0.06, (0, hy - 0.10, zb + 0.66), seg=18, rings=10, name="crest_pearl")
    torus("oldgold", 0.07, 0.008, (0, hy - 0.10, zb + 0.66), rot=(math.pi / 2, 0, 0), seg=24, minor=5, name="crest_pearl_ring")
    torus("cyan", 0.085, 0.005, (0, hy - 0.10, zb + 0.66), rot=(math.pi / 2, 0, 0), seg=24, minor=4, name="crest_halo")
    # front corner steps with open clams
    for s in (-1, 1):
        x, y = s * 0.50, -0.30
        cyl("pearl", 0.16, 0.17, 0.05, z, xy=(x, y), seg=24, name="clam_step")
        cyl("oldgold", 0.172, 0.172, 0.01, z + 0.03, xy=(x, y), seg=24, name="clam_step_trim")
        cyl("pearl", 0.11, 0.12, 0.05, z + 0.05, xy=(x, y), seg=24, name="clam_step2")
        cyl("oldgold", 0.122, 0.122, 0.01, z + 0.08, xy=(x, y), seg=24, name="clam_step2_trim")
        clam(x, y, z + 0.10, 0.10)
        coral(s * 0.66, -0.06, z, 0.20, seed=s)
        coral(s * 0.30, -0.52, z + 0.03, 0.14, seed=s + 1)


def poseidon_structure_tidevault():
    """DT art: colossal round pearl vault door with gold spokes and a pearl lock,
    flanked by twin domed pearl towers with glowing tide windows and marble Poseidon
    statues bearing tridents; a bridge in front, a treasure clam and coin hoard."""
    z = plinth()
    cyl("deepteal", 0.70, 0.72, 0.04, z, seg=6, rot=(0, 0, HEX30), name="terrace")
    cyl("oldgold", 0.722, 0.722, 0.01, z + 0.02, seg=6, rot=(0, 0, HEX30), name="terrace_trim")
    zt = z + 0.04
    # back wall with arcade
    wy = 0.22
    box("pearl", 1.00, 0.20, 0.62, (0, wy, zt + 0.31), bevel=0.01, name="vault_wall")
    box("oldgold", 1.02, 0.22, 0.016, (0, wy, zt + 0.60), name="wall_cornice")
    box("pearl", 0.96, 0.18, 0.04, (0, wy, zt + 0.63), name="wall_parapet")
    for x in (-0.33, -0.22, 0.22, 0.33):
        box("seaglass", 0.05, 0.01, 0.12, (x, wy - 0.10, zt + 0.48), name="wall_window")
        tube("oldgold", arc((x, wy - 0.105, zt + 0.54), 0.025, 0, math.pi, steps=6), 0.005, sides=4, name="window_arch")
    # glowing tide runnels on the wall
    for s in (-1, 1):
        box("cyan", 0.03, 0.01, 0.30, (s * 0.27, wy - 0.10, zt + 0.17), name="runnel")
    # the vault door
    dz = zt + 0.34
    dy = wy - 0.11
    door_r = 0.30
    cyl("teal", door_r + 0.04, door_r + 0.04, 0.03, dz, xy=(0, dy + 0.02), rot=(math.pi / 2, 0, 0), seg=40, name="door_frame")
    torus("cyan", door_r + 0.015, 0.012, (0, dy - 0.012, dz), rot=(math.pi / 2, 0, 0), seg=36, minor=5, name="door_glow")
    cyl("pearl", door_r, door_r, 0.03, dz, xy=(0, dy), rot=(math.pi / 2, 0, 0), seg=40, name="door")
    torus("oldgold", door_r, 0.016, (0, dy - 0.03, dz), rot=(math.pi / 2, 0, 0), seg=36, minor=6, name="door_rim")
    torus("oldgold", door_r * 0.62, 0.008, (0, dy - 0.032, dz), rot=(math.pi / 2, 0, 0), seg=32, minor=4, name="door_ring")
    torus("oldgold", door_r * 0.30, 0.008, (0, dy - 0.032, dz), rot=(math.pi / 2, 0, 0), seg=32, minor=5, name="door_ring_inner")
    for k in range(16):
        a = TAU * k / 16
        rm = door_r * 0.62
        box("oldgold", door_r * 0.70, 0.012, 0.010, (rm * math.cos(a) * 0.98, dy - 0.034, dz + rm * math.sin(a) * 0.98), rot=(0, -a, 0), name="spoke")
        if k % 2 == 0:
            sphere("pearl", 0.014, (door_r * 0.9 * math.cos(a), dy - 0.035, dz + door_r * 0.9 * math.sin(a)), seg=6, rings=4, name="rivet")
    # shell scallop panels between spokes (petal lines)
    for k in range(16):
        a = TAU * (k + 0.5) / 16
        tube("white", [(door_r * 0.32 * math.cos(a), dy - 0.033, dz + door_r * 0.32 * math.sin(a)), (door_r * 0.58 * math.cos(a), dy - 0.033, dz + door_r * 0.58 * math.sin(a))], 0.004, 0.009, sides=4, name="petal")
    # lock: pearl in gold mount
    cyl("oldgold", 0.07, 0.07, 0.02, dz, xy=(0, dy - 0.03), rot=(math.pi / 2, 0, 0), seg=24, name="lock_mount")
    sphere("pearl", 0.05, (0, dy - 0.07, dz), seg=18, rings=10, name="lock_pearl")
    torus("cyan", 0.06, 0.005, (0, dy - 0.07, dz), rot=(math.pi / 2, 0, 0), seg=24, minor=4, name="lock_glow")
    # crest above the door
    scallop(0, wy - 0.06, zt + 0.66, 0.16, n=9)
    sphere("pearl", 0.035, (0, wy - 0.10, zt + 0.68), seg=12, rings=8, name="crest_pearl")
    # twin towers
    for s in (-1, 1):
        tx, ty = s * 0.52, 0.18
        cyl("pearl", 0.13, 0.13, 0.08, zt, xy=(tx, ty), seg=20, name="tower_foot")
        cyl("oldgold", 0.135, 0.135, 0.012, zt + 0.06, xy=(tx, ty), seg=20, name="tower_foot_band")
        cyl("pearl", 0.11, 0.10, 0.74, zt + 0.08, xy=(tx, ty), seg=20, name="tower")
        for k in range(4):
            a = -math.pi / 2 + (k - 1.5) * 0.45
            box("seaglass", 0.03, 0.01, 0.40, (tx + 0.103 * math.cos(a), ty + 0.103 * math.sin(a), zt + 0.42), rot=(0, 0, a + math.pi / 2), name="tide_window")
        box("cyan", 0.015, 0.012, 0.40, (tx, ty - 0.106, zt + 0.42), name="tide_core")
        for zz in (zt + 0.20, zt + 0.64):
            cyl("oldgold", 0.115, 0.115, 0.016, zz, xy=(tx, ty), seg=20, name="tower_band")
        cyl("pearl", 0.11, 0.13, 0.04, zt + 0.82, xy=(tx, ty), seg=20, name="tower_corbel")
        cyl("oldgold", 0.133, 0.133, 0.012, zt + 0.84, xy=(tx, ty), seg=20, name="tower_corbel_trim")
        for k in range(8):
            a = TAU * k / 8
            box("pearl", 0.03, 0.02, 0.05, (tx + 0.12 * math.cos(a), ty + 0.12 * math.sin(a), zt + 0.885), rot=(0, 0, a + math.pi / 2), name="crenel")
        dome("teal", 0.10, zt + 0.86, xy=(tx, ty), seg=20, rings=5, height=1.3, name="tower_dome")
        cyl("oldgold", 0.012, 0.0, 0.10, zt + 0.98, xy=(tx, ty), seg=8, name="tower_finial")
        sphere("pearl", 0.035, (tx, ty, zt + 1.10), seg=12, rings=6, name="tower_pearl")
        torus("cyan", 0.04, 0.004, (tx, ty, zt + 1.08), seg=16, minor=4, name="tower_pearl_glow")
        # statue in front of each tower
        statue(s * 0.40, -0.12, zt, s=0.95, trident_side=s)
        # glowing tide stream from tower down the terrace
        box("cyan", 0.03, 0.22, 0.008, (tx, ty - 0.25, zt + 0.004), name="stream")
    # bridge and steps at the front
    box("pearl", 0.30, 0.18, 0.03, (0, -0.30, zt + 0.015), name="landing")
    box("oldgold", 0.31, 0.19, 0.008, (0, -0.30, zt + 0.006), name="landing_trim")
    for i in range(3):
        h = 0.012 * (i + 1) + 0.0
        box("pearl", 0.30, 0.055, h + 0.0, (0, -0.565 + 0.055 * i, zt - 0.04 + h / 2 + 0.0), name="step")
    for s in (-1, 1):
        pearl_lamp(s * 0.17, -0.37, zt + 0.03, h=0.12, r=0.024)
    # treasure: open clam on a chest (right front), coin hoard (left front)
    cx, cy = 0.42, -0.42
    box("deepteal", 0.16, 0.10, 0.07, (cx, cy, zt + 0.035), bevel=0.006, name="chest")
    box("oldgold", 0.17, 0.11, 0.012, (cx, cy, zt + 0.05), name="chest_band")
    clam(cx, cy, zt + 0.07, 0.085)
    for (x, y, r) in ((-0.42, -0.40, 0.08), (-0.30, -0.48, 0.05), (-0.50, -0.30, 0.05)):
        dome("oldgold", r, zt, xy=(x, y), seg=14, rings=4, height=0.6, name="coin_hoard")
    for (x, y) in ((-0.44, -0.38), (-0.36, -0.44), (-0.48, -0.33)):
        sphere("pearl", 0.018, (x, y, zt + 0.045), seg=8, rings=5, name="hoard_pearl")
    trident(-0.36, -0.36, zt + 0.02, 0.22, face=0.3)


def poseidon_tutor_structure_1():
    """DT art: Undertow Burrow Gate. A pearly ribbed arch over a swirling cyan tunnel,
    a great scallop crest with a pearl above it, curved horn spires either side,
    pearl orbs on flanking posts, coral and dark rock around the mouth."""
    z = plinth()
    rock_lumps([(-0.55, -0.20, 0.13), (0.58, -0.18, 0.12), (-0.40, 0.42, 0.14), (0.42, 0.44, 0.13), (0.0, 0.52, 0.14)])
    # surf ring at the mouth
    cyl("cyan", 0.36, 0.36, 0.01, z, xy=(0, -0.30), seg=28, name="surf")
    for r in (0.20, 0.30):
        torus("pearl", r, 0.007, (0, -0.30, z + 0.012), seg=28, minor=4, name="foam")
    # tunnel: open-ended dark tube receding back, vortex rings inside
    cz = z + 0.34
    R = 0.32
    cyl("deepteal", R, R * 0.9, 0.46, cz, xy=(0, -0.10), rot=(-math.pi / 2, 0, 0), seg=28, cap=False, name="tunnel")
    cyl("abyss", R * 0.92, R * 0.92, 0.02, cz, xy=(0, 0.34), rot=(-math.pi / 2, 0, 0), seg=28, name="tunnel_back")
    cyl("cyan", R * 0.30, R * 0.30, 0.02, cz, xy=(0, 0.32), rot=(-math.pi / 2, 0, 0), seg=20, name="vortex_eye")
    for i, r in enumerate((0.27, 0.21, 0.15, 0.10)):
        torus("cyan" if i % 2 == 0 else "seaglass", r, 0.010, (0, -0.02 + i * 0.09, cz), rot=(math.pi / 2, 0, 0), seg=28, minor=4, name="vortex_ring")
    pts = []
    for i in range(41):
        t = i / 40
        a = t * TAU * 2.5
        r = 0.26 * (1 - 0.6 * t)
        pts.append((r * math.cos(a), -0.05 + 0.36 * t, cz + r * math.sin(a)))
    tube("cyan", pts, 0.012, 0.005, sides=6, name="vortex_spiral")
    # ribbed pearl arch around the mouth
    tube("pearl", [(-R - 0.05, -0.12, z)] + [(p[0], -0.12, p[2]) for p in arc((0, -0.12, cz), R + 0.05, math.pi, 0, steps=20)] + [(R + 0.05, -0.12, z)], 0.06, sides=12, name="arch")
    tube("oldgold", [(p[0], -0.18, p[2]) for p in arc((0, -0.18, cz), R + 0.005, math.pi * 0.98, math.pi * 0.02, steps=20)], 0.012, sides=6, name="arch_inlay")
    for i in range(9):
        a = math.pi * (i + 0.5) / 9
        x, zz = (R + 0.05) * math.cos(a), cz + (R + 0.05) * math.sin(a)
        tube("white", [(x * 0.84, -0.18, cz + (zz - cz) * 0.84), (x * 1.22, -0.12, cz + (zz - cz) * 1.22)], 0.016, 0.008, sides=6, name="arch_rib")
    # flanking shell pillars with pearl orbs
    for s in (-1, 1):
        px = s * 0.50
        cyl("pearl", 0.07, 0.05, 0.44, z, xy=(px, -0.12), seg=12, name="post")
        for zz in (z + 0.10, z + 0.36):
            cyl("oldgold", 0.065, 0.065, 0.014, zz, xy=(px, -0.12), seg=12, name="post_band")
        cyl("oldgold", 0.04, 0.06, 0.04, z + 0.44, xy=(px, -0.12), seg=12, name="post_cup")
        sphere("pearl", 0.06, (px, -0.12, z + 0.53), seg=14, rings=8, name="post_pearl")
        torus("cyan", 0.068, 0.006, (px, -0.12, z + 0.50), seg=24, minor=4, name="post_glow")
        # horn spires sweeping up and out
        horn((s * 0.40, 0.05, z), (s * 0.5, 0.2), 1.18, 0.12, 0.06, curl=0.02)
        horn((s * 0.58, 0.20, z), (s, 0.1), 0.80, 0.10, 0.05)
        horn((s * 0.26, 0.20, z + 0.30), (s * 0.3, 0.3), 1.00, 0.06, 0.04)
        horn((s * 0.62, -0.05, z), (s, -0.2), 0.42, 0.06, 0.035)
        # small clams at the gate feet
        clam(s * 0.62, -0.40, z + 0.02, 0.07)
        coral(s * 0.66, 0.10, z + 0.04, 0.24, sw="teal", seed=s)
        coral(s * 0.30, -0.56, z, 0.16, sw="teal", seed=s * 2)
    # great scallop crest with pearl above the arch
    sz = cz + R + 0.07
    scallop(0, -0.10, sz, 0.36, n=13)
    sphere("pearl", 0.085, (0, -0.17, sz + 0.06), seg=22, rings=12, name="crest_pearl")
    torus("oldgold", 0.095, 0.010, (0, -0.17, sz + 0.06), rot=(math.pi / 2, 0, 0), seg=28, minor=5, name="crest_pearl_ring")
    for s in (-1, 1):
        tube("oldgold", [(s * 0.095, -0.17, sz + 0.06), (s * 0.16, -0.17, sz + 0.02), (s * 0.22, -0.15, sz - 0.02)], 0.012, 0.006, sides=6, name="crest_claw")


def poseidon_tutor_structure_2():
    """DT art: Tideguard Barracks. Pearl citadel with a tall central gate of glowing
    water under a gold trident, flanking banners and armoured guard statues, arched
    side wings with gold portcullis gates, sweeping horn spires, steps into a pool."""
    z = plinth()
    # pool at the front with curved pearl rim
    cyl("cyan", 0.60, 0.60, 0.01, z, xy=(0, -0.08), seg=36, name="pool")
    torus("pearl", 0.61, 0.018, (0, -0.08, z + 0.01), seg=48, minor=6, name="pool_rim")
    torus("oldgold", 0.63, 0.005, (0, -0.08, z + 0.005), seg=48, minor=4, name="pool_rim_trim")
    for r in (0.22, 0.38):
        torus("seaglass", r, 0.004, (0, -0.25, z + 0.012), seg=32, minor=4, name="ripple")
    # raised barracks terrace at the back
    ty = 0.30
    box("pearl", 1.24, 0.34, 0.10, (0, ty, z + 0.05), name="terrace")
    box("oldgold", 1.26, 0.36, 0.014, (0, ty, z + 0.085), name="terrace_trim")
    zt = z + 0.10
    # central steps down into the pool with falls either side
    for i in range(4):
        h = 0.10 - 0.025 * i
        box("pearl", 0.22, 0.045, h, (0, ty - 0.17 - 0.045 * (i + 0.5), z + h / 2), name="step")
    for s in (-1, 1):
        box("cyan", 0.06, 0.012, 0.09, (s * 0.15, ty - 0.175, z + 0.05), name="fall")
    # side wings with arched portcullis gates
    for s in (-1, 1):
        wx = s * 0.40
        box("pearl", 0.40, 0.22, 0.32, (wx, ty + 0.04, zt + 0.16), bevel=0.008, name="wing")
        box("oldgold", 0.42, 0.24, 0.014, (wx, ty + 0.04, zt + 0.30), name="wing_cornice")
        box("teal", 0.40, 0.20, 0.025, (wx, ty + 0.04, zt + 0.335), name="wing_parapet")
        for g in (-1, 1):
            gx = wx + g * 0.10
            box("abyss", 0.08, 0.01, 0.16, (gx, ty - 0.072, zt + 0.08), name="gate_dark")
            tube("pearl", arc((gx, ty - 0.08, zt + 0.16), 0.045, 0, math.pi, steps=8), 0.012, sides=5, name="gate_arch")
            for b in range(4):
                box("oldgold", 0.006, 0.012, 0.16, (gx - 0.03 + 0.02 * b, ty - 0.08, zt + 0.08), name="portcullis_bar")
            box("oldgold", 0.08, 0.012, 0.006, (gx, ty - 0.08, zt + 0.12), name="portcullis_cross")
        # sweeping horn at the wing end
        horn((s * 0.58, ty + 0.04, zt + 0.34), (s, -0.3), 0.30, 0.08, 0.035)
        horn((s * 0.40, ty + 0.12, zt + 0.34), (s * 0.4, 0.3), 0.22, 0.05, 0.025)
        # guard statues flanking the stairs
        statue(s * 0.24, ty - 0.24, z + 0.0, s=0.75, trident_side=s)
        # banners on the central tower faces
        banner(s * 0.20, ty - 0.125, zt + 0.66, w=0.08, h=0.30)
        # shields on the wing walls
        sphere("deepteal", 0.035, (wx + s * 0.13, ty - 0.075, zt + 0.22), seg=12, rings=6, scale=(1, 0.3, 1.2), name="shield")
        torus("oldgold", 0.033, 0.005, (wx + s * 0.13, ty - 0.085, zt + 0.22), rot=(math.pi / 2, 0, 0), seg=16, minor=4, name="shield_rim")
    # central gate tower
    box("pearl", 0.34, 0.24, 0.78, (0, ty + 0.0, zt + 0.39), bevel=0.01, name="gate_tower")
    box("oldgold", 0.36, 0.26, 0.016, (0, ty, zt + 0.76), name="tower_cornice")
    box("deepteal", 0.20, 0.02, 0.46, (0, ty - 0.115, zt + 0.23), name="portal_recess")
    pointed_panel("cyan", 0, ty - 0.125, zt + 0.40, 0.09, 0.16, z_bottom=zt, slices=6)
    tube("pearl", pointed_arch_pts(0, ty - 0.13, zt + 0.40, 0.11, 0.19), 0.018, sides=6, name="portal_arch")
    tube("oldgold", pointed_arch_pts(0, ty - 0.142, zt + 0.40, 0.125, 0.21), 0.006, sides=4, name="portal_arch_trim")
    for s in (-1, 1):
        box("pearl", 0.035, 0.035, 0.40, (s * 0.11, ty - 0.13, zt + 0.20), name="portal_jamb")
        # drapes
        box("deepteal", 0.03, 0.012, 0.30, (s * 0.075, ty - 0.14, zt + 0.40), rot=(0, s * 0.18, 0), name="drape")
    trident(0, ty - 0.14, zt + 0.12, 0.40, r=0.010)
    # spiky crown of the gate tower
    zc = zt + 0.78
    for (x, h, r0) in ((0.0, 0.42, 0.05), (-0.11, 0.30, 0.035), (0.11, 0.30, 0.035)):
        horn((x, ty, zc), (0 if x == 0 else math.copysign(1, x), 0), h, 0.0 if x == 0 else 0.05, r0)
    for s in (-1, 1):
        horn((s * 0.17, ty - 0.08, zc - 0.10), (s, -0.2), 0.50, 0.17, 0.045, curl=0.03)
        horn((s * 0.17, ty + 0.10, zc - 0.10), (s, 0.4), 0.36, 0.12, 0.035)
    sphere("pearl", 0.03, (0, ty - 0.12, zc - 0.06), seg=12, rings=8, name="crest_pearl")


def poseidon_tutor_structure_3():
    """DT art: Nereid Calling Conch. A giant gold-crowned conch raised on a fountain
    of shell-carved pearl, its glowing mouth pouring a beam, ringed by swirling cyan
    current bands, with pearl-topped gold staffs at the front."""
    z = plinth()
    # stepped round fountain base
    cyl("pearl", 0.66, 0.68, 0.05, z, seg=36, name="tier1")
    cyl("oldgold", 0.685, 0.685, 0.01, z + 0.03, seg=36, name="tier1_trim")
    cyl("cyan", 0.62, 0.62, 0.008, z + 0.046, seg=36, name="basin_water")
    cyl("pearl", 0.44, 0.46, 0.06, z + 0.05, seg=32, name="tier2")
    cyl("oldgold", 0.465, 0.465, 0.01, z + 0.09, seg=32, name="tier2_trim")
    # front stairs
    for i in range(3):
        box("pearl", 0.26, 0.05, 0.04 * (i + 1), (0, -0.62 + 0.05 * i, z + 0.02 * (i + 1)), name="step")
    # shell-carved pedestal: ring of upright scallops around a drum
    zp = z + 0.11
    cyl("teal", 0.28, 0.30, 0.24, zp, seg=24, name="pedestal")
    cyl("oldgold", 0.305, 0.305, 0.014, zp + 0.20, seg=24, name="pedestal_band")
    for k in range(6):
        a = TAU * k / 6 - math.pi / 2
        x, y = 0.31 * math.cos(a), 0.31 * math.sin(a)
        # each scallop is built facing -Y then placed by rotating its anchor around the drum
        n = 7
        for j in range(n):
            b = math.pi * (j + 0.5) / n
            ex = 0.11 * math.cos(b)
            ez = 0.11 * math.sin(b)
            ca, sa = math.cos(a + math.pi / 2), math.sin(a + math.pi / 2)
            tube("pearl" if j % 2 == 0 else "white", [(x, y, zp + 0.02), (x + ex * ca + 0.02 * math.cos(a), y + ex * sa + 0.02 * math.sin(a), zp + 0.02 + ez)], 0.006, 0.022, sides=6, name="pedestal_shell")
        box("seaglass", 0.05, 0.01, 0.06, (x * 0.97, y * 0.97, zp + 0.16), rot=(0, 0, a + math.pi / 2), name="pedestal_window")
        # little spill of water from each shell
        box("cyan", 0.03, 0.008, 0.10, ((0.33) * math.cos(a), 0.33 * math.sin(a), z + 0.11), rot=(0, 0, a + math.pi / 2), name="spill")
    cyl("pearl", 0.24, 0.30, 0.05, zp + 0.24, seg=24, name="pedestal_cap")
    sphere("pearl", 0.06, (0, -0.28, zp + 0.12), seg=16, rings=10, name="front_pearl")
    torus("oldgold", 0.065, 0.008, (0, -0.29, zp + 0.12), rot=(math.pi / 2, 0, 0), seg=24, minor=5, name="front_pearl_ring")
    coral(-0.22, -0.30, zp + 0.24, 0.12, seed=1)
    coral(0.22, -0.30, zp + 0.24, 0.12, seed=2)
    coral(0.0, 0.28, zp + 0.24, 0.10, seed=3)
    # gold stem and crown cradle
    zs = zp + 0.29
    cyl("oldgold", 0.08, 0.05, 0.10, zs, seg=16, name="stem_foot")
    cyl("cyan", 0.045, 0.045, 0.26, zs + 0.10, seg=16, name="beam")
    for k in range(4):
        a = TAU * k / 4 + TAU / 8
        tube("oldgold", [(0.05 * math.cos(a), 0.05 * math.sin(a), zs + 0.10), (0.075 * math.cos(a), 0.075 * math.sin(a), zs + 0.22), (0.10 * math.cos(a), 0.10 * math.sin(a), zs + 0.34)], 0.010, sides=6, name="stem_strut")
    sphere("pearl", 0.05, (0, 0, zs + 0.22), seg=16, rings=10, name="stem_pearl")
    torus("oldgold", 0.12, 0.012, (0, 0, zs + 0.34), seg=28, minor=6, name="cradle_ring")
    for k in range(10):
        a = TAU * k / 10
        cyl("oldgold", 0.012, 0.0, 0.07, zs + 0.34, xy=(0.12 * math.cos(a), 0.12 * math.sin(a)), seg=6, name="cradle_spike")
    # the conch: lying almost level, spire up-left, glowing aperture facing the viewer,
    # great flared lip rising above the body
    cx, cyy, cz = 0.03, 0.0, zs + 0.50
    th = math.radians(-70)  # rotation about Y: +Z axis -> left and slightly up
    axis = (math.sin(th), 0.0, math.cos(th))
    up = (math.cos(th), 0.0, -math.sin(th))
    dep = (0.0, 1.0, 0.0)
    def along(d, u=0.0, v=0.0):
        return tuple((cx, cyy, cz)[j] + axis[j] * d + up[j] * u + dep[j] * v for j in range(3))
    def seg_cone(sw, d0, d1, r0, r1, seg=24, name="conch"):
        b = along(d0)
        cyl(sw, r0, r1, d1 - d0, b[2], xy=(b[0], b[1]), rot=(0, th, 0), seg=seg, name=name)
    seg_cone("pearl", -0.34, -0.14, 0.025, 0.15, name="conch_canal")
    seg_cone("pearl", -0.14, 0.06, 0.15, 0.20, name="conch_body")
    seg_cone("pearl", 0.06, 0.13, 0.20, 0.13, name="conch_shoulder")
    seg_cone("white", 0.13, 0.42, 0.13, 0.0, seg=20, name="conch_spire")
    # spiral ridge winding up the spire
    pts = []
    for i in range(49):
        t = i / 48
        d = 0.10 + 0.30 * t
        r = (0.205 if d < 0.13 else 0.13 * (1 - (d - 0.13) / 0.29)) + 0.006
        a = t * TAU * 3.5
        pts.append(along(d, r * math.cos(a), r * math.sin(a)))
    tube("oldgold", pts, 0.011, 0.004, sides=5, name="conch_ridge")
    # gold crown of spikes along the shoulder
    for k in range(9):
        a = math.pi * (k + 0.5) / 9 - math.pi * 0.05
        p = along(0.09, 0.19 * math.cos(a) * 0 + 0.19 * math.sin(a), -0.19 * math.cos(a))
        cyl("oldgold", 0.018, 0.0, 0.06, p[2] - 0.01, xy=(p[0], p[1]), seg=6, name="conch_spike")
    # glowing aperture on the viewer side, ringed by a pearl lip
    for d, r in ((-0.20, 0.06), (-0.11, 0.085), (-0.02, 0.085), (0.05, 0.06)):
        p = along(d, -0.01, -0.165)
        cyl("cyan", r, r, 0.012, p[2], xy=(p[0], p[1]), rot=(math.pi / 2, th - math.radians(0), 0), seg=20, name="conch_mouth")
    lip = []
    for i in range(25):
        t = TAU * i / 24
        lip.append(along(-0.075 + 0.17 * math.cos(t), -0.01 + 0.10 * math.sin(t), -0.175))
    tube("pearl", lip, 0.026, sides=8, name="conch_lip")
    tube("oldgold", [(p[0], p[1] - 0.022, p[2]) for p in lip], 0.006, sides=4, name="conch_lip_trim")
    # flared outer lip: a fan of ribs rising from the aperture
    n = 10
    tips = []
    for k in range(n):
        f = k / (n - 1)
        d0 = -0.20 + 0.22 * f
        d1 = -0.34 + 0.52 * f
        hgt = 0.17 + 0.15 * math.sin(math.pi * f)
        tip = along(d1, hgt + 0.05, -0.03)
        tips.append(tip)
        tube("pearl" if k % 2 == 0 else "white", [along(d0, 0.05, -0.15), along((d0 + d1) / 2, hgt * 0.55, -0.11), tip], 0.03, 0.006, sides=6, name="lip_flare")
    tube("oldgold", tips, 0.006, sides=4, name="lip_flare_rim")
    # cradle strut tops touching the shell
    sphere("pearl", 0.035, along(-0.34), seg=10, rings=6, name="canal_pearl")
    # swirling current bands around the conch
    for (rr, tilt, dz, sw) in ((0.42, 12, 0.0, "cyan"), (0.34, -18, 0.18, "seaglass"), (0.48, 6, -0.20, "cyan")):
        torus(sw, rr, 0.008, (0, 0, cz + dz), rot=(math.radians(tilt), math.radians(tilt * 0.5), 0), seg=40, minor=4, name="current_band")
    for k in range(6):
        a = TAU * k / 6
        sphere("pearl", 0.014, (0.42 * math.cos(a), 0.42 * math.sin(a) * math.cos(math.radians(12)), cz + 0.42 * math.sin(a) * math.sin(math.radians(12))), seg=8, rings=5, name="bubble")
    # gold staffs with pearl tops at the front corners
    for s in (-1, 1):
        x, y = s * 0.50, -0.30
        cyl("oldgold", 0.010, 0.010, 0.62, z + 0.05, xy=(x, y), seg=8, name="staff")
        for zz in (z + 0.30, z + 0.55):
            cyl("oldgold", 0.018, 0.018, 0.014, zz, xy=(x, y), seg=8, name="staff_band")
        for k in range(4):
            a = TAU * k / 4
            tube("oldgold", [(x, y, z + 0.64), (x + 0.03 * math.cos(a), y + 0.03 * math.sin(a), z + 0.68), (x + 0.025 * math.cos(a), y + 0.025 * math.sin(a), z + 0.74)], 0.006, 0.003, sides=4, name="staff_claw")
        sphere("pearl", 0.035, (x, y, z + 0.70), seg=14, rings=8, name="staff_pearl")
        torus("cyan", 0.04, 0.004, (x, y, z + 0.70), seg=16, minor=4, name="staff_glow")


def poseidon_tutor_structure_4():
    """DT art: Leviathan Muster Dock. A pearl cathedral gate with a glowing sea portal
    and a towering central spire, twin piers running out over a glowing channel lined
    with obelisk pylons and deep banners, chained leviathans moored alongside."""
    z = plinth()
    # glowing channel down the middle
    box("cyan", 0.22, 1.10, 0.012, (0, -0.03, z + 0.006), name="channel")
    # two piers with pylons
    for s in (-1, 1):
        px = s * 0.20
        box("deepteal", 0.14, 0.98, 0.06, (px, -0.05, z + 0.03), name="pier")
        box("pearl", 0.16, 1.00, 0.02, (px, -0.05, z + 0.07), name="pier_deck")
        box("oldgold", 0.165, 1.005, 0.008, (px, -0.05, z + 0.062), name="pier_trim")
        for i in range(5):
            y = -0.51 + i * 0.22
            cyl("oldgold", 0.012, 0.012, 0.03, z + 0.08, xy=(px - s * 0.07, y), seg=8, name="bollard")
            sphere("cyan", 0.012, (px - s * 0.07, y, z + 0.115), seg=6, rings=4, name="bollard_light")
        for j, y in enumerate((-0.49, -0.24, 0.01)):
            ox = px + s * 0.03
            box("pearl", 0.07, 0.07, 0.36 - 0.06 * (j == 0), (ox, y, z + 0.08 + (0.36 - 0.06 * (j == 0)) / 2), bevel=0.006, name="pylon")
            top = z + 0.08 + 0.36 - 0.06 * (j == 0)
            box("seaglass", 0.012, 0.075, 0.20, (ox, y, top - 0.15), name="pylon_glow")
            box("oldgold", 0.08, 0.08, 0.016, (ox, y, top - 0.02), name="pylon_band")
            cyl("pearl", 0.05, 0.0, 0.12, top, xy=(ox, y), seg=4, rot=(0, 0, math.pi / 4), name="pylon_cap")
            sphere("cyan", 0.015, (ox, y, top + 0.02), seg=8, rings=5, name="pylon_light")
            banner(ox, y - 0.036, top - 0.04, w=0.055, h=0.17)
            # chains from pylon out to the leviathan
            pts = []
            for k in range(7):
                t = k / 6
                pts.append((ox + s * (0.33 * t), y + 0.02, top - 0.06 - 0.20 * math.sin(math.pi * t * 0.8)))
            for k in range(1, 6):
                p = pts[k]
                torus("oldgold", 0.012, 0.003, p, rot=(0, (k % 2) * math.pi / 2, 0), seg=6, minor=3, name="chain_link")
            tube("oldgold", pts, 0.003, sides=4, name="chain_cable")
        # leviathan moored alongside
        lx = s * 0.55
        sphere("deepteal", 0.12, (lx, -0.02, z + 0.12), seg=20, rings=12, scale=(0.9, 3.0, 0.85), name="leviathan_body")
        sphere("teal", 0.10, (lx, -0.36, z + 0.11), seg=16, rings=10, scale=(0.95, 1.5, 0.75), name="leviathan_head")
        sphere("abyss", 0.07, (lx, -0.40, z + 0.075), seg=12, rings=6, scale=(1.2, 1.4, 0.5), name="leviathan_jaw")
        for e in (-1, 1):
            sphere("cyan", 0.014, (lx + e * 0.085, -0.40, z + 0.14), seg=8, rings=5, name="leviathan_eye")
        for k in range(8):
            y = -0.26 + k * 0.075
            cyl("teal", 0.025, 0.0, 0.10 - abs(k - 3) * 0.008, z + 0.21 - abs(k - 3) * 0.006, xy=(lx, y), rot=(0.25, 0, 0), seg=6, name="dorsal_spike")
            box("cyan", 0.012, 0.03, 0.02, (lx, y, z + 0.215 - abs(k - 3) * 0.006), name="dorsal_glow")
        for k in range(3):
            tube("cyan", [(lx - s * 0.105, -0.22 + k * 0.12, z + 0.08), (lx - s * 0.11, -0.18 + k * 0.12, z + 0.14)], 0.006, sides=4, name="gill_glow")
        tube("deepteal", [(lx, 0.32, z + 0.13), (lx, 0.40, z + 0.16), (lx, 0.46, z + 0.22)], 0.05, 0.012, sides=8, name="leviathan_tail")
        for f in (-1, 1):
            tube("deepteal", [(lx, 0.46, z + 0.22), (lx + f * 0.08, 0.50, z + 0.26)], 0.02, 0.004, sides=6, name="fluke")
        tube("teal", [(lx + s * 0.09, -0.16, z + 0.08), (lx + s * 0.18, -0.10, z + 0.04)], 0.03, 0.006, sides=6, name="flipper")
    # cathedral gate at the back
    gy = 0.42
    box("pearl", 0.76, 0.20, 0.08, (0, gy, z + 0.04), name="gate_base")
    box("oldgold", 0.78, 0.22, 0.012, (0, gy, z + 0.07), name="gate_base_trim")
    zg = z + 0.08
    box("pearl", 0.42, 0.18, 0.62, (0, gy, zg + 0.31), bevel=0.01, name="gate_hall")
    box("oldgold", 0.44, 0.20, 0.014, (0, gy, zg + 0.60), name="gate_cornice")
    box("abyss", 0.24, 0.02, 0.48, (0, gy - 0.09, zg + 0.24), name="portal_recess")
    pointed_panel("cyan", 0, gy - 0.10, zg + 0.32, 0.11, 0.20, z_bottom=zg, slices=7)
    tube("pearl", pointed_arch_pts(0, gy - 0.11, zg + 0.32, 0.13, 0.23), 0.02, sides=6, name="portal_arch")
    tube("oldgold", pointed_arch_pts(0, gy - 0.122, zg + 0.32, 0.15, 0.26), 0.006, sides=4, name="portal_arch_trim")
    for s in (-1, 1):
        box("pearl", 0.04, 0.04, 0.32, (s * 0.13, gy - 0.11, zg + 0.16), name="portal_jamb")
    # dolphins/silhouettes in the portal: little pearl arcs
    for (x, zz) in ((-0.04, zg + 0.22), (0.05, zg + 0.30)):
        tube("pearl", arc((x, gy - 0.115, zz), 0.03, math.pi * 0.1, math.pi * 0.9, steps=6), 0.007, 0.003, sides=4, name="dolphin")
    # side towers with spires
    for s in (-1, 1):
        tx = s * 0.30
        box("pearl", 0.11, 0.12, 0.80, (tx, gy + 0.02, zg + 0.40), bevel=0.006, name="side_tower")
        box("seaglass", 0.03, 0.01, 0.40, (tx, gy - 0.045, zg + 0.40), name="side_tower_glow")
        for zz in (zg + 0.18, zg + 0.62, zg + 0.79):
            box("oldgold", 0.12, 0.13, 0.014, (tx, gy + 0.02, zz), name="side_tower_band")
        cyl("teal", 0.07, 0.0, 0.32, zg + 0.80, xy=(tx, gy + 0.02), seg=8, rot=(0, 0, math.pi / 8), name="side_spire")
        cyl("oldgold", 0.006, 0.006, 0.08, zg + 1.10, xy=(tx, gy + 0.02), seg=6, name="side_spire_tip")
        horn((s * 0.20, gy, zg + 0.60), (s, -0.2), 0.30, 0.06, 0.03)
        # gate flank walls
        box("pearl", 0.16, 0.14, 0.34, (s * 0.44, gy + 0.02, zg + 0.17), name="flank_wall")
        box("oldgold", 0.17, 0.15, 0.012, (s * 0.44, gy + 0.02, zg + 0.32), name="flank_trim")
        tube("pearl", arc((s * 0.44, gy - 0.055, zg + 0.17), 0.045, 0, math.pi, steps=8), 0.01, sides=5, name="flank_arch")
        box("seaglass", 0.07, 0.01, 0.16, (s * 0.44, gy - 0.052, zg + 0.09), name="flank_glow")
    # central tall spire with gable
    zc = zg + 0.62
    cyl("pearl", 0.12, 0.0, 0.26, zc, xy=(0, gy), seg=4, rot=(0, 0, math.pi / 4), name="gable")
    box("pearl", 0.10, 0.10, 0.52, (0, gy, zc + 0.26), bevel=0.006, name="spire_shaft")
    box("cyan", 0.02, 0.012, 0.40, (0, gy - 0.052, zc + 0.26), name="spire_glow")
    box("oldgold", 0.11, 0.11, 0.014, (0, gy, zc + 0.52), name="spire_band")
    cyl("pearl", 0.06, 0.0, 0.20, zc + 0.52, xy=(0, gy), seg=4, rot=(0, 0, math.pi / 4), name="spire_cap")
    sphere("pearl", 0.03, (0, gy - 0.09, zc + 0.05), seg=12, rings=8, name="gable_pearl")


def poseidon_tutor_structure_5():
    """DT art: Thalassic Hero Hall. A soaring pointed pearl arch framing a swirling
    glowing sea portal with a pearl at its apex, a grand staircase between falls,
    colossal marble statues of Poseidon with tridents and deep trident banners."""
    z = plinth()
    # front pool and grand stairs
    box("cyan", 0.70, 0.22, 0.01, (0, -0.46, z + 0.005), name="pool")
    box("pearl", 0.74, 0.03, 0.04, (0, -0.58, z + 0.02), name="pool_lip")
    box("oldgold", 0.75, 0.035, 0.008, (0, -0.58, z + 0.03), name="pool_lip_trim")
    hall_z = z + 0.20
    n = 6
    for i in range(n):
        h = (hall_z - z) * (i + 1) / n
        box("pearl", 0.36, 0.05, h, (0, -0.33 + 0.05 * i, z + h / 2), name="step")
        box("seaglass", 0.30, 0.004, 0.006, (0, -0.58 + 0.05 * i + 0.25 - 0.026, z + h - 0.004), name="step_glow")
    # podium terrace
    py = 0.18
    box("pearl", 1.20, 0.44, hall_z - z, (0, py, (hall_z + z) / 2), name="podium")
    box("oldgold", 1.22, 0.46, 0.014, (0, py, hall_z - 0.02), name="podium_trim")
    for s in (-1, 1):
        box("deepteal", 0.38, 0.012, 0.12, (s * 0.38, py - 0.226, z + 0.07), name="podium_panel")
        box("cyan", 0.04, 0.012, hall_z - z - 0.02, (s * 0.21, py - 0.228, (hall_z + z) / 2), name="podium_fall")
        box("cyan", 0.03, 0.012, hall_z - z - 0.02, (s * 0.52, py - 0.228, (hall_z + z) / 2), name="podium_fall")
    # balustrade with pearl lamps
    for s in (-1, 1):
        box("pearl", 0.36, 0.03, 0.05, (s * 0.38, py - 0.20, hall_z + 0.025), name="balustrade")
        pearl_lamp(s * 0.20, py - 0.20, hall_z + 0.05, h=0.08, r=0.026)
        pearl_lamp(s * 0.55, py - 0.20, hall_z + 0.05, h=0.08, r=0.026)
    # hall facade
    fy = 0.32
    box("pearl", 0.90, 0.16, 0.60, (0, fy, hall_z + 0.30), bevel=0.01, name="facade")
    box("oldgold", 0.92, 0.18, 0.014, (0, fy, hall_z + 0.59), name="facade_cornice")
    # grand pointed arch portal
    zs = hall_z + 0.48
    pointed_panel("deepteal", 0, fy - 0.082, zs, 0.18, 0.40, z_bottom=hall_z, slices=9, depth=0.012)
    pointed_panel("cyan", 0, fy - 0.092, zs, 0.14, 0.33, z_bottom=hall_z, slices=9)
    # swirling vortex in the portal
    pts = []
    for i in range(49):
        t = i / 48
        a = t * TAU * 3
        r = 0.12 * (1 - 0.8 * t)
        pts.append((r * math.cos(a), fy - 0.102, zs - 0.06 + r * math.sin(a) * 1.4))
    tube("pearl", pts, 0.007, 0.003, sides=4, name="portal_swirl")
    for k in range(3):
        x = -0.06 + 0.06 * k
        tube("abyss", [(x, fy - 0.100, hall_z), (x, fy - 0.100, hall_z + 0.10)], 0.012, 0.008, sides=5, name="hero_silhouette")
        sphere("abyss", 0.014, (x, fy - 0.100, hall_z + 0.115), seg=8, rings=5, name="hero_head")
    tube("pearl", pointed_arch_pts(0, fy - 0.10, zs, 0.20, 0.44, steps=12), 0.03, sides=8, name="portal_arch")
    tube("oldgold", pointed_arch_pts(0, fy - 0.12, zs, 0.225, 0.48, steps=12), 0.008, sides=4, name="portal_arch_trim")
    tube("pearl", pointed_arch_pts(0, fy - 0.11, zs, 0.25, 0.54, steps=12), 0.02, sides=6, name="outer_arch")
    for s in (-1, 1):
        box("pearl", 0.05, 0.05, zs - hall_z, (s * 0.20, fy - 0.10, (zs + hall_z) / 2), name="portal_jamb")
        box("pearl", 0.04, 0.04, zs - hall_z, (s * 0.25, fy - 0.11, (zs + hall_z) / 2), name="outer_jamb")
        cyl("oldgold", 0.035, 0.035, 0.014, zs - 0.01, xy=(s * 0.20, fy - 0.10), seg=8, name="impost")
    sphere("pearl", 0.05, (0, fy - 0.13, zs + 0.47), seg=18, rings=10, name="apex_pearl")
    torus("oldgold", 0.058, 0.007, (0, fy - 0.13, zs + 0.47), rot=(math.pi / 2, 0, 0), seg=24, minor=5, name="apex_ring")
    torus("cyan", 0.07, 0.004, (0, fy - 0.13, zs + 0.47), rot=(math.pi / 2, 0, 0), seg=24, minor=4, name="apex_glow")
    # gothic flanking towers and pinnacles
    for s in (-1, 1):
        tx = s * 0.38
        box("pearl", 0.12, 0.14, 0.92, (tx, fy + 0.02, hall_z + 0.46), bevel=0.008, name="tower")
        box("seaglass", 0.05, 0.01, 0.24, (tx, fy - 0.052, hall_z + 0.70), name="tower_window")
        tube("oldgold", arc((tx, fy - 0.056, hall_z + 0.82), 0.025, 0, math.pi, steps=6), 0.005, sides=4, name="tower_window_arch")
        for zz in (hall_z + 0.58, hall_z + 0.90):
            box("oldgold", 0.13, 0.15, 0.014, (tx, fy + 0.02, zz), name="tower_band")
        cyl("teal", 0.08, 0.0, 0.28, hall_z + 0.92, xy=(tx, fy + 0.02), seg=4, rot=(0, 0, math.pi / 4), name="tower_spire")
        cyl("pearl", 0.03, 0.0, 0.14, hall_z + 0.60, xy=(s * 0.27, fy - 0.05), seg=6, name="pinnacle")
        sphere("pearl", 0.03, (tx, fy - 0.06, hall_z + 0.50), seg=12, rings=8, name="tower_pearl")
        # banners on the towers
        banner(tx, fy - 0.07, hall_z + 0.46, w=0.10, h=0.30)
        # colossal statue on a high pedestal in front of each tower
        sx = s * 0.42
        cyl("pearl", 0.10, 0.11, 0.06, hall_z, xy=(sx, 0.02), seg=12, name="colossus_base")
        cyl("oldgold", 0.112, 0.112, 0.012, hall_z + 0.035, xy=(sx, 0.02), seg=12, name="colossus_base_trim")
        statue(sx, 0.02, hall_z + 0.06, s=1.45, trident_side=-s)
    # roof crest of small horns
    for x in (-0.12, 0.12):
        horn((x, fy + 0.04, hall_z + 0.60), (math.copysign(1, x), 0), 0.22, 0.05, 0.025)


BUILDERS = {
    "poseidon_structure_current_exchange": ("Current Exchange", "Poseidon", poseidon_structure_current_exchange),
    "poseidon_structure_pearl_infirmary": ("Pearl Infirmary", "Poseidon", poseidon_structure_pearl_infirmary),
    "poseidon_structure_tidevault": ("Tidevault", "Poseidon", poseidon_structure_tidevault),
    "poseidon_tutor_structure_1": ("Undertow Burrow Gate", "Poseidon", poseidon_tutor_structure_1),
    "poseidon_tutor_structure_2": ("Tideguard Barracks", "Poseidon", poseidon_tutor_structure_2),
    "poseidon_tutor_structure_3": ("Nereid Calling Conch", "Poseidon", poseidon_tutor_structure_3),
    "poseidon_tutor_structure_4": ("Leviathan Muster Dock", "Poseidon", poseidon_tutor_structure_4),
    "poseidon_tutor_structure_5": ("Thalassic Hero Hall", "Poseidon", poseidon_tutor_structure_5),
}
