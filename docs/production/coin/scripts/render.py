"""Renders the coin review sheet: both skins, heads up, tails up and on edge.

  python3 -I render.py <out_dir>/coin.blend <skins_dir> <out_png>
"""
import math
import os
import sys

import bpy
from mathutils import Euler
from PIL import Image

blend, skins, out_png = sys.argv[1], sys.argv[2], sys.argv[3]
tmp = os.path.dirname(out_png)


def setup():
    bpy.ops.wm.open_mainfile(filepath=blend)
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 48
    sc.render.resolution_x = sc.render.resolution_y = 420
    sc.render.film_transparent = False
    world = bpy.data.worlds.new("W")
    world.color = (0.05, 0.06, 0.08)
    sc.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.06, 0.07, 0.10, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.6
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.location = (0, -1.25, 1.25)
    cam.rotation_euler = Euler((math.radians(45), 0, 0))
    cam.data.lens = 50
    for loc, energy in (((1.5, -1.5, 2.5), 250), ((-2, 1, 1.5), 90)):
        l = bpy.data.objects.new("L", bpy.data.lights.new("L", "AREA"))
        l.data.energy = energy
        l.data.size = 1.5
        l.location = loc
        l.rotation_euler = (Euler((0, 0, 0)))
        l.constraints.new("TRACK_TO").target = bpy.data.objects["Coin"]
        sc.collection.objects.link(l)


def skin(name):
    for side in ("Heads", "Tails"):
        nt = bpy.data.materials["Coin_" + side].node_tree
        tex = next(n for n in nt.nodes if n.type == "TEX_IMAGE")
        tex.image = bpy.data.images.load(os.path.join(skins, f"{name}_{side.lower()}.png"))


setup()
coin = bpy.data.objects["Coin"]
shots = []
for name in ("olympus", "bronze"):
    skin(name)
    for label, rot in (("heads", (0, 0, 0)), ("tails", (math.pi, 0, 0)), ("edge", (math.radians(70), math.radians(20), math.radians(30)))):
        coin.rotation_euler = rot
        path = os.path.join(tmp, f"coin_{name}_{label}.png")
        bpy.context.scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        shots.append(path)

ims = [Image.open(p) for p in shots]
w, h = ims[0].size
sheet = Image.new("RGB", (w * 3, h * 2))
for i, im in enumerate(ims):
    sheet.paste(im, ((i % 3) * w, (i // 3) * h))
sheet.save(out_png, quality=90)
print("sheet", out_png)
