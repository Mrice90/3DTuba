"""Builds the ornate coin and hex tile for each cosmetic set (Blender 5.2, headless bpy).

  python3 -I build.py <textures_dir> <models_dir>

Coin (coin_<set>): a drop-in for the AI-094 coin. 1.0 across, origin at the centre, heads +Z
(Unity +Y), the same three material slots in the same order (Coin_Rim, Coin_Heads, Coin_Tails)
and the same planar face UVs, so CoinFlip.cs drives it unchanged. On top of that the faces are
real relief (displaced from the set's height map) and the edge is reeded.
Two clips ride along as NLA tracks: "Flip" (toss, five turns, land heads, settle) and "Idle"
(slow hover and turn, loops). CoinFlip animates the root itself; the clips are for menus,
the collection screen and anyone who prefers an Animator.

Tile (hex_<set>): pointy-top hex 2.0 across the flats and 0.30 thick, centred on the origin
like BoardLayout.TileMesh (top face at +0.15). One mesh, one material (Tile_<set>), the top
displaced from the height map. Scale it by tileSize / 2.0 to match the board.
"""
import math
import os
import sys

import bpy  # noqa: I001
import bmesh
import numpy as np
from PIL import Image

SETS = ("celestial", "abyssal", "obsidian")
# per set: rim colour, rim metallic, rim roughness, emission strength (preview and glTF)
RIM = {
    "celestial": ((0.85, 0.60, 0.20), 1.0, 0.25, 4.0),
    "abyssal": ((0.62, 0.48, 0.22), 1.0, 0.30, 4.0),
    "obsidian": ((0.20, 0.18, 0.17), 0.9, 0.45, 5.0),
}
COIN_H, TILE_H = 0.012, 0.03
R, RF, ZF, ZL = 0.5, 0.45, 0.030, 0.040
SEG, NR = 160, 40
REEDS = 80
TILE_N = 26
SQ3 = math.sqrt(3)
HEX_RC = 2 / SQ3
THICK = 0.30


class Height:
    def __init__(self, path, scale):
        a = np.asarray(Image.open(path), dtype=np.float32) / 65535.0
        self.a, self.n, self.s = a, a.shape[0], scale

    def uv(self, u, v):
        x = min(max(u * self.n - 0.5, 0), self.n - 1.001)
        y = min(max((1 - v) * self.n - 0.5, 0), self.n - 1.001)
        i, j = int(x), int(y)
        fx, fy = x - i, y - j
        a = self.a
        top = a[j, i] * (1 - fx) + a[j, i + 1] * fx
        bot = a[j + 1, i] * (1 - fx) + a[j + 1, i + 1] * fx
        return (top * (1 - fy) + bot * fy) * self.s


def image(path, colorspace):
    img = bpy.data.images.load(path, check_existing=True)
    img.colorspace_settings.name = colorspace
    return img


def pbr_material(name, tex, prefix, emit_strength):
    """Principled BSDF fed by the set's maps, wired the way the glTF exporter understands."""
    m = bpy.data.materials.new(name)
    nt = m.node_tree
    b = nt.nodes.get("Principled BSDF")
    out = nt.nodes.get("Material Output")
    b.location, out.location = (300, 0), (600, 0)

    def tnode(suffix, cs, y):
        n = nt.nodes.new("ShaderNodeTexImage")
        n.image = image(os.path.join(tex, f"{prefix}_{suffix}.png"), cs)
        n.location = (-500, y)
        return n

    base = tnode("BaseColor", "sRGB", 300)
    nt.links.new(base.outputs["Color"], b.inputs["Base Color"])
    mr = tnode("MetallicRoughness", "Non-Color", 0)
    sep = nt.nodes.new("ShaderNodeSeparateColor")
    sep.location = (-150, 0)
    nt.links.new(mr.outputs["Color"], sep.inputs["Color"])
    nt.links.new(sep.outputs["Green"], b.inputs["Roughness"])
    nt.links.new(sep.outputs["Blue"], b.inputs["Metallic"])
    nm = tnode("Normal", "Non-Color", -300)
    nmap = nt.nodes.new("ShaderNodeNormalMap")
    nmap.location = (-150, -300)
    nt.links.new(nm.outputs["Color"], nmap.inputs["Color"])
    nt.links.new(nmap.outputs["Normal"], b.inputs["Normal"])
    em = tnode("Emission", "sRGB", -600)
    nt.links.new(em.outputs["Color"], b.inputs["Emission Color"])
    b.inputs["Emission Strength"].default_value = emit_strength
    m["emit_base"] = emit_strength  # render.py animates the pulse from this
    return m


