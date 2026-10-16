# Re-import a rigged FBX, measure every take, and render a review GIF (all clips side by side, animated)
# plus a still contact sheet. Usage: python3 -I sheet2.py <rigged.fbx> <outdir> <card_id> [facing_deg]
import bpy, sys, math, os, json
import numpy as np
from PIL import Image, ImageDraw
fbx, out, name = sys.argv[1], sys.argv[2], sys.argv[3]; yaw = float(sys.argv[4]) if len(sys.argv) > 4 else 0.0
tmp = os.path.join(out, "_frames_" + name); os.makedirs(tmp, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]; mesh = [o for o in bpy.data.objects if o.type == 'MESH'][0]
# Blender's FBX importer connects a single child to its parent's tail (Root -> Hips), which silently drops the
# Hips translation keys. Unity reads transforms directly and is not affected; disconnect so this check matches.
bpy.context.view_layer.objects.active = arm; bpy.ops.object.mode_set(mode='EDIT')
for eb in arm.data.edit_bones: eb.use_connect = False
bpy.ops.object.mode_set(mode='OBJECT')
sc = bpy.context.scene; sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.05, 0.06, 0.09)
sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
sc.display.shading.show_shadows = True
R = 260; sc.render.resolution_x = R; sc.render.resolution_y = R
def evz():
    dg = bpy.context.evaluated_depsgraph_get(); m = mesh.evaluated_get(dg); me = m.to_mesh()
    co = np.empty(len(me.vertices) * 3); me.vertices.foreach_get("co", co); m.to_mesh_clear()
    co = co.reshape(-1, 3) @ np.array(mesh.matrix_world.to_3x3()).T + np.array(mesh.matrix_world.translation); return co
order = ["Idle", "Walk", "Attack", "Hit", "Death", "Summon"]
acts = {a.name.split('|')[-1]: a for a in bpy.data.actions}
names = [n for n in order if n in acts] + [n for n in acts if n not in order]
# frame the camera on the union of all poses
pts = []; info = {"bones": len(arm.data.bones), "tris": sum(len(p.vertices) - 2 for p in mesh.data.polygons), "takes": {}, "minZ": {}}
for n in names:
    a = acts[n]; arm.animation_data.action = a
    if a.slots: arm.animation_data.action_slot = a.slots[0]
    f0, f1 = int(a.frame_range[0]), int(a.frame_range[1]); info["takes"][n] = [f0, f1]; zs = []
    for f in range(f0, f1 + 1):
        sc.frame_set(f); co = evz(); zs.append(co[:, 2].min())
        if f in (f0, (f0 + f1) // 2, f1): pts.append(co[::7])
    info["minZ"][n] = [round(float(min(zs)), 4), round(float(max(zs)), 4)]
pts = np.concatenate(pts); lo, hi = pts.min(0), pts.max(0); ctr = (lo + hi) / 2; ext = float(max(hi - lo)) * 1.05
bpy.ops.mesh.primitive_circle_add(vertices=48, radius=ext * 0.45, fill_type='NGON', location=(ctr[0], ctr[1], 0))
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = ext
az = math.radians(yaw + 30)  # three-quarter front view relative to the model facing
d = np.array([math.sin(az), -math.cos(az), 0.45]); d = d / np.linalg.norm(d)
cam.location = tuple(np.array([ctr[0], ctr[1], (lo[2] + hi[2]) / 2]) + d * 30)
from mathutils import Vector
cam.rotation_euler = (Vector((ctr[0], ctr[1], (lo[2] + hi[2]) / 2)) - Vector(cam.location)).to_track_quat('-Z', 'Y').to_euler()
seqs = {}
for n in names:
    a = acts[n]; arm.animation_data.action = a
    if a.slots: arm.animation_data.action_slot = a.slots[0]
    f0, f1 = info["takes"][n]; fr = []
    for f in range(f0, f1 + 1, 2):
        sc.frame_set(f); p = f"{tmp}/{n}_{f:03d}.png"; sc.render.filepath = p; bpy.ops.render.render(write_still=True)
        fr.append(Image.open(p).convert('RGB'))
    seqs[n] = fr
L = max(len(v) for v in seqs.values()); frames = []
for i in range(L):
    im = Image.new('RGB', (R * len(names), R + 18), (10, 12, 18)); d_ = ImageDraw.Draw(im)
    for k, n in enumerate(names):
        s = seqs[n]; im.paste(s[min(i, len(s) - 1)] if n in ("Death",) else s[i % len(s)], (k * R, 18))
        d_.text((k * R + 6, 3), n, fill=(255, 220, 120))
    d_.text((R * len(names) - 230, 3), name, fill=(160, 200, 255)); frames.append(im.convert('P', palette=Image.ADAPTIVE))
frames[0].save(f"{out}/{name}_clips.gif", save_all=True, append_images=frames[1:], duration=1000 * 2 // 24, loop=0)
# still sheet: 4 phases per clip
rows = []
for n in names:
    s = seqs[n]; idx = [0, len(s) // 3, 2 * len(s) // 3, len(s) - 1]
    row = Image.new('RGB', (R * 4, R + 18), (10, 12, 18)); d_ = ImageDraw.Draw(row)
    for k, i in enumerate(idx): row.paste(s[i], (k * R, 18))
    d_.text((6, 3), f"{name}  {n}", fill=(255, 220, 120)); rows.append(row)
sheet = Image.new('RGB', (R * 4 * 2, (R + 18) * ((len(rows) + 1) // 2)), (10, 12, 18))
for i, r_ in enumerate(rows): sheet.paste(r_, ((i % 2) * R * 4, (i // 2) * (R + 18)))
sheet.save(f"{out}/{name}_sheet.png")
for f in os.listdir(tmp): os.remove(os.path.join(tmp, f))
os.rmdir(tmp)
json.dump(info, open(f"{out}/{name}_fbx_verify.json", "w"), indent=1); print(json.dumps(info))
