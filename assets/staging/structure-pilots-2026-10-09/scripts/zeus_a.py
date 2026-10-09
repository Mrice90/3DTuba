"""Zeus structure set A. Each builder only adds parts via kit."""
import math
from kit import box, cyl, sphere, dome, torus, tube, helix, arc

TAU = math.tau
HEX30 = 0.0  # bmesh hexes are already pointy-top


# ---------------------------------------------------------------- shared bits
def rock_terrace(top=True):
    """Short rock plinth and a marble hex terrace with a gold side band. Returns terrace top z."""
    cyl("stone", 0.80, 0.86, 0.10, 0.0, seg=6, rot=(0, 0, HEX30), name="rock")
    cyl("marble", 0.82, 0.80, 0.05, 0.10, seg=6, rot=(0, 0, HEX30), name="terrace")
    cyl("gold", 0.83, 0.83, 0.014, 0.125, seg=6, rot=(0, 0, HEX30), name="terrace_trim")
    return 0.15


def upper_terrace(r=0.54, z=0.15, h=0.06):
    cyl("marble", r, r - 0.02, h, z, seg=6, rot=(0, 0, HEX30), name="terrace2")
    cyl("gold", r + 0.005, r + 0.005, 0.014, z + h - 0.025, seg=6, rot=(0, 0, HEX30), name="terrace2_trim")
    return z + h


def round_deck(z=0.10):
    cyl("stone", 0.78, 0.84, z, 0.0, seg=6, rot=(0, 0, HEX30), name="rock")
    cyl("marble", 0.78, 0.76, 0.05, z, seg=32, name="deck")
    cyl("gold", 0.785, 0.785, 0.014, z + 0.025, seg=32, name="deck_trim")
    return z + 0.05


def flat_banners(degs=(240, 300), d=0.752, z=0.08):
    """Royal sun banners hung on hex plinth flats."""
    for deg in degs:
        a = math.radians(deg)
        rz = a + math.pi / 2
        def at(dd, zz, lateral=0.0):
            return (dd * math.cos(a) - lateral * math.sin(a), dd * math.sin(a) + lateral * math.cos(a), zz)
        box("royal", 0.18, 0.012, 0.13, at(d, z), rot=(0, 0, rz), name="banner")
        box("gold", 0.20, 0.016, 0.016, at(d, z + 0.07), rot=(0, 0, rz), name="banner_rod")
        box("gold", 0.18, 0.014, 0.008, at(d, z - 0.062), rot=(0, 0, rz), name="banner_hem")
        x, y, _ = at(d + 0.008, 0)
        cyl("gold", 0.026, 0.026, 0.004, z, xy=(x, y), rot=(math.pi / 2, 0, rz), seg=16, name="banner_sun")
        torus("gold", 0.04, 0.004, at(d + 0.008, z), rot=(math.pi / 2, 0, rz), seg=20, minor=4, name="banner_sun_ring")


def stairs(w, y0, z_base, z_top, n=3, depth=0.05, sw="marble"):
    """Steps descending toward -Y from y0 (back edge of the top step)."""
    dh = (z_top - z_base) / n
    for i in range(n):
        top = z_top - i * dh - 0.004
        box(sw, w, depth, top - z_base, (0, y0 - depth / 2 - i * depth, z_base + (top - z_base) / 2), name="step")
    box("gold", 0.012, n * depth, 0.012, (-w / 2 - 0.006, y0 - n * depth / 2, z_top - (z_top - z_base) / 2 + 0.006), name="stair_rail")
    box("gold", 0.012, n * depth, 0.012, (w / 2 + 0.006, y0 - n * depth / 2, z_top - (z_top - z_base) / 2 + 0.006), name="stair_rail")


def orb_post(x, y, z, h=0.22, s=0.08, ring=True):
    """Square marble post with a royal band, gold cap and a glass orb in a gold ring."""
    box("marble", s, s, h, (x, y, z + h / 2), bevel=0.006, name="post")
    box("royal", s + 0.005, s + 0.005, h * 0.35, (x, y, z + h * 0.55), name="post_band")
    box("gold", s + 0.015, s + 0.015, 0.015, (x, y, z + 0.008), name="post_foot")
    box("gold", s + 0.02, s + 0.02, 0.02, (x, y, z + h + 0.01), name="post_cap")
    cyl("gold", 0.022, 0.032, 0.025, z + h + 0.02, xy=(x, y), seg=12, name="post_cup")
    sphere("glass", 0.035, (x, y, z + h + 0.075), seg=14, rings=8, name="post_orb")
    if ring:
        torus("gold", 0.048, 0.004, (x, y, z + h + 0.075), rot=(math.radians(70), 0, math.radians(30)), seg=20, minor=4, name="post_ring")
        torus("gold", 0.048, 0.004, (x, y, z + h + 0.075), rot=(math.radians(-60), 0, math.radians(-30)), seg=20, minor=4, name="post_ring")
        cyl("gold", 0.006, 0.0, 0.05, z + h + 0.12, xy=(x, y), seg=6, name="post_finial")


def armillary(c, R, sw_core="glass", core_r=None, n=3, minor=0.008, seg=36):
    core_r = core_r if core_r is not None else R * 0.72
    big = R > 0.15
    sphere(sw_core, core_r, c, seg=24 if big else 14, rings=14 if big else 8, name="armillary_core")
    tilts = [(math.radians(90), 0, 0), (math.radians(90), 0, math.radians(90)), (math.radians(20), math.radians(15), 0), (math.radians(70), 0, math.radians(45))]
    for k in range(n):
        torus("gold", R, minor, c, rot=tilts[k], seg=seg if big else min(seg, 24), minor=6 if big else 4, name="armillary_ring")


def sun_disc(x, y, z, rz=0.0, r=0.026):
    """Gold sun emblem on a front face (face normal = local -Y rotated by rz)."""
    cyl("gold", r, r, 0.004, z, xy=(x, y), rot=(math.pi / 2, 0, rz), seg=16, name="sun")
    for k in range(4):
        box("gold", 0.006, 0.004, r * 2.8, (x, y, z), rot=(0, TAU * k / 8, rz), name="sun_ray")