def rim_material(sid):
    col, met, rough, _ = RIM[sid]
    m = bpy.data.materials.new("Coin_Rim")
    b = m.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (*col, 1)
    b.inputs["Metallic"].default_value = met
    b.inputs["Roughness"].default_value = rough
    return m


# ------------------------------------------------------------------ coin
def build_coin(sid, tex):
    hh = Height(os.path.join(tex, f"{sid}_heads_Height.png"), COIN_H)
    ht = Height(os.path.join(tex, f"{sid}_tails_Height.png"), COIN_H)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    # Rim: the AI-094 profile, with the outer edge reeded (grooves across the edge).
    profile = [(RF, ZF), (0.462, ZL), (0.488, ZL), (R, 0.028), (R, -0.028), (0.488, -ZL), (0.462, -ZL), (RF, -ZF)]
    rings = []
    for j, (r, z) in enumerate(profile):
        ring = []
        for i in range(SEG):
            a = 2 * math.pi * i / SEG
            rr = r
            if j in (3, 4):  # the edge band: reeding
                rr = r - 0.0035 * (0.5 + 0.5 * math.cos(a * REEDS))
            ring.append(bm.verts.new((rr * math.cos(a), rr * math.sin(a), z)))
        rings.append(ring)
    for j in range(len(profile) - 1):
        for i in range(SEG):
            k = (i + 1) % SEG
            f = bm.faces.new((rings[j][i], rings[j][k], rings[j + 1][k], rings[j + 1][i]))
            f.material_index = 0
            f.smooth = True
            v0, v1 = 1 - j / (len(profile) - 1), 1 - (j + 1) / (len(profile) - 1)
            for loop, (u, v) in zip(f.loops, ((i / SEG, v0), ((i + 1) / SEG, v0), ((i + 1) / SEG, v1), (i / SEG, v1))):
                loop[uv].uv = (u, v)

    # Faces: polar grids displaced from the height maps, sharing the rim's outer ring.
    def face(outer, sign, hm, mat):
        flip = sign < 0
        grid = []
        for ri in range(1, NR):
            rr = RF * ri / NR
            ring = []
            for i in range(SEG):
                a = 2 * math.pi * i / SEG
                x, y = rr * math.cos(a), rr * math.sin(a)
                u, v = 0.5 + x / (2 * RF), 0.5 + (-y if flip else y) / (2 * RF)
                ring.append(bm.verts.new((x, y, sign * (ZF + hm.uv(u, v)))))
            grid.append(ring)
        grid.append(outer)
        c = bm.verts.new((0, 0, sign * (ZF + hm.uv(0.5, 0.5))))

        def setuv(f):
            for loop in f.loops:
                x, y, _ = loop.vert.co
                loop[uv].uv = (0.5 + x / (2 * RF), 0.5 + (-y if flip else y) / (2 * RF))
            f.material_index = mat
            f.smooth = True

        for i in range(SEG):
            k = (i + 1) % SEG
            setuv(bm.faces.new((c, grid[0][i], grid[0][k]) if not flip else (c, grid[0][k], grid[0][i])))
        for g in range(len(grid) - 1):
            a, b = grid[g], grid[g + 1]
            for i in range(SEG):
                k = (i + 1) % SEG
                q = (a[i], b[i], b[k], a[k]) if not flip else (a[i], a[k], b[k], b[i])
                setuv(bm.faces.new(q))

    face(rings[0], 1, hh, 1)
    face(rings[-1], -1, ht, 2)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    me = bpy.data.meshes.new(f"coin_{sid}")
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(f"coin_{sid}", me)
    bpy.context.scene.collection.objects.link(obj)
    me.set_sharp_from_angle(angle=math.radians(40))
    _, _, _, es = RIM[sid]
    me.materials.append(rim_material(sid))
    me.materials.append(pbr_material("Coin_Heads", tex, f"{sid}_heads", es))
    me.materials.append(pbr_material("Coin_Tails", tex, f"{sid}_tails", es))
    add_coin_clips(obj)
    return obj


