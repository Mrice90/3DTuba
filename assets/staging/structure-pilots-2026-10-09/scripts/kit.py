"""Kitbash helpers for AI-081 structure pilots (Blender 5.2, run headless via bpy).

Units are AI-063 contract units: a hex is 2.0 across the flats, a structure fits
inside 0.9 hex (1.8 diameter) and stays at or under 1.6 tall. Blender is Z-up;
the front of every structure faces -Y, which the glTF/FBX exports turn into +Z.
Every part gets one palette swatch; the whole model shares one material whose
textures are a small palette atlas, so Unity's single "Token" material slot works.
"""
import math
import bmesh
import bpy
from mathutils import Matrix, Vector

# name: (base RGB, metallic, roughness, emission RGB or None)
SWATCHES = {
    # Zeus: white dominant, gold strong second, royal blue third, blue-white lightning
    "marble":      ((0.90, 0.89, 0.86), 0.0, 0.45, None),
    "gold":        ((0.85, 0.62, 0.22), 1.0, 0.28, None),
    "royal":       ((0.07, 0.14, 0.48), 0.0, 0.55, None),
    "lightning":   ((0.45, 0.65, 1.00), 0.0, 0.20, (0.20, 0.45, 1.00)),
    "stone":       ((0.30, 0.29, 0.28), 0.0, 0.85, None),
    "glass":       ((0.30, 0.52, 0.95), 0.0, 0.08, (0.12, 0.30, 0.95)),
    # Poseidon: abyssal navy/teal dominant, cyan glow, pearl and restrained gold
    "abyss":       ((0.03, 0.09, 0.15), 0.0, 0.75, None),
    "teal":        ((0.05, 0.33, 0.40), 0.6, 0.35, None),
    "cyan":        ((0.10, 0.75, 0.90), 0.0, 0.20, (0.00, 0.45, 0.70)),
    "pearl":       ((0.84, 0.88, 0.87), 0.1, 0.30, None),
    "oldgold":     ((0.66, 0.52, 0.24), 1.0, 0.35, None),
    "deepteal":    ((0.02, 0.18, 0.24), 0.3, 0.50, None),
    "rock":        ((0.10, 0.13, 0.16), 0.0, 0.90, None),
    "seaglass":    ((0.10, 0.55, 0.62), 0.1, 0.10, (0.04, 0.38, 0.45)),
    "white":       ((0.95, 0.95, 0.95), 0.0, 0.50, None),
    "dark":        ((0.05, 0.05, 0.06), 0.0, 0.90, None),
}
ORDER = list(SWATCHES)
GRID = 4
CELL = 32


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def _uv_of(sw):
    i = ORDER.index(sw)
    cx, cy = i % GRID, i // GRID
    return ((cx + 0.5) / GRID, 1.0 - (cy + 0.5) / GRID)


PARTS = []


def _finish(obj, sw, smooth=True):
    me = obj.data
    if not me.uv_layers:
        me.uv_layers.new(name="UVMap")
    u, v = _uv_of(sw)
    for d in me.uv_layers.active.data:
        d.uv = (u, v)
    for p in me.polygons:
        p.use_smooth = smooth
    obj["swatch"] = sw
    PARTS.append(obj)
    return obj