# ---------------------------------------------------------------- 1. Oracle Spire
def zeus_ability_oracle_spire():
    """DT art: central marble keep with tall blue lightning windows, a great
    lightning globe in gold armillary rings on top, a crown of slender gold-tipped
    spires flying blue banners, armillary orb posts at the front gate, stairs."""
    z = rock_terrace()
    flat_banners()
    z2 = upper_terrace(0.58, z, 0.06)
    stairs(0.32, -0.50, z, z2, n=3, depth=0.045)
    # orb posts flanking the gate
    for s in (-1, 1):
        orb_post(s * 0.30, -0.53, z, h=0.22, s=0.075)
    # round drum
    zd = z2
    cyl("marble", 0.36, 0.36, 0.26, zd, seg=24, name="drum")
    cyl("gold", 0.365, 0.365, 0.014, zd + 0.005, seg=24, name="drum_foot")
    for k in range(12):
        a = TAU * k / 12
        cyl("white", 0.017, 0.017, 0.24, zd + 0.01, xy=(0.372 * math.cos(a), 0.372 * math.sin(a)), seg=8, name="colonnette")
    cyl("gold", 0.39, 0.39, 0.03, zd + 0.24, seg=24, name="drum_cornice")
    # front arched door glowing
    box("lightning", 0.10, 0.012, 0.15, (0, -0.362, zd + 0.085), name="door_glow")
    tube("gold", arc((0, -0.372, zd + 0.16), 0.055, 0, math.pi, steps=8), 0.008, sides=5, name="door_arch")
    box("gold", 0.012, 0.012, 0.15, (-0.055, -0.372, zd + 0.085), name="door_jamb")
    box("gold", 0.012, 0.012, 0.15, (0.055, -0.372, zd + 0.085), name="door_jamb")
    # central tower
    zt0, zt1 = zd + 0.27, 1.02
    cyl("marble", 0.21, 0.20, zt1 - zt0, zt0, seg=8, rot=(0, 0, math.pi / 8), name="tower")
    for k in range(8):
        a = TAU * k / 8
        if k % 2 == 0:
            box("lightning", 0.06, 0.012, zt1 - zt0 - 0.16, (0.20 * math.cos(a), 0.20 * math.sin(a), (zt0 + zt1) / 2), rot=(0, 0, a + math.pi / 2), name="tower_window")
            box("gold", 0.08, 0.016, 0.014, (0.205 * math.cos(a), 0.205 * math.sin(a), zt1 - 0.07), rot=(0, 0, a + math.pi / 2), name="window_lintel")
        else:
            box("royal", 0.055, 0.012, zt1 - zt0 - 0.20, (0.196 * math.cos(a), 0.196 * math.sin(a), (zt0 + zt1) / 2), rot=(0, 0, a + math.pi / 2), name="tower_panel")
            sun_disc(0.205 * math.cos(a), 0.205 * math.sin(a), (zt0 + zt1) / 2 + 0.05, rz=a + math.pi / 2, r=0.018)
    for zz in (zt0 + 0.02, 0.80):
        cyl("gold", 0.225, 0.225, 0.025, zz - 0.012, seg=8, rot=(0, 0, math.pi / 8), name="tower_band")
    # tower corner pinnacles
    for k in range(4):
        a = TAU * k / 4 + TAU / 8
        x, y = 0.23 * math.cos(a), 0.23 * math.sin(a)
        cyl("marble", 0.03, 0.025, 0.40, zt1 - 0.40, xy=(x, y), seg=8, name="pinnacle")
        cyl("gold", 0.025, 0.0, 0.10, zt1, xy=(x, y), seg=8, name="pinnacle_tip")
    # crown + globe
    cyl("gold", 0.21, 0.25, 0.05, zt1 - 0.01, seg=16, name="crown")
    cyl("marble", 0.25, 0.25, 0.025, zt1 + 0.04, seg=16, name="crown_lip")
    cyl("gold", 0.255, 0.255, 0.01, zt1 + 0.05, seg=16, name="crown_trim")
    cyl("gold", 0.06, 0.10, 0.06, zt1 + 0.065, seg=16, name="cradle")
    gz = zt1 + 0.26
    armillary((0, 0, gz), 0.21, sw_core="glass", core_r=0.155, n=3, minor=0.009, seg=40)
    torus("gold", 0.25, 0.010, (0, 0, gz), rot=(math.radians(-25), math.radians(20), 0), seg=44, minor=6, name="orbit")
    for ph in (0.4, 2.5, 4.4):
        tube("lightning", [(0.155 * math.cos(ph + d * 0.5), 0.155 * math.sin(ph + d * 0.5), gz - 0.10 + d * 0.07) for d in range(4)], 0.006, sides=4, name="globe_bolt")
    cyl("gold", 0.012, 0.0, 0.09, gz + 0.20, seg=8, name="finial")
    # ring of flag spires
    for k, deg in enumerate((30, 90, 150, 210, 330)):
        a = math.radians(deg)
        r = 0.48
        x, y = r * math.cos(a), r * math.sin(a)
        h = 0.98 if deg == 90 else 0.86
        cyl("marble", 0.035, 0.028, h, z2, xy=(x, y), seg=8, name="flag_spire")
        cyl("gold", 0.04, 0.04, 0.015, z2 + 0.25, xy=(x, y), seg=8, name="spire_band")
        cyl("gold", 0.04, 0.04, 0.015, z2 + h - 0.02, xy=(x, y), seg=8, name="spire_band")
        cyl("gold", 0.03, 0.0, 0.16, z2 + h, xy=(x, y), seg=8, name="spire_tip")
        sphere("gold", 0.014, (x, y, z2 + h + 0.17), seg=8, rings=6, name="spire_bead")
        # banner hung on the outward side
        ox, oy = math.cos(a), math.sin(a)
        bx, by = x + ox * 0.045, y + oy * 0.045
        bz = z2 + h - 0.24
        box("royal", 0.075, 0.008, 0.30, (bx, by, bz), rot=(0, 0, a + math.pi / 2), name="flag")
        box("lightning", 0.012, 0.010, 0.22, (bx, by, bz), rot=(0, 0, a + math.pi / 2), name="flag_bolt")
        box("gold", 0.09, 0.012, 0.012, (bx, by, bz + 0.155), rot=(0, 0, a + math.pi / 2), name="flag_rod")
    # glowing floor rune ring
    torus("lightning", 0.46, 0.006, (0, 0, z2 + 0.004), seg=48, minor=4, name="rune_ring")


# ---------------------------------------------------------------- 2. Worldstorm Spire
def zeus_apex_worldstorm_spire():
    """DT art: a soaring cluster of white needle spires with gold tips, a huge gold
    halo ring around the upper spires, a lightning armillary globe on the front,
    royal sun banners on the keep, a vertical lightning beam up the central spire."""
    z = rock_terrace()
    flat_banners()
    z2 = upper_terrace(0.60, z, 0.06)
    stairs(0.28, -0.52, z, z2, n=3, depth=0.045)
    for s in (-1, 1):
        orb_post(s * 0.30, -0.54, z, h=0.20, s=0.07)
    # octagonal keep
    zk0, zk1 = z2, z2 + 0.30
    cyl("marble", 0.42, 0.40, zk1 - zk0, zk0, seg=8, rot=(0, 0, math.pi / 8), name="keep")
    cyl("gold", 0.425, 0.425, 0.014, zk1 - 0.04, seg=8, rot=(0, 0, math.pi / 8), name="keep_trim")
    cyl("gold", 0.43, 0.43, 0.014, zk0 + 0.01, seg=8, rot=(0, 0, math.pi / 8), name="keep_foot")
    for k in range(8):
        a = TAU * k / 8 + TAU / 16
        d = 0.395 * math.cos(TAU / 16)
        x, y = d * math.cos(a), d * math.sin(a)
        if k in (4, 5, 6, 7, 0, 3):
            box("royal", 0.12, 0.010, 0.20, (x, y, zk0 + 0.14), rot=(0, 0, a + math.pi / 2), name="keep_banner")
            box("gold", 0.135, 0.014, 0.012, (x, y, zk0 + 0.245), rot=(0, 0, a + math.pi / 2), name="keep_banner_rod")
            sun_disc(x + 0.006 * math.cos(a), y + 0.006 * math.sin(a), zk0 + 0.15, rz=a + math.pi / 2, r=0.022)
    # corner turrets on the keep
    for k in range(8):
        a = TAU * k / 8
        x, y = 0.42 * math.cos(a), 0.42 * math.sin(a)
        cyl("marble", 0.04, 0.04, 0.10, zk1 - 0.02, xy=(x, y), seg=8, name="turret")
        cyl("gold", 0.045, 0.045, 0.012, zk1 + 0.06, xy=(x, y), seg=8, name="turret_ring")
        cyl("gold", 0.04, 0.0, 0.10, zk1 + 0.08, xy=(x, y), seg=8, name="turret_tip")
    # secondary spires
    zs = zk1
    for k in range(6):
        a = TAU * k / 6 + TAU / 12
        x, y = 0.24 * math.cos(a), 0.24 * math.sin(a)
        h = 0.52 if k in (1, 4) else 0.42
        cyl("marble", 0.065, 0.045, h, zs, xy=(x, y), seg=8, name="spire")
        box("lightning", 0.022, 0.01, h * 0.6, (x + 0.06 * math.cos(a), y + 0.06 * math.sin(a), zs + h * 0.45), rot=(0, 0, a + math.pi / 2), name="spire_slit")
        cyl("gold", 0.07, 0.07, 0.015, zs + h * 0.3, xy=(x, y), seg=8, name="spire_band")
        cyl("gold", 0.052, 0.052, 0.015, zs + h - 0.02, xy=(x, y), seg=8, name="spire_band")
        cyl("gold", 0.045, 0.0, 0.18, zs + h, xy=(x, y), seg=8, name="spire_tip")
    # central needle
    cyl("marble", 0.14, 0.08, 0.72, zs, seg=8, rot=(0, 0, math.pi / 8), name="needle")
    box("lightning", 0.035, 0.012, 0.66, (0, -0.115, zs + 0.36), rot=(math.radians(-3.5), 0, 0), name="beam")
    for zz, rr in ((zs + 0.25, 0.135), (zs + 0.50, 0.11)):
        cyl("gold", rr, rr, 0.02, zz, seg=8, rot=(0, 0, math.pi / 8), name="needle_band")
    cyl("gold", 0.09, 0.09, 0.03, zs + 0.70, seg=8, rot=(0, 0, math.pi / 8), name="needle_collar")
    cyl("marble", 0.07, 0.0, 0.26, zs + 0.73, seg=8, rot=(0, 0, math.pi / 8), name="needle_tip")
    cyl("gold", 0.02, 0.0, 0.12, zs + 0.97, seg=8, name="needle_point")
    # halo rings around the upper spires
    hz = 1.26
    torus("gold", 0.58, 0.016, (0, 0, hz), rot=(math.radians(6), 0, 0), seg=64, minor=8, name="halo")
    torus("gold", 0.50, 0.008, (0, 0, hz + 0.05), rot=(math.radians(6), 0, 0), seg=56, minor=6, name="halo_inner")
    for k in range(8):
        a = TAU * k / 8
        ys = math.cos(math.radians(6))
        sphere("lightning", 0.02, (0.58 * math.cos(a), 0.58 * math.sin(a) * ys, hz + 0.58 * math.sin(a) * math.sin(math.radians(6))), seg=10, rings=6, name="halo_node")
    # halo struts to the needle
    for s in (-1, 1):
        tube("gold", [(s * 0.10, 0.0, 1.12), (s * 0.30, 0.0, 1.20), (s * 0.57, 0.0, hz)], 0.009, sides=6, name="halo_strut")
    # lightning armillary globe on the front of the needle
    gc = (0, -0.21, 0.92)
    armillary(gc, 0.13, sw_core="lightning", core_r=0.085, n=3, minor=0.007, seg=32)
    tube("gold", [(0, -0.12, 0.88), (0, -0.16, 0.90)], 0.012, sides=6, name="globe_mount")
    # bolts arcing out from the halo
    ct = math.cos(math.radians(6)); st_ = math.sin(math.radians(6))
    for k in (0, 2, 3, 5, 7):
        a = TAU * k / 8
        end = (0.58 * math.cos(a), 0.58 * math.sin(a) * ct, hz + 0.58 * math.sin(a) * st_)
        pts = []
        for i in range(6):
            t = i / 5
            r = 0.09 + (0.58 - 0.09) * t
            j = (0.05 if i % 2 else -0.05) if 0 < i < 5 else 0.0
            pts.append((r * math.cos(a + j), r * math.sin(a + j) * ct, 1.32 + (end[2] - 1.32) * t + (0.05 * math.sin(math.pi * t))))
        tube("lightning", pts, 0.009, 0.005, sides=4, name="bolt")


