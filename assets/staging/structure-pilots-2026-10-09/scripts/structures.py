"""The three AI-081 pilot structures. Each builder only adds parts via kit."""
import math
from kit import box, cyl, sphere, dome, torus, tube, helix, arc

TAU = math.tau
HEX30 = 0.0  # bmesh hexes are already pointy-top (corners at +-Y), matching BoardLayout.TileMesh


def zeus_storm_relay_pylon():
    """DT art: marble column wrapped by a gold ribbon with blue lightning panels,
    gold crescent horns holding a lightning orb, terraced marble plinth on rock,
    blue sun banners, corner pillars with blue orbs."""
    # rock + terraces (footprint: hex circumradius 0.86 -> 1.72 across corners)
    cyl("stone", 0.80, 0.86, 0.10, 0.0, seg=6, rot=(0, 0, HEX30), name="rock")
    cyl("marble", 0.82, 0.80, 0.05, 0.10, seg=6, rot=(0, 0, HEX30), name="terrace")
    cyl("gold", 0.83, 0.83, 0.014, 0.125, seg=6, rot=(0, 0, HEX30), name="terrace_trim")
    cyl("marble", 0.52, 0.50, 0.06, 0.157, seg=6, rot=(0, 0, HEX30), name="terrace2")
    cyl("gold", 0.525, 0.525, 0.014, 0.195, seg=6, rot=(0, 0, HEX30), name="terrace2_trim")
    # front stairs
    for i in range(3):
        box("marble", 0.34, 0.07, 0.02, (0, -0.47 - i * 0.05 + 0.0, 0.157 + 0.01 + 0.0 - i * 0.0 + (2 - i) * 0.02 - 0.02), name="step")
    # banners hanging on the two front flats of the plinth (pointy-top hex: flats face 240 and 300 deg)
    for deg in (240, 300):
        a = math.radians(deg)
        rz = a + math.pi / 2  # local -Y of each part points out of the flat
        def at(d, z, lateral=0.0):
            return (d * math.cos(a) - lateral * math.sin(a), d * math.sin(a) + lateral * math.cos(a), z)
        box("royal", 0.18, 0.012, 0.13, at(0.752, 0.08), rot=(0, 0, rz), name="banner")
        box("gold", 0.20, 0.016, 0.016, at(0.752, 0.15), rot=(0, 0, rz), name="banner_rod")
        box("gold", 0.18, 0.014, 0.008, at(0.752, 0.018), rot=(0, 0, rz), name="banner_hem")
        x, y, _ = at(0.760, 0)
        cyl("gold", 0.026, 0.026, 0.004, 0.08, xy=(x, y), rot=(math.pi / 2, 0, rz), seg=16, name="banner_sun")
        torus("gold", 0.04, 0.004, at(0.760, 0.08), rot=(math.pi / 2, 0, rz), seg=20, minor=4, name="banner_sun_ring")
    # corner pillars with blue orbs
    for a in (210, 330, 90):
        r = 0.66
        x, y = r * math.cos(math.radians(a)), r * math.sin(math.radians(a))
        box("marble", 0.08, 0.08, 0.22, (x, y, 0.157 + 0.11), bevel=0.006, name="pillar")
        box("gold", 0.10, 0.10, 0.02, (x, y, 0.157 + 0.23), name="pillar_cap")
        box("gold", 0.095, 0.095, 0.015, (x, y, 0.165), name="pillar_foot")
        sphere("glass", 0.035, (x, y, 0.157 + 0.275), seg=14, rings=8, name="pillar_orb")
    # dais
    cyl("marble", 0.30, 0.27, 0.05, 0.217, seg=8, rot=(0, 0, math.pi / 8), name="dais")
    cyl("gold", 0.305, 0.305, 0.014, 0.245, seg=8, rot=(0, 0, math.pi / 8), name="dais_trim")
    z0, z1 = 0.274, 1.02
    # column: octagonal marble shaft, blue lightning panels between gold-edged ribs
    cyl("royal", 0.15, 0.15, z1 - z0, z0, seg=8, rot=(0, 0, math.pi / 8), name="shaft_core")
    for k in range(8):
        a = TAU * k / 8
        box("marble", 0.06, 0.03, z1 - z0, (0.155 * math.cos(a), 0.155 * math.sin(a), (z0 + z1) / 2), rot=(0, 0, a + math.pi / 2), name="rib")
        if k % 2 == 0:
            box("lightning", 0.055, 0.01, z1 - z0 - 0.2, (0.152 * math.cos(a + TAU / 16), 0.152 * math.sin(a + TAU / 16), (z0 + z1) / 2), rot=(0, 0, a + TAU / 16 + math.pi / 2), name="bolt_panel")
    for z in (z0 + 0.01, 0.52, 0.80, z1 - 0.01):
        cyl("gold", 0.185, 0.185, 0.03, z - 0.015, seg=8, rot=(0, 0, math.pi / 8), name="band")
    # gold ribbon spiralling up the shaft
    tube("gold", helix(0, 0, 0.30, 0.95, 0.19, 1.6, phase=-math.pi / 2), 0.012, sides=6, name="ribbon")
    # clock medallion on the front
    torus("gold", 0.055, 0.01, (0, -0.19, 0.86), rot=(math.pi / 2, 0, 0), seg=24, minor=6, name="medallion")
    cyl("royal", 0.05, 0.05, 0.01, 0.86, xy=(0, -0.19), rot=(math.pi / 2, 0, 0), seg=20, name="medallion_face")
    # flared collar
    cyl("gold", 0.18, 0.27, 0.07, z1, seg=16, name="collar")
    cyl("marble", 0.27, 0.27, 0.03, z1 + 0.07, seg=16, name="collar_lip")
    cyl("gold", 0.275, 0.275, 0.012, z1 + 0.08, seg=16, name="collar_trim")
    # crescent horns: two big ones left/right, two small front/back
    def horn(angle, reach, top, r0):
        pts = []
        for i in range(17):
            t = i / 16
            rad = 0.2 + reach * math.sin(t * math.pi * 0.85)
            z = z1 + 0.10 + top * t
            pts.append((rad * math.cos(angle), rad * math.sin(angle), z))
        tube("gold", pts, r0, 0.006, sides=8, name="horn")
        # white inner face to echo the marble-and-gold crescents
        tube("marble", [(p[0] * 0.93, p[1] * 0.93, p[2]) for p in pts[:-3]], r0 * 0.7, 0.008, sides=6, name="horn_inner")
        tip = pts[-1]
        cyl("gold", 0.012, 0.0, 0.06, tip[2], xy=(tip[0], tip[1]), seg=8, name="horn_tip")
    horn(0.0, 0.14, 0.33, 0.035)
    horn(math.pi, 0.14, 0.33, 0.035)
    horn(math.pi / 2, 0.08, 0.24, 0.022)
    horn(-math.pi / 2, 0.08, 0.24, 0.022)
    # orb on a gold cradle, gyroscope rings, finial
    cyl("gold", 0.05, 0.09, 0.05, z1 + 0.13, seg=16, name="cradle")
    sphere("lightning", 0.085, (0, 0, z1 + 0.27), seg=24, rings=14, name="orb")
    torus("gold", 0.11, 0.006, (0, 0, z1 + 0.27), rot=(math.radians(70), 0, math.radians(20)), seg=32, minor=6, name="gyro")
    torus("gold", 0.115, 0.006, (0, 0, z1 + 0.27), rot=(math.radians(-60), math.radians(30), 0), seg=32, minor=6, name="gyro")
    cyl("gold", 0.012, 0.0, 0.10, z1 + 0.355, seg=8, name="finial")


