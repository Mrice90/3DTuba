"""Preview renders for the coin/hex sets (Blender 5.2, headless bpy, Cycles CPU).

  python3 -I render.py <models_dir> <previews_dir> <set> <shot> [samples]

shots:
  hero    1600x900 still: heads and tails coins over a seven-tile cluster
  board   1600x900 still: a 37-hex board in the tile skin, game camera angle
  anim    640x360, 72 frames at 24 fps: the coin's Flip clip over the cluster with the glow pulse,
          the land flash and (celestial) the lightning flicker that CosmeticGlow.cs plays in game.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

models, out, sid, shot = sys.argv[1:5]
samples = int(sys.argv[5]) if len(sys.argv) > 5 else 64
SQ3 = math.sqrt(3)
SPACING = 2.2  # tile 2.0 across the flats plus a gap, as on the board


def glow_curve(t, land=None, flicker=False):
    """Same shape as CosmeticGlow.cs: slow breathing pulse, optional flicker, flash on landing."""
    g = 0.78 + 0.22 * math.sin(2 * math.pi * t / 2.4)
    if flicker:
        # deterministic stand-in for the script's random arcs: short spikes
        for c in (0.55, 1.35, 2.2):
            d = t - c
            if 0 <= d < 0.12:
                g += 0.9 * (1 - d / 0.12)
    if land is not None and t >= land:
        g += 1.8 * math.exp(-(t - land) / 0.18)
    return g


def scene_setup(res, cam_loc, target):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = samples
    sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.resolution_percentage = 100
    sc.view_settings.view_transform = "AgX"
    sc.view_settings.look = "AgX - Medium High Contrast"
    world = bpy.data.worlds.new("W")
    sc.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.012, 0.014, 0.022, 1)
    bg.inputs["Strength"].default_value = 1.0
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.location = cam_loc
    tgt = bpy.data.objects.new("Target", None)
    sc.collection.objects.link(tgt)
    tgt.location = target
    c = cam.constraints.new("TRACK_TO")
    c.target = tgt
    cam.data.lens = 50
    for loc, energy, size, color in (((4, -5, 7), 650, 4, (1.0, 0.96, 0.9)), ((-6, 3, 4), 220, 5, (0.7, 0.8, 1.0)),
                                     ((0, 7, 2.5), 450, 3, (1, 1, 1))):
        l = bpy.data.objects.new("L", bpy.data.lights.new("L", "AREA"))
        l.data.energy, l.data.size, l.data.color = energy, size, color
        l.location = loc
        l.constraints.new("TRACK_TO").target = tgt
        sc.collection.objects.link(l)
    # dark slate floor so the glow has something to fall on
    bpy.ops.mesh.primitive_plane_add(size=80, location=(0, 0, -0.16))
    fl = bpy.context.active_object
    fm = bpy.data.materials.new("Floor")
    b = fm.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (0.006, 0.007, 0.010, 1)
    b.inputs["Roughness"].default_value = 0.6
    fl.data.materials.append(fm)
    bloom(sc)
    return sc, cam


def bloom(sc):
    """Compositor glare for the glow; skipped quietly if this Blender's compositor API differs."""
    try:
        ng = bpy.data.node_groups.new("Comp", "CompositorNodeTree")
        sc.compositing_node_group = ng
        rl = ng.nodes.new("CompositorNodeRLayers")
        gl = ng.nodes.new("CompositorNodeGlare")
        out_ = ng.interface.new_socket("Image", in_out="OUTPUT", socket_type="NodeSocketColor")
        del out_
        go = ng.nodes.new("NodeGroupOutput")
        for name, val in (("Type", "Bloom"), ("Quality", "High")):
            if name in gl.inputs:
                try:
                    gl.inputs[name].default_value = val
                except Exception:
                    pass
        if "Strength" in gl.inputs:
            gl.inputs["Strength"].default_value = 0.6
        if "Threshold" in gl.inputs:
            gl.inputs["Threshold"].default_value = 1.0
        if "Size" in gl.inputs:
            gl.inputs["Size"].default_value = 0.6
        ng.links.new(rl.outputs["Image"], gl.inputs["Image"])
        ng.links.new(gl.outputs["Image"], go.inputs[0])
    except Exception as e:  # noqa: BLE001
        print("BLOOM skipped:", e)