# ---------------------------------------------------------------- 3. Cloudwall Bastion
def sail_turbine(cx, cy, z, R, n=4, sail_h=0.20):
    """Gold sail wheel: a horizontal ring with curved sail vanes around a mast."""
    cyl("gold", 0.012, 0.010, sail_h + 0.10, z, xy=(cx, cy), seg=8, name="mast")
    torus("gold", R, 0.008, (cx, cy, z + 0.03), seg=36, minor=5, name="sail_ring")
    torus("gold", R * 0.93, 0.006, (cx, cy, z + 0.05 + sail_h * 0.6), seg=32, minor=4, name="sail_ring_top")
    for k in range(n):
        a = TAU * k / n + TAU / (2 * n)
        # each sail: a bowed sheet (two halves angled like a V) in two tapering tiers
        for tier, (wf, h0, h1) in enumerate(((0.62, 0.0, 0.62), (0.36, 0.60, 1.0))):
            hh = (h1 - h0) * sail_h
            zc = z + 0.05 + (h0 + h1) / 2 * sail_h
            for side in (-1, 1):
                ang = a + side * (R * wf * 0.25) / (R * 0.85)
                d = R * 0.85 + 0.012
                x, y = cx + d * math.cos(ang), cy + d * math.sin(ang)
                box("gold", R * wf * 0.52, 0.006, hh, (x, y, zc), rot=(math.radians(-10), 0, ang + math.pi / 2 - side * 0.35), name="sail")
    sphere("lightning", 0.03, (cx, cy, z + 0.03), seg=12, rings=8, name="sail_hub")
    cyl("gold", 0.012, 0.0, 0.08, z + sail_h + 0.10, xy=(cx, cy), seg=6, name="mast_tip")


def zeus_cloudwall_bastion():
    """DT art: long white curtain wall with a gold-trimmed arched gate and royal
    sun banners, a tall central keep and two flanking round towers crowned by gold
    sail turbines, crenellated parapets, a front arcade bridge with lamp posts."""
    z = rock_terrace()
    flat_banners()
    # curtain wall
    wy, wd = 0.14, 0.30
    wz0, wz1 = z, z + 0.32
    box("marble", 1.12, wd, wz1 - wz0, (0, wy, (wz0 + wz1) / 2), bevel=0.006, name="wall")
    box("gold", 1.14, wd + 0.02, 0.016, (0, wy, wz1 - 0.04), name="wall_cornice")
    box("gold", 1.14, wd + 0.02, 0.016, (0, wy, wz0 + 0.02), name="wall_plinth")
    for i in range(13):
        x = -0.54 + i * 0.09
        box("marble", 0.05, 0.05, 0.05, (x, wy - wd / 2 + 0.025, wz1 + 0.025), name="merlon")
    # pilasters + windows on the wall front
    for i, x in enumerate((-0.46, -0.34, 0.34, 0.46)):
        box("marble", 0.04, 0.03, wz1 - wz0, (x, wy - wd / 2 - 0.01, (wz0 + wz1) / 2), name="pilaster")
    for x in (-0.40, 0.40):
        box("lightning", 0.05, 0.01, 0.12, (x, wy - wd / 2 - 0.003, wz0 + 0.17), name="window")
        tube("gold", arc((x, wy - wd / 2 - 0.008, wz0 + 0.23), 0.026, 0, math.pi, steps=6), 0.005, sides=4, name="window_arch")
    for x in (-0.22, 0.22):
        box("royal", 0.08, 0.008, 0.22, (x, wy - wd / 2 - 0.005, wz0 + 0.16), name="wall_banner")
        box("gold", 0.095, 0.012, 0.012, (x, wy - wd / 2 - 0.008, wz0 + 0.275), name="wall_banner_rod")
        sun_disc(x, wy - wd / 2 - 0.012, wz0 + 0.17, r=0.02)
    # gatehouse projecting forward
    gy = wy - wd / 2 - 0.06
    box("marble", 0.30, 0.14, 0.38, (0, gy, z + 0.19), bevel=0.006, name="gatehouse")
    box("gold", 0.32, 0.16, 0.016, (0, gy, z + 0.34), name="gate_cornice")
    box("lightning", 0.13, 0.01, 0.20, (0, gy - 0.072, z + 0.11), name="gate_glow")
    for dx in (-0.03, 0.03):
        box("gold", 0.006, 0.012, 0.20, (dx, gy - 0.075, z + 0.11), name="gate_bar")
    tube("gold", arc((0, gy - 0.078, z + 0.21), 0.07, 0, math.pi, steps=10), 0.010, sides=6, name="gate_arch")
    for s in (-1, 1):
        box("gold", 0.02, 0.02, 0.21, (s * 0.07, gy - 0.078, z + 0.105), name="gate_jamb")
        cyl("marble", 0.035, 0.035, 0.46, z, xy=(s * 0.16, gy - 0.05), seg=10, name="gate_tower")
        cyl("gold", 0.04, 0.04, 0.014, z + 0.42, xy=(s * 0.16, gy - 0.05), seg=10, name="gate_tower_ring")
        cyl("gold", 0.04, 0.0, 0.10, z + 0.46, xy=(s * 0.16, gy - 0.05), seg=10, name="gate_tower_tip")
    # front bridge with lamp posts
    box("gold", 0.16, 0.05, 0.012, (0, gy - 0.095, z + 0.006), name="gate_threshold")
    for s in (-1, 1):
        x, y = s * 0.30, -0.42
        box("marble", 0.05, 0.05, 0.12, (x, y, z + 0.06), name="lamp_post")
        box("gold", 0.065, 0.065, 0.014, (x, y, z + 0.12), name="lamp_cap")
        cyl("gold", 0.018, 0.026, 0.02, z + 0.127, xy=(x, y), seg=10, name="lamp_cup")
        sphere("lightning", 0.026, (x, y, z + 0.17), seg=12, rings=8, name="lamp")
        torus("gold", 0.034, 0.003, (x, y, z + 0.17), rot=(math.pi / 2, 0, 0), seg=16, minor=4, name="lamp_ring")
    # central keep
    kx, ky = 0.0, 0.16
    k0, k1 = wz1, 1.02
    cyl("marble", 0.16, 0.15, k1 - k0, k0 - 0.02, xy=(kx, ky), seg=16, name="keep")
    for zz in (k0 + 0.08, k0 + 0.32, k1 - 0.04):
        cyl("gold", 0.165, 0.165, 0.016, zz, xy=(kx, ky), seg=16, name="keep_band")
    box("royal", 0.07, 0.01, 0.28, (kx, ky - 0.152, k0 + 0.24), name="keep_banner")
    sun_disc(kx, ky - 0.158, k0 + 0.27, r=0.02)
    box("lightning", 0.04, 0.01, 0.10, (kx, ky - 0.148, k1 - 0.14), name="keep_window")
    for k in range(8):
        a = TAU * k / 8
        box("marble", 0.04, 0.04, 0.04, (kx + 0.15 * math.cos(a), ky + 0.15 * math.sin(a), k1 + 0.0), rot=(0, 0, a), name="keep_merlon")
    cyl("marble", 0.12, 0.12, 0.05, k1 - 0.02, xy=(kx, ky), seg=16, name="keep_top")
    cyl("gold", 0.11, 0.05, 0.06, k1 + 0.03, xy=(kx, ky), seg=16, name="keep_crown")
    sail_turbine(kx, ky, k1 + 0.09, 0.26, n=4, sail_h=0.24)
    # flanking towers
    for s in (-1, 1):
        tx, ty = s * 0.47, 0.18
        t1 = 0.86
        cyl("marble", 0.11, 0.10, t1 - z, z, xy=(tx, ty), seg=16, name="tower")
        for zz in (wz1 - 0.04, t1 - 0.05):
            cyl("gold", 0.115, 0.115, 0.016, zz, xy=(tx, ty), seg=16, name="tower_band")
        box("lightning", 0.03, 0.01, 0.10, (tx, ty - 0.105, t1 - 0.16), name="tower_window")
        box("royal", 0.06, 0.01, 0.20, (tx, ty - 0.11, z + 0.42), name="tower_banner")
        for k in range(8):
            a = TAU * k / 8
            box("marble", 0.03, 0.03, 0.035, (tx + 0.10 * math.cos(a), ty + 0.10 * math.sin(a), t1 + 0.012), rot=(0, 0, a), name="tower_merlon")
        cyl("gold", 0.08, 0.03, 0.05, t1, xy=(tx, ty), seg=12, name="tower_crown")
        sail_turbine(tx, ty, t1 + 0.05, 0.18, n=4, sail_h=0.17)
    # end turrets on the wall corners (back)
    for s in (-1, 1):
        x, y = s * 0.30, 0.27
        cyl("marble", 0.045, 0.045, 0.50, z, xy=(x, y), seg=10, name="back_turret")
        cyl("gold", 0.05, 0.05, 0.014, z + 0.46, xy=(x, y), seg=10, name="back_turret_ring")
        cyl("gold", 0.05, 0.0, 0.12, z + 0.50, xy=(x, y), seg=10, name="back_turret_tip")


