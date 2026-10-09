"""Build, export and measure structures. Usage: python3 -I build.py OUT_DIR [MODULE ...] [--only card_id,...]
MODULE defaults to structures; each module exposes a BUILDERS dict."""
import importlib, json, os, sys, hashlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import kit
args = sys.argv[1:]
only = None
if "--only" in args:
    i = args.index("--only"); only = set(args[i + 1].split(",")); del args[i:i + 2]
out = os.path.abspath(args[0])
BUILDERS = {}
for mod in (args[1:] or ["structures"]):
    BUILDERS.update(importlib.import_module(mod).BUILDERS)
if only:
    BUILDERS = {k: v for k, v in BUILDERS.items() if k in only}
os.makedirs(out, exist_ok=True)
report = {}
for card_id, (title, faction, fn) in BUILDERS.items():
    kit.reset()
    fn()
    ob, mat = kit.assemble(card_id)
    st = kit.stats(ob)
    st.update(title=title, faction=faction)
    st["within_budget"] = st["height"] <= 1.6 + 1e-6 and max(st["size_x"], st["size_y"]) <= 1.8 + 1e-6
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, f"{card_id}.blend"), compress=True)
    bpy.ops.object.select_all(action="DESELECT"); ob.select_set(True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(out, f"{card_id}.glb"), export_format="GLB",
                              use_selection=True, export_yup=True, export_apply=True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, f"{card_id}.fbx"), use_selection=True,
                             axis_forward="-Z", axis_up="Y", apply_scale_options="FBX_SCALE_ALL",
                             path_mode="COPY", embed_textures=True, mesh_smooth_type="FACE")
    for ext in ("glb", "fbx", "blend"):
        p = os.path.join(out, f"{card_id}.{ext}")
        st[ext] = {"bytes": os.path.getsize(p), "sha256": hashlib.sha256(open(p, "rb").read()).hexdigest()}
    report[card_id] = st
    print(card_id, json.dumps(st))
json.dump(report, open(os.path.join(out, "stats.json"), "w"), indent=2)

# The palette atlas is identical for every pilot, so one set of PNGs serves all of them.
# Unity URP Lit wants metallic in R and smoothness in A, so that map is written separately.
kit.reset()
_, imgs = kit.palette_material("Token")
for kind in ("BaseColor", "Emission"):
    im = imgs[kind]
    im.filepath_raw = os.path.join(out, f"Token_{kind}.png"); im.file_format = "PNG"; im.save()
size = kit.GRID * kit.CELL
ms = bpy.data.images.new("Token_MetallicSmoothness", size, size, alpha=True)
px = list(imgs["ORM"].pixels)
outpx = []
for i in range(0, len(px), 4):
    rough, metal = px[i + 1], px[i + 2]
    outpx += [metal, metal, metal, 1.0 - rough]
ms.pixels.foreach_set(outpx)
ms.filepath_raw = os.path.join(out, "Token_MetallicSmoothness.png"); ms.file_format = "PNG"; ms.save()