def key(obj, frame, loc, rot):
    obj.location, obj.rotation_euler = loc, rot
    obj.keyframe_insert("location", frame=frame)
    obj.keyframe_insert("rotation_euler", frame=frame)


def add_coin_clips(obj):
    """Flip: 24 fps, 0-30 toss (5 turns about X), 30-46 settle and wobble, 46-60 hold. Idle: 96-frame loop."""
    ad = obj.animation_data_create()
    clips = []
    act = bpy.data.actions.new("Flip")
    ad.action = act
    peak, turns = 0.9, 5
    for f in range(0, 31):
        t = f / 30
        z = 4 * peak * t * (1 - t) + 0.04
        key(obj, f, (0, 0, z), (2 * math.pi * turns * (1 - (1 - t) ** 1.6), 0, math.radians(25) * t))
    for f, wob, z in ((34, 9, 0.046), (38, -5, 0.042), (42, 2.5, 0.04), (46, 0, 0.04), (60, 0, 0.04)):
        key(obj, f, (0, 0, z), (math.radians(wob), math.radians(wob * 0.6), math.radians(25)))
    clips.append((act, 0))
    act = bpy.data.actions.new("Idle")
    ad.action = act
    for f in range(0, 97, 8):
        t = f / 96
        key(obj, f, (0, 0, 0.04 + 0.03 * math.sin(2 * math.pi * t)),
            (math.radians(12) * math.sin(2 * math.pi * t), 0, 2 * math.pi * t))
    clips.append((act, 0))
    ad.action = None
    for act, start in clips:
        tr = ad.nla_tracks.new()
        tr.name = act.name
        tr.strips.new(act.name, start, act)
        tr.mute = True  # exporters read the tracks; muted so the rest pose stays at the origin
    obj.location, obj.rotation_euler = (0, 0, 0), (0, 0, 0)