# ---------------------------------------------------------------- 4. Ion Storm Lattice
def zeus_ion_storm_lattice():
    """Alpha art: a riveted cube lattice of dark storm-iron girders full of glowing
    gold conduits, crackling with blue-white lightning. Here it hovers on a marble
    and gold pylon dais with a lightning core feeding it."""
    z = rock_terrace()
    flat_banners()
    z2 = upper_terrace(0.56, z, 0.05)
    # corner pylon cradles
    half = 0.44
    cz0 = 0.42
    cz1 = cz0 + 2 * half
    cc = (cz0 + cz1) / 2
    for k in range(4):
        a = TAU * k / 4 + TAU / 8
        x, y = 0.66 * math.cos(a) * 0.86, 0.66 * math.sin(a) * 0.86
        box("marble", 0.09, 0.09, cz0 - z2 + 0.06, (x, y, z2 + (cz0 - z2 + 0.06) / 2), bevel=0.006, name="cradle_pylon")
        box("gold", 0.11, 0.11, 0.016, (x, y, z2 + 0.012), name="pylon_foot")
        box("royal", 0.095, 0.095, 0.08, (x, y, z2 + 0.13), name="pylon_band")
        box("gold", 0.11, 0.11, 0.018, (x, y, cz0 + 0.04), name="pylon_cap")
    stairs(0.26, -0.47, z, z2, n=2, depth=0.045)
    for sgn in (-1, 1):
        orb_post(sgn * 0.30, -0.58, z, h=0.20, s=0.07)
        # lightning arcing from the pylons into the lattice corners
        tube("lightning", [(sgn * 0.40, -0.40, cz0 + 0.06), (sgn * 0.36, -0.36, cz0 + 0.14), (sgn * 0.41, -0.33, cz0 + 0.20), (sgn * 0.33, -0.30, cz0 + 0.28)], 0.008, 0.004, sides=4, name="pylon_arc")
    # lightning rod on top of the cube
    cyl("gold", 0.08, 0.0, 0.08, cz1 + 0.02, seg=4, rot=(0, 0, math.pi / 4), name="roof_pyramid")
    cyl("gold", 0.012, 0.006, 0.16, cz1 + 0.06, seg=8, name="rod")
    sphere("lightning", 0.025, (0, 0, cz1 + 0.23), seg=12, rings=8, name="rod_spark")
    torus("gold", 0.05, 0.005, (0, 0, cz1 + 0.12), seg=20, minor=4, name="rod_ring")
    # central pedestal with lightning feed
    cyl("marble", 0.20, 0.17, 0.10, z2, seg=8, rot=(0, 0, math.pi / 8), name="pedestal")
    cyl("gold", 0.205, 0.205, 0.014, z2 + 0.07, seg=8, rot=(0, 0, math.pi / 8), name="pedestal_trim")
    cyl("gold", 0.12, 0.08, 0.06, z2 + 0.10, seg=16, name="emitter")
    cyl("lightning", 0.05, 0.05, cc - z2 - 0.14, z2 + 0.14, seg=12, name="feed_beam")
    # lattice: 4x4 girders along each axis
    g = [-half + 2 * half * i / 3 for i in range(4)]
    t = 0.032
    L = 2 * half + 0.06
    for a in g:
        for b in g:
            outer = (abs(a) > half - 1e-6) + (abs(b) > half - 1e-6)
            sw = "gold" if outer == 2 else "stone"
            th = t * (1.25 if outer == 2 else 1.0)
            box(sw, L, th, th, (0, a, cc + b), name="girder_x")
            box(sw, th, L, th, (a, 0, cc + b), name="girder_y")
            box(sw, th, th, L, (a, b, cc), name="girder_z")
    # gold joint plates at the outer nodes
    for a in g:
        for b in g:
            for c in g:
                if max(abs(a), abs(b), abs(c)) > half - 1e-6 and (abs(a) > half - 1e-6) + (abs(b) > half - 1e-6) + (abs(c) > half - 1e-6) == 1:
                    box("gold", 0.05, 0.05, 0.05, (a, b, cc + c), name="joint")
    # girder end caps sticking out at the corners (like the riveted art)
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                box("gold", 0.06, 0.06, 0.06, (sx * half, sy * half, cc + sz * half), name="corner_node")
    # inner glowing conduits (gold pipes) wound through the cells + lightning core
    s = 2 * half / 3
    for k, (px, py) in enumerate(((-s, -s / 2), (s, s / 2), (s / 2, -s), (-s / 2, s))):
        pts = [(px, py, cc - half + 0.06), (px, py, cc - 0.08), (px * 0.4, py * 0.4, cc), (px, py, cc + 0.08), (px, py, cc + half - 0.06)]
        tube("gold", pts, 0.016, sides=8, name="conduit")
    for zz in (cc - s, cc + s):
        tube("gold", [(-s, -s, zz), (s, -s, zz), (s, s, zz), (-s, s, zz), (-s, -s + 0.001, zz)], 0.013, sides=8, caps=False, name="conduit_loop")
    for a in (0, TAU / 4):
        tube("gold", [(s * math.cos(a), s * math.sin(a), cc - s), (s * math.cos(a) * 0.5, s * math.sin(a) * 0.5, cc - s * 0.5), (0, 0, cc)], 0.012, sides=6, name="conduit_feed")
    # serpentine conduits just inside each vertical face and under the top
    ins = half - 0.07
    for k in range(4):
        a = TAU * k / 4
        ux, uy = math.cos(a), math.sin(a)      # face normal
        vx, vy = -uy, ux                        # along the face
        pts = []
        for i, zz in enumerate((cc - half + 0.10, cc - 0.15, cc + 0.13, cc + half - 0.10)):
            sgn = -1 if i % 2 == 0 else 1
            pts.append((ux * ins + vx * sgn * 0.30, uy * ins + vy * sgn * 0.30, zz))
            pts.append((ux * ins - vx * sgn * 0.30, uy * ins - vy * sgn * 0.30, zz))
        tube("gold", pts, 0.012, sides=6, name="face_conduit")
    for sx in (-1, 1):
        for zz in (cc - half, cc + half):
            for yy in (-half / 3, half / 3):
                box("gold", 0.045, 0.045, 0.045, (sx * half, yy, zz), name="rivet_node")
                box("gold", 0.045, 0.045, 0.045, (yy, sx * half, zz), name="rivet_node")
    sphere("lightning", 0.10, (0, 0, cc), seg=20, rings=12, name="ion_core")
    torus("gold", 0.13, 0.008, (0, 0, cc), rot=(math.radians(60), 0, math.radians(30)), seg=28, minor=6, name="core_ring")
    torus("gold", 0.13, 0.008, (0, 0, cc), rot=(math.radians(-60), 0, math.radians(-30)), seg=28, minor=6, name="core_ring")
    # lightning bolts crackling off the cube
    def bolt(p0, d, n=5, step=0.05, jit=0.025, r0=0.010):
        pts = [p0]
        x, y, zz = p0
        for i in range(1, n):
            x += d[0] * step; y += d[1] * step; zz += d[2] * step
            j = jit * (1 if i % 2 else -1)
            pts.append((x + j * d[1], y - j * d[0], zz + j * 0.6))
        tube("lightning", pts, r0, 0.003, sides=4, name="bolt")
    for sx in (-1, 1):
        for sy in (-1, 1):
            bolt((sx * half, sy * half, cc + half), (sx * 0.35, sy * 0.35, 0.65), n=4, step=0.05)
            bolt((sx * half, sy * half, cc - half), (sx * 0.5, sy * 0.5, -0.55), n=3, step=0.05)
    for sx in (-1, 1):
        bolt((sx * half, 0, cc + 0.15), (sx * 0.8, 0, 0.4), n=3, step=0.05)
        bolt((0, sx * half, cc - 0.1), (0, sx * 0.8, 0.3), n=3, step=0.05)
    bolt((0, 0, cc + half), (0.1, 0, 1.0), n=4, step=0.05)


