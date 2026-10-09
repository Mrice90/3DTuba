"""Poseidon structures, batch C. Each builder only adds parts via kit."""
from kit import box, cyl, sphere, dome, torus, tube, helix, arc
import math

TAU = math.tau
HEX30 = 0.0  # bmesh hexes are already pointy-top, matching the board


def plinth(top="abyss", r=0.86):
    """Short rock plinth with a seabed cap; returns the top z."""
    cyl("rock", r - 0.06, r, 0.10, 0.0, seg=6, rot=(0, 0, HEX30), name="rock")
    cyl(top, r - 0.04, r - 0.06, 0.04, 0.10, seg=6, rot=(0, 0, HEX30), name="seabed")
    cyl("oldgold", r - 0.035, r - 0.035, 0.008, 0.118, seg=6, rot=(0, 0, HEX30), name="seabed_trim")
    return 0.14


def polar(r, deg, z=0.0):
    a = math.radians(deg)
    return (r * math.cos(a), r * math.sin(a), z)


def coral(x, y, z, h=0.10, sw="cyan", n=4, seed=0.0):
    """Small branching glowing coral clump."""
    for k in range(n):
        a = seed + TAU * k / n
        r = 0.02 + 0.01 * (k % 2)
        pts = [(x, y, z), (x + r * math.cos(a), y + r * math.sin(a), z + h * 0.5),
               (x + 1.8 * r * math.cos(a), y + 1.8 * r * math.sin(a), z + h * (0.8 + 0.2 * (k % 2)))]
        tube(sw, pts, 0.008, 0.004, sides=5, name="coral")
        sphere(sw, 0.010, pts[-1], seg=5, rings=3, name="coral_tip")


def pearl_cup(x, y, z, r=0.05):
    """Shell cup holding a pearl, the faction's turret finial."""
    cyl("pearl", r * 0.4, r * 1.5, r * 0.9, z, xy=(x, y), seg=10, name="cup")
    for k in range(5):
        a = TAU * k / 5
        cyl("pearl", 0.008, 0.0, r * 1.2, z + r * 0.6, xy=(x + r * 1.45 * math.cos(a), y + r * 1.45 * math.sin(a)), seg=5, name="cup_spike")
    cyl("oldgold", r * 1.52, r * 1.52, 0.008, z + r * 0.75, xy=(x, y), seg=12, name="cup_rim")
    sphere("pearl", r, (x, y, z + r * 1.4), seg=16, rings=10, name="pearl")