def _place(obj, loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
    obj.location = loc
    obj.rotation_euler = rot
    obj.scale = scale
    return obj


def _mesh_obj(name, bm):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def cyl(sw, r1, r2, h, z0=0.0, xy=(0, 0), seg=24, rot=(0, 0, 0), smooth=True, name="cyl", cap=True):
    """Truncated cone with its base at z0 (before rotation about its own base)."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=seg,
                          radius1=r1, radius2=r2, depth=h,
                          matrix=Matrix.Translation((0, 0, h / 2)))
    ob = _mesh_obj(name, bm)
    _place(ob, (xy[0], xy[1], z0), rot)
    # flat-shade low-seg prisms so hexes and octagons keep crisp faces
    return _finish(ob, sw, smooth and seg >= 12)


def box(sw, sx, sy, sz, loc=(0, 0, 0), rot=(0, 0, 0), bevel=0.0, name="box"):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=(sx, sy, sz), verts=bm.verts)
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=bm.edges[:] + bm.verts[:], offset=bevel, segments=1, affect='EDGES')
    ob = _mesh_obj(name, bm)
    _place(ob, loc, rot)
    return _finish(ob, sw, smooth=False)


def sphere(sw, r, loc=(0, 0, 0), seg=20, rings=12, scale=(1, 1, 1), name="sphere"):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=r)
    ob = _mesh_obj(name, bm)
    _place(ob, loc, (0, 0, 0), scale)
    return _finish(ob, sw)


def dome(sw, r, z0, xy=(0, 0), seg=20, rings=6, height=1.0, name="dome"):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings * 2, radius=r)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -1e-5], context='VERTS')
    bmesh.ops.scale(bm, vec=(1, 1, height), verts=bm.verts)
    ob = _mesh_obj(name, bm)
    _place(ob, (xy[0], xy[1], z0))
    return _finish(ob, sw)


def torus(sw, R, r, loc=(0, 0, 0), rot=(0, 0, 0), seg=40, minor=8, name="torus"):
    bm = bmesh.new()
    verts = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        ring = []
        for j in range(minor):
            b = 2 * math.pi * j / minor
            x = (R + r * math.cos(b)) * math.cos(a)
            y = (R + r * math.cos(b)) * math.sin(a)
            z = r * math.sin(b)
            ring.append(bm.verts.new((x, y, z)))
        verts.append(ring)
    for i in range(seg):
        for j in range(minor):
            a, b = verts[i][j], verts[i][(j + 1) % minor]
            c, d = verts[(i + 1) % seg][(j + 1) % minor], verts[(i + 1) % seg][j]
            bm.faces.new((a, d, c, b))
    ob = _mesh_obj(name, bm)
    _place(ob, loc, rot)
    return _finish(ob, sw)


def tube(sw, pts, r0, r1=None, sides=10, name="tube", caps=True):
    """Sweep a circle along a polyline, radius tapering r0 -> r1."""
    r1 = r0 if r1 is None else r1
    pts = [Vector(p) for p in pts]
    n = len(pts)
    bm = bmesh.new()
    rings = []
    prev_side = None
    for i, p in enumerate(pts):
        t = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        ref = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
        side = t.cross(ref).normalized()
        if prev_side is not None and side.dot(prev_side) < 0:
            side = -side
        prev_side = side
        up = side.cross(t).normalized()
        rad = r0 + (r1 - r0) * (i / (n - 1))
        ring = [bm.verts.new(p + rad * (math.cos(2 * math.pi * k / sides) * side + math.sin(2 * math.pi * k / sides) * up)) for k in range(sides)]
        rings.append(ring)
    for i in range(n - 1):
        for k in range(sides):
            a, b = rings[i][k], rings[i][(k + 1) % sides]
            c, d = rings[i + 1][(k + 1) % sides], rings[i + 1][k]
            bm.faces.new((a, b, c, d))
    if caps:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    ob = _mesh_obj(name, bm)
    return _finish(ob, sw)


def helix(cx, cy, z0, z1, radius, turns, phase=0.0, steps_per_turn=24):
    n = max(2, int(turns * steps_per_turn))
    return [(cx + radius * math.cos(phase + 2 * math.pi * turns * i / n),
             cy + radius * math.sin(phase + 2 * math.pi * turns * i / n),
             z0 + (z1 - z0) * i / n) for i in range(n + 1)]


def arc(center, radius, a0, a1, plane="xz", steps=16):
    cx, cy, cz = center
    out = []
    for i in range(steps + 1):
        a = a0 + (a1 - a0) * i / steps
        c, s = radius * math.cos(a), radius * math.sin(a)
        out.append((cx + c, cy, cz + s) if plane == "xz" else (cx, cy + c, cz + s) if plane == "yz" else (cx + c, cy + s, cz))
    return out


def mirror_x(fn):
    """Call fn(sign) for sign in (-1, 1)."""
    for s in (-1, 1):
        fn(s)


def palette_material(name="Token"):
    """One material for the whole token: base, ORM and emission atlases."""
    size = GRID * CELL
    imgs = {}
    for kind in ("BaseColor", "ORM", "Emission"):
        im = bpy.data.images.new(f"{name}_{kind}", size, size, alpha=False)
        im.colorspace_settings.name = "sRGB" if kind != "ORM" else "Non-Color"
        imgs[kind] = im
    px = {k: [0.0] * (size * size * 4) for k in imgs}
    for i, sw in enumerate(ORDER):
        base, metal, rough, emit = SWATCHES[sw]
        cx, cy = i % GRID, i // GRID
        y0 = (GRID - 1 - cy) * CELL
        for y in range(y0, y0 + CELL):
            for x in range(cx * CELL, cx * CELL + CELL):
                o = (y * size + x) * 4
                px["BaseColor"][o:o + 4] = [*base, 1.0]
                px["ORM"][o:o + 4] = [1.0, rough, metal, 1.0]
                px["Emission"][o:o + 4] = [*(emit or (0, 0, 0)), 1.0]
    for k, im in imgs.items():
        im.pixels.foreach_set(px[k])
        im.pack()

    mat = bpy.data.materials.new(name)
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    def tex(kind, x, y):
        n = nt.nodes.new("ShaderNodeTexImage")
        n.image = imgs[kind]
        n.interpolation = "Closest"
        n.location = (x, y)
        return n
    tb, to, te = tex("BaseColor", -700, 300), tex("ORM", -700, 0), tex("Emission", -700, -300)
    sep = nt.nodes.new("ShaderNodeSeparateColor")
    sep.location = (-400, 0)
    nt.links.new(tb.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(to.outputs["Color"], sep.inputs["Color"])
    nt.links.new(sep.outputs["Green"], bsdf.inputs["Roughness"])
    nt.links.new(sep.outputs["Blue"], bsdf.inputs["Metallic"])
    nt.links.new(te.outputs["Color"], bsdf.inputs["Emission Color"])
    bsdf.inputs["Emission Strength"].default_value = 2.0
    return mat, imgs


def assemble(name):
    """Join every part into one mesh with the palette material, origin at base centre."""
    global PARTS
    mat, imgs = palette_material("Token")
    for ob in PARTS:
        ob.data.materials.clear()
        ob.data.materials.append(mat)
    bpy.ops.object.select_all(action="DESELECT")
    for ob in PARTS:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = PARTS[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = name
    ob.data.name = name
    # merge coincident verts per part would weld swatches; keep parts separate
    bpy.ops.object.shade_auto_smooth(angle=math.radians(35))
    mins = Vector([min(v.co[i] for v in ob.data.vertices) for i in range(3)])
    maxs = Vector([max(v.co[i] for v in ob.data.vertices) for i in range(3)])
    off = Vector(((mins.x + maxs.x) / 2, (mins.y + maxs.y) / 2, mins.z))
    for v in ob.data.vertices:
        v.co -= off
    PARTS = []
    return ob, mat


def stats(ob):
    me = ob.data
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    xs = [v.co.x for v in me.vertices]; ys = [v.co.y for v in me.vertices]; zs = [v.co.z for v in me.vertices]
    radial = max(math.hypot(v.co.x, v.co.y) for v in me.vertices)
    return {
        "tris": tris, "verts": len(me.vertices),
        "size_x": round(max(xs) - min(xs), 4), "size_y": round(max(ys) - min(ys), 4),
        "height": round(max(zs) - min(zs), 4), "max_radius": round(radial, 4),
        "min_z": round(min(zs), 4),
    }