# ---------------------------------------------------------------- 5. Aegis Conductor
def zeus_structure_aegis_conductor():
    """DT art: a towering upright gold-ringed shield of crackling lightning with a
    glowing central orb, flanked by marble columns with glowing blue cores, gold
    rings and domed caps, on a fortified marble base with royal sun banners and
    armillary orb posts."""
    z = rock_terrace()
    flat_banners()
    # fortified base
    b0, b1 = z, z + 0.20
    box("marble", 1.02, 0.46, b1 - b0, (0, 0.08, (b0 + b1) / 2), bevel=0.008, name="bastion")
    box("gold", 1.04, 0.48, 0.016, (0, 0.08, b1 - 0.035), name="bastion_cornice")
    for x in (-0.20, 0.20):
        box("royal", 0.10, 0.01, 0.15, (x, -0.157, b0 + 0.09), name="base_banner")
        box("gold", 0.115, 0.014, 0.012, (x, -0.16, b0 + 0.165), name="base_banner_rod")
        sun_disc(x, -0.165, b0 + 0.095, r=0.022)
    for x in (-0.48, -0.30, 0.0, 0.30, 0.48):
        box("marble", 0.05, 0.03, b1 - b0, (x, -0.16, (b0 + b1) / 2), name="pilaster")
    stairs(0.14, -0.15, z, b1, n=3, depth=0.045)
    for s in (-1, 1):
        orb_post(s * 0.38, -0.40, z, h=0.20, s=0.07)
    # shield dais
    cyl("marble", 0.22, 0.24, 0.07, b1, seg=8, rot=(0, 0, math.pi / 8), name="shield_dais")
    cyl("gold", 0.245, 0.245, 0.014, b1 + 0.04, seg=8, rot=(0, 0, math.pi / 8), name="dais_trim")
    # the shield disc (faces -Y)
    sc_z, R = 0.86, 0.38
    sy = 0.06
    cyl("royal", R, R, 0.04, sc_z, xy=(0, sy + 0.04), rot=(math.pi / 2, 0, 0), seg=40, name="shield_back")
    cyl("lightning", R - 0.02, R - 0.02, 0.01, sc_z, xy=(0, sy - 0.0), rot=(math.pi / 2, 0, 0), seg=40, name="shield_field")
    torus("gold", R, 0.028, (0, sy + 0.01, sc_z), rot=(math.pi / 2, 0, 0), seg=56, minor=8, name="shield_rim")
    torus("gold", R * 0.62, 0.014, (0, sy - 0.015, sc_z), rot=(math.pi / 2, 0, 0), seg=44, minor=6, name="shield_ring")
    torus("gold", R + 0.06, 0.008, (0, sy + 0.01, sc_z), rot=(math.pi / 2, 0, 0), seg=56, minor=5, name="shield_outer_ring")
    for k in range(8):
        a = TAU * k / 8
        box("gold", 0.018, 0.016, R * 0.75, (R * 0.6 * math.cos(a), sy - 0.015, sc_z + R * 0.6 * math.sin(a)), rot=(0, -a + math.pi / 2, 0), name="shield_spoke")
    # vertical blade through the shield
    box("gold", 0.035, 0.03, 2 * R + 0.16, (0, sy - 0.02, sc_z), name="blade")
    cyl("gold", 0.025, 0.0, 0.14, sc_z + R + 0.08, xy=(0, sy - 0.02), seg=4, rot=(0, 0, math.pi / 4), name="blade_tip")
    box("gold", 0.30, 0.03, 0.03, (0, sy - 0.02, sc_z), name="crossbar")
    # central orb
    sphere("glass", 0.08, (0, sy - 0.06, sc_z), seg=20, rings=12, name="shield_orb")
    torus("gold", 0.095, 0.010, (0, sy - 0.06, sc_z), rot=(math.pi / 2, 0, 0), seg=28, minor=6, name="orb_bezel")
    # lightning veins on the field
    for a in (0.6, 2.2, 3.9, 5.3):
        pts = [((0.10 + i * 0.06) * math.cos(a + (0.12 if i % 2 else -0.08)), sy - 0.012, sc_z + (0.10 + i * 0.06) * math.sin(a + (0.12 if i % 2 else -0.08))) for i in range(5)]
        tube("lightning", pts, 0.008, 0.004, sides=4, name="vein")
    # small spheres on the outer ring
    for k in range(6):
        a = TAU * k / 6 + TAU / 12
        sphere("gold", 0.022, ((R + 0.06) * math.cos(a), sy + 0.01, sc_z + (R + 0.06) * math.sin(a)), seg=10, rings=6, name="ring_bead")
    # flanking columns with glowing cores
    for s in (-1, 1):
        x, y = s * 0.52, 0.12
        c0, c1 = b1, 1.18
        cyl("marble", 0.085, 0.085, 0.10, c0, xy=(x, y), seg=12, name="col_base")
        cyl("gold", 0.09, 0.09, 0.02, c0 + 0.09, xy=(x, y), seg=12, name="col_base_ring")
        cyl("lightning", 0.055, 0.055, c1 - c0 - 0.2, c0 + 0.11, xy=(x, y), seg=12, name="col_core")
        for k in range(6):
            a = TAU * k / 6
            cyl("marble", 0.012, 0.012, c1 - c0 - 0.2, c0 + 0.11, xy=(x + 0.07 * math.cos(a), y + 0.07 * math.sin(a)), seg=6, name="col_rib")
        for zz in (0.55, 0.80):
            torus("gold", 0.075, 0.012, (x, y, zz), seg=24, minor=6, name="col_ring")
        cyl("marble", 0.085, 0.085, 0.09, c1 - 0.09, xy=(x, y), seg=12, name="col_head")
        cyl("gold", 0.09, 0.09, 0.016, c1 - 0.08, xy=(x, y), seg=12, name="col_head_ring")
        sphere("glass", 0.05, (x, y - 0.06, c1 - 0.045), seg=12, rings=8, name="col_orb")
        dome("gold", 0.085, c1, xy=(x, y), seg=16, rings=5, height=1.1, name="col_dome")
        cyl("gold", 0.012, 0.0, 0.14, c1 + 0.08, xy=(x, y), seg=8, name="col_finial")
        # arc braces from column to shield ring
        tube("gold", [(x - s * 0.06, y - 0.02, sc_z + 0.26), (x - s * 0.10, y - 0.02, sc_z + 0.30), (s * (R + 0.04) * 0.75, sy, sc_z + (R + 0.04) * 0.66)], 0.008, sides=6, name="brace")
        tube("gold", [(x - s * 0.07, y, sc_z - 0.20), (s * (R + 0.05), sy + 0.01, sc_z - 0.08)], 0.008, sides=6, name="brace")
        tube("lightning", [(x - s * 0.06, y - 0.02, 0.70), (x - s * 0.12, y - 0.05, 0.74), (x - s * 0.15, y - 0.03, 0.70), (s * (R + 0.05), sy - 0.01, 0.73)], 0.006, 0.003, sides=4, name="arc_bolt")