def zeus_keraunos_charging_spire():
    """DT art: glass charging chamber with a blue lightning coil, framed by twin
    marble pylons with blue sun banners, gold crown and spire with orbital rings,
    round marble deck with orb lamps on corner turrets."""
    cyl("stone", 0.78, 0.84, 0.09, 0.0, seg=6, rot=(0, 0, HEX30), name="rock")
    cyl("marble", 0.80, 0.78, 0.05, 0.09, seg=32, name="deck")
    cyl("gold", 0.805, 0.805, 0.014, 0.115, seg=32, name="deck_trim")
    cyl("royal", 0.42, 0.42, 0.006, 0.14, seg=32, name="deck_inlay")
    torus("gold", 0.42, 0.006, (0, 0, 0.146), seg=40, minor=4, name="inlay_ring")
    # four corner turrets with orb lamps
    for a in (225, 315, 45, 135):
        x, y = 0.62 * math.cos(math.radians(a)), 0.62 * math.sin(math.radians(a))
        box("marble", 0.12, 0.12, 0.22, (x, y, 0.14 + 0.11), bevel=0.006, name="turret")
        box("royal", 0.125, 0.125, 0.08, (x, y, 0.14 + 0.13), name="turret_band")
        box("gold", 0.14, 0.14, 0.02, (x, y, 0.37), name="turret_cap")
        cyl("gold", 0.03, 0.04, 0.03, 0.38, xy=(x, y), seg=12, name="lamp_cup")
        sphere("lightning", 0.04, (x, y, 0.445), seg=14, rings=8, name="lamp_orb")
    # low arcade drum around the chamber base
    cyl("marble", 0.30, 0.30, 0.20, 0.146, seg=24, name="drum")
    for k in range(12):
        a = TAU * k / 12
        cyl("white", 0.018, 0.018, 0.18, 0.15, xy=(0.315 * math.cos(a), 0.315 * math.sin(a)), seg=8, name="colonnette")
    cyl("gold", 0.33, 0.33, 0.025, 0.346, seg=24, name="cornice")
    torus("gold", 0.07, 0.012, (0, -0.33, 0.27), rot=(math.pi / 2, 0, 0), seg=24, minor=6, name="gear")
    for k in range(8):
        a = TAU * k / 8
        box("gold", 0.012, 0.01, 0.14, (0, -0.335, 0.27), rot=(0, a, 0), name="gear_spoke")
    # charging chamber: gold rings top and bottom, glass ribs, lightning coil around a core
    zc0, zc1 = 0.37, 1.00
    cyl("gold", 0.24, 0.24, 0.04, zc0, seg=24, name="chamber_foot")
    cyl("gold", 0.24, 0.24, 0.04, zc1 - 0.04, seg=24, name="chamber_head")
    cyl("glass", 0.07, 0.07, zc1 - zc0, zc0, seg=16, name="core_glow")
    cyl("marble", 0.035, 0.035, zc1 - zc0 + 0.02, zc0 - 0.01, seg=10, name="core")
    for z in (0.58, 0.79):
        torus("gold", 0.205, 0.008, (0, 0, z), seg=32, minor=5, name="cage_ring")
    for ph in (0.0, math.pi):
        tube("lightning", helix(0, 0, zc0 + 0.05, zc1 - 0.06, 0.135, 2.5, phase=ph), 0.016, sides=6, name="coil")
    for k in range(10):
        a = TAU * k / 10 + TAU / 20
        cyl("gold", 0.01, 0.01, zc1 - zc0, zc0, xy=(0.205 * math.cos(a), 0.205 * math.sin(a)), seg=6, name="cage_rib")
    # twin pylons with banners
    for s in (-1, 1):
        x = s * 0.42
        box("marble", 0.13, 0.13, 0.98, (x, 0.02, 0.14 + 0.49), bevel=0.008, name="pylon")
        for z in (0.30, 0.80, 1.10):
            box("gold", 0.145, 0.145, 0.02, (x, 0.02, z), name="pylon_band")
        cyl("gold", 0.10, 0.0, 0.16, 1.12, xy=(x, 0.02), seg=4, rot=(0, 0, math.pi / 4), name="pylon_cap")
        box("royal", 0.10, 0.008, 0.44, (x, -0.052, 0.66), name="banner")
        box("gold", 0.012, 0.01, 0.44, (x - 0.052, -0.054, 0.66), name="banner_edge")
        box("gold", 0.012, 0.01, 0.44, (x + 0.052, -0.054, 0.66), name="banner_edge")
        cyl("gold", 0.028, 0.028, 0.006, 0.74, xy=(x, -0.058), rot=(math.pi / 2, 0, 0), seg=16, name="banner_sun")
        # arms bracing the crown
        tube("gold", [(x, 0.02, 1.02), (x * 0.6, 0.0, 1.06), (x * 0.25, 0.0, 1.07)], 0.014, sides=6, name="brace")
    # crown, orbit rings, spires
    cyl("marble", 0.22, 0.24, 0.12, 1.0, seg=24, name="crown")
    cyl("gold", 0.25, 0.25, 0.02, 1.0, seg=24, name="crown_band")
    cyl("gold", 0.25, 0.22, 0.03, 1.12, seg=24, name="crown_cornice")
    torus("gold", 0.045, 0.008, (0, -0.235, 1.06), rot=(math.pi / 2, 0, 0), seg=20, minor=6, name="crown_medallion_ring")
    sphere("lightning", 0.035, (0, -0.24, 1.06), seg=14, rings=8, name="crown_medallion")
    torus("gold", 0.44, 0.008, (0, 0, 1.06), rot=(math.radians(8), 0, 0), seg=48, minor=6, name="orbit")
    for k in range(6):
        a = TAU * k / 6
        sphere("gold", 0.022, (0.44 * math.cos(a), 0.44 * math.sin(a) * math.cos(math.radians(8)), 1.06 + 0.44 * math.sin(a) * math.sin(math.radians(8))), seg=10, rings=6, name="orbit_bead")
    cyl("gold", 0.13, 0.0, 0.42, 1.15, seg=12, name="spire")
    for s in (-1, 1):
        cyl("gold", 0.05, 0.0, 0.25, 1.15, xy=(s * 0.15, 0.03), seg=8, name="side_spire")


