import bpy, glob, os, sys, json
O = sys.argv[-1]; res = {}
for d in sorted(glob.glob(O + "/models/*")):
    n = os.path.basename(d); r = {}
    for ext in ("glb", "fbx"):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        p = f"{d}/{n}_30k.{ext}"
        (bpy.ops.import_scene.gltf if ext == "glb" else bpy.ops.import_scene.fbx)(filepath=p)
        ms = [o for o in bpy.data.objects if o.type == 'MESH']
        t = 0
        for o in ms: o.data.calc_loop_triangles(); t += len(o.data.loop_triangles)
        r[ext] = {"meshes": len(ms), "tris": t, "materials": len({m.name for o in ms for m in o.data.materials if m}),
                  "images": sorted(i.name for i in bpy.data.images), "uv_layers": [len(o.data.uv_layers) for o in ms],
                  "height_m": round(max(o.dimensions.z for o in ms), 3)}
    res[n] = r
json.dump(res, open(O + "/verify.json", "w"), indent=1)
for n, r in res.items(): print(n, r["glb"]["tris"], r["fbx"]["tris"], r["fbx"]["images"], r["glb"]["height_m"], r["fbx"]["height_m"])