# ---------------------------------------------------------------- 6. Cloud Archive
def zeus_structure_cloud_archive():
    """DT art: a round marble rotunda of tall columns around curved royal book
    stacks, glowing star-map ribbons spiralling inside, a gold armillary dome with a
    globe on top, side towers bearing orbs in gold rings, sun banners, front stairs."""
    zd = round_deck()
    # floor mosaic
    cyl("royal", 0.40, 0.40, 0.006, zd, seg=32, name="floor_inlay")
    torus("lightning", 0.40, 0.006, (0, 0, zd + 0.006), seg=48, minor=4, name="floor_ring")
    torus("gold", 0.25, 0.006, (0, 0, zd + 0.006), seg=40, minor=4, name="floor_ring_gold")
    # podium ring
    cyl("marble", 0.58, 0.58, 0.04, zd, seg=32, name="podium")
    cyl("gold", 0.585, 0.585, 0.012, zd + 0.02, seg=32, name="podium_trim")
    zc = zd + 0.04
    stairs(0.24, -0.575, zd, zc, n=2, depth=0.035)
    # curved book stacks at the back
    for k in range(9):
        a = math.radians(20 + k * 17.5)
        x, y = 0.42 * math.cos(a), 0.42 * math.sin(a)
        box("royal", 0.13, 0.05, 0.62, (x, y, zc + 0.31), rot=(0, 0, a + math.pi / 2), name="stack")
        for j in range(4):
            box("gold", 0.135, 0.056, 0.010, (x, y, zc + 0.10 + j * 0.14), rot=(0, 0, a + math.pi / 2), name="shelf")
    # columns
    zc1 = 0.97
    n = 10
    for k in range(n):
        a = TAU * k / n + TAU / (2 * n)
        x, y = 0.52 * math.cos(a), 0.52 * math.sin(a)
        cyl("gold", 0.05, 0.045, 0.04, zc, xy=(x, y), seg=8, name="col_base")
        cyl("white", 0.034, 0.030, zc1 - zc - 0.08, zc + 0.04, xy=(x, y), seg=10, name="column")
        cyl("gold", 0.032, 0.055, 0.04, zc1 - 0.04, xy=(x, y), seg=10, name="capital")
        if math.sin(a) < 0:  # banners on the front columns
            box("royal", 0.055, 0.008, 0.30, (x * 1.09, y * 1.09, zc + 0.42), rot=(0, 0, a + math.pi / 2), name="col_banner")
            box("gold", 0.065, 0.012, 0.010, (x * 1.09, y * 1.09, zc + 0.575), rot=(0, 0, a + math.pi / 2), name="col_banner_rod")
            sun_disc(x * 1.105, y * 1.105, zc + 0.44, rz=a + math.pi / 2, r=0.016)
    # entablature
    cyl("marble", 0.58, 0.58, 0.07, zc1, seg=32, name="entablature")
    cyl("gold", 0.585, 0.585, 0.016, zc1 + 0.01, seg=32, name="architrave")
    cyl("gold", 0.59, 0.59, 0.016, zc1 + 0.045, seg=32, name="frieze_band")
    torus("gold", 0.57, 0.01, (0, 0, zc1 + 0.075), seg=44, minor=4, name="cornice")
    # holographic star-map ribbons
    for ph, zz0, zz1 in ((0.0, zc + 0.12, zc1 - 0.08), (math.pi, zc + 0.20, zc1 - 0.04)):
        tube("lightning", helix(0, 0, zz0, zz1, 0.34, 1.25, phase=ph), 0.013, sides=5, name="ribbon")
    # central lectern with orb
    cyl("marble", 0.08, 0.10, 0.20, zc, seg=8, rot=(0, 0, math.pi / 8), name="lectern")
    cyl("gold", 0.105, 0.105, 0.014, zc + 0.16, seg=8, rot=(0, 0, math.pi / 8), name="lectern_trim")
    cyl("gold", 0.04, 0.07, 0.04, zc + 0.20, seg=12, name="lectern_cup")
    sphere("glass", 0.07, (0, 0, zc + 0.30), seg=16, rings=10, name="lectern_orb")
    torus("gold", 0.09, 0.006, (0, 0, zc + 0.30), rot=(math.radians(65), 0, 0), seg=28, minor=5, name="lectern_ring")
    # armillary dome
    zt = zc1 + 0.07
    for k in range(4):
        a = math.pi * k / 4
        pts = [(math.cos(a) * p[0], math.sin(a) * p[0], p[2]) for p in arc((0, 0, zt), 0.42, 0, math.pi, plane="xz", steps=14)]
        tube("gold", pts, 0.010, sides=5, name="meridian")
    torus("gold", 0.36, 0.008, (0, 0, zt + 0.21), seg=36, minor=4, name="parallel")
    torus("gold", 0.21, 0.008, (0, 0, zt + 0.36), seg=24, minor=4, name="parallel")
    sphere("glass", 0.13, (0, 0, zt + 0.26), seg=20, rings=12, name="globe")
    torus("gold", 0.16, 0.007, (0, 0, zt + 0.26), rot=(math.radians(66), 0, math.radians(20)), seg=36, minor=5, name="globe_ring")
    cyl("gold", 0.05, 0.03, 0.04, zt + 0.40, seg=12, name="lantern")
    cyl("gold", 0.012, 0.0, 0.10, zt + 0.44, seg=8, name="finial")
    # side towers with orbs
    for s in (-1, 1):
        x, y = s * 0.66, -0.12
        cyl("marble", 0.07, 0.07, 0.40, zd, xy=(x, y), seg=12, name="side_tower")
        cyl("gold", 0.075, 0.075, 0.014, zd + 0.30, xy=(x, y), seg=12, name="side_band")
        box("royal", 0.05, 0.01, 0.20, (x, y - 0.068, zd + 0.17), name="side_banner")
        cyl("gold", 0.08, 0.08, 0.02, zd + 0.40, xy=(x, y), seg=12, name="side_cap")
        cyl("gold", 0.03, 0.045, 0.04, zd + 0.42, xy=(x, y), seg=12, name="side_cup")
        armillary((x, y, zd + 0.52), 0.075, sw_core="glass", core_r=0.055, n=2, minor=0.005, seg=24)
        cyl("gold", 0.008, 0.0, 0.06, zd + 0.595, xy=(x, y), seg=6, name="side_finial")
    # front gate posts
    for s in (-1, 1):
        orb_post(s * 0.22, -0.66, 0.10, h=0.16, s=0.06, ring=False)