def load_set():
    bpy.ops.wm.open_mainfile(filepath=os.path.join(models, f"{sid}_set.blend"))
    coin = bpy.data.objects[f"coin_{sid}"]
    tile = bpy.data.objects[f"hex_{sid}"]
    tile.location = (0, 0, 0)
    # muted NLA tracks: unmute nothing; we key what we need per shot
    return coin, tile


def hex_cells(radius):
    for q in range(-radius, radius + 1):
        for r in range(-radius, radius + 1):
            if abs(q + r) <= radius:
                # pointy-top axial layout
                yield (SPACING * (q + r / 2), SPACING * (SQ3 / 2) * r)


def place_tiles(tile, radius):
    tiles = []
    for i, (x, y) in enumerate(hex_cells(radius)):
        t = tile if i == 0 else tile.copy()
        if i:
            bpy.context.scene.collection.objects.link(t)
        t.location = (x, y, 0)
        tiles.append(t)
    return tiles


def emit_mats():
    return [m for m in bpy.data.materials if "emit_base" in m]


def set_glow(g, frame=None):
    for m in emit_mats():
        sock = m.node_tree.nodes["Principled BSDF"].inputs["Emission Strength"]
        sock.default_value = m["emit_base"] * g
        if frame is not None:
            sock.keyframe_insert("default_value", frame=frame)


coin, tile = load_set()
os.makedirs(out, exist_ok=True)

if shot == "hero":
    place_tiles(tile, 1)
    c2 = coin.copy()
    bpy.context.scene.collection.objects.link(c2)
    coin.scale = c2.scale = (1.9, 1.9, 1.9)
    coin.location, coin.rotation_euler = (-1.25, -0.4, 1.25), (math.radians(62), 0, math.radians(-14))
    c2.location, c2.rotation_euler = (1.25, -0.4, 1.25), (math.radians(180 + 62), 0, math.radians(14))
    scene_setup((1600, 900), (0, -7.6, 4.2), (0, 0, 0.75))
    set_glow(1.0)
    bpy.context.scene.render.filepath = os.path.join(out, f"{sid}_hero.png")
    bpy.ops.render.render(write_still=True)

elif shot == "board":
    place_tiles(tile, 3)
    coin.hide_render = True
    scene_setup((1600, 900), (0, -15.5, 13.5), (0, 0.6, 0))
    bpy.context.scene.camera.data.lens = 42
    set_glow(1.0)
    bpy.context.scene.render.filepath = os.path.join(out, f"{sid}_board.png")
    bpy.ops.render.render(write_still=True)

elif shot == "anim":
    place_tiles(tile, 1)
    sc, cam = scene_setup((640, 360), (0, -5.2, 2.9), (0, 0, 0.55))
    coin.scale = (1.8, 1.8, 1.8)
    ad = coin.animation_data
    flip = bpy.data.actions["Flip"]
    for tr in list(ad.nla_tracks):
        ad.nla_tracks.remove(tr)
    ad.action = flip
    # the clip is authored for a 1.0 coin resting at z 0.04; lift it to sit on the centre tile
    coin.delta_location = Vector((0, 0, 0.15 + 0.035))
    sc.frame_start, sc.frame_end = 0, 71
    sc.render.fps = 24
    land = 30 / 24
    for f in range(0, 72):
        set_glow(glow_curve(f / 24, land, flicker=(sid == "celestial")), f)
    sc.render.filepath = os.path.join(out, f"frames_{sid}", "f_")
    sc.render.image_settings.file_format = "PNG"
    bpy.ops.render.render(animation=True)
