"""Render preview views of each pilot on a hex tile. Usage: python3 -I render.py OUT_DIR"""
import math, os, sys, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Matrix, Vector

out = os.path.abspath(sys.argv[1])
ids = json.load(open(os.path.join(out, "stats.json")))
views = {"front34": (-35, 22, 4.4), "front": (0, 8, 4.4), "side": (90, 8, 4.4), "top": (0, 89, 4.4)}
# RENDER_VIEWS=front34,side and RENDER_SAMPLES=16 give quick iteration renders
if os.environ.get("RENDER_VIEWS"):
    views = {k: v for k, v in views.items() if k in os.environ["RENDER_VIEWS"].split(",")}
if os.environ.get("RENDER_ONLY"):
    ids = {k: v for k, v in ids.items() if k in os.environ["RENDER_ONLY"].split(",")}

def look(cam, target):
    d = Vector(target) - cam.location
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()

for cid, st in ids.items():
    bpy.ops.wm.open_mainfile(filepath=os.path.join(out, f"{cid}.blend"))
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"; sc.cycles.samples = int(os.environ.get("RENDER_SAMPLES", 48)); sc.cycles.use_denoising = True
    sc.render.resolution_x = sc.render.resolution_y = 640
    sc.view_settings.view_transform = "AgX"
    # hex tile: 2.0 across the flats, 0.1 thick, top at z=0
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=2 / math.sqrt(3), radius2=2 / math.sqrt(3), depth=0.1, matrix=Matrix.Translation((0, 0, -0.05)))
    me = bpy.data.meshes.new("tile"); bm.to_mesh(me)
    tile = bpy.data.objects.new("tile", me); sc.collection.objects.link(tile)
    tm = bpy.data.materials.new("tile"); tm.diffuse_color = (0.12, 0.14, 0.17, 1)
    tm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.10, 0.12, 0.15, 1)
    me.materials.append(tm)
    w = bpy.data.worlds.new("w"); sc.world = w
    bg = w.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.20, 0.24, 0.32, 1) if st["faction"] == "Zeus" else (0.03, 0.10, 0.16, 1)
    bg.inputs["Strength"].default_value = 0.8
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sc.collection.objects.link(sun)
    sun.data.energy = 3.5; sun.rotation_euler = (math.radians(50), 0, math.radians(-30))
    fill = bpy.data.objects.new("fill", bpy.data.lights.new("fill", "AREA")); sc.collection.objects.link(fill)
    fill.data.energy = 120; fill.data.size = 3; fill.location = (2.5, -2.5, 2.0); look(fill, (0, 0, 0.6))
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.lens = 50
    for vname, (az, el, dist) in views.items():
        a, e = math.radians(az), math.radians(el)
        tgt = (0, 0, 0.7 if vname != "top" else 0)
        cam.location = (dist * math.sin(a) * math.cos(e), -dist * math.cos(a) * math.cos(e), tgt[2] + dist * math.sin(e))
        look(cam, tgt)
        if vname == "top":
            cam.rotation_euler = (0, 0, 0)
            cam.location = (0, 0, dist)
        sc.render.filepath = os.path.join(out, "renders", f"{cid}_{vname}.png")
        bpy.ops.render.render(write_still=True)
    print("rendered", cid)