# ---------------------------------------------------------------- 7. Oracle of Storms
def zeus_structure_oracle_of_storms():
    """DT art: a gold-and-marble oracle palace with a grand glowing arched portal
    holding an armillary orb, wings of glowing arched alcoves and royal banners, a
    great staircase, and a branching lightning tree on the roof bearing seer orbs in
    gold rings."""
    z = rock_terrace()
    flat_banners()
    z2 = upper_terrace(0.62, z, 0.06)
    # grand stairs
    stairs(0.22, -0.30, z, z2 + 0.06, n=5, depth=0.06)
    # podium under the palace
    box("marble", 1.00, 0.52, 0.06, (0, 0.10, z2 + 0.03), name="podium")
    box("gold", 1.02, 0.54, 0.014, (0, 0.10, z2 + 0.035), name="podium_trim")
    p0 = z2 + 0.06
    # central hall
    hw, hd, hh = 0.42, 0.36, 0.52
    box("marble", hw, hd, hh, (0, 0.12, p0 + hh / 2), bevel=0.008, name="hall")
    box("gold", hw + 0.02, hd + 0.02, 0.018, (0, 0.12, p0 + hh - 0.04), name="hall_cornice")
    box("marble", hw + 0.04, hd + 0.04, 0.03, (0, 0.12, p0 + hh + 0.015), name="hall_roof")
    fy = 0.12 - hd / 2
    # portal
    box("lightning", 0.20, 0.012, 0.30, (0, fy - 0.004, p0 + 0.15), name="portal_glow")
    tube("gold", arc((0, fy - 0.014, p0 + 0.30), 0.10, 0, math.pi, steps=14), 0.014, sides=6, name="portal_arch")
    cyl("lightning", 0.10, 0.10, 0.012, p0 + 0.30, xy=(0, fy - 0.0), rot=(math.pi / 2, 0, 0), seg=20, name="portal_tympanum")
    for s in (-1, 1):
        box("gold", 0.026, 0.026, 0.30, (s * 0.10, fy - 0.014, p0 + 0.15), name="portal_jamb")
        cyl("marble", 0.03, 0.03, hh - 0.04, p0, xy=(s * 0.17, fy - 0.03), seg=10, name="portal_column")
        cyl("gold", 0.04, 0.04, 0.02, p0 + hh - 0.06, xy=(s * 0.17, fy - 0.03), seg=10, name="portal_capital")
    tube("gold", [(-0.19, fy - 0.03, p0 + hh - 0.02), (0, fy - 0.03, p0 + hh + 0.07), (0.19, fy - 0.03, p0 + hh - 0.02)], 0.014, sides=6, name="pediment")
    sun_disc(0, fy - 0.02, p0 + hh - 0.0, r=0.03)
    # armillary orb in front of the portal
    cyl("gold", 0.05, 0.07, 0.05, p0, xy=(0, fy - 0.10), seg=12, name="orb_stand")
    cyl("gold", 0.012, 0.012, 0.08, p0 + 0.05, xy=(0, fy - 0.10), seg=8, name="orb_stem")
    armillary((0, fy - 0.10, p0 + 0.20), 0.08, sw_core="glass", core_r=0.055, n=3, minor=0.006, seg=28)
    # wings with alcoves
    for s in (-1, 1):
        wx = s * 0.38
        ww, wd, wh = 0.28, 0.34, 0.36
        box("marble", ww, wd, wh, (wx, 0.14, p0 + wh / 2), bevel=0.006, name="wing")
        box("gold", ww + 0.02, wd + 0.02, 0.016, (wx, 0.14, p0 + wh - 0.035), name="wing_cornice")
        wf = 0.14 - wd / 2
        for dx in (-0.07, 0.07):
            x = wx + dx
            box("lightning", 0.07, 0.01, 0.17, (x, wf - 0.004, p0 + 0.12), name="alcove_glow")
            tube("gold", arc((x, wf - 0.01, p0 + 0.205), 0.035, 0, math.pi, steps=8), 0.007, sides=5, name="alcove_arch")
            box("gold", 0.010, 0.012, 0.17, (x - 0.035, wf - 0.01, p0 + 0.12), name="alcove_jamb")
            box("gold", 0.010, 0.012, 0.17, (x + 0.035, wf - 0.01, p0 + 0.12), name="alcove_jamb")
        # banners between alcoves and at the inner edge
        box("royal", 0.05, 0.01, 0.26, (wx, wf - 0.008, p0 + 0.17), name="wing_banner")
        box("gold", 0.06, 0.014, 0.010, (wx, wf - 0.01, p0 + 0.305), name="wing_banner_rod")
        box("royal", 0.05, 0.01, 0.30, (s * 0.235, fy - 0.006, p0 + 0.20), name="hall_banner")
        sun_disc(s * 0.235, fy - 0.012, p0 + 0.22, r=0.016)
        # corner tower with dome
        tx, ty = s * 0.50, 0.02
        cyl("marble", 0.06, 0.06, wh + 0.14, p0, xy=(tx, ty), seg=12, name="corner_tower")
        cyl("gold", 0.065, 0.065, 0.014, p0 + wh + 0.08, xy=(tx, ty), seg=12, name="corner_band")
        dome("gold", 0.06, p0 + wh + 0.14, xy=(tx, ty), seg=14, rings=4, name="corner_dome")
        cyl("gold", 0.008, 0.0, 0.08, p0 + wh + 0.20, xy=(tx, ty), seg=6, name="corner_finial")
        # small balustrade posts on the wing roofs
        for dx in (-0.08, 0.0, 0.08):
            cyl("marble", 0.016, 0.016, 0.05, p0 + wh, xy=(wx + dx, wf + 0.02), seg=8, name="baluster")
    # front gate pillars
    for s in (-1, 1):
        orb_post(s * 0.20, -0.56, z2, h=0.14, s=0.06, ring=False)
    # lightning tree on the roof
    rz = p0 + hh + 0.03
    cyl("gold", 0.08, 0.10, 0.04, rz, xy=(0, 0.12), seg=12, name="tree_base")
    trunk = [(0, 0.12, rz + 0.04), (0.01, 0.12, rz + 0.14), (-0.01, 0.12, rz + 0.24), (0, 0.12, rz + 0.32)]
    tube("lightning", trunk, 0.035, 0.025, sides=8, name="trunk")
    top = trunk[-1]
    orbs = [((0, 0.12, 1.40), 0.13), ((-0.40, 0.14, 1.14), 0.10), ((0.40, 0.14, 1.14), 0.10), ((-0.24, 0.18, 1.36), 0.085), ((0.24, 0.18, 1.36), 0.085)]
    for (c, r) in orbs:
        mid = ((top[0] + c[0]) / 2 * 1.1, (top[1] + c[1]) / 2, (top[2] + c[2]) / 2 - 0.03)
        tube("lightning", [top, mid, (c[0] * 0.95, c[1], c[2] - r - 0.01)], 0.016, 0.008, sides=6, name="branch")
        sphere("glass", r, c, seg=18, rings=10, name="seer_orb")
        torus("gold", r + 0.02, 0.008, c, rot=(math.radians(80), 0, math.radians(15)), seg=28, minor=4, name="orb_ring")
        cyl("gold", r * 0.4, r * 0.6, 0.025, c[2] - r - 0.02, xy=(c[0], c[1]), seg=12, name="orb_cup")
        # twigs
        if r > 0.12:
            for t in (0.6, 2.4):
                tube("lightning", [(c[0], c[1], c[2] - r * 0.5), (c[0] + r * 0.8 * math.cos(t), c[1] - 0.02, c[2] + r * 0.2 * math.sin(t))], 0.005, 0.002, sides=4, name="twig")
    # great elliptical gold rings around the tree
    torus("gold", 0.56, 0.010, (0, 0.14, 1.12), rot=(math.radians(10), 0, 0), seg=48, minor=5, name="tree_ring")
    torus("gold", 0.36, 0.008, (0, 0.14, 1.26), rot=(math.radians(-8), 0, 0), seg=40, minor=4, name="tree_ring")
    for s in (-1, 1):
        tube("gold", [(s * 0.50, 0.04, p0 + wh + 0.22), (s * 0.54, 0.10, 1.05), (s * 0.55, 0.14, 1.11)], 0.008, sides=5, name="ring_post")


# ---------------------------------------------------------------- 8. Stormglass Relay
def crystal(sw, c, r, h):
    """Upright octahedral crystal: a 4-sided uv sphere stretched vertically."""
    sphere(sw, r, c, seg=4, rings=2, scale=(1, 1, h / r), name="crystal")


