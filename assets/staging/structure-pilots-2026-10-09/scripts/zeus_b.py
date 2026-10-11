"""Zeus structures, batch B: tutor structures 1-5, Zephyr Mooring Mast, Zeus Command Nexus.
Each builder only adds parts via kit; conventions follow structures.py."""
from kit import box, cyl, sphere, dome, torus, tube, helix, arc
import math

TAU = math.tau
HEX30 = 0.0  # bmesh hexes are already pointy-top


# ---------------------------------------------------------------- shared bits
def _plinth(top=0.15):
    """Short rock plinth with a marble terrace and a gold band on its side."""
    cyl("stone", 0.80, 0.86, 0.10, 0.0, seg=6, rot=(0, 0, HEX30), name="rock")
    cyl("marble", 0.82, 0.80, top - 0.10, 0.10, seg=6, rot=(0, 0, HEX30), name="terrace")
    cyl("gold", 0.83, 0.83, 0.014, top - 0.025, seg=6, rot=(0, 0, HEX30), name="terrace_trim")


def _flat_banners(degs=(240, 300), d=0.752, zc=0.08, w=0.16, h=0.12):
    """Royal sun banners hanging on plinth flats (pointy-top hex: flats at 0,60,...)."""
    for deg in degs:
        a = math.radians(deg)
        rz = a + math.pi / 2

        def at(dd, z):
            return (dd * math.cos(a), dd * math.sin(a), z)
        box("royal", w, 0.012, h, at(d, zc), rot=(0, 0, rz), name="banner")
        box("gold", w + 0.02, 0.016, 0.016, at(d, zc + h / 2 + 0.008), rot=(0, 0, rz), name="banner_rod")
        box("gold", w, 0.014, 0.008, at(d, zc - h / 2), rot=(0, 0, rz), name="banner_hem")
        x, y, _ = at(d + 0.008, 0)
        cyl("gold", 0.024, 0.024, 0.004, zc, xy=(x, y), rot=(math.pi / 2, 0, rz), seg=16, name="banner_sun")
        torus("gold", 0.036, 0.004, at(d + 0.008, zc), rot=(math.pi / 2, 0, rz), seg=20, minor=4, name="banner_sun_ring")


def _brazier(x, y, z, s=1.0):
    """Gold bowl on a short stem with a blue lightning flame."""
    cyl("gold", 0.012 * s, 0.018 * s, 0.03 * s, z, xy=(x, y), seg=8, name="brazier_stem")
    cyl("gold", 0.018 * s, 0.045 * s, 0.035 * s, z + 0.03 * s, xy=(x, y), seg=12, name="brazier_bowl")
    torus("gold", 0.045 * s, 0.005 * s, (x, y, z + 0.065 * s), seg=12, minor=4, name="brazier_lip")
    cyl("lightning", 0.03 * s, 0.0, 0.08 * s, z + 0.055 * s, xy=(x, y), seg=8, name="brazier_flame")


def _orb_post(x, y, z, h=0.2, w=0.07, orb="glass"):
    """Square marble post with gold cap and foot, a glowing orb on top."""
    box("marble", w, w, h, (x, y, z + h / 2), bevel=0.005, name="post")
    box("gold", w + 0.015, w + 0.015, 0.014, (x, y, z + 0.012), name="post_foot")
    box("gold", w + 0.02, w + 0.02, 0.018, (x, y, z + h), name="post_cap")
    cyl("gold", 0.018, 0.028, 0.02, z + h + 0.009, xy=(x, y), seg=10, name="orb_cup")
    sphere(orb, w * 0.42, (x, y, z + h + 0.03 + w * 0.38), seg=14, rings=8, name="post_orb")


