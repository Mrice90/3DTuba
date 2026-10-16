# Render front / side / top orthographic views of a mesh with a labelled grid in units of model height H,
# so joints can be placed by eye. Coordinates: mesh grounded at z=0, bbox centred on x/y. Front looks from -Y.
import bpy, sys, math, os, json
import numpy as np
from mathutils import Matrix
from PIL import Image, ImageDraw
a = sys.argv[sys.argv.index('--') + 1:]
src, out = a[0], a[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
bpy.context.view_layer.update()
for m in meshes:
    mw = m.matrix_world.copy(); m.parent = None; m.data.transform(mw); m.matrix_world = Matrix.Identity(4)
V = np.concatenate([np.array([tuple(v.co) for v in m.data.vertices]) for m in meshes])
lo, hi = V.min(0), V.max(0); H = hi[2] - lo[2]
off = np.array([-(lo[0] + hi[0]) / 2, -(lo[1] + hi[1]) / 2, -lo[2]])
for m in meshes: m.data.transform(Matrix.Translation(tuple(off)))
V = V + off
sc = bpy.context.scene; sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.05, 0.06, 0.09)
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; S = 1.25 * max(H, np.ptp(V[:, 0]), np.ptp(V[:, 1])); cam.data.ortho_scale = S
R = 768; sc.render.resolution_x = R; sc.render.resolution_y = R
views = {"front": ((math.radians(90), 0, 0), (0, -20, H / 2), (0, 2)),
         "side": ((math.radians(90), 0, math.radians(90)), (20, 0, H / 2), (1, 2)),
         "top": ((0, 0, 0), (0, 0, 20), (0, 1))}
tiles = []
for nm, (rot, loc, ax) in views.items():
    cam.rotation_euler = rot; cam.location = loc
    p = f"{out}_{nm}.png"; sc.render.filepath = p; bpy.ops.render.render(write_still=True)
    im = Image.open(p).convert('RGB'); im.save(f"{out}_{nm}_clean.png"); d = ImageDraw.Draw(im)
    cx = loc[ax[0]] if nm != "side" else 0; cy = loc[2] if nm != "top" else 0
    def px(u, v):  # world (u horizontal axis value, v vertical) -> pixel
        hs = 1 if nm != "side" else 1
        return (R / 2 + (u - (0 if nm != "top" else 0)) / S * R, R / 2 - (v - (H / 2 if nm != "top" else 0)) / S * R)
    step = 0.1
    for k in range(-12, 13):
        u = k * step * H
        x, _ = px(u, 0); col = (90, 90, 140) if k else (200, 80, 80)
        d.line([(x, 0), (x, R)], fill=col, width=1); d.text((x + 2, 2), f"{k*step:+.1f}", fill=(255, 220, 120))
        y = px(0, u)[1]; col = (90, 90, 140) if k else (200, 80, 80)
        d.line([(0, y), (R, y)], fill=col, width=1); d.text((2, y - 11), f"{k*step:+.1f}", fill=(120, 220, 255))
    d.text((R - 150, R - 14), f"{nm} ({'xz' if nm=='front' else 'yz' if nm=='side' else 'xy'})", fill=(255, 255, 255))
    if nm == "side": d.text((R - 150, R - 28), "left=-Y(front) right=+Y", fill=(255, 255, 255))
    if nm == "front": d.text((R - 150, R - 28), "left=-X right=+X", fill=(255, 255, 255))
    im.save(p); tiles.append(im)
sheet = Image.new('RGB', (R * 3, R)); [sheet.paste(t, (i * R, 0)) for i, t in enumerate(tiles)]
sheet.save(f"{out}_probe.png")
json.dump({"H": float(H), "off": off.tolist(), "dims": (V.max(0) - V.min(0)).tolist(), "verts": len(V)}, open(f"{out}_probe.json", "w"))
print("H", H, "dims", (V.max(0) - V.min(0)) / H)
