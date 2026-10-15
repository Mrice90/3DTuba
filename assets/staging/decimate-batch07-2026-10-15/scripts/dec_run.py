# Decimate one Meshy GLB to a triangle budget, keep UVs/material/textures, export GLB+FBX, render before/after.
import bpy, sys, os, math, json, hashlib
from mathutils import Vector
src, outdir, target = sys.argv[-3], sys.argv[-2], int(sys.argv[-1])
name = os.path.splitext(os.path.basename(src))[0]
os.makedirs(f"{outdir}/models/{name}", exist_ok=True); os.makedirs(f"{outdir}/renders", exist_ok=True)

def sha(p): return hashlib.sha256(open(p,'rb').read()).hexdigest()
def tris(o):
    o.data.calc_loop_triangles(); return len(o.data.loop_triangles)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
mesh = [o for o in bpy.data.objects if o.type == 'MESH'][0]
before = tris(mesh)
bb = [mesh.matrix_world @ Vector(c) for c in mesh.bound_box]
lo = Vector((min(v.x for v in bb), min(v.y for v in bb), min(v.z for v in bb)))
hi = Vector((max(v.x for v in bb), max(v.y for v in bb), max(v.z for v in bb)))
ctr = (lo + hi) / 2; size = max(hi - lo)

sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
sc.render.resolution_x = 420; sc.render.resolution_y = 420; sc.render.film_transparent = True; sc.view_settings.view_transform = 'Standard'
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = 'ORTHO'; cam.data.ortho_scale = size * 1.15
VIEWS = {"front": (0, -1, 0), "front34": (0.7, -0.7, 0.35), "side": (1, 0, 0), "top": (0, -0.25, 1)}
def render(tag):
    paths = {}
    for k, d in VIEWS.items():
        d = Vector(d).normalized(); cam.location = ctr + d * size * 4
        cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
        cam.data.ortho_scale = size * 1.15
        p = f"{outdir}/renders/{name}_{tag}_{k}.png"; sc.render.filepath = p
        bpy.ops.render.render(write_still=True); paths[k] = p
    # close-up of the top third (face/head detail is where decimation shows first)
    d = Vector(VIEWS["front34"]).normalized(); top = Vector((ctr.x, ctr.y, hi.z - (hi.z - lo.z) * 0.2))
    cam.location = top + d * size * 4; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    cam.data.ortho_scale = size * 0.42
    p = f"{outdir}/renders/{name}_{tag}_detail.png"; sc.render.filepath = p; bpy.ops.render.render(write_still=True); paths["detail"] = p
    return paths
r_before = render("before")

ratio = min(1.0, target / before)
mod = mesh.modifiers.new("dec", 'DECIMATE'); mod.decimate_type = 'COLLAPSE'; mod.ratio = ratio
mod.use_collapse_triangulate = True
bpy.context.view_layer.objects.active = mesh; mesh.select_set(True)
bpy.ops.object.modifier_apply(modifier="dec")
after = tris(mesh)
r_after = render("after")

# Give textures real file names with extensions (Unity FBX import needs them; see rig-roster gotcha).
mdir = f"{outdir}/models/{name}"
roles = {}
mat = mesh.data.materials[0]
for n in mat.node_tree.nodes:
    if n.type == 'TEX_IMAGE' and n.image:
        for l in mat.node_tree.links:
            if l.from_node != n: continue
            t = l.to_node
            if t.type == 'BSDF_PRINCIPLED': roles[n.image.name] = l.to_socket.name.replace(" ", "")
            elif t.type == 'NORMAL_MAP': roles[n.image.name] = "Normal"
            elif t.type in ('SEPARATE_COLOR', 'SEPRGB', 'SEPARATE_RGB'): roles[n.image.name] = "MetallicRoughness"
for img in list(bpy.data.images):
    if not img.packed_file: continue
    ext = {'JPEG': '.jpg', 'PNG': '.png'}.get(img.file_format, '.png')
    fn = f"{name}_{roles.get(img.name, img.name)}{ext}"
    open(f"{mdir}/{fn}", "wb").write(img.packed_file.data)   # original bytes, no re-encode
    img.unpack(method="REMOVE"); img.filepath = f"{mdir}/{fn}"; img.reload(); img.name = fn

glb = f"{mdir}/{name}_30k.glb"; fbx = f"{mdir}/{name}_30k.fbx"
bpy.ops.export_scene.gltf(filepath=glb, export_format='GLB', use_selection=False)
bpy.ops.export_scene.fbx(filepath=fbx, path_mode='COPY', embed_textures=True, use_selection=False)
bpy.ops.wm.save_as_mainfile(filepath=f"{mdir}/{name}_30k.blend", compress=True)

# Silhouette agreement (alpha IoU) per view, as an objective "did the shape survive" check.
def alpha(p):
    im = bpy.data.images.load(p); px = list(im.pixels)[3::4]; bpy.data.images.remove(im); return [a > 0.5 for a in px]
iou = {}
for k in r_before:
    a, b = alpha(r_before[k]), alpha(r_after[k])
    inter = sum(1 for x, y in zip(a, b) if x and y); uni = sum(1 for x, y in zip(a, b) if x or y)
    iou[k] = round(inter / uni, 4) if uni else 1.0
rep = {"card": name, "source": os.path.basename(src), "source_sha256": sha(src), "tris_before": before, "tris_after": after,
       "ratio": round(ratio, 5), "dims_m": [round(x, 3) for x in (hi - lo)], "silhouette_iou": iou,
       "uv_layers": len(mesh.data.uv_layers), "materials": len(mesh.data.materials),
       "outputs": {os.path.basename(p): {"bytes": os.path.getsize(p), "sha256": sha(p)} for p in (glb, fbx)}}
json.dump(rep, open(f"{mdir}/{name}_report.json", "w"), indent=1); print("REPORT", json.dumps(rep))