def zeus_structure_stormglass_relay():
    """DT art: an hourglass relay tower of stacked glowing storm crystals held in a
    gold frame with halo rings and a gold finial, on a round marble plaza; marble
    pylons with royal banners carry orbs in gold rings and stream lightning beams
    into the tower; a staircase climbs to the tower door."""
    zd = round_deck()
    cyl("royal", 0.48, 0.48, 0.006, zd, seg=32, name="plaza_inlay")
    torus("gold", 0.48, 0.006, (0, 0, zd + 0.006), seg=48, minor=4, name="inlay_ring")
    torus("lightning", 0.36, 0.005, (0, 0, zd + 0.006), seg=40, minor=4, name="inlay_glow")
    # tower base
    tb0, tb1 = zd, zd + 0.20
    cyl("marble", 0.26, 0.28, tb1 - tb0, tb0, seg=8, rot=(0, 0, math.pi / 8), name="tower_base")
    cyl("gold", 0.285, 0.285, 0.016, tb0 + 0.01, seg=8, rot=(0, 0, math.pi / 8), name="base_foot")
    cyl("gold", 0.27, 0.27, 0.016, tb1 - 0.04, seg=8, rot=(0, 0, math.pi / 8), name="base_trim")
    for k in range(8):
        a = TAU * k / 8 + TAU / 16
        d = 0.26
        if k in (5, 6):
            continue
        box("royal", 0.07, 0.01, 0.10, (d * math.cos(a), d * math.sin(a), tb0 + 0.09), rot=(0, 0, a + math.pi / 2), name="base_panel")
    # door + small stair up to the base
    box("lightning", 0.08, 0.01, 0.12, (0, -0.262, tb0 + 0.07), name="door")
    tube("gold", arc((0, -0.268, tb0 + 0.13), 0.04, 0, math.pi, steps=8), 0.007, sides=5, name="door_arch")
    stairs(0.14, -0.27, zd, zd + 0.08, n=3, depth=0.04)
    cyl("gold", 0.20, 0.22, 0.04, tb1, seg=16, name="tower_collar")
    # frame posts
    f0, f1 = tb1 + 0.04, 1.30
    for k in range(4):
        a = TAU * k / 4 + TAU / 8
        x, y = 0.19 * math.cos(a), 0.19 * math.sin(a)
        cyl("marble", 0.022, 0.022, f1 - f0, f0, xy=(x, y), seg=8, name="frame_post")
        cyl("gold", 0.028, 0.028, 0.02, f0 + 0.15, xy=(x, y), seg=8, name="post_band")
        cyl("gold", 0.028, 0.028, 0.02, f1 - 0.12, xy=(x, y), seg=8, name="post_band")
        cyl("gold", 0.022, 0.0, 0.08, f1, xy=(x, y), seg=8, name="post_tip")
        # curved outer struts (the hourglass outline)
        tube("gold", [(x, y, f0 + 0.02), (x * 1.45, y * 1.45, f0 + 0.20), (x * 1.0, y * 1.0, 0.86), (x * 1.5, y * 1.5, 1.08), (x, y, f1 - 0.02)], 0.010, sides=6, name="hourglass_strut")
    # crystals
    cyl("lightning", 0.02, 0.02, f1 - f0, f0, seg=8, name="core_beam")
    crystal("glass", (0, 0, f0 + 0.17), 0.11, 0.17)
    crystal("glass", (0, 0, 0.86), 0.09, 0.12)
    crystal("glass", (0, 0, 1.10), 0.13, 0.18)
    crystal("lightning", (0, 0, f0 + 0.17), 0.05, 0.10)
    crystal("lightning", (0, 0, 1.10), 0.06, 0.11)
    # gold waist rings
    for zz, R in ((f0 + 0.005, 0.19), (0.72, 0.17), (0.98, 0.17), (f1 - 0.01, 0.19)):
        torus("gold", R, 0.012, (0, 0, zz), seg=36, minor=6, name="frame_ring")
    torus("gold", 0.30, 0.010, (0, 0, 0.98), rot=(math.radians(8), 0, 0), seg=48, minor=6, name="halo")
    torus("gold", 0.26, 0.008, (0, 0, 0.72), rot=(math.radians(-8), 0, 0), seg=44, minor=5, name="halo")
    # crown and finial
    cyl("gold", 0.20, 0.12, 0.04, f1, seg=16, name="crown")
    cyl("gold", 0.10, 0.0, 0.16, f1 + 0.04, seg=8, rot=(0, 0, math.pi / 8), name="crown_spire")
    sphere("lightning", 0.025, (0, 0, f1 + 0.13), seg=10, rings=6, name="crown_spark")
    cyl("gold", 0.01, 0.0, 0.10, f1 + 0.18, seg=6, name="finial")
    # pylons on the plaza with orbs and beams
    for deg in (210, 330, 30, 150):
        a = math.radians(deg)
        r = 0.60
        x, y = r * math.cos(a), r * math.sin(a)
        ph = 0.34 if deg in (210, 330) else 0.42
        box("marble", 0.10, 0.10, ph, (x, y, zd + ph / 2), rot=(0, 0, a), bevel=0.006, name="pylon")
        box("gold", 0.12, 0.12, 0.016, (x, y, zd + 0.012), rot=(0, 0, a), name="pylon_foot")
        box("gold", 0.12, 0.12, 0.02, (x, y, zd + ph + 0.01), rot=(0, 0, a), name="pylon_cap")
        box("gold", 0.106, 0.106, 0.014, (x, y, zd + ph - 0.05), rot=(0, 0, a), name="pylon_band")
        # banner facing outward-front
        ox, oy = math.cos(a), math.sin(a)
        box("royal", 0.07, 0.008, 0.20, (x + ox * 0.052, y + oy * 0.052, zd + ph * 0.48), rot=(0, 0, a + math.pi / 2), name="pylon_banner")
        sun_disc(x + ox * 0.058, y + oy * 0.058, zd + ph * 0.5, rz=a + math.pi / 2, r=0.016)
        cyl("gold", 0.025, 0.035, 0.025, zd + ph + 0.02, xy=(x, y), seg=12, name="pylon_cup")
        oc = (x, y, zd + ph + 0.09)
        sphere("glass", 0.04, oc, seg=14, rings=8, name="pylon_orb")
        torus("gold", 0.055, 0.004, oc, rot=(math.radians(75), 0, a), seg=24, minor=4, name="pylon_ring")
        torus("gold", 0.055, 0.004, oc, rot=(math.radians(15), 0, a), seg=24, minor=4, name="pylon_ring")
        cyl("gold", 0.007, 0.0, 0.06, oc[2] + 0.055, xy=(x, y), seg=6, name="pylon_finial")
        # lightning beam arcing to the middle crystal
        tgt = (0.10 * ox, 0.10 * oy, 0.88)
        midp = ((x + tgt[0]) / 2, (y + tgt[1]) / 2, max(oc[2], tgt[2]) + 0.10)
        pts = [(oc[0] - ox * 0.04, oc[1] - oy * 0.04, oc[2] + 0.01),
               ((oc[0] + midp[0]) / 2, (oc[1] + midp[1]) / 2, (oc[2] + midp[2]) / 2 + 0.04), midp,
               ((midp[0] + tgt[0]) / 2, (midp[1] + tgt[1]) / 2, (midp[2] + tgt[2]) / 2 + 0.02), tgt]
        tube("lightning", pts, 0.009, 0.006, sides=6, name="beam")


BUILDERS = {
    "zeus_ability_oracle_spire": ("Oracle Spire", "Zeus", zeus_ability_oracle_spire),
    "zeus_apex_worldstorm_spire": ("Worldstorm Spire", "Zeus", zeus_apex_worldstorm_spire),
    "zeus_cloudwall_bastion": ("Cloudwall Bastion", "Zeus", zeus_cloudwall_bastion),
    "zeus_ion_storm_lattice": ("Ion Storm Lattice", "Zeus", zeus_ion_storm_lattice),
    "zeus_structure_aegis_conductor": ("Aegis Conductor", "Zeus", zeus_structure_aegis_conductor),
    "zeus_structure_cloud_archive": ("Cloud Archive", "Zeus", zeus_structure_cloud_archive),
    "zeus_structure_oracle_of_storms": ("Oracle of Storms", "Zeus", zeus_structure_oracle_of_storms),
    "zeus_structure_stormglass_relay": ("Stormglass Relay", "Zeus", zeus_structure_stormglass_relay),
}