def _armillary(c, R, orb_r, orb="lightning", tilt=(70, -60), seg=36, minor=6):
    """Glowing orb inside an open gold armillary cage."""
    x, y, z = c
    sphere(orb, orb_r, c, seg=max(10, seg * 2 // 3), rings=max(6, seg // 3), name="arm_orb")
    torus("gold", R, R * 0.06, c, seg=seg, minor=minor, name="arm_equator")
    torus("gold", R * 1.02, R * 0.05, c, rot=(math.pi / 2, 0, 0), seg=seg, minor=minor, name="arm_meridian")
    torus("gold", R * 1.04, R * 0.045, c, rot=(math.radians(tilt[0]), 0, math.radians(25)), seg=seg, minor=minor, name="arm_ring")
    torus("gold", R * 1.06, R * 0.045, c, rot=(math.radians(tilt[1]), math.radians(30), 0), seg=seg, minor=minor, name="arm_ring")


def _key_ring(z, R, h=0.04, n=16):
    """Gold ring band with royal greek-key notches and gold rims."""
    cyl("gold", R, R, h, z - h / 2, seg=32, name="ring_band", cap=False)
    cyl("gold", R - 0.012, R - 0.012, h, z - h / 2, seg=32, name="ring_inner", cap=False)
    torus("gold", R, 0.008, (0, 0, z + h / 2), seg=40, minor=5, name="ring_rim")
    torus("gold", R, 0.008, (0, 0, z - h / 2), seg=40, minor=5, name="ring_rim")
    for k in range(n):
        a = TAU * k / n
        box("royal", 0.022, 0.006, h * 0.5, (R * math.cos(a), R * math.sin(a), z), rot=(0, 0, a + math.pi / 2), name="ring_key")


def _feather_wing(s, root, n=10):
    """Fan of marble feathers with gold edges sweeping up and out, side s=+-1."""
    rx, ry, rz = root
    for k in range(n):
        t = k / (n - 1)
        th0 = math.radians(4 + 62 * t)
        L = 0.94 - 0.40 * t
        yy = ry + 0.010 * k
        pts, edge, inl = [], [], []
        for i in range(7):
            u = i / 6
            th = th0 + math.radians(14) * u * u  # curl outward toward the tip
            d = L * u
            px, pz = rx + s * d * math.sin(th), rz + d * math.cos(th)
            pts.append((px, yy, pz))
            nx, nz = math.cos(th), -math.sin(th)  # outward normal in the wing plane
            edge.append((px + s * 0.04 * nx, yy - 0.004, pz + 0.04 * nz))
            inl.append((px - s * 0.004 * nx, yy - 0.03, pz - 0.004 * nz))
        tube("marble", pts, 0.05, 0.016, sides=5, name="feather")
        tube("gold", edge[1:], 0.009, 0.004, sides=3, name="feather_edge")
        if k % 2 == 0:
            tube("royal", inl[2:5], 0.014, sides=4, name="feather_inlay")
        tip = pts[-1]
        sphere("gold", 0.014, tip, seg=6, rings=3, name="feather_tip")
    # covert layer: short broad feathers over the wing root
    for k in range(5):
        th = math.radians(10 + 14 * k)
        L = 0.34
        p0 = (rx, ry - 0.035, rz)
        p1 = (rx + s * L * math.sin(th), ry - 0.035, rz + L * math.cos(th))
        tube("marble", [p0, ((p0[0] + p1[0]) / 2, p0[1], (p0[2] + p1[2]) / 2), p1], 0.055, 0.02, sides=5, name="covert")
        tube("gold", [(p1[0] - s * 0.05 * math.sin(th), p1[1] - 0.03, p1[2] - 0.05 * math.cos(th)), (p1[0], p1[1] - 0.025, p1[2])], 0.01, 0.004, sides=4, name="covert_tip")
    # gold shoulder arc tying the feathers together
    pts = [(rx + s * 0.32 * math.sin(math.radians(a)), ry - 0.02, rz + 0.32 * math.cos(math.radians(a))) for a in range(4, 80, 8)]
    tube("gold", pts, 0.014, sides=6, name="wing_arc")
    sphere("gold", 0.04, (rx, ry, rz), seg=10, rings=6, name="wing_root")


# ---------------------------------------------------------------- 1
def zeus_tutor_structure_1():
    """Sparkstep Beacon: round marble terrace with a grand front stair, four tall
    flaring marble pillars around a lightning beam and chalice, gold armillary
    orb with a spire on top, banners and blue-flame braziers."""
    _plinth()
    _flat_banners()
    cyl("marble", 0.64, 0.62, 0.06, 0.15, seg=32, name="round_terrace")
    cyl("gold", 0.645, 0.645, 0.014, 0.185, seg=32, name="round_trim")
    # balustrade: posts and rail with a gap at the front for the stair
    for k in range(18):
        a = math.radians(-90) + TAU * k / 18
        if abs(math.degrees((a + math.pi / 2 + math.pi) % TAU - math.pi)) < 25:
            continue
        cyl("marble", 0.012, 0.012, 0.05, 0.21, xy=(0.6 * math.cos(a), 0.6 * math.sin(a)), seg=6, name="baluster")
    tube("gold", arc((0, 0, 0.262), 0.6, math.radians(-65), math.radians(245), plane="xy", steps=40), 0.008, sides=5, name="rail")
    # inner dais
    cyl("marble", 0.36, 0.34, 0.07, 0.21, seg=24, name="dais")
    cyl("gold", 0.365, 0.365, 0.014, 0.255, seg=24, name="dais_trim")
    cyl("royal", 0.22, 0.22, 0.006, 0.28, seg=24, name="dais_inlay")
    torus("gold", 0.22, 0.006, (0, 0, 0.286), seg=32, minor=4, name="inlay_ring")
    # grand stair from the front edge up to the dais, with a glowing runner
    for i in range(6):
        z = 0.15 + 0.022 * (i + 1)
        y = -0.80 + i * 0.075
        box("marble", 0.30, 0.08, z - 0.15 + 0.0, (0, y, 0.15 + (z - 0.15) / 2), name="step")
        box("gold", 0.31, 0.012, 0.006, (0, y - 0.04, z - 0.004), name="step_nosing")
    box("lightning", 0.05, 0.42, 0.006, (0, -0.58, 0.215), rot=(math.atan2(0.13, 0.45), 0, 0), name="stair_runner")
    for s in (-1, 1):
        box("marble", 0.04, 0.46, 0.06, (s * 0.17, -0.58, 0.20), rot=(math.atan2(0.13, 0.45), 0, 0), name="stair_wall")
        _orb_post(s * 0.17, -0.80, 0.15, h=0.12, w=0.06)
    # four flaring pillars (hourglass frame) with gold ribs
    z0, z1 = 0.29, 0.98
    for a in (45, 135, 225, 315):
        ar = math.radians(a)
        pts = []
        for i in range(13):
            t = i / 12
            r = 0.21 + 0.05 * math.cos(t * math.pi) ** 2 * (1 if t < 0.5 else 1.6)
            pts.append((r * math.cos(ar), r * math.sin(ar), z0 + (z1 - z0) * t))
        tube("marble", pts, 0.055, 0.05, sides=8, name="pillar")
        for off in (-0.33, 0.33):
            tube("gold", [(p[0] * 1.0 + 0.05 * math.cos(ar + math.pi / 2) * off * 3, p[1] + 0.05 * math.sin(ar + math.pi / 2) * off * 3, p[2]) for p in pts], 0.008, sides=4, name="pillar_rib")
        tube("lightning", [(p[0] * 1.22, p[1] * 1.22, p[2]) for p in pts[2:11]], 0.006, sides=5, name="pillar_glow")
        x, y = pts[0][0], pts[0][1]
        box("gold", 0.13, 0.13, 0.03, (x, y, z0 + 0.0), rot=(0, 0, ar), name="pillar_foot")
        x, y = pts[-1][0], pts[-1][1]
        box("gold", 0.14, 0.14, 0.035, (x, y, z1), rot=(0, 0, ar), name="pillar_capital")
        cyl("gold", 0.012, 0.0, 0.07, z1 + 0.017, xy=(x, y), seg=8, name="pillar_finial")
    # crown platform tying the pillars
    cyl("gold", 0.30, 0.30, 0.04, z1 + 0.0, seg=24, name="crown_ring", cap=False)
    torus("gold", 0.30, 0.012, (0, 0, z1 + 0.04), seg=28, minor=5, name="crown_lip")
    torus("gold", 0.30, 0.012, (0, 0, z1), seg=28, minor=5, name="crown_lip")
    cyl("gold", 0.06, 0.12, 0.06, z1 + 0.0, seg=16, name="orb_cradle")
    # chalice with lightning beam rising through the frame
    cyl("gold", 0.05, 0.03, 0.04, 0.29, seg=12, name="chalice_foot")
    cyl("gold", 0.02, 0.02, 0.06, 0.33, seg=8, name="chalice_stem")
    cyl("gold", 0.02, 0.09, 0.06, 0.39, seg=16, name="chalice_bowl")
    torus("gold", 0.09, 0.008, (0, 0, 0.45), seg=24, minor=5, name="chalice_lip")
    cyl("lightning", 0.06, 0.0, 0.14, 0.43, seg=10, name="chalice_flame")
    cyl("lightning", 0.022, 0.022, z1 - 0.45, 0.45, seg=10, name="beam")
    tube("lightning", helix(0, 0, 0.5, z1 - 0.02, 0.05, 2.0, steps_per_turn=16), 0.006, sides=4, name="beam_coil")
    # gold armillary with a lightning orb and spire
    _armillary((0, 0, 1.24), 0.20, 0.12, seg=30)
    cyl("gold", 0.012, 0.012, 0.50, 1.00, seg=8, name="axis")
    cyl("gold", 0.03, 0.0, 0.12, 1.47, seg=8, name="spire")
    sphere("gold", 0.025, (0, 0, 1.47), seg=10, rings=6, name="spire_knob")
    # tall banners hung beside the frame and braziers on the terrace
    for s in (-1, 1):
        x = s * 0.42
        cyl("gold", 0.01, 0.01, 0.62, 0.21, xy=(x, 0.06), seg=6, name="banner_pole")
        box("gold", 0.16, 0.012, 0.012, (x, 0.05, 0.80), name="banner_bar")
        box("royal", 0.13, 0.008, 0.34, (x, 0.04, 0.62), name="tall_banner")
        cyl("gold", 0.028, 0.028, 0.004, 0.66, xy=(x, 0.035), rot=(math.pi / 2, 0, 0), seg=16, name="banner_sun")
        box("gold", 0.13, 0.01, 0.01, (x, 0.038, 0.45), name="banner_hem")
        cyl("gold", 0.012, 0.0, 0.05, 0.83, xy=(x, 0.06), seg=6, name="pole_tip")
    for a in (200, 340, 70, 110):
        x, y = 0.5 * math.cos(math.radians(a)), 0.5 * math.sin(math.radians(a))
        box("marble", 0.06, 0.06, 0.12, (x, y, 0.27), bevel=0.004, name="brazier_post")
        box("gold", 0.07, 0.07, 0.014, (x, y, 0.33), name="brazier_post_cap")
        _brazier(x, y, 0.337)


# ---------------------------------------------------------------- 2
def zeus_tutor_structure_2():
    """Cloudline Dispatch: arcaded marble base, cluster-column tower flanked by
    banner spires, gold ring beacon with a glowing orb and spikes on top,
    corner orb posts with gold rings and lightning tethers, gold griffins."""
    _plinth()
    _flat_banners()
    # arcade base
    zb = 0.15
    box("marble", 0.92, 0.58, 0.22, (0, 0.06, zb + 0.11), bevel=0.01, name="arcade")
    box("gold", 0.94, 0.60, 0.018, (0, 0.06, zb + 0.205), name="arcade_cornice")
    box("marble", 0.96, 0.62, 0.025, (0, 0.06, zb + 0.235), name="arcade_roof")
    box("gold", 0.94, 0.60, 0.014, (0, 0.06, zb + 0.012), name="arcade_plinth")
    for i in range(5):
        x = -0.36 + i * 0.18
        box("royal", 0.10, 0.01, 0.12, (x, -0.236, zb + 0.08), name="arch_recess")
        cyl("royal", 0.05, 0.05, 0.01, zb + 0.14, xy=(x, -0.231), rot=(math.pi / 2, 0, 0), seg=16, name="arch_head")
        tube("gold", arc((x, -0.245, zb + 0.14), 0.058, 0, math.pi, steps=10), 0.007, sides=5, name="arch_trim")
        if i != 2:
            box("lightning", 0.04, 0.006, 0.06, (x, -0.243, zb + 0.07), name="arch_glow")
    for i in range(6):
        x = -0.45 + i * 0.18
        box("marble", 0.035, 0.03, 0.2, (x, -0.245, zb + 0.10), name="pilaster")
    # front banner over the centre arch
    box("royal", 0.11, 0.008, 0.16, (0, -0.252, zb + 0.12), name="gate_banner")
    cyl("gold", 0.025, 0.025, 0.004, zb + 0.13, xy=(0, -0.258), rot=(math.pi / 2, 0, 0), seg=16, name="gate_sun")
    zt = zb + 0.26
    # central tower: square podium, four marble columns around a lightning core
    box("marble", 0.30, 0.30, 0.08, (0, 0.08, zt + 0.04), bevel=0.006, name="tower_podium")
    box("gold", 0.32, 0.32, 0.016, (0, 0.08, zt + 0.07), name="podium_trim")
    zc0, zc1 = zt + 0.08, 1.06
    cyl("lightning", 0.05, 0.05, zc1 - zc0, zc0, xy=(0, 0.08), seg=12, name="tower_core")
    for dx in (-0.08, 0.08):
        for dy in (-0.08, 0.08):
            cyl("marble", 0.045, 0.04, zc1 - zc0, zc0, xy=(dx, 0.08 + dy), seg=12, name="tower_column")
            for z in (zc0 + 0.02, 0.75, zc1 - 0.03):
                cyl("gold", 0.05, 0.05, 0.016, z - 0.008, xy=(dx, 0.08 + dy), seg=12, name="column_band")
    box("royal", 0.08, 0.008, 0.30, (0, -0.04, 0.72), name="tower_banner")
    box("gold", 0.012, 0.01, 0.30, (-0.044, -0.042, 0.72), name="tower_banner_edge")
    box("gold", 0.012, 0.01, 0.30, (0.044, -0.042, 0.72), name="tower_banner_edge")
    cyl("gold", 0.024, 0.024, 0.004, 0.78, xy=(0, -0.046), rot=(math.pi / 2, 0, 0), seg=16, name="tower_sun")
    box("gold", 0.26, 0.26, 0.04, (0, 0.08, zc1 + 0.02), name="tower_capital")
    box("marble", 0.22, 0.22, 0.05, (0, 0.08, zc1 + 0.065), name="tower_attic")
    cyl("gold", 0.06, 0.03, 0.08, zc1 + 0.09, xy=(0, 0.08), seg=12, name="ring_mount")
    # ring beacon: vertical gold ring facing front with a glowing orb, spikes
    rc = (0, 0.06, 1.32)
    torus("gold", 0.17, 0.018, rc, rot=(math.pi / 2, 0, 0), seg=48, minor=8, name="beacon_ring")
    torus("gold", 0.135, 0.008, rc, rot=(math.pi / 2, 0, 0), seg=40, minor=5, name="beacon_ring_inner")
    torus("royal", 0.152, 0.01, rc, rot=(math.pi / 2, 0, 0), seg=40, minor=5, name="beacon_ring_blue")
    sphere("lightning", 0.07, rc, seg=20, rings=12, name="beacon_orb")
    torus("gold", 0.085, 0.006, rc, rot=(math.radians(60), 0, 0), seg=28, minor=5, name="beacon_gyro")
    for k in range(4):
        a = TAU * k / 4 + math.pi / 4
        p0 = (rc[0] + 0.14 * math.cos(a), rc[1], rc[2] + 0.14 * math.sin(a))
        p1 = (rc[0] + 0.24 * math.cos(a), rc[1], rc[2] + 0.24 * math.sin(a))
        tube("gold", [p0, p1], 0.012, 0.002, sides=6, name="beacon_spike")
    for k in range(4):
        a = TAU * k / 4
        p0 = (rc[0] + 0.07 * math.cos(a), rc[1], rc[2] + 0.07 * math.sin(a))
        p1 = (rc[0] + 0.13 * math.cos(a), rc[1], rc[2] + 0.13 * math.sin(a))
        tube("gold", [p0, p1], 0.007, sides=5, name="beacon_spoke")
    # flanking banner spires
    for s in (-1, 1):
        x = s * 0.26
        box("marble", 0.09, 0.09, 0.62, (x, 0.12, zt + 0.31), bevel=0.006, name="spire_shaft")
        for z in (zt + 0.03, zt + 0.40, zt + 0.61):
            box("gold", 0.10, 0.10, 0.016, (x, 0.12, z), name="spire_band")
        cyl("marble", 0.07, 0.0, 0.26, zt + 0.62, xy=(x, 0.12), seg=4, rot=(0, 0, math.pi / 4), name="spire_cap")
        cyl("gold", 0.008, 0.0, 0.08, zt + 0.86, xy=(x, 0.12), seg=6, name="spire_tip")
        box("gold", 0.11, 0.012, 0.012, (x, 0.065, zt + 0.55), name="spire_banner_rod")
        box("royal", 0.09, 0.008, 0.28, (x, 0.068, zt + 0.40), name="spire_banner")
        cyl("royal", 0.045, 0.0, 0.05, zt + 0.26 - 0.0, xy=(x, 0.068), seg=4, rot=(0, math.pi, math.pi / 4), name="spire_banner_tail")
        cyl("gold", 0.022, 0.022, 0.004, zt + 0.43, xy=(x, 0.063), rot=(math.pi / 2, 0, 0), seg=16, name="spire_sun")
    # corner orb posts in gold rings, lightning tethers out from the beacon
    for (x, y) in ((-0.56, -0.30), (0.56, -0.30), (-0.50, 0.42), (0.50, 0.42)):
        _orb_post(x, y, 0.15, h=0.22, w=0.075)
        torus("gold", 0.05, 0.005, (x, y, 0.15 + 0.22 + 0.06), rot=(math.pi / 2, 0, 0), seg=20, minor=4, name="orb_ring")
        top = (x, y, 0.15 + 0.22 + 0.06)
        p0 = (rc[0] + 0.17 * math.copysign(1, x), rc[1], rc[2])
        ctrl = (x * 1.05, y * 0.9, rc[2] + 0.02)
        pts = []
        for i in range(13):
            t = i / 12
            q = [(1 - t) ** 2 * p0[j] + 2 * (1 - t) * t * ctrl[j] + t * t * top[j] for j in range(3)]
            q[0] += 0.012 * math.sin(t * 19)  # crackle
            q[2] += 0.012 * math.cos(t * 23)
            pts.append(tuple(q))
        tube("lightning", pts, 0.006, sides=5, name="tether")
    # gold griffins on the arcade roof corners
    for s in (-1, 1):
        x, y, z = s * 0.40, -0.16, zb + 0.25
        box("gold", 0.06, 0.06, 0.03, (x, y, z + 0.015), name="griffin_plinth")
        sphere("gold", 0.03, (x, y, z + 0.055), seg=12, rings=8, scale=(0.8, 1.2, 1), name="griffin_body")
        sphere("gold", 0.017, (x, y - 0.03, z + 0.09), seg=10, rings=6, name="griffin_head")
        cyl("gold", 0.007, 0.0, 0.02, z + 0.09, xy=(x, y - 0.045), rot=(math.pi / 2, 0, 0), seg=6, name="griffin_beak")
        for w in (-1, 1):
            box("gold", 0.012, 0.04, 0.07, (x + w * 0.03, y + 0.01, z + 0.10), rot=(0.4, w * 0.35, 0), name="griffin_wing")


# ---------------------------------------------------------------- 3
def zeus_tutor_structure_3():
    """Sharp-Shot Observatory: round tiered platform, marble rotunda with banners,
    a giant vertical gold lens ring with a starry royal disc and lightning
    crosshair on top, armillary pedestals, glowing summoning circle."""
    _plinth()
    _flat_banners()
    cyl("marble", 0.66, 0.64, 0.05, 0.15, seg=32, name="tier1")
    cyl("gold", 0.665, 0.665, 0.012, 0.18, seg=32, name="tier1_trim")
    cyl("marble", 0.48, 0.46, 0.05, 0.20, seg=32, name="tier2")
    cyl("gold", 0.485, 0.485, 0.012, 0.23, seg=32, name="tier2_trim")
    # gold balustrade ring on tier 1 with braziers
    for k in range(20):
        a = TAU * k / 20
        if math.sin(a) < -0.93:
            continue
        cyl("gold", 0.008, 0.008, 0.045, 0.20, xy=(0.6 * math.cos(a), 0.6 * math.sin(a)), seg=6, name="baluster")
    tube("gold", arc((0, 0, 0.245), 0.6, math.radians(-68), math.radians(248), plane="xy", steps=40), 0.008, sides=5, name="rail")
    for a in (200, 340, 150, 30):
        x, y = 0.6 * math.cos(math.radians(a)), 0.6 * math.sin(math.radians(a))
        box("marble", 0.06, 0.06, 0.08, (x, y, 0.24), name="brazier_post")
        _brazier(x, y, 0.28, 0.9)
    # front steps
    box("marble", 0.24, 0.07, 0.025, (0, -0.675, 0.1625), name="step")
    box("gold", 0.25, 0.008, 0.006, (0, -0.71, 0.172), name="step_nosing")
    box("marble", 0.22, 0.06, 0.025, (0, -0.495, 0.2125), name="step")
    box("gold", 0.23, 0.008, 0.006, (0, -0.525, 0.222), name="step_nosing")
    # summoning circle with a beam
    cyl("royal", 0.20, 0.20, 0.006, 0.25, seg=32, name="circle_base")
    torus("lightning", 0.18, 0.006, (0, 0, 0.258), seg=32, minor=3, name="circle_ring")
    torus("lightning", 0.11, 0.005, (0, 0, 0.258), seg=24, minor=3, name="circle_ring")
    for k in range(6):
        a = TAU * k / 6
        box("lightning", 0.006, 0.07, 0.004, (0.145 * math.cos(a), 0.145 * math.sin(a), 0.258), rot=(0, 0, a + math.pi / 2), name="circle_rune")
    cyl("lightning", 0.03, 0.03, 0.6, 0.256, seg=10, name="beam")
    # rotunda: columns, entablature
    zr0, zr1 = 0.256, 0.84
    for k in range(8):
        a = TAU * k / 8 + TAU / 16
        x, y = 0.36 * math.cos(a), 0.36 * math.sin(a)
        cyl("marble", 0.04, 0.035, zr1 - zr0, zr0, xy=(x, y), seg=12, name="column")
        box("gold", 0.10, 0.10, 0.03, (x, y, zr0 + 0.015), rot=(0, 0, a), name="column_base")
        box("gold", 0.10, 0.10, 0.035, (x, y, zr1 - 0.017), rot=(0, 0, a), name="column_capital")
    for k in (2, 5):  # banners hang on the front-left and front-right column pairs
        a = TAU * k / 8 + TAU / 16 + TAU / 16
        x, y = 0.30 * math.cos(a), 0.30 * math.sin(a)
        box("royal", 0.11, 0.008, 0.30, (x, y, 0.58), rot=(0, 0, a + math.pi / 2), name="banner")
        box("gold", 0.13, 0.012, 0.012, (x, y, 0.735), rot=(0, 0, a + math.pi / 2), name="banner_rod")
        torus("gold", 0.03, 0.004, (x * 1.03, y * 1.03, 0.62), rot=(math.pi / 2, 0, a + math.pi / 2), seg=16, minor=4, name="banner_sun")
        cyl("gold", 0.02, 0.02, 0.004, 0.62, xy=(x * 1.03, y * 1.03), rot=(math.pi / 2, 0, a + math.pi / 2), seg=12, name="banner_sun_core")
    cyl("marble", 0.42, 0.42, 0.07, zr1, seg=32, name="entablature")
    cyl("gold", 0.425, 0.425, 0.014, zr1 + 0.005, seg=32, name="entab_trim")
    cyl("gold", 0.425, 0.425, 0.014, zr1 + 0.045, seg=32, name="entab_trim")
    cyl("royal", 0.43, 0.43, 0.018, zr1 + 0.02, seg=32, name="entab_frieze")
    # back dome half-shell cradle
    dome("marble", 0.28, zr1 + 0.07, xy=(0, 0.10), seg=24, rings=6, height=0.7, name="dome")
    for k in range(6):
        a = TAU * k / 12
        tube("gold", [(0.281 * math.cos(a) * math.cos(t), 0.10 + 0.281 * math.sin(a) * math.cos(t), zr1 + 0.07 + 0.196 * math.sin(t)) for t in [i * math.pi / 2 / 8 for i in range(9)]], 0.006, sides=4, name="dome_rib")
    # giant lens ring facing front with cradle brackets
    rc = (0, -0.06, 1.17)
    R = 0.30
    torus("gold", R, 0.035, rc, rot=(math.pi / 2, 0, 0), seg=48, minor=8, name="lens_ring")
    torus("gold", R - 0.055, 0.016, rc, rot=(math.pi / 2, 0, 0), seg=40, minor=5, name="lens_inner")
    torus("marble", R - 0.03, 0.012, (rc[0], rc[1] + 0.01, rc[2]), rot=(math.pi / 2, 0, 0), seg=40, minor=5, name="lens_marble")
    cyl("royal", R - 0.06, R - 0.06, 0.012, rc[2], xy=(rc[0], rc[1] + 0.006), rot=(math.pi / 2, 0, 0), seg=40, name="lens_disc")
    for k in range(10):  # stars
        a = k * 2.4
        r = 0.05 + 0.017 * k
        sphere("lightning", 0.008, (rc[0] + r * math.cos(a), rc[1] - 0.008, rc[2] + r * math.sin(a)), seg=6, rings=4, name="star")
    torus("lightning", 0.10, 0.006, (rc[0], rc[1] - 0.01, rc[2]), rot=(math.pi / 2, 0, 0), seg=32, minor=4, name="reticle")
    box("lightning", 2 * (R - 0.06), 0.006, 0.008, (rc[0], rc[1] - 0.01, rc[2]), name="crosshair")
    box("lightning", 0.008, 0.006, 2 * (R - 0.06), (rc[0], rc[1] - 0.01, rc[2]), name="crosshair")
    sphere("lightning", 0.04, (rc[0], rc[1] - 0.012, rc[2]), seg=16, rings=10, name="lens_core")
    for k in range(12):  # gold studs on the ring face
        a = TAU * k / 12
        sphere("gold", 0.012, (rc[0] + R * math.cos(a), rc[1] - 0.035, rc[2] + R * math.sin(a)), seg=6, rings=4, name="stud")
    for s in (-1, 1):  # trunnion hubs and brackets down to the entablature
        x = s * (R + 0.03)
        cyl("gold", 0.05, 0.05, 0.05, rc[2], xy=(x - s * 0.0, rc[1] - 0.0), rot=(0, s * math.pi / 2, 0), seg=16, name="trunnion")
        cyl("royal", 0.035, 0.035, 0.052, rc[2], xy=(x, rc[1]), rot=(0, s * math.pi / 2, 0), seg=12, name="trunnion_core")
        box("gold", 0.04, 0.08, rc[2] - zr1 - 0.07, (s * 0.33, rc[1], (rc[2] + zr1 + 0.07) / 2), name="bracket")
    cyl("gold", 0.025, 0.0, 0.10, rc[2] + R + 0.025, xy=(rc[0], rc[1]), seg=8, name="lens_finial")
    sphere("gold", 0.02, (rc[0], rc[1], rc[2] + R + 0.03), seg=10, rings=6, name="lens_knob")
    # armillary pedestals at the front
    for s in (-1, 1):
        x, y = s * 0.50, -0.30
        box("marble", 0.08, 0.08, 0.22, (x, y, 0.15 + 0.11), bevel=0.005, name="ped")
        box("gold", 0.10, 0.10, 0.018, (x, y, 0.37), name="ped_cap")
        box("royal", 0.082, 0.082, 0.05, (x, y, 0.26), name="ped_band")
        cyl("gold", 0.008, 0.008, 0.14, 0.38, xy=(x, y), seg=6, name="ped_axis")
        _armillary((x, y, 0.47), 0.06, 0.035, orb="glass", seg=18, minor=4)


# ---------------------------------------------------------------- 4
def zeus_tutor_structure_4():
    """Seraphic Relay: three glowing arched portals across the front, huge
    marble-and-gold wings fanning out behind, a central tower with a gold halo
    ring and armillary orb, flanking spires, domed kiosks and orb posts."""
    _plinth()
    _flat_banners()
    box("marble", 1.10, 0.66, 0.05, (0, 0.04, 0.175), name="terrace2")
    box("gold", 1.12, 0.68, 0.014, (0, 0.04, 0.185), name="terrace2_trim")
    for i in range(3):
        box("marble", 0.30, 0.06, 0.02 * (i + 1), (0, -0.33 - 0.06 * (2 - i) - 0.0, 0.15 + 0.01 * (i + 1)), name="step")
    zt = 0.20
    # wings behind everything
    for s in (-1, 1):
        _feather_wing(s, (s * 0.24, 0.30, 0.50))
    # central tower
    cyl("marble", 0.16, 0.14, 0.82, zt, xy=(0, 0.18), seg=16, name="tower")
    for z in (zt + 0.02, 0.62, zt + 0.80):
        cyl("gold", 0.165, 0.165, 0.02, z - 0.01, xy=(0, 0.18), seg=16, name="tower_band")
    box("lightning", 0.05, 0.01, 0.55, (0, 0.035, 0.68), name="tower_glow")
    for s in (-1, 1):
        box("royal", 0.07, 0.008, 0.30, (s * 0.10, 0.05, 0.75), rot=(0, 0, s * 0.5), name="tower_banner")
    torus("gold", 0.33, 0.018, (0, 0.10, 1.00), rot=(math.radians(10), 0, 0), seg=44, minor=6, name="halo")
    torus("gold", 0.28, 0.008, (0, 0.10, 1.00), rot=(math.radians(10), 0, 0), seg=36, minor=4, name="halo_inner")
    for k in range(8):
        a = TAU * k / 8
        tube("gold", [(0.15 * math.cos(a), 0.18 + 0.15 * math.sin(a), 0.99), (0.29 * math.cos(a), 0.10 + 0.29 * math.sin(a) * 0.98, 1.0 + 0.29 * math.sin(a) * 0.17)], 0.006, sides=4, name="halo_spoke")
    cyl("gold", 0.10, 0.14, 0.06, zt + 0.82, xy=(0, 0.18), seg=16, name="tower_crown")
    cyl("gold", 0.03, 0.05, 0.10, 1.08, xy=(0, 0.18), seg=12, name="orb_stem")
    _armillary((0, 0.18, 1.30), 0.15, 0.09, seg=28)
    cyl("gold", 0.02, 0.0, 0.10, 1.45, xy=(0, 0.18), seg=8, name="finial")
    # flanking spires
    for s in (-1, 1):
        for (x, y, h) in ((s * 0.20, 0.30, 0.75), (s * 0.48, 0.12, 0.55)):
            cyl("marble", 0.04, 0.035, h, zt, xy=(x, y), seg=8, name="spire")
            cyl("gold", 0.045, 0.045, 0.015, zt + h * 0.6, xy=(x, y), seg=8, name="spire_band")
            cyl("gold", 0.04, 0.0, 0.18, zt + h, xy=(x, y), seg=8, name="spire_cap")
            box("royal", 0.05, 0.006, h * 0.4, (x, y - 0.04, zt + h * 0.35), name="spire_banner")
    # three portals along the front
    for x, w, h in ((-0.33, 0.20, 0.36), (0.0, 0.24, 0.44), (0.33, 0.20, 0.36)):
        y = -0.12
        r = w / 2
        box("marble", w + 0.12, 0.10, h + r + 0.06, (x, y + 0.04, zt + (h + r + 0.06) / 2), bevel=0.008, name="portal_wall")
        box("lightning", w, 0.012, h, (x, y - 0.012, zt + h / 2), name="portal_glow")
        cyl("lightning", r, r, 0.012, zt + h, xy=(x, y - 0.006), rot=(math.pi / 2, 0, 0), seg=20, name="portal_glow_top")
        tube("gold", [(x - r - 0.01, y - 0.02, zt)] + arc((x, y - 0.02, zt + h), r + 0.01, math.pi, 0, steps=10) + [(x + r + 0.01, y - 0.02, zt)], 0.012, sides=6, name="portal_frame")
        tube("marble", arc((x, y - 0.025, zt + h), r + 0.04, math.pi, 0, steps=10), 0.018, sides=6, name="portal_archivolt")
        sphere("gold", 0.025, (x, y - 0.05, zt + h + r + 0.04), seg=10, rings=6, name="keystone")
        for k in range(8):
            a = TAU * k / 8
            box("gold", 0.006, 0.006, 0.075 if k % 2 == 0 else 0.045, (x, y - 0.06, zt + h + r + 0.04), rot=(0, a, 0), name="keystone_ray")
        for sx in (-1, 1):
            px = x + sx * (r + 0.05)
            box("marble", 0.04, 0.04, h + r + 0.10, (px, y - 0.03, zt + (h + r + 0.10) / 2), name="portal_pier")
            cyl("gold", 0.02, 0.0, 0.07, zt + h + r + 0.10, xy=(px, y - 0.03), seg=6, name="pier_tip")
        for k in range(3):  # light streaks spilling out of the portal
            box("lightning", 0.008, 0.20, 0.004, (x + (k - 1) * w * 0.3, y - 0.12, zt + 0.004), name="portal_streak")
    # domed kiosks and orb posts at the front corners
    for s in (-1, 1):
        x, y = s * 0.56, -0.30
        cyl("marble", 0.065, 0.065, 0.015, 0.15, xy=(x, y), seg=12, name="kiosk_floor")
        for k in range(6):
            a = TAU * k / 6
            cyl("marble", 0.008, 0.008, 0.11, 0.165, xy=(x + 0.05 * math.cos(a), y + 0.05 * math.sin(a)), seg=6, name="kiosk_col")
        cyl("gold", 0.068, 0.068, 0.015, 0.275, xy=(x, y), seg=12, name="kiosk_ring")
        dome("royal", 0.062, 0.29, xy=(x, y), seg=14, rings=4, name="kiosk_dome")
        cyl("gold", 0.008, 0.0, 0.06, 0.35, xy=(x, y), seg=6, name="kiosk_finial")
        sphere("lightning", 0.025, (x, y, 0.20), seg=10, rings=6, name="kiosk_light")
        _orb_post(s * 0.20, -0.50, 0.15, h=0.16, w=0.06)


# ---------------------------------------------------------------- 5
def zeus_tutor_structure_5():
    """Skyfather's Summons: marble processional walk with royal inlays and a
    lightning runner, orb pedestals in gold crescents, grand stair up to a
    columned temple with a glowing doorway, banner towers with gold eagles,
    and a crown of gold rings and spikes around a lightning orb above."""
    _plinth()
    _flat_banners()
    # processional walk with inlaid tiles and a glowing runner
    box("marble", 0.42, 0.40, 0.02, (0, -0.56, 0.16), name="walk")
    for i in range(3):
        for sx in (-1, 1):
            box("royal", 0.08, 0.08, 0.004, (sx * 0.10, -0.68 + i * 0.12, 0.171), rot=(0, 0, math.pi / 4), name="tile")
            box("gold", 0.1, 0.004, 0.003, (sx * 0.10, -0.68 + i * 0.12, 0.172), rot=(0, 0, math.pi / 4), name="tile_line")
    box("lightning", 0.035, 0.40, 0.005, (0, -0.56, 0.172), name="runner")
    for sx in (-1, 1):
        box("marble", 0.03, 0.40, 0.04, (sx * 0.225, -0.56, 0.19), name="walk_wall")
        box("gold", 0.034, 0.40, 0.008, (sx * 0.225, -0.56, 0.206), name="walk_wall_trim")
        # big front orb pedestals with crescents
        x, y = sx * 0.33, -0.62
        box("marble", 0.12, 0.12, 0.22, (x, y, 0.26), bevel=0.006, name="pedestal")
        box("gold", 0.14, 0.14, 0.02, (x, y, 0.16), name="pedestal_foot")
        box("gold", 0.14, 0.14, 0.02, (x, y, 0.37), name="pedestal_cap")
        box("gold", 0.04, 0.004, 0.04, (x, y - 0.061, 0.27), rot=(0, math.pi / 4, 0), name="pedestal_star")
        cyl("gold", 0.03, 0.05, 0.03, 0.38, xy=(x, y), seg=12, name="crescent_mount")
        tube("gold", arc((x, y, 0.47), 0.065, math.radians(-160), math.radians(-20), plane="xz", steps=12), 0.01, 0.006, sides=6, name="crescent")
        sphere("glass", 0.05, (x, y, 0.47), seg=16, rings=10, name="pedestal_orb")
        sphere("lightning", 0.02, (x, y - 0.035, 0.47), seg=8, rings=6, name="orb_spark")
        _orb_post(sx * 0.2, -0.35, 0.15, h=0.10, w=0.05, orb="glass")
    # grand stair up to the temple platform
    zp = 0.40
    nst = 8
    for i in range(nst):
        z = 0.15 + (zp - 0.15) * (i + 1) / nst
        y = -0.36 + i * 0.035
        box("marble", 0.34, 0.035, z - 0.15, (0, y, 0.15 + (z - 0.15) / 2), name="stair")
    box("lightning", 0.03, 0.30, 0.004, (0, -0.24, 0.28), rot=(math.atan2(zp - 0.15, 0.28), 0, 0), name="stair_runner")
    box("marble", 1.04, 0.62, zp - 0.15, (0, 0.24, 0.15 + (zp - 0.15) / 2), name="platform")
    box("gold", 1.06, 0.64, 0.014, (0, 0.24, zp - 0.02), name="platform_trim")
    for sx in (-1, 1):
        box("marble", 0.04, 0.30, 0.03, (sx * 0.19, -0.22, 0.30), rot=(math.atan2(zp - 0.15, 0.28), 0, 0), name="stair_wall")
        _orb_post(sx * 0.19, -0.07, zp, h=0.06, w=0.04)
    # temple: cella, colonnade, pediment
    zt = zp
    box("marble", 0.46, 0.40, 0.44, (0, 0.30, zt + 0.22), name="cella")
    box("lightning", 0.08, 0.01, 0.22, (0, 0.096, zt + 0.11), name="door_glow")
    cyl("lightning", 0.04, 0.04, 0.01, zt + 0.22, xy=(0, 0.094), rot=(math.pi / 2, 0, 0), seg=16, name="door_glow_top")
    tube("gold", [(-0.05, 0.09, zt)] + arc((0, 0.09, zt + 0.22), 0.05, math.pi, 0, steps=10) + [(0.05, 0.09, zt)], 0.008, sides=5, name="door_frame")
    cyl("lightning", 0.015, 0.015, 1.05 - zt, zt, xy=(0, 0.10), seg=8, name="door_beam")
    box("marble", 0.56, 0.12, 0.015, (0, 0.04, zt + 0.0075), name="stylobate")
    zc1 = zt + 0.40
    for i in range(6):
        x = -0.24 + i * 0.096
        cyl("marble", 0.026, 0.022, zc1 - zt - 0.015, zt + 0.015, xy=(x, 0.03), seg=12, name="column")
        box("gold", 0.06, 0.06, 0.02, (x, 0.03, zc1 - 0.01), name="capital")
        box("gold", 0.055, 0.055, 0.012, (x, 0.03, zt + 0.021), name="column_base")
    for x in (-0.144, 0.144):
        box("royal", 0.07, 0.008, 0.28, (x, 0.07, zt + 0.22), name="temple_banner")
        cyl("gold", 0.022, 0.022, 0.004, zt + 0.25, xy=(x, 0.065), rot=(math.pi / 2, 0, 0), seg=16, name="temple_sun")
        box("gold", 0.08, 0.01, 0.01, (x, 0.068, zt + 0.36), name="temple_banner_rod")
    box("marble", 0.58, 0.48, 0.05, (0, 0.24, zc1 + 0.025), name="entablature")
    box("gold", 0.59, 0.49, 0.012, (0, 0.24, zc1 + 0.01), name="entab_trim")
    box("royal", 0.586, 0.012, 0.018, (0, -0.002, zc1 + 0.033), name="frieze")
    zp0 = zc1 + 0.05
    for i, w in enumerate((0.54, 0.40, 0.26, 0.12)):
        box("marble", w, 0.44, 0.035, (0, 0.24, zp0 + 0.0175 + i * 0.035), name="pediment_step")
    ang = math.atan2(0.14, 0.29)
    for sx in (-1, 1):
        box("gold", 0.33, 0.47, 0.016, (sx * 0.145, 0.24, zp0 + 0.075), rot=(0, sx * ang, 0), name="roof_edge")
    sphere("gold", 0.03, (0, 0.0, zp0 + 0.12), seg=10, rings=6, name="acroterion")
    # banner towers with gold eagles
    for sx in (-1, 1):
        x = sx * 0.40
        box("marble", 0.12, 0.12, 0.58, (x, 0.22, zp + 0.29), bevel=0.006, name="tower")
        for z in (zp + 0.02, zp + 0.40, zp + 0.57):
            box("gold", 0.135, 0.135, 0.018, (x, 0.22, z), name="tower_band")
        box("royal", 0.09, 0.008, 0.30, (x, 0.155, zp + 0.22), name="tower_banner")
        cyl("gold", 0.025, 0.025, 0.004, zp + 0.26, xy=(x, 0.15), rot=(math.pi / 2, 0, 0), seg=16, name="tower_sun")
        cyl("gold", 0.06, 0.0, 0.10, zp + 0.58, xy=(x, 0.22), seg=4, rot=(0, 0, math.pi / 4), name="tower_cap")
        ez = zp + 0.66
        sphere("gold", 0.03, (x, 0.22, ez), seg=12, rings=8, scale=(0.8, 0.8, 1.2), name="eagle_body")
        sphere("gold", 0.016, (x, 0.20, ez + 0.04), seg=10, rings=6, name="eagle_head")
        for w in (-1, 1):
            box("gold", 0.08, 0.012, 0.035, (x + w * 0.05, 0.22, ez + 0.04), rot=(0, -w * 0.6, 0), name="eagle_wing")
        sphere("glass", 0.025, (x + sx * 0.10, 0.10, zp + 0.10), seg=10, rings=6, name="tower_lamp")
        box("marble", 0.05, 0.05, 0.08, (x + sx * 0.10, 0.10, zp + 0.04), name="tower_lamp_post")
    # crown of rings above the temple with a lightning orb and spikes
    cc = (0, 0.24, 1.30)
    cyl("gold", 0.012, 0.012, cc[2] - zp0 - 0.12, zp0 + 0.12, xy=(0, 0.24), seg=8, name="crown_mast")
    torus("gold", 0.36, 0.014, (cc[0], cc[1], cc[2] - 0.05), rot=(math.radians(12), 0, 0), seg=56, minor=6, name="crown_ring")
    torus("gold", 0.28, 0.012, (cc[0], cc[1], cc[2] + 0.02), rot=(math.radians(-10), 0, 0), seg=48, minor=6, name="crown_ring")
    torus("gold", 0.15, 0.016, cc, rot=(math.pi / 2, 0, 0), seg=40, minor=6, name="crown_disc_ring")
    torus("royal", 0.12, 0.01, cc, rot=(math.pi / 2, 0, 0), seg=32, minor=5, name="crown_disc_blue")
    sphere("lightning", 0.08, cc, seg=20, rings=12, name="crown_orb")
    for k in range(6):
        a = TAU * k / 6 + math.pi / 6
        r = 0.36
        x, y = cc[0] + r * math.cos(a), cc[1] + r * math.sin(a) * math.cos(math.radians(12))
        z = cc[2] - 0.05 + r * math.sin(a) * math.sin(math.radians(12))
        cyl("gold", 0.018, 0.0, 0.12, z, xy=(x, y), seg=8, name="crown_spike")
        sphere("gold", 0.018, (x, y, z), seg=8, rings=5, name="crown_bead")
    cyl("gold", 0.025, 0.0, 0.14, cc[2] + 0.15, xy=(0, 0.24), seg=8, name="crown_spire")
    for s in (-1, 1):
        tube("gold", [(0, 0.24, zp0 + 0.12), (s * 0.18, 0.24, cc[2] - 0.2), (s * 0.30, 0.20, cc[2] - 0.08)], 0.008, sides=5, name="crown_strut")


# ---------------------------------------------------------------- 6
def zeus_zephyr_mooring_mast():
    """Zephyr Mooring Mast (alpha art): tall stepped obelisk with glowing circuit
    traces and royal panels, three gold greek-key rings, a sleek sky-skiff moored
    to the middle ring by gold and lightning tethers. Marble instead of the
    art's black stone so the Zeus palette stays white-dominant."""
    _plinth()
    _flat_banners()
    # stepped base
    cyl("marble", 0.36, 0.34, 0.06, 0.15, seg=8, rot=(0, 0, math.pi / 8), name="base1")
    cyl("gold", 0.365, 0.365, 0.012, 0.185, seg=8, rot=(0, 0, math.pi / 8), name="base1_trim")
    cyl("marble", 0.26, 0.24, 0.06, 0.21, seg=8, rot=(0, 0, math.pi / 8), name="base2")
    cyl("gold", 0.265, 0.265, 0.012, 0.245, seg=8, rot=(0, 0, math.pi / 8), name="base2_trim")
    cyl("royal", 0.18, 0.18, 0.006, 0.27, seg=8, rot=(0, 0, math.pi / 8), name="base_inlay")
    for a in (45, 135, 225, 315):
        x, y = 0.30 * math.cos(math.radians(a)), 0.30 * math.sin(math.radians(a))
        cyl("gold", 0.02, 0.0, 0.08, 0.21, xy=(x, y), seg=6, name="base_spike")
        sphere("lightning", 0.015, (x, y, 0.22), seg=8, rings=5, name="base_light")
    # lower shaft, step collar, upper shaft, tip
    zs = 0.27
    cyl("marble", 0.17, 0.145, 0.42, zs, seg=8, rot=(0, 0, math.pi / 8), name="shaft_low")
    cyl("gold", 0.16, 0.13, 0.04, zs + 0.42, seg=8, rot=(0, 0, math.pi / 8), name="shaft_step")
    zu = zs + 0.46
    cyl("marble", 0.125, 0.075, 0.62, zu, seg=8, rot=(0, 0, math.pi / 8), name="shaft_up")
    cyl("marble", 0.075, 0.0, 0.24, zu + 0.62, seg=8, rot=(0, 0, math.pi / 8), name="tip")
    cyl("gold", 0.08, 0.08, 0.012, zu + 0.615, seg=8, rot=(0, 0, math.pi / 8), name="tip_band")
    # circuit traces and royal panels on the eight faces
    for k in range(8):
        a = TAU * k / 8
        ca, sa = math.cos(a), math.sin(a)
        def face(r, z, lat=0.0):
            return (r * ca - lat * sa, r * sa + lat * ca, z)
        # lower: royal panel with gold edge
        r_lo = 0.152
        box("royal", 0.05, 0.008, 0.30, face(r_lo, zs + 0.21), rot=(math.atan2(0.025, 0.42) * 0, 0, a + math.pi / 2), name="panel_low")
        # glowing circuit line: up, jog, up
        lat = 0.018 if k % 2 == 0 else -0.018
        pts = [face(0.165, zs + 0.03, lat), face(0.155, zs + 0.18, lat), face(0.153, zs + 0.22, -lat), face(0.148, zs + 0.40, -lat)]
        tube("lightning" if k % 2 == 0 else "gold", pts, 0.0045, sides=4, name="trace_low")
        # upper traces taper with the shaft
        pts = [face(0.122 - 0.0, zu + 0.02, lat * 0.6), face(0.108, zu + 0.22, lat * 0.6), face(0.104, zu + 0.27, -lat * 0.6),
               face(0.088, zu + 0.48, -lat * 0.6), face(0.082, zu + 0.6, 0)]
        tube("gold" if k % 2 == 0 else "lightning", pts, 0.004, sides=4, name="trace_up")
        if k % 2 == 1:
            box("lightning", 0.01, 0.006, 0.04, face(0.148, zs + 0.06), rot=(0, 0, a + math.pi / 2), name="node")
    # three greek-key rings
    for z, R in ((0.55, 0.26), (0.95, 0.22), (1.27, 0.18)):
        _key_ring(z, R)
        rshaft = 0.16 if z < zu else 0.125 - 0.05 * (z - zu) / 0.62
        for k in range(4):
            a = TAU * k / 4 + math.pi / 8
            box("gold", R - rshaft, 0.014, 0.012, (((R + rshaft) / 2) * math.cos(a), ((R + rshaft) / 2) * math.sin(a), z), rot=(0, 0, a), name="ring_strut")
    # moored sky-skiff on the right, nose pointing front
    sx, sy, sz = 0.56, -0.10, 0.78
    sphere("marble", 0.07, (sx, sy, sz), seg=20, rings=12, scale=(1.0, 2.6, 0.75), name="hull")
    sphere("gold", 0.072, (sx, sy + 0.01, sz - 0.012), seg=20, rings=12, scale=(1.02, 2.2, 0.55), name="hull_keel")
    sphere("glass", 0.042, (sx, sy - 0.07, sz + 0.035), seg=16, rings=10, scale=(1.0, 2.0, 0.8), name="canopy")
    box("royal", 0.145, 0.24, 0.012, (sx, sy + 0.02, sz + 0.005), name="hull_stripe")
    for s in (-1, 1):
        box("gold", 0.16, 0.10, 0.012, (sx + s * 0.11, sy + 0.07, sz - 0.01), rot=(0, s * 0.15, s * 0.5), name="wing")
        cyl("gold", 0.022, 0.022, 0.06, sz - 0.01, xy=(sx + s * 0.06, sy + 0.12), rot=(-math.pi / 2, 0, 0), seg=12, name="engine")
        cyl("lightning", 0.016, 0.016, 0.006, sz - 0.01, xy=(sx + s * 0.06, sy + 0.18), rot=(-math.pi / 2, 0, 0), seg=12, name="engine_glow")
        sphere("lightning", 0.01, (sx + s * 0.05, sy - 0.15, sz), seg=8, rings=5, name="headlight")
    box("gold", 0.012, 0.07, 0.07, (sx, sy + 0.14, sz + 0.05), rot=(0.4, 0, 0), name="tail_fin")
    # mooring boom and tethers from the middle ring
    rz = 0.95
    tube("gold", [(0.22, -0.02, rz), (0.36, -0.06, rz - 0.04), (sx - 0.06, sy, sz + 0.02)], 0.01, sides=6, name="boom")
    tube("gold", [(0.18, -0.12, 1.27), (0.34, -0.16, 1.12), (sx, sy - 0.06, sz + 0.05)], 0.005, sides=5, name="tether")
    tube("lightning", [(0.24, 0.08, 0.55), (0.40, 0.04, 0.60), (sx, sy + 0.08, sz - 0.03)], 0.005, sides=5, name="power_tether")
    tube("lightning", [(0.17, 0.10, 1.27), (0.30, 0.10, 1.05), (0.46, 0.02, 0.90), (sx, sy + 0.12, sz + 0.02)], 0.004, sides=4, name="arc_tether")
    # docking cradle on the plinth below the skiff
    box("marble", 0.10, 0.10, 0.20, (sx - 0.02, sy + 0.30, 0.25), bevel=0.005, name="dock_post")
    box("gold", 0.12, 0.12, 0.02, (sx - 0.02, sy + 0.30, 0.36), name="dock_cap")
    sphere("glass", 0.035, (sx - 0.02, sy + 0.30, 0.40), seg=12, rings=8, name="dock_orb")
    _orb_post(-0.48, -0.30, 0.15, h=0.16, w=0.07)
    _orb_post(-0.42, 0.40, 0.15, h=0.12, w=0.07)


# ---------------------------------------------------------------- 7
def zeus_zeus_command_nexus():
    """Zeus Command Nexus: glowing royal floor etched with lightning lines and
    nodes, round arcaded command hall, a great lightning orb in a gold armillary
    with a spear spire, four banner towers with orb tops linked by gold beams,
    front gatehouse and a gold rail around the deck."""
    _plinth()
    _flat_banners()
    # etched floor
    cyl("royal", 0.70, 0.70, 0.008, 0.15, seg=6, rot=(0, 0, HEX30), name="floor")
    for k in range(6):
        a = TAU * k / 6 + math.pi / 6
        box("lightning", 0.012, 0.42, 0.006, (0.46 * math.cos(a), 0.46 * math.sin(a), 0.16), rot=(0, 0, a + math.pi / 2), name="floor_line")
    for r in (0.42, 0.60):
        torus("lightning", r, 0.006, (0, 0, 0.158), seg=6, minor=4, rot=(0, 0, math.pi / 6), name="floor_hex")
    for k in range(6):
        a = TAU * k / 6
        torus("gold", 0.035, 0.006, (0.52 * math.cos(a), 0.52 * math.sin(a), 0.162), seg=12, minor=4, name="node_ring")
        sphere("lightning", 0.014, (0.52 * math.cos(a), 0.52 * math.sin(a), 0.162), seg=6, rings=4, name="node")
    torus("gold", 0.72, 0.01, (0, 0, 0.19), seg=6, minor=5, rot=(0, 0, math.pi / 6), name="deck_rail")
    for k in range(6):
        a = TAU * k / 6 + math.pi / 6
        for t in (0.25, 0.5, 0.75):
            p0 = (0.72 * math.cos(a), 0.72 * math.sin(a))
            p1 = (0.72 * math.cos(a + TAU / 6), 0.72 * math.sin(a + TAU / 6))
            x, y = p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t
            cyl("marble", 0.01, 0.01, 0.04, 0.15, xy=(x, y), seg=6, name="rail_post")
    # command hall
    zh = 0.158
    cyl("marble", 0.30, 0.30, 0.28, zh, seg=24, name="hall")
    cyl("gold", 0.305, 0.305, 0.016, zh + 0.01, seg=24, name="hall_foot")
    for k in range(8):
        a = TAU * k / 8 - math.pi / 2
        x, y = 0.30 * math.cos(a), 0.30 * math.sin(a)
        box("royal", 0.075, 0.012, 0.13, (x, y, zh + 0.11), rot=(0, 0, a + math.pi / 2), name="hall_window")
        cyl("royal", 0.0375, 0.0375, 0.012, zh + 0.175, xy=(x, y), rot=(math.pi / 2, 0, a + math.pi / 2), seg=12, name="hall_window_head")
        tube("gold", [(x + 0.045 * math.cos(a + math.pi / 2) + 0.008 * math.cos(a), y + 0.045 * math.sin(a + math.pi / 2) + 0.008 * math.sin(a), zh + 0.05)] +
             [(x + 0.045 * math.cos(t) * math.cos(a + math.pi / 2) + 0.008 * math.cos(a), y + 0.045 * math.cos(t) * math.sin(a + math.pi / 2) + 0.008 * math.sin(a), zh + 0.175 + 0.045 * math.sin(t)) for t in [i * math.pi / 8 for i in range(9)]] +
             [(x - 0.045 * math.cos(a + math.pi / 2) + 0.008 * math.cos(a), y - 0.045 * math.sin(a + math.pi / 2) + 0.008 * math.sin(a), zh + 0.05)], 0.006, sides=3, name="window_arch")
        if k == 0:
            box("lightning", 0.05, 0.014, 0.12, (x, y - 0.002, zh + 0.10), name="hall_door_glow")
    cyl("gold", 0.32, 0.32, 0.025, zh + 0.27, seg=24, name="hall_cornice")
    cyl("marble", 0.24, 0.24, 0.10, zh + 0.295, seg=24, name="drum")
    for k in range(16):
        a = TAU * k / 16
        cyl("white", 0.012, 0.012, 0.09, zh + 0.30, xy=(0.25 * math.cos(a), 0.25 * math.sin(a)), seg=6, name="colonnette")
    cyl("gold", 0.26, 0.20, 0.03, zh + 0.395, seg=24, name="drum_cornice")
    for k in range(8):  # small pinnacles around the drum
        a = TAU * k / 8 + TAU / 16
        cyl("gold", 0.018, 0.0, 0.08, zh + 0.295, xy=(0.31 * math.cos(a), 0.31 * math.sin(a)), seg=6, name="pinnacle")
    # great orb in an armillary, spear spire
    oc = (0, 0, 0.88)
    cyl("gold", 0.06, 0.12, 0.06, zh + 0.425, seg=16, name="orb_cradle")
    sphere("glass", 0.19, oc, seg=24, rings=14, name="great_orb")
    for ph in (0.0, 2.1, 4.2):
        tube("lightning", helix(0, 0, oc[2] - 0.15, oc[2] + 0.15, 0.192, 0.6, phase=ph), 0.005, sides=4, name="orb_vein")
    torus("lightning", 0.192, 0.005, oc, rot=(math.radians(20), 0, 0), seg=32, minor=4, name="orb_vein_ring")
    torus("gold", 0.25, 0.014, oc, seg=40, minor=5, name="arm_equator")
    torus("gold", 0.255, 0.012, oc, rot=(math.pi / 2, 0, math.radians(30)), seg=40, minor=5, name="arm_meridian")
    torus("gold", 0.26, 0.012, oc, rot=(math.radians(65), 0, math.radians(-40)), seg=40, minor=5, name="arm_ring")
    torus("gold", 0.30, 0.012, (0, 0, oc[2] - 0.05), rot=(math.radians(-20), math.radians(15), 0), seg=44, minor=5, name="arm_outer")
    cyl("gold", 0.012, 0.012, 0.66, oc[2] - 0.26, seg=8, name="axis")
    cyl("gold", 0.04, 0.0, 0.16, oc[2] + 0.40, seg=8, name="spear_tip")
    cyl("gold", 0.045, 0.045, 0.02, oc[2] + 0.38, seg=8, name="spear_collar")
    torus("gold", 0.05, 0.006, (0, 0, oc[2] + 0.30), seg=16, minor=4, name="spear_ring")
    # four banner towers with orb tops and beams to the hall
    for a in (210, 330, 30, 150):
        ar = math.radians(a)
        x, y = 0.56 * math.cos(ar), 0.56 * math.sin(ar)
        box("marble", 0.12, 0.12, 0.56, (x, y, zh + 0.28), rot=(0, 0, ar), bevel=0.006, name="tower")
        for z in (zh + 0.02, zh + 0.40, zh + 0.55):
            box("gold", 0.135, 0.135, 0.018, (x, y, z), rot=(0, 0, ar), name="tower_band")
        ox, oy = x + 0.062 * math.cos(ar), y + 0.062 * math.sin(ar)
        box("royal", 0.008, 0.09, 0.28, (ox, oy, zh + 0.24), rot=(0, 0, ar), name="tower_banner")
        cyl("gold", 0.024, 0.024, 0.004, zh + 0.28, xy=(ox + 0.005 * math.cos(ar), oy + 0.005 * math.sin(ar)), rot=(0, math.pi / 2, ar), seg=16, name="tower_sun")
        cyl("gold", 0.07, 0.0, 0.14, zh + 0.56, xy=(x, y), seg=4, rot=(0, 0, ar + math.pi / 4), name="tower_cap")
        cyl("gold", 0.01, 0.01, 0.10, zh + 0.66, xy=(x, y), seg=6, name="tower_mast")
        sphere("glass", 0.035, (x, y, zh + 0.79), seg=12, rings=6, name="tower_orb")
        torus("gold", 0.045, 0.005, (x, y, zh + 0.79), rot=(math.pi / 2, 0, ar), seg=14, minor=4, name="tower_orb_ring")
        # sloping gold beam with a lightning conduit from tower to the hall cornice
        p0 = (x * 0.9, y * 0.9, zh + 0.48)
        p1 = (0.30 * math.cos(ar), 0.30 * math.sin(ar), zh + 0.30)
        tube("gold", [p0, p1], 0.018, sides=6, name="beam")
        tube("lightning", [(p0[0], p0[1], p0[2] + 0.022), (p1[0], p1[1], p1[2] + 0.022)], 0.006, sides=4, name="beam_glow")
        tube("lightning", [(x * 0.97, y * 0.97, zh + 0.75), (x * 0.6, y * 0.6, zh + 0.85), (0.2 * math.cos(ar), 0.2 * math.sin(ar), oc[2] - 0.02)], 0.004, sides=4, name="arc")
    # front gatehouse
    gy = -0.60
    box("marble", 0.20, 0.12, 0.18, (0, gy, zh + 0.09), bevel=0.006, name="gate")
    box("gold", 0.22, 0.14, 0.018, (0, gy, zh + 0.18), name="gate_cornice")
    cyl("royal", 0.07, 0.07, 0.008, zh + 0.18, xy=(0, gy), seg=16, name="gate_roof_inlay")
    box("royal", 0.08, 0.008, 0.13, (0, gy - 0.064, zh + 0.085), name="gate_banner")
    cyl("gold", 0.022, 0.022, 0.004, zh + 0.10, xy=(0, gy - 0.07), rot=(math.pi / 2, 0, 0), seg=16, name="gate_sun")
    for s in (-1, 1):
        box("marble", 0.04, 0.04, 0.24, (s * 0.12, gy - 0.04, zh + 0.12), name="gate_pier")
        cyl("gold", 0.02, 0.0, 0.08, zh + 0.24, xy=(s * 0.12, gy - 0.04), seg=6, name="gate_pier_tip")
        sphere("glass", 0.02, (s * 0.12, gy - 0.04, zh + 0.27), seg=10, rings=6, name="gate_orb")
    box("lightning", 0.03, 0.26, 0.006, (0, -0.42, 0.162), name="gate_path_glow")
    # planters with cypress stubs replaced by marble tubs with gold rims at side flats
    for a in (0, 180):
        ar = math.radians(a)
        x, y = 0.42 * math.cos(ar), 0.42 * math.sin(ar) - 0.18
        box("marble", 0.16, 0.08, 0.05, (x, y, zh + 0.025), name="tub")
        box("gold", 0.17, 0.09, 0.01, (x, y, zh + 0.045), name="tub_rim")


BUILDERS = {
    "zeus_tutor_structure_1": ("Sparkstep Beacon", "Zeus", zeus_tutor_structure_1),
    "zeus_tutor_structure_2": ("Cloudline Dispatch", "Zeus", zeus_tutor_structure_2),
    "zeus_tutor_structure_3": ("Sharp-Shot Observatory", "Zeus", zeus_tutor_structure_3),
    "zeus_tutor_structure_4": ("Seraphic Relay", "Zeus", zeus_tutor_structure_4),
    "zeus_tutor_structure_5": ("Skyfather's Summons", "Zeus", zeus_tutor_structure_5),
    "zeus_zephyr_mooring_mast": ("Zephyr Mooring Mast", "Zeus", zeus_zephyr_mooring_mast),
    "zeus_zeus_command_nexus": ("Zeus Command Nexus", "Zeus", zeus_zeus_command_nexus),
}