# ------------------------------------------------------------------ tile
def build_tile(sid, tex):
    hm = Height(os.path.join(tex, f"{sid}_tile_Height.png"), TILE_H)
    n = TILE_N
    top = THICK / 2
    bev = 0.035
    rc_in = HEX_RC - bev * 1.2
    e1 = (math.cos(math.radians(30)), math.sin(math.radians(30)))
    e2 = (0.0, 1.0)
    s = rc_in / n
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")

    def uv_top(x, y):
        return (x / (2 * HEX_RC) + 0.5, y / (2 * HEX_RC) + 0.5)

    verts = {}
    for q in range(-n, n + 1):
        for r in range(-n, n + 1):
            if abs(q + r) > n:
                continue
            x = s * (q * e1[0] + r * e2[0])
            y = s * (q * e1[1] + r * e2[1])
            u, v = uv_top(x, y)
            edge = max(abs(q), abs(r), abs(q + r)) == n
            z = top + (hm.uv(u, v) if not edge else hm.uv(u, v) * 0.5)
            verts[(q, r)] = bm.verts.new((x, y, z))
    faces = []
    for (q, r), v0 in verts.items():
        for tri in (((q + 1, r), (q, r + 1)), ((q, r + 1), (q - 1, r + 1))):
            a, b = verts.get(tri[0]), verts.get(tri[1])
            if a and b:
                faces.append(bm.faces.new((v0, a, b)))
    for f in faces:
        for loop in f.loops:
            loop[uv].uv = uv_top(loop.vert.co.x, loop.vert.co.y)
        f.smooth = True

    # boundary of the lattice, walked corner to corner (counter-clockwise from the +30 degree corner)
    corners = [(n, 0), (0, n), (-n, n), (-n, 0), (0, -n), (n, -n)]
    boundary = []
    for i in range(6):
        a, b = corners[i], corners[(i + 1) % 6]
        for k in range(n):
            boundary.append(verts[(a[0] + (b[0] - a[0]) * k // n, a[1] + (b[1] - a[1]) * k // n)])
    m = len(boundary)
    # chamfer ring at the full radius, then walls down to the bottom ring
    cham, bot = [], []
    for v in boundary:
        x, y = v.co.x, v.co.y
        k = HEX_RC / rc_in
        cham.append(bm.verts.new((x * k, y * k, top - bev)))
        bot.append(bm.verts.new((x * k, y * k, -top)))
    for i in range(m):
        j = (i + 1) % m
        f = bm.faces.new((boundary[i], cham[i], cham[j], boundary[j]))
        for loop in f.loops:
            loop[uv].uv = uv_top(loop.vert.co.x, loop.vert.co.y)
    # walls: every edge maps to the same strip at x < -1 (u across the thickness, v along the edge)
    per = n
    for i in range(m):
        j = (i + 1) % m
        f = bm.faces.new((cham[i], bot[i], bot[j], cham[j]))
        k0, k1 = (i % per) / per, ((i % per) + 1) / per
        vv0, vv1 = 0.5 + (k0 - 0.5) * 0.5, 0.5 + (k1 - 0.5) * 0.5   # y in [-Rc/2, Rc/2]
        umin, umax = (-HEX_RC + 0.01) / (2 * HEX_RC) + 0.5, (-1.0 - 0.01) / (2 * HEX_RC) + 0.5
        top_u, bot_u = umax, umin
        for loop, (uu, vv) in zip(f.loops, ((top_u, vv0), (bot_u, vv0), (bot_u, vv1), (top_u, vv1))):
            loop[uv].uv = (uu, vv)
    # underside: six-corner fan mapped to the plain top-left patch
    bc = bm.verts.new((0, 0, -top))
    for i in range(m):
        j = (i + 1) % m
        f = bm.faces.new((bc, bot[j], bot[i]))
        for loop in f.loops:
            loop[uv].uv = (0.04, 0.96)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    me = bpy.data.meshes.new(f"hex_{sid}")
    bm.to_mesh(me)
    bm.free()
    me.set_sharp_from_angle(angle=math.radians(35))
    obj = bpy.data.objects.new(f"hex_{sid}", me)
    bpy.context.scene.collection.objects.link(obj)
    me.materials.append(pbr_material(f"Tile_{sid}", tex, f"{sid}_tile", RIM[sid][3] * 0.6))
    return obj


# ------------------------------------------------------------------ export
def export(obj, out, name, anim):
    for o in bpy.context.scene.objects:
        o.select_set(o == obj)
    bpy.context.view_layer.objects.active = obj
    glb = os.path.join(out, name + ".glb")
    bpy.ops.export_scene.gltf(filepath=glb, export_format="GLB", use_selection=True,
                              export_animations=anim, export_animation_mode="NLA_TRACKS" if anim else "ACTIONS")
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, name + ".fbx"), use_selection=True,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=True, mesh_smooth_type="FACE", path_mode="STRIP",
                             add_leaf_bones=False, bake_anim=anim, bake_anim_use_nla_strips=False,
                             bake_anim_use_all_actions=anim, bake_anim_force_startend_keying=True)
    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    dims = tuple(round(d, 3) for d in obj.dimensions)
    return {"name": name, "tris": tris, "verts": len(obj.data.vertices), "dims_xyz": dims,
            "materials": [m.name for m in obj.data.materials]}


def main(tex, out):
    import json
    os.makedirs(out, exist_ok=True)
    stats = []
    for sid in SETS:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.context.scene.render.fps = 24
        coin = build_coin(sid, tex)
        tile = build_tile(sid, tex)
        tile.location.x = 2.0
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, f"{sid}_set.blend"), relative_remap=True)
        tile.location.x = 0
        stats.append(export(coin, out, f"coin_{sid}", True))
        stats.append(export(tile, out, f"hex_{sid}", False))
        print("BUILT", stats[-2], stats[-1])
    with open(os.path.join(out, "stats.json"), "w") as f:
        json.dump(stats, f, indent=1)


if __name__ == "__main__":
    main(os.path.abspath(sys.argv[1]), os.path.abspath(sys.argv[2]))