def poseidon_tidal_pump_station():
    """DT art: pearl-and-gold pump hall around a glowing water column, two big
    cyan turbines on teal housings fed by arching pipes, aqueduct arcade with
    glowing channels, small teal domed kiosks, dark abyssal rock below."""
    cyl("rock", 0.80, 0.87, 0.10, 0.0, seg=6, rot=(0, 0, HEX30), name="rock")
    cyl("abyss", 0.82, 0.80, 0.04, 0.10, seg=6, rot=(0, 0, HEX30), name="seabed")
    # aqueduct deck on an arcade of pearl piers
    deck_z = 0.26
    box("pearl", 1.16, 0.62, 0.05, (0, 0.02, deck_z), name="deck")
    box("oldgold", 1.18, 0.64, 0.014, (0, 0.02, deck_z + 0.012), name="deck_trim")
    for i in range(7):
        x = -0.54 + i * 0.18
        for y in (-0.27, 0.31):
            box("pearl", 0.05, 0.05, deck_z - 0.14, (x, y, 0.14 + (deck_z - 0.14) / 2), name="pier")
        if i < 6:
            tube("pearl", arc((x + 0.09, -0.27, deck_z - 0.03), 0.065, math.pi, 0, steps=8), 0.012, sides=5, name="arch")
    # glowing channel running out to the front
    box("cyan", 0.14, 0.62, 0.012, (0, -0.33, 0.145), name="channel_water")
    box("pearl", 0.03, 0.62, 0.05, (-0.085, -0.33, 0.165), name="channel_wall")
    box("pearl", 0.03, 0.62, 0.05, (0.085, -0.33, 0.165), name="channel_wall")
    box("cyan", 0.12, 0.012, deck_z - 0.15, (0, -0.282, 0.15 + (deck_z - 0.15) / 2), name="spill")
    # central pump hall
    box("pearl", 0.40, 0.34, 0.24, (0, 0.06, deck_z + 0.025 + 0.12), bevel=0.01, name="hall")
    box("oldgold", 0.42, 0.36, 0.02, (0, 0.06, deck_z + 0.27), name="hall_cornice")
    for x in (-0.12, -0.04, 0.04, 0.12):
        box("seaglass", 0.05, 0.01, 0.14, (x, -0.115, deck_z + 0.13), name="hall_window")
        tube("oldgold", arc((x, -0.118, deck_z + 0.20), 0.025, 0, math.pi, steps=6), 0.004, sides=4, name="window_arch")
    # water column with buttresses, dome and spires
    zc0 = deck_z + 0.28
    cyl("cyan", 0.13, 0.13, 0.46, zc0, seg=20, name="water_column")
    for z in (zc0, zc0 + 0.22, zc0 + 0.44):
        torus("oldgold", 0.14, 0.012, (0, 0, z + 0.01), seg=28, minor=6, name="column_ring")
    for k in range(4):
        a = TAU * k / 4 + TAU / 8
        x, y = 0.17 * math.cos(a), 0.17 * math.sin(a)
        box("pearl", 0.05, 0.05, 0.50, (x, y, zc0 + 0.25), rot=(0, 0, a), bevel=0.006, name="buttress")
        cyl("pearl", 0.03, 0.0, 0.16, zc0 + 0.50, xy=(x, y), seg=8, name="buttress_spire")
    dome("teal", 0.17, zc0 + 0.46, seg=24, rings=6, height=0.9, name="dome")
    cyl("oldgold", 0.175, 0.175, 0.02, zc0 + 0.455, seg=24, name="dome_ring")
    cyl("oldgold", 0.02, 0.0, 0.18, zc0 + 0.60, seg=8, name="dome_finial")
    # back spires
    for s in (-1, 1):
        cyl("pearl", 0.05, 0.035, 0.55, deck_z + 0.03, xy=(s * 0.28, 0.24), seg=8, name="tower")
        cyl("oldgold", 0.055, 0.055, 0.015, deck_z + 0.55, xy=(s * 0.28, 0.24), seg=8, name="tower_band")
        cyl("pearl", 0.04, 0.0, 0.22, deck_z + 0.58, xy=(s * 0.28, 0.24), seg=8, name="tower_spire")
    # turbines on each side, axis along X, intakes facing outward
    tz = deck_z + 0.25
    for s in (-1, 1):
        cx = s * 0.47
        cyl("teal", 0.20, 0.20, 0.22, tz, xy=(s * 0.25, 0.04), rot=(0, s * math.pi / 2, 0), seg=28, name="housing")
        for dx in (0.25, 0.47):
            torus("oldgold", 0.205, 0.018, (s * dx, 0.04, tz), rot=(0, math.pi / 2, 0), seg=36, minor=8, name="housing_ring")
        cyl("cyan", 0.17, 0.17, 0.01, tz, xy=(s * 0.455, 0.04), rot=(0, s * math.pi / 2, 0), seg=28, name="intake_glow")
        for k in range(10):
            a = TAU * k / 10
            box("pearl", 0.012, 0.16, 0.035, (s * 0.465, 0.04 + 0.09 * math.cos(a), tz + 0.09 * math.sin(a)), rot=(a + math.radians(25), 0, 0), name="blade")
        sphere("oldgold", 0.045, (s * 0.475, 0.04, tz), seg=14, rings=8, scale=(0.7, 1, 1), name="hub")
        # feed pipe arching from the turbine up and back into the hall
        pts = [(s * 0.36, 0.20, tz + 0.12), (s * 0.40, 0.28, tz + 0.30), (s * 0.30, 0.30, tz + 0.42), (s * 0.15, 0.24, tz + 0.42), (s * 0.10, 0.18, tz + 0.36)]
        tube("teal", pts, 0.045, sides=12, name="pipe")
        for p in (pts[1], pts[3]):
            torus("oldgold", 0.05, 0.01, p, rot=(0, math.pi / 2, 0) if p is pts[3] else (math.pi / 2, 0, 0), seg=20, minor=5, name="pipe_collar")
        # waterfall from the intake down to the deck edge
        box("cyan", 0.008, 0.16, tz - 0.17, (s * 0.50, 0.04, 0.14 + (tz - 0.17) / 2 + 0.02), name="falls")
    # domed kiosks at the front corners of the deck
    for s in (-1, 1):
        x, y = s * 0.46, -0.20
        cyl("pearl", 0.06, 0.06, 0.12, deck_z + 0.03, xy=(x, y), seg=12, name="kiosk")
        box("seaglass", 0.03, 0.01, 0.07, (x, y - 0.06, deck_z + 0.10), name="kiosk_window")
        cyl("oldgold", 0.065, 0.065, 0.012, deck_z + 0.15, xy=(x, y), seg=12, name="kiosk_ring")
        dome("teal", 0.06, deck_z + 0.16, xy=(x, y), seg=14, rings=4, name="kiosk_dome")
        cyl("oldgold", 0.008, 0.0, 0.06, deck_z + 0.22, xy=(x, y), seg=6, name="kiosk_finial")


BUILDERS = {
    "zeus_storm_relay_pylon": ("Storm Relay Pylon", "Zeus", zeus_storm_relay_pylon),
    "zeus_keraunos_charging_spire": ("Keraunos Charging Spire", "Zeus", zeus_keraunos_charging_spire),
    "poseidon_tidal_pump_station": ("Tidal Pump Station", "Poseidon", poseidon_tidal_pump_station),
}