# ---------------------------------------------------------------- Tidewell Bastion
def poseidon_ability_tidewell():
    """DT art: pearl curtain walls with round shell-capped turrets holding pearls,
    glowing gates and spillways, coral gardens inside, and a tall shell-crown keep
    with a great pearl over a glowing seaglass window."""
    z = plinth()
    # inner courtyard terrace
    cyl("deepteal", 0.56, 0.58, 0.06, z, seg=6, name="court")
    cyl("oldgold", 0.585, 0.585, 0.01, z + 0.04, seg=6, name="court_trim")
    zc = z + 0.06
    # curtain walls along the six flats between corner towers
    R = 0.66
    wall_h = 0.22
    for i in range(6):
        a0, a1 = 30 + 60 * i, 90 + 60 * i
        p0, p1 = polar(R, a0), polar(R, a1)
        mx, my = (p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2
        mid = math.radians(a0 + 30)
        rz = mid + math.pi / 2
        L = R
        box("pearl", L, 0.07, wall_h, (mx, my, z + wall_h / 2), rot=(0, 0, rz), name="wall")
        box("oldgold", L + 0.002, 0.075, 0.012, (mx, my, z + wall_h - 0.03), rot=(0, 0, rz), name="wall_band")
        # merlons
        for k in range(5):
            t = -0.24 + k * 0.12
            box("pearl", 0.05, 0.075, 0.035, (mx + t * math.cos(rz), my + t * math.sin(rz), z + wall_h + 0.0175), rot=(0, 0, rz), name="merlon")
        # buttress pilasters on the outer face
        n = (math.cos(mid), math.sin(mid))
        for t in (-0.16, 0.16):
            box("pearl", 0.035, 0.03, wall_h - 0.02, (mx + t * math.cos(rz) + n[0] * 0.045, my + t * math.sin(rz) + n[1] * 0.045, z + (wall_h - 0.02) / 2), rot=(0, 0, rz), name="pilaster")
        # glowing gate / window slots
        gate = i in (3, 4)  # front flats (240, 300)
        gw, gh = (0.10, 0.14) if gate else (0.04, 0.09)
        gx, gy = mx + n[0] * 0.037, my + n[1] * 0.037
        box("seaglass" if not gate else "cyan", gw, 0.008, gh, (gx, gy, z + 0.02 + gh / 2), rot=(0, 0, rz), name="gate")
        tube("oldgold", [(gx + c * math.cos(rz), gy + c * math.sin(rz), z + 0.02 + gh + s) for c, s in
                         [((gw / 2) * math.cos(t), (gw / 2) * math.sin(t)) for t in [math.pi * j / 6 for j in range(7)]]],
             0.006, sides=4, name="gate_arch")
        if gate:
            # spillway of glowing water down from the gate over the plinth
            box("cyan", 0.08, 0.10, 0.01, (gx + n[0] * 0.05, gy + n[1] * 0.05, 0.135), rot=(0, 0, rz), name="spill")
    # corner towers with shell cups and pearls
    for i, a in enumerate((30, 90, 150, 210, 270, 330)):
        x, y, _ = polar(R, a)
        tall = a in (90, 270)
        h = 0.40 if tall else 0.33
        cyl("pearl", 0.085, 0.065, 0.08, z, xy=(x, y), seg=12, name="tower_foot")
        cyl("pearl", 0.065, 0.055, h, z, xy=(x, y), seg=12, name="tower")
        cyl("oldgold", 0.07, 0.07, 0.012, z + 0.07, xy=(x, y), seg=12, name="tower_band")
        cyl("oldgold", 0.062, 0.062, 0.012, z + h - 0.06, xy=(x, y), seg=12, name="tower_band")
        ox, oy = math.cos(math.radians(a)), math.sin(math.radians(a))
        box("seaglass", 0.025, 0.006, 0.07, (x + ox * 0.058, y + oy * 0.058, z + h - 0.15), rot=(0, 0, math.radians(a) + math.pi / 2), name="tower_slit")
        pearl_cup(x, y, z + h, 0.045 if not tall else 0.05)
    # coral garden in the courtyard
    for k, (x, y) in enumerate([(-0.36, -0.12), (0.36, -0.10), (-0.30, 0.22), (0.32, 0.24), (0.0, -0.40), (-0.16, -0.36), (0.18, -0.34)]):
        coral(x, y, zc, h=0.08 + 0.03 * (k % 3), sw="cyan" if k % 2 == 0 else "pearl", n=4, seed=k)
    # keep: tiered drum
    cyl("pearl", 0.30, 0.28, 0.14, zc, seg=16, name="keep")
    cyl("oldgold", 0.305, 0.305, 0.014, zc + 0.11, seg=16, name="keep_band")
    for k in range(8):
        a = TAU * k / 8 + TAU / 16
        box("seaglass", 0.05, 0.008, 0.07, (0.285 * math.cos(a), 0.285 * math.sin(a), zc + 0.06), rot=(0, 0, a + math.pi / 2), name="keep_window")
    zk = zc + 0.14
    cyl("deepteal", 0.25, 0.22, 0.05, zk, seg=16, name="keep_roof")
    # arched bridges from the keep to the side towers
    for a in (30, 150):
        x, y, _ = polar(R, a)
        tube("pearl", [(x * 0.42, y * 0.42, zk), (x * 0.65, y * 0.65, zk + 0.06), (x * 0.88, y * 0.88, z + 0.30)], 0.02, sides=6, name="bridge")
    # small side spires on the keep
    for k in range(6):
        a = TAU * k / 6 + TAU / 12
        x, y = 0.24 * math.cos(a), 0.24 * math.sin(a)
        cyl("pearl", 0.03, 0.025, 0.12, zk, xy=(x, y), seg=8, name="keep_turret")
        cyl("teal", 0.032, 0.0, 0.12, zk + 0.12, xy=(x, y), seg=8, name="keep_spire")
    # central shell crown: glowing seaglass core inside curved pearl petals
    z0, z1 = zk + 0.05, 1.22
    cyl("seaglass", 0.075, 0.06, z1 - z0, z0, seg=12, name="core")
    cyl("cyan", 0.03, 0.03, z1 - z0 - 0.1, z0 + 0.05, xy=(0, -0.07), seg=8, name="core_glow")
    for k in range(8):
        a = TAU * k / 8 + math.pi / 8
        pts = []
        for j in range(9):
            t = j / 8
            r = 0.10 + 0.10 * math.sin(t * math.pi * 0.9) - 0.05 * t
            pts.append((r * math.cos(a), r * math.sin(a), z0 + (z1 - z0 + 0.06) * t))
        tube("pearl", pts, 0.028, 0.006, sides=6, name="petal")
    for zz in (z0 + 0.02, z0 + 0.30):
        torus("oldgold", 0.12 if zz < z0 + 0.1 else 0.19, 0.01, (0, 0, zz), seg=28, minor=5, name="crown_ring")
    # outer flared horns
    for s in (-1, 1):
        pts = [(s * (0.12 + 0.18 * math.sin(t * 2.6)), 0.0, z0 + 0.05 + 0.55 * t) for t in [j / 10 for j in range(11)]]
        tube("pearl", pts, 0.03, 0.004, sides=6, name="horn")
    cyl("oldgold", 0.07, 0.10, 0.04, z1 - 0.02, seg=16, name="pearl_cradle")
    sphere("pearl", 0.13, (0, 0, z1 + 0.13), seg=24, rings=14, name="great_pearl")
    torus("oldgold", 0.135, 0.008, (0, 0, z1 + 0.10), seg=32, minor=5, name="pearl_band")
    cyl("oldgold", 0.012, 0.0, 0.08, z1 + 0.25, seg=6, name="finial")


# ---------------------------------------------------------------- Abyss Gate
def poseidon_abyss_gate():
    """DT art: a great round portal of swirling cyan light in a pearl-and-gold
    frame under a pointed keystone with a pearl, flanked by trident-bearing
    guardian statues, on dark abyssal cliffs with a glowing path out front."""
    z = plinth("abyss")
    # cliff chunks at the sides and back
    for k, (x, y, sx, sy, sz, rz) in enumerate([(-0.55, 0.20, 0.22, 0.30, 0.16, 0.3), (0.56, 0.18, 0.24, 0.28, 0.14, -0.4),
                                                  (-0.30, 0.45, 0.26, 0.18, 0.12, 0.6), (0.30, 0.46, 0.22, 0.18, 0.18, -0.2),
                                                  (-0.50, -0.30, 0.18, 0.18, 0.08, 0.9), (0.48, -0.32, 0.16, 0.20, 0.10, 0.2)]):
        box("rock", sx, sy, sz, (x, y, z + sz / 2 - 0.01), rot=(0, 0, rz), bevel=0.02, name="cliff")
    # gate platform
    box("deepteal", 1.04, 0.36, 0.06, (0, 0.06, z + 0.03), name="gate_deck")
    box("oldgold", 1.05, 0.37, 0.01, (0, 0.06, z + 0.045), name="gate_deck_trim")
    zd = z + 0.06
    # glowing path out the front
    box("cyan", 0.16, 0.56, 0.012, (0, -0.42, z + 0.002), name="path_glow")
    box("abyss", 0.03, 0.56, 0.03, (-0.10, -0.42, z + 0.01), name="path_edge")
    box("abyss", 0.03, 0.56, 0.03, (0.10, -0.42, z + 0.01), name="path_edge")
    # portal ring
    cz, R = zd + 0.48, 0.40
    cyl("cyan", R - 0.02, R - 0.02, 0.02, cz, xy=(0, 0.07), rot=(math.pi / 2, 0, 0), seg=40, name="portal_disc")
    cyl("white", 0.10, 0.10, 0.025, cz, xy=(0, 0.075), rot=(math.pi / 2, 0, 0), seg=20, name="portal_eye")
    tube("seaglass", [(0.04 * t * math.cos(5 * t) * 8, 0.035, cz + 0.04 * t * math.sin(5 * t) * 8) for t in [j / 30 for j in range(31)]], 0.012, 0.004, sides=5, name="swirl")
    tube("seaglass", [(-0.04 * t * math.cos(5 * t) * 8, 0.035, cz - 0.04 * t * math.sin(5 * t) * 8) for t in [j / 30 for j in range(31)]], 0.012, 0.004, sides=5, name="swirl")
    torus("pearl", R + 0.04, 0.05, (0, 0.06, cz), rot=(math.pi / 2, 0, 0), seg=48, minor=8, name="portal_ring")
    torus("oldgold", R - 0.01, 0.014, (0, 0.03, cz), rot=(math.pi / 2, 0, 0), seg=48, minor=6, name="portal_inner")
    torus("oldgold", R + 0.10, 0.012, (0, 0.06, cz), rot=(math.pi / 2, 0, 0), seg=48, minor=6, name="portal_outer")
    for k in range(12):
        a = TAU * k / 12
        box("oldgold", 0.03, 0.12, 0.05, ((R + 0.05) * math.cos(a), 0.06, cz + (R + 0.05) * math.sin(a)), rot=(0, -a, 0), name="ring_clamp")
    # pier feet under the ring
    for s in (-1, 1):
        box("pearl", 0.14, 0.16, 0.18, (s * 0.36, 0.06, zd + 0.09), bevel=0.01, name="ring_foot")
        box("oldgold", 0.15, 0.17, 0.012, (s * 0.36, 0.06, zd + 0.15), name="ring_foot_band")
    # pointed gothic frame and wing sails behind
    for s in (-1, 1):
        pts = [(s * 0.50, 0.10, zd), (s * 0.52, 0.10, zd + 0.40), (s * 0.46, 0.10, zd + 0.72), (s * 0.30, 0.10, zd + 0.95), (s * 0.12, 0.10, zd + 1.06), (0, 0.10, zd + 1.12)]
        tube("pearl", pts, 0.045, 0.03, sides=8, name="frame")
        tube("oldgold", [(p[0] * 1.06, p[1] - 0.02, p[2]) for p in pts[1:]], 0.01, sides=5, name="frame_gilt")
        # curved shell wings sweeping out behind the frame
        for j, (reach, top, r0) in enumerate(((0.66, 0.98, 0.05), (0.60, 0.80, 0.035))):
            wing = [(s * (0.16 + (reach - 0.16) * math.sin(t * math.pi / 2)), 0.20 + 0.04 * j,
                     zd + top - 0.05 + 0.10 * math.sin(t * math.pi * 0.8) - (top - 0.15) * max(0.0, t - 0.55) / 0.45) for t in [i / 12 for i in range(13)]]
            tube("pearl" if j == 0 else "teal", wing, r0, 0.01, sides=8, name="wing")
        box("deepteal", 0.12, 0.04, 0.50, (s * 0.60, 0.22, zd + 0.30), rot=(0, s * 0.08, 0), name="wing_web")
        box("seaglass", 0.03, 0.01, 0.38, (s * 0.60, 0.195, zd + 0.30), rot=(0, s * 0.08, 0), name="wing_glow")
    # diamond keystone with pearl
    kz = zd + 1.06
    cyl("pearl", 0.20, 0.20, 0.07, kz, xy=(0, 0.10), rot=(math.pi / 2, 0, 0), seg=4, name="keystone")
    cyl("oldgold", 0.17, 0.17, 0.08, kz, xy=(0, 0.105), rot=(math.pi / 2, 0, 0), seg=4, name="keystone_gilt")
    sphere("pearl", 0.06, (0, -0.03, kz), seg=18, rings=10, name="keystone_pearl")
    torus("oldgold", 0.065, 0.008, (0, -0.02, kz), rot=(math.pi / 2, 0, 0), seg=24, minor=5, name="keystone_bezel")
    cyl("pearl", 0.025, 0.0, 0.16, kz + 0.17, xy=(0, 0.06), seg=8, name="keystone_spire")
    # trident guardians on pedestals
    for s in (-1, 1):
        x, y = s * 0.62, -0.18
        box("pearl", 0.16, 0.16, 0.10, (x, y, z + 0.05), bevel=0.01, name="pedestal")
        box("oldgold", 0.17, 0.17, 0.012, (x, y, z + 0.09), name="pedestal_trim")
        zs = z + 0.10
        cyl("pearl", 0.05, 0.065, 0.24, zs, xy=(x, y), seg=10, name="robe")
        cyl("pearl", 0.065, 0.05, 0.16, zs + 0.24, xy=(x, y), seg=10, name="torso")
        box("pearl", 0.17, 0.06, 0.04, (x, y, zs + 0.38), bevel=0.01, name="shoulders")
        sphere("pearl", 0.04, (x, y - 0.005, zs + 0.45), seg=12, rings=8, name="head")
        for c in (-1, 0, 1):
            cyl("oldgold", 0.008, 0.0, 0.07 if c == 0 else 0.05, zs + 0.47, xy=(x + c * 0.022, y), seg=5, name="crown_spike")
        box("teal", 0.08, 0.012, 0.18, (x, y - 0.06, zs + 0.28), name="breastplate")
        # trident in the outer hand
        tx = x + s * 0.10
        tube("pearl", [(x + s * 0.06, y, zs + 0.36), (tx, y - 0.02, zs + 0.30)], 0.018, sides=6, name="arm")
        cyl("oldgold", 0.01, 0.01, 0.80, z + 0.08, xy=(tx, y - 0.02), seg=6, name="trident_shaft")
        tz = z + 0.88
        box("oldgold", 0.10, 0.012, 0.014, (tx, y - 0.02, tz), name="trident_bar")
        for c in (-1, 0, 1):
            cyl("oldgold", 0.008, 0.0, 0.10 if c == 0 else 0.08, tz, xy=(tx + c * 0.045, y - 0.02), seg=5, name="trident_prong")
        sphere("cyan", 0.016, (tx, y - 0.02, tz - 0.03), seg=8, rings=6, name="trident_gem")
    # glowing waterfalls off the cliffs
    for s in (-1, 1):
        box("cyan", 0.06, 0.008, 0.10, (s * 0.40, -0.30, z + 0.04), name="falls")
    # background obelisks
    for s in (-1, 1):
        cyl("pearl", 0.05, 0.03, 0.70, zd, xy=(s * 0.22, 0.34), seg=6, name="obelisk")
        cyl("oldgold", 0.04, 0.0, 0.10, zd + 0.70, xy=(s * 0.22, 0.34), seg=6, name="obelisk_cap")
        box("seaglass", 0.02, 0.006, 0.35, (s * 0.22, 0.30, zd + 0.40), name="obelisk_glow")


# ---------------------------------------------------------------- Leviathan Gate
def poseidon_apex_leviathan_gate():
    """DT art: a towering pearl gateway whose arch is the ribbed body of a
    leviathan with dorsal fins and a fanged head, pillars studded with pearls,
    glowing waterfalls through the gate, coral at the feet, arcade wings."""
    z = plinth("abyss")
    box("deepteal", 1.10, 0.40, 0.04, (0, 0.04, z + 0.02), name="deck")
    box("oldgold", 1.11, 0.41, 0.008, (0, 0.04, z + 0.03), name="deck_trim")
    zd = z + 0.04
    box("cyan", 0.36, 0.86, 0.012, (0, -0.10, z + 0.042), name="gate_water")
    # pillars
    for s in (-1, 1):
        x = s * 0.40
        box("pearl", 0.20, 0.22, 0.10, (x, 0.04, zd + 0.05), bevel=0.012, name="pillar_base")
        box("oldgold", 0.21, 0.23, 0.012, (x, 0.04, zd + 0.09), name="pillar_base_band")
        for dx in (-0.045, 0.045):
            cyl("pearl", 0.04, 0.035, 0.66, zd + 0.10, xy=(x + dx, 0.04), seg=10, name="pillar_shaft")
        box("seaglass", 0.05, 0.01, 0.46, (x, -0.035, zd + 0.40), name="pillar_glow")
        tube("oldgold", arc((x, -0.04, zd + 0.63), 0.03, 0, math.pi, steps=6), 0.006, sides=4, name="pillar_arch")
        for zz in (zd + 0.30, zd + 0.52, zd + 0.74):
            sphere("pearl", 0.038, (x + s * 0.09, -0.02, zz), seg=14, rings=8, name="stud_pearl")
            torus("oldgold", 0.04, 0.007, (x + s * 0.09, -0.01, zz), rot=(math.pi / 2, 0, 0), seg=18, minor=4, name="stud_bezel")
        box("oldgold", 0.22, 0.20, 0.03, (x, 0.04, zd + 0.77), name="capital")
        cyl("cyan", 0.04, 0.04, 0.008, zd + 0.785, xy=(x, -0.065), rot=(math.pi / 2, 0, 0), seg=12, name="capital_gem")
        # waterfalls inside the gate
        box("cyan", 0.03, 0.01, 0.60, (x - s * 0.13, 0.02, zd + 0.30), name="falls")
        box("cyan", 0.03, 0.01, 0.45, (x - s * 0.20, 0.06, zd + 0.24), name="falls")
        # coral clumps at the feet
        coral(x + s * 0.12, -0.14, zd, h=0.10, sw="cyan", n=5, seed=s)
        coral(x - s * 0.02, -0.16, zd, h=0.07, sw="pearl", n=4, seed=2 * s)
        # arcade wings running back
        for j in range(3):
            xx = s * (0.60 + j * 0.0)
            yy = -0.05 + j * 0.16
            box("pearl", 0.04, 0.04, 0.36, (s * 0.72, yy - 0.08, zd + 0.18), name="arcade_pier")
            tube("pearl", arc((s * 0.72, yy, zd + 0.30), 0.08, math.pi, 0, plane="yz", steps=8), 0.015, sides=5, name="arcade_arch")
        box("pearl", 0.06, 0.52, 0.03, (s * 0.72, 0.16, zd + 0.395), name="arcade_top")
        box("seaglass", 0.012, 0.42, 0.012, (s * 0.72, 0.16, zd + 0.415), name="arcade_channel")
        for yy in (-0.05, 0.27):
            sphere("pearl", 0.03, (s * 0.72, yy, zd + 0.44), seg=12, rings=8, name="arcade_pearl")
    # leviathan arch body: rising from the left pillar over to the right, head beyond
    body = []
    for j in range(15):
        t = j / 14
        x = -0.40 + 0.80 * t
        zz = zd + 0.80 + 0.36 * math.sin(math.pi * t) + 0.04 * t
        body.append((x, 0.04, zz))
    tube("deepteal", body, 0.10, 0.085, sides=12, name="body")
    tube("teal", [(p[0], p[1] - 0.06, p[2] - 0.02) for p in body[1:-1]], 0.05, sides=8, name="belly_scales")
    for j in range(1, 14):
        p = body[j]
        q = body[j + 1]
        ang = math.atan2(q[2] - p[2], q[0] - p[0])
        torus("pearl", 0.098, 0.014, p, rot=(0, -ang + math.pi / 2, 0), seg=12, minor=4, name="rib")
        if j % 2 == 1 and 1 < j < 13:
            # dorsal fin spikes
            nx, nz = -math.sin(ang), math.cos(ang)
            fin = [(p[0] + nx * 0.06, 0.04, p[2] + nz * 0.06), (p[0] + nx * 0.16 - 0.04, 0.04, p[2] + nz * 0.16), (p[0] + nx * 0.24 - 0.10, 0.04, p[2] + nz * 0.24)]
            tube("pearl", fin, 0.035, 0.003, sides=6, name="dorsal_fin")
    for j in (3, 7, 11):
        p = body[j]
        sphere("pearl", 0.04, (p[0], -0.07, p[2] - 0.02), seg=14, rings=8, name="body_pearl")
        torus("oldgold", 0.043, 0.007, (p[0], -0.06, p[2] - 0.02), rot=(math.pi / 2, 0, 0), seg=18, minor=4, name="body_bezel")
    # neck curls forward-right into the head
    hx, hz = 0.58, zd + 0.98
    tube("deepteal", [body[-1], (0.50, 0.00, zd + 0.94), (hx, -0.06, hz)], 0.085, 0.07, sides=12, name="neck")
    torus("pearl", 0.085, 0.014, (0.50, 0.0, zd + 0.94), rot=(0, math.pi / 2, 0.4), seg=16, minor=4, name="neck_rib")
    sphere("pearl", 0.10, (hx + 0.08, -0.10, hz), seg=18, rings=10, scale=(1.5, 0.75, 0.6), name="skull")
    sphere("teal", 0.08, (hx + 0.10, -0.10, hz - 0.06), seg=14, rings=8, scale=(1.5, 0.7, 0.35), name="jaw")
    sphere("cyan", 0.02, (hx + 0.08, -0.17, hz + 0.02), seg=10, rings=6, name="eye")
    sphere("cyan", 0.02, (hx + 0.08, -0.03, hz + 0.02), seg=10, rings=6, name="eye")
    for k in range(4):
        cyl("pearl", 0.008, 0.0, 0.035, hz - 0.08, xy=(hx + 0.04 + k * 0.04, -0.10), rot=(math.pi, 0, 0), seg=5, name="fang")
    for k, (dx, dz, ln) in enumerate([(0.0, 0.05, 0.16), (-0.05, 0.03, 0.12), (0.04, 0.06, 0.10)]):
        tube("pearl", [(hx + dx, -0.10, hz + dz), (hx + dx - ln * 0.6, -0.10, hz + dz + ln * 0.5), (hx + dx - ln, -0.10, hz + dz + ln * 0.6)], 0.022, 0.002, sides=5, name="crest")
    # tail tucked down the left pillar
    tube("deepteal", [body[0], (-0.50, 0.02, zd + 0.72), (-0.54, -0.02, zd + 0.58)], 0.08, 0.02, sides=10, name="tail")
    tube("pearl", [(-0.54, -0.02, zd + 0.58), (-0.60, -0.04, zd + 0.62), (-0.66, -0.06, zd + 0.70)], 0.03, 0.002, sides=6, name="tail_fluke")
    # glowing keystone under the apex
    sphere("cyan", 0.05, (0, -0.02, zd + 0.98), seg=16, rings=10, name="apex_glow")
    torus("oldgold", 0.065, 0.01, (0, -0.02, zd + 0.98), rot=(math.pi / 2, 0, 0), seg=24, minor=5, name="apex_ring")


# ---------------------------------------------------------------- Coral Bulwark
def poseidon_coral_bulwark():
    """DT art: a tiered pearl fortress crowned by a gothic spire with a great
    pearl medallion, glowing cyan lancet windows and spillways down the walls,
    white coral trees and shelf corals growing across the ramparts."""
    z = plinth("abyss")
    # tier 1: broad rampart (front half bulges forward)
    cyl("pearl", 0.70, 0.66, 0.20, z, seg=12, rot=(0, 0, math.pi / 12), name="rampart")
    cyl("oldgold", 0.668, 0.668, 0.012, z + 0.17, seg=12, rot=(0, 0, math.pi / 12), name="rampart_band")
    for k in range(12):
        a = TAU * k / 12
        box("pearl", 0.06, 0.04, 0.035, (0.66 * math.cos(a), 0.66 * math.sin(a), z + 0.215), rot=(0, 0, a + math.pi / 2), name="merlon")
        if math.sin(a) < 0.3:
            a2 = a + TAU / 24
            box("cyan", 0.04, 0.008, 0.10, (0.68 * math.cos(a2), 0.68 * math.sin(a2), z + 0.08), rot=(0, 0, a2 + math.pi / 2), name="lancet")
            box("pearl", 0.03, 0.06, 0.20, (0.68 * math.cos(a), 0.68 * math.sin(a), z + 0.10), rot=(0, 0, a + math.pi / 2), name="buttress")
    z1 = z + 0.20
    # tier 2
    cyl("deepteal", 0.56, 0.56, 0.01, z1, seg=12, rot=(0, 0, math.pi / 12), name="walk")
    cyl("pearl", 0.48, 0.45, 0.20, z1, seg=12, rot=(0, 0, math.pi / 12), name="tier2")
    cyl("oldgold", 0.458, 0.458, 0.012, z1 + 0.17, seg=12, rot=(0, 0, math.pi / 12), name="tier2_band")
    for k in range(12):
        a = TAU * k / 12 + TAU / 24
        if math.sin(a) < 0.4:
            box("seaglass", 0.05, 0.008, 0.13, (0.47 * math.cos(a), 0.47 * math.sin(a), z1 + 0.09), rot=(0, 0, a + math.pi / 2), name="window")
            tube("oldgold", [(0.473 * math.cos(a) + 0.025 * math.cos(t) * -math.sin(a), 0.473 * math.sin(a) + 0.025 * math.cos(t) * math.cos(a), z1 + 0.155 + 0.025 * math.sin(t)) for t in [math.pi * j / 6 for j in range(7)]], 0.004, sides=4, name="window_arch")
    # spillways from tier 2 down over the rampart
    for a in (240, 300, 200, 340):
        x, y, _ = polar(0.60, a)
        box("cyan", 0.06, 0.012, 0.18, (x, y, z + 0.11), rot=(0, 0, math.radians(a) + math.pi / 2), name="spill")
    z2 = z1 + 0.20
    # corner towers on tier 2
    for a in (210, 330, 90, 150, 30):
        x, y, _ = polar(0.52, a)
        h = 0.36 if a in (210, 330) else 0.26
        cyl("pearl", 0.07, 0.06, h, z1, xy=(x, y), seg=12, name="tower")
        cyl("oldgold", 0.072, 0.072, 0.012, z1 + h - 0.04, xy=(x, y), seg=12, name="tower_band")
        box("cyan", 0.02, 0.006, 0.10, (x + 0.065 * math.cos(math.radians(a)), y + 0.065 * math.sin(math.radians(a)), z1 + h - 0.15), rot=(0, 0, math.radians(a) + math.pi / 2), name="tower_slit")
        cyl("teal", 0.068, 0.0, 0.18, z1 + h, xy=(x, y), seg=12, name="tower_spire")
        sphere("pearl", 0.025, (x, y, z1 + h + 0.02), seg=10, rings=6, name="tower_pearl")
    # tier 3: keep
    cyl("pearl", 0.30, 0.28, 0.22, z2, seg=12, rot=(0, 0, math.pi / 12), name="keep")
    cyl("oldgold", 0.285, 0.285, 0.012, z2 + 0.19, seg=12, rot=(0, 0, math.pi / 12), name="keep_band")
    z3 = z2 + 0.22
    # central gothic spire facade
    box("pearl", 0.26, 0.20, 0.42, (0, -0.06, z3 + 0.21), bevel=0.01, name="spire_body")
    box("cyan", 0.10, 0.01, 0.38, (0, -0.165, z2 + 0.20), name="great_lancet")
    tube("oldgold", [(0.05 * math.cos(t), -0.17, z2 + 0.39 + 0.07 * math.sin(t)) for t in [math.pi * j / 8 for j in range(9)]], 0.008, sides=5, name="lancet_arch")
    for s in (-1, 1):
        tube("pearl", [(s * 0.13, -0.16, z3), (s * 0.10, -0.17, z3 + 0.20), (s * 0.03, -0.17, z3 + 0.38)], 0.02, sides=6, name="ogee")
        cyl("pearl", 0.035, 0.0, 0.26, z3 + 0.30, xy=(s * 0.14, -0.06), seg=8, name="pinnacle")
    cyl("pearl", 0.13, 0.0, 0.42, z3 + 0.42, xy=(0, -0.06), seg=8, rot=(0, 0, math.pi / 8), name="spire")
    cyl("oldgold", 0.135, 0.135, 0.015, z3 + 0.40, xy=(0, -0.06), seg=8, rot=(0, 0, math.pi / 8), name="spire_band")
    # pearl medallion
    sphere("pearl", 0.07, (0, -0.16, z3 + 0.25), seg=20, rings=12, name="medallion")
    torus("oldgold", 0.078, 0.012, (0, -0.15, z3 + 0.25), rot=(math.pi / 2, 0, 0), seg=28, minor=5, name="medallion_ring")
    # coral trees (white branching) and shelf corals
    def coral_tree(x, y, zb, h, sw="pearl"):
        tube(sw, [(x, y, zb), (x, y, zb + h * 0.5)], 0.02, 0.014, sides=6, name="coral_trunk")
        for k in range(5):
            a = TAU * k / 5 + x
            tip = (x + 0.07 * math.cos(a), y + 0.07 * math.sin(a), zb + h)
            tube(sw, [(x, y, zb + h * 0.45), (x + 0.035 * math.cos(a), y + 0.035 * math.sin(a), zb + h * 0.75), tip], 0.011, 0.004, sides=5, name="coral_branch")
            sphere(sw, 0.012, tip, seg=6, rings=4, name="coral_bud")
    coral_tree(-0.34, -0.42, z1 + 0.01, 0.20)
    coral_tree(0.36, -0.40, z1 + 0.01, 0.17)
    coral_tree(-0.18, -0.28, z2, 0.14)
    for (x, y, zz, r) in [(-0.22, -0.48, z1 + 0.06, 0.05), (0.20, -0.46, z1 + 0.05, 0.045), (0.22, -0.30, z2 + 0.06, 0.04), (-0.62, -0.25, z + 0.17, 0.04)]:
        cyl("pearl", 0.008, 0.008, 0.06, zz - 0.06, xy=(x, y), seg=6, name="shelf_stem")
        dome("pearl", r, zz, xy=(x, y), seg=14, rings=3, height=0.35, name="shelf_coral")
    for k, (x, y) in enumerate([(0.10, -0.50), (-0.08, -0.52), (0.30, -0.15)]):
        coral(x, y, z1 + 0.01, h=0.08, sw="cyan", n=4, seed=k)


# ---------------------------------------------------------------- Driftwood Breakwater
def poseidon_driftwood_breakwater():
    """Alpha art: a breakwater of weathered driftwood posts and long logs lashed
    with rope, crusted with glowing cyan coral, waves breaking white at its feet."""
    z = plinth("deepteal")
    # sea surface with foam
    cyl("teal", 0.74, 0.74, 0.02, z, seg=6, name="sea")
    for k, (x, y, sx) in enumerate([(-0.45, -0.25, 1.0), (-0.10, -0.38, 1.3), (0.30, -0.30, 1.1), (0.55, -0.05, 0.9), (-0.55, 0.10, 0.8), (0.10, -0.20, 0.9), (-0.25, -0.12, 1.0)]):
        sphere("white", 0.06, (x, y, z + 0.02), seg=10, rings=5, scale=(sx * 1.2, 0.7, 0.30), name="foam")
        sphere("white", 0.025, (x + 0.04, y - 0.02, z + 0.06), seg=8, rings=5, scale=(1, 1, 1.6), name="spray")
    # the line of the breakwater runs diagonally across the hex
    ang = math.radians(-15)
    ux, uy = math.cos(ang), math.sin(ang)
    def along(t, off=0.0):
        return (t * ux - off * uy, t * uy + off * ux)
    # vertical posts (two rows), slightly tilted, broken tops
    for i, t in enumerate([-0.60, -0.36, -0.12, 0.12, 0.36, 0.60]):
        for off, hh in ((-0.08, 0.55 + 0.08 * (i % 2)), (0.10, 0.48 + 0.06 * ((i + 1) % 2))):
            x, y = along(t, off)
            tilt = 0.10 * (1 if (i + (off > 0)) % 2 else -1)
            cyl("stone", 0.045, 0.040, hh, z - 0.02, xy=(x, y), seg=8, rot=(tilt, tilt * 0.5, 0), name="post")
            cyl("rock", 0.041, 0.020, 0.05, z - 0.02 + hh - 0.01, xy=(x + math.sin(tilt * 0.5) * hh, y - math.sin(tilt) * hh), seg=8, rot=(tilt, tilt * 0.5, 0), name="post_split")
            torus("dark", 0.048, 0.008, (x + math.sin(tilt * 0.5) * 0.36, y - math.sin(tilt) * 0.36, z + 0.34), rot=(tilt, tilt * 0.5, 0), seg=10, minor=3, name="rope")
            torus("dark", 0.048, 0.008, (x + math.sin(tilt * 0.5) * 0.25, y - math.sin(tilt) * 0.25, z + 0.23), rot=(tilt + 0.3, tilt * 0.5, 0), seg=10, minor=3, name="rope")
    # long horizontal logs
    for off, zz, r in ((-0.11, 0.36, 0.05), (0.12, 0.30, 0.045), (-0.09, 0.20, 0.045), (0.00, 0.44, 0.05)):
        x0, y0 = along(-0.72, off)
        x1, y1 = along(0.72, off)
        pts = [(x0, y0, z + zz - 0.02), ((x0 + x1) / 2, (y0 + y1) / 2, z + zz + 0.02), (x1, y1, z + zz - 0.03)]
        tube("stone", pts, r, r * 0.9, sides=8, name="log")
        # weathered ring ends
        for p in (pts[0], pts[-1]):
            sphere("rock", r * 0.95, p, seg=8, rings=5, scale=(0.4, 1, 1), name="log_end")
    # diagonal braces
    for t in (-0.48, 0.0, 0.48):
        xa, ya = along(t - 0.12, 0.16)
        xb, yb = along(t + 0.12, 0.16)
        tube("stone", [(xa, ya, z + 0.0), (xb, yb, z + 0.42)], 0.03, sides=7, name="brace")
        xa, ya = along(t + 0.14, -0.17)
        xb, yb = along(t - 0.08, -0.12)
        tube("rock", [(xa, ya, z + 0.0), (xb, yb, z + 0.38)], 0.028, sides=7, name="brace")
    # rope spans sagging between posts
    for t in (-0.48, -0.24, 0.0, 0.24, 0.48):
        xa, ya = along(t - 0.12, -0.12)
        xb, yb = along(t + 0.12, -0.12)
        tube("dark", [(xa, ya, z + 0.30), ((xa + xb) / 2, (ya + yb) / 2, z + 0.24), (xb, yb, z + 0.30)], 0.007, sides=4, name="rope_span")
    # glowing coral crusts along the top and down the posts
    for k, t in enumerate([-0.62, -0.42, -0.20, 0.05, 0.26, 0.46, 0.64]):
        x, y = along(t, 0.0 if k % 2 else -0.06)
        zb = z + 0.45
        coral(x, y, zb, h=0.10 + 0.03 * (k % 3), sw="cyan", n=5, seed=k)
        sphere("seaglass", 0.05, (x + 0.02, y + 0.01, zb + 0.01), seg=10, rings=6, scale=(1.3, 1, 0.6), name="coral_mound")
        for j in range(4):
            aa = TAU * j / 4 + k
            sphere("cyan", 0.022, (x + 0.05 * math.cos(aa), y + 0.05 * math.sin(aa), zb + 0.03), seg=8, rings=5, name="coral_bulb")
    for k, t in enumerate([-0.50, -0.14, 0.18, 0.52]):
        x, y = along(t, -0.13)
        for j in range(3):
            sphere("cyan", 0.016, (x + 0.01 * j, y - 0.035, z + 0.12 + 0.07 * j), seg=8, rings=5, name="polyp")
    # small pearl lanterns hung on the end posts
    for t in (-0.60, 0.60):
        x, y = along(t, -0.08)
        sphere("pearl", 0.03, (x, y - 0.06, z + 0.52), seg=12, rings=8, name="lantern")
        torus("oldgold", 0.032, 0.006, (x, y - 0.06, z + 0.52), seg=16, minor=4, name="lantern_band")


# ---------------------------------------------------------------- Moonwell Tidegate
def poseidon_moonwell_tidegate():
    """Alpha art: a colossal segmented tech ring standing over the tide with the
    glowing moon held inside, flanked by white arches and domes, water cascading
    from its foot, glowing coral growing on the ring base."""
    z = plinth("abyss")
    # curved deck and pool
    cyl("deepteal", 0.70, 0.72, 0.05, z, seg=24, name="deck")
    cyl("oldgold", 0.725, 0.725, 0.01, z + 0.03, seg=24, name="deck_trim")
    cyl("cyan", 0.46, 0.46, 0.012, z + 0.045, seg=24, name="pool")
    zd = z + 0.05
    # ring: segmented plates around a circle in the XZ plane
    cz, R = zd + 0.60, 0.52
    n = 18
    for k in range(n):
        a = TAU * k / n
        x, zz = R * math.cos(a), cz + R * math.sin(a)
        sw = "pearl" if k % 3 else "white"
        box(sw, 0.17, 0.20, 0.10, (x, 0.0, zz), rot=(0, -a + math.pi / 2, 0), bevel=0.01, name="ring_plate")
        box("deepteal", 0.05, 0.21, 0.06, ((R + 0.02) * math.cos(a + TAU / (2 * n)), 0.0, cz + (R + 0.02) * math.sin(a + TAU / (2 * n))), rot=(0, -a - TAU / (2 * n) + math.pi / 2, 0), name="ring_joint")
        box("teal", 0.10, 0.012, 0.05, ((R + 0.01) * math.cos(a), -0.106, cz + (R + 0.01) * math.sin(a)), rot=(0, -a + math.pi / 2, 0), name="ring_panel")
    torus("cyan", R - 0.07, 0.01, (0, -0.06, cz), rot=(math.pi / 2, 0, 0), seg=48, minor=5, name="ring_glow")
    torus("cyan", R - 0.07, 0.01, (0, 0.06, cz), rot=(math.pi / 2, 0, 0), seg=48, minor=5, name="ring_glow")
    torus("pearl", R + 0.05, 0.02, (0, 0.0, cz), rot=(math.pi / 2, 0, 0), seg=48, minor=6, name="ring_rim")
    # the moon inside
    sphere("pearl", 0.24, (0, 0.0, cz), seg=28, rings=16, scale=(1, 0.5, 1), name="moon")
    sphere("cyan", 0.20, (0, 0.03, cz), seg=20, rings=12, scale=(1, 0.5, 1), name="moon_glow_core")
    for (dx, dz, r) in [(-0.08, 0.06, 0.04), (0.06, -0.08, 0.05), (0.10, 0.10, 0.025), (-0.05, -0.12, 0.03)]:
        sphere("white", r, (dx, -0.11, cz + dz), seg=10, rings=6, scale=(1, 0.3, 1), name="crater")
    torus("seaglass", 0.30, 0.008, (0, 0.0, cz), rot=(math.pi / 2, 0, 0), seg=40, minor=4, name="moon_halo")
    # ring cradle
    for s in (-1, 1):
        box("pearl", 0.26, 0.30, 0.14, (s * 0.30, 0.0, zd + 0.07), bevel=0.02, rot=(0, s * 0.25, 0), name="cradle")
        box("teal", 0.20, 0.31, 0.03, (s * 0.30, 0.0, zd + 0.09), rot=(0, s * 0.25, 0), name="cradle_band")
    # waterfalls from the foot of the ring
    for s in (-1, 1):
        box("cyan", 0.06, 0.012, 0.22, (s * 0.20, -0.12, zd + 0.13), rot=(0.2, 0, 0), name="falls")
    box("white", 0.30, 0.10, 0.03, (0, -0.20, zd + 0.01), name="foam")
    # glowing coral on the base
    for k, (x, y) in enumerate([(-0.40, -0.12), (-0.28, -0.18), (0.36, -0.14), (0.44, -0.04), (0.22, -0.20)]):
        coral(x, y, zd + 0.05 if abs(x) > 0.3 else zd + 0.10, h=0.10, sw="cyan", n=5, seed=k)
    # side arches behind
    for s in (-1, 1):
        tube("pearl", arc((s * 0.55, 0.22, zd), 0.20, 0, math.pi, plane="xz", steps=14), 0.035, sides=8, name="side_arch")
        tube("seaglass", arc((s * 0.55, 0.19, zd), 0.20, 0.2, math.pi - 0.2, plane="xz", steps=12), 0.008, sides=4, name="side_arch_glow")
    # domes at the back corners
    for s in (-1, 1):
        x, y = s * 0.42, 0.44
        cyl("pearl", 0.13, 0.13, 0.10, zd, xy=(x, y), seg=20, name="dome_drum")
        cyl("teal", 0.135, 0.135, 0.012, zd + 0.07, xy=(x, y), seg=20, name="dome_band")
        dome("pearl", 0.13, zd + 0.10, xy=(x, y), seg=20, rings=5, height=0.8, name="dome")
        box("cyan", 0.06, 0.008, 0.03, (x, y - 0.13, zd + 0.04), name="dome_window")
    # front console pad on the deck
    box("pearl", 0.24, 0.10, 0.04, (0, -0.52, zd + 0.02), bevel=0.01, name="console")
    box("cyan", 0.18, 0.012, 0.012, (0, -0.57, zd + 0.03), name="console_light")


# ---------------------------------------------------------------- Sonar Beacon
def poseidon_sonar_beacon():
    """DT art: a slender pearl lighthouse-tower on an arched causeway, gothic
    glowing windows, a gold-and-pearl cage crown holding a great pearl with
    pearl-tipped antennae, and concentric cyan sonar rings pulsing around it."""
    z = plinth("abyss")
    # causeway arches across the plinth
    box("pearl", 1.40, 0.16, 0.04, (0, 0.0, z + 0.16), name="causeway")
    box("oldgold", 1.41, 0.17, 0.008, (0, 0.0, z + 0.165), name="causeway_trim")
    for s in (-1, 1):
        for j in range(2):
            cx = s * (0.38 + j * 0.22)
            tube("pearl", arc((cx, -0.06, z + 0.02), 0.10, 0, math.pi, steps=10), 0.02, sides=6, name="causeway_arch")
            box("pearl", 0.04, 0.12, 0.14, (cx + s * 0.11, 0.0, z + 0.07), name="causeway_pier")
    # tower base: wide drum with flying buttresses
    cyl("pearl", 0.28, 0.24, 0.20, z, seg=16, name="base_drum")
    cyl("oldgold", 0.245, 0.245, 0.012, z + 0.18, seg=16, name="base_band")
    for k in range(8):
        a = TAU * k / 8 + TAU / 16
        box("seaglass", 0.05, 0.008, 0.11, (0.265 * math.cos(a), 0.265 * math.sin(a), z + 0.075), rot=(0, 0, a + math.pi / 2), name="base_window")
        tube("oldgold", [(0.268 * math.cos(a) - 0.025 * math.cos(t) * math.sin(a), 0.268 * math.sin(a) + 0.025 * math.cos(t) * math.cos(a), z + 0.13 + 0.025 * math.sin(t)) for t in [math.pi * j / 6 for j in range(7)]], 0.004, sides=4, name="base_arch")
    zb = z + 0.20
    for k in range(6):
        a = TAU * k / 6
        tube("pearl", [(0.30 * math.cos(a), 0.30 * math.sin(a), z), (0.24 * math.cos(a), 0.24 * math.sin(a), zb + 0.08), (0.14 * math.cos(a), 0.14 * math.sin(a), zb + 0.30)], 0.022, 0.012, sides=6, name="buttress")
        cyl("pearl", 0.022, 0.0, 0.14, zb, xy=(0.27 * math.cos(a), 0.27 * math.sin(a)), seg=6, name="buttress_spire")
    # mid shaft, tapering, with tiers
    t1 = zb + 0.36
    cyl("pearl", 0.17, 0.13, t1 - zb, zb, seg=12, name="shaft")
    for k in range(6):
        a = TAU * k / 6
        box("cyan", 0.04, 0.008, 0.20, (0.152 * math.cos(a), 0.152 * math.sin(a), zb + 0.17), rot=(0, 0, a + math.pi / 2), name="shaft_window")
        box("pearl", 0.03, 0.04, t1 - zb, (0.16 * math.cos(a + TAU / 12), 0.16 * math.sin(a + TAU / 12), (zb + t1) / 2), rot=(0, 0, a + TAU / 12), name="shaft_rib")
    cyl("oldgold", 0.17, 0.17, 0.014, t1 - 0.03, seg=12, name="shaft_band")
    cyl("pearl", 0.17, 0.15, 0.04, t1, seg=12, name="gallery")
    for k in range(12):
        a = TAU * k / 12
        cyl("pearl", 0.008, 0.008, 0.05, t1 + 0.04, xy=(0.155 * math.cos(a), 0.155 * math.sin(a)), seg=5, name="baluster")
    torus("oldgold", 0.155, 0.007, (0, 0, t1 + 0.09), seg=28, minor=4, name="rail")
    t2 = t1 + 0.04
    cyl("pearl", 0.10, 0.08, 0.20, t2, seg=12, name="upper_shaft")
    for k in range(4):
        a = TAU * k / 4 + TAU / 8
        box("seaglass", 0.03, 0.008, 0.12, (0.088 * math.cos(a), 0.088 * math.sin(a), t2 + 0.10), rot=(0, 0, a + math.pi / 2), name="upper_window")
    t3 = t2 + 0.20
    cyl("oldgold", 0.09, 0.12, 0.04, t3, seg=16, name="cage_base")
    # cage crown: curved ribs around the great pearl
    pc = t3 + 0.16
    sphere("pearl", 0.11, (0, 0, pc), seg=24, rings=14, name="great_pearl")
    sphere("cyan", 0.05, (0, 0, pc), seg=12, rings=8, name="pearl_core")
    for k in range(8):
        a = TAU * k / 8
        pts = [((0.10 + 0.08 * math.sin(math.pi * t)) * math.cos(a), (0.10 + 0.08 * math.sin(math.pi * t)) * math.sin(a), t3 + 0.04 + 0.30 * t) for t in [j / 8 for j in range(9)]]
        tube("oldgold" if k % 2 else "pearl", pts, 0.009, 0.006, sides=5, name="cage_rib")
    # antennae with pearls
    for k in range(4):
        a = TAU * k / 4
        x, y = 0.19 * math.cos(a), 0.19 * math.sin(a)
        tube("pearl", [(0.14 * math.cos(a), 0.14 * math.sin(a), t3 + 0.08), (x, y, t3 + 0.22), (x, y, t3 + 0.32)], 0.008, 0.005, sides=5, name="antenna")
        sphere("pearl", 0.025, (x, y, t3 + 0.34), seg=10, rings=6, name="antenna_pearl")
    mast0 = t3 + 0.30
    cyl("oldgold", 0.008, 0.006, 1.58 - mast0 - 0.02, mast0, seg=6, name="mast")
    for zz, r in ((mast0 + 0.06, 0.03), (mast0 + 0.15, 0.025), (mast0 + 0.22, 0.02)):
        sphere("pearl", r, (0, 0, zz), seg=12, rings=8, name="mast_pearl")
    # concentric sonar rings
    for R, zz in ((0.30, t3 + 0.10), (0.50, t3 + 0.06), (0.70, t3 + 0.02), (0.84, t3 - 0.02)):
        torus("cyan", R, 0.008, (0, 0, zz), seg=int(24 + R * 48), minor=4, name="sonar_ring")
        for k in range(int(R * 12)):
            a = TAU * k / int(R * 12)
            sphere("seaglass", 0.012, (R * math.cos(a), R * math.sin(a), zz), seg=6, rings=4, name="sonar_bead")
    # ring hangers: thin gold struts from the gallery out to the rings
    for k in range(4):
        a = TAU * k / 4 + TAU / 8
        tube("oldgold", [(0.12 * math.cos(a), 0.12 * math.sin(a), t3 + 0.04), (0.84 * math.cos(a), 0.84 * math.sin(a), t3 - 0.02)], 0.004, sides=4, name="ring_strut")


# ---------------------------------------------------------------- Ambrosial Spring
def poseidon_structure_ambrosial_spring():
    """DT art: an open pearl rotunda on coral-wrapped columns under a glowing
    dome with a pearl crown, a great scallop-shell basin in its heart with a
    cascade of glowing water spilling down tiered steps into a pool."""
    z = plinth("abyss")
    # pool and tiered steps
    cyl("pearl", 0.74, 0.74, 0.04, z, seg=6, name="pool_rim")
    cyl("cyan", 0.68, 0.68, 0.012, z + 0.035, seg=6, name="pool")
    cyl("oldgold", 0.745, 0.745, 0.008, z + 0.02, seg=6, name="pool_trim")
    cyl("pearl", 0.52, 0.52, 0.06, z, seg=24, name="step1")
    cyl("pearl", 0.46, 0.46, 0.06, z + 0.06, seg=24, name="step2")
    cyl("oldgold", 0.465, 0.465, 0.008, z + 0.10, seg=24, name="step2_trim")
    zf = z + 0.12
    # columns
    Rc = 0.40
    zc1 = zf + 0.58
    for k in range(6):
        a = TAU * k / 6 + TAU / 12
        x, y = Rc * math.cos(a), Rc * math.sin(a)
        cyl("pearl", 0.05, 0.05, 0.04, zf, xy=(x, y), seg=12, name="col_base")
        cyl("pearl", 0.032, 0.03, 0.52, zf + 0.04, xy=(x, y), seg=12, name="column")
        cyl("oldgold", 0.05, 0.035, 0.04, zf + 0.54, xy=(x, y), seg=12, name="capital")
        # coral vine
        tube("cyan", helix(x, y, zf + 0.05, zf + 0.50, 0.036, 1.5, phase=a), 0.007, sides=4, name="coral_vine")
        sphere("pearl", 0.018, (x * 1.12, y * 1.12, zf + 0.56), seg=8, rings=6, name="col_pearl")
    # entablature ring with arches between columns
    cyl("pearl", 0.46, 0.46, 0.05, zc1, seg=24, name="entablature")
    cyl("oldgold", 0.465, 0.465, 0.012, zc1 + 0.01, seg=24, name="entablature_trim")
    for k in range(6):
        a0 = TAU * k / 6 + TAU / 12
        a1 = a0 + TAU / 6
        am = (a0 + a1) / 2
        pts = []
        for j in range(9):
            t = j / 8
            aa = a0 + (a1 - a0) * t
            pts.append((Rc * math.cos(aa), Rc * math.sin(aa), zc1 - 0.10 * math.sin(math.pi * t) * -1 - 0.10))
        tube("pearl", pts, 0.018, sides=6, name="arch")
    # glowing dome with pearl ribs
    zd = zc1 + 0.05
    dome("seaglass", 0.40, zd, seg=24, rings=6, height=0.95, name="dome")
    for k in range(8):
        a = TAU * k / 8
        tube("pearl", [(0.41 * math.cos(t) * math.cos(a), 0.41 * math.cos(t) * math.sin(a), zd + 0.39 * math.sin(t)) for t in [math.pi / 2 * j / 8 for j in range(8)]], 0.016, sides=5, name="dome_rib")
    torus("oldgold", 0.36, 0.01, (0, 0, zd + 0.17), seg=40, minor=5, name="dome_ring")
    cyl("pearl", 0.06, 0.05, 0.06, zd + 0.37, seg=12, name="lantern")
    sphere("pearl", 0.08, (0, 0, zd + 0.49), seg=20, rings=12, name="crown_pearl")
    cyl("oldgold", 0.012, 0.0, 0.08, zd + 0.56, seg=6, name="finial")
    # front pearl medallion at the dome rim
    sphere("pearl", 0.06, (0, -0.42, zc1 + 0.06), seg=18, rings=10, name="medallion")
    torus("oldgold", 0.065, 0.01, (0, -0.41, zc1 + 0.06), rot=(math.pi / 2, 0, 0), seg=24, minor=5, name="medallion_ring")
    for s in (-1, 1):
        tube("pearl", [(s * 0.06, -0.43, zc1 + 0.06), (s * 0.14, -0.43, zc1 + 0.14), (s * 0.20, -0.42, zc1 + 0.12)], 0.016, 0.004, sides=5, name="medallion_wing")
    # scallop basin
    zb = zf + 0.16
    cyl("pearl", 0.03, 0.05, 0.16, zf, seg=10, name="basin_stem")
    cyl("pearl", 0.06, 0.22, 0.08, zb, seg=20, name="basin")
    for k in range(10):
        a = TAU * k / 10
        tube("pearl", [(0.05 * math.cos(a), 0.05 * math.sin(a), zb + 0.01), (0.23 * math.cos(a), 0.23 * math.sin(a), zb + 0.085)], 0.014, 0.02, sides=5, name="scallop_rib")
    cyl("cyan", 0.20, 0.20, 0.01, zb + 0.065, seg=20, name="basin_water")
    torus("oldgold", 0.225, 0.008, (0, 0, zb + 0.08), seg=32, minor=4, name="basin_rim")
    # water column from the dome into the basin, and spill down the steps
    cyl("cyan", 0.035, 0.05, zd - zb - 0.07, zb + 0.07, seg=10, name="water_column")
    for s in (-1, 0, 1):
        box("cyan", 0.06, 0.012, 0.16, (s * 0.10, -0.21, zf + 0.04), rot=(0.2, 0, 0), name="spill")
    box("cyan", 0.30, 0.12, 0.01, (0, -0.47, z + 0.115), name="spill_step")
    box("cyan", 0.26, 0.06, 0.01, (0, -0.52, z + 0.055), name="spill_step")
    # front stairs
    for i in range(2):
        box("pearl", 0.36, 0.05, 0.03, (-0.0, -0.55 + i * 0.04, z + 0.015 + i * 0.03), name="stair")
    # coral clumps and pearls at the pool
    for k, (x, y) in enumerate([(-0.58, -0.10), (0.58, -0.08), (-0.50, 0.30), (0.52, 0.30)]):
        coral(x, y, z + 0.04, h=0.12, sw="cyan" if k < 2 else "teal", n=5, seed=k)
    for s in (-1, 1):
        x, y = s * 0.56, -0.30
        cyl("pearl", 0.06, 0.05, 0.08, z + 0.04, xy=(x, y), seg=10, name="font")
        sphere("pearl", 0.04, (x, y, z + 0.16), seg=12, rings=8, name="font_pearl")


BUILDERS = {
    "poseidon_ability_tidewell": ("Tidewell Bastion", "Poseidon", poseidon_ability_tidewell),
    "poseidon_abyss_gate": ("Abyss Gate", "Poseidon", poseidon_abyss_gate),
    "poseidon_apex_leviathan_gate": ("Leviathan Gate", "Poseidon", poseidon_apex_leviathan_gate),
    "poseidon_coral_bulwark": ("Coral Bulwark", "Poseidon", poseidon_coral_bulwark),
    "poseidon_driftwood_breakwater": ("Driftwood Breakwater", "Poseidon", poseidon_driftwood_breakwater),
    "poseidon_moonwell_tidegate": ("Moonwell Tidegate", "Poseidon", poseidon_moonwell_tidegate),
    "poseidon_sonar_beacon": ("Sonar Beacon", "Poseidon", poseidon_sonar_beacon),
    "poseidon_structure_ambrosial_spring": ("Ambrosial Spring", "Poseidon", poseidon_structure_ambrosial_spring),
}
