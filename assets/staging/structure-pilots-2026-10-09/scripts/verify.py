"""Re-import each exported GLB and FBX and check the token contract. Usage: python3 -I verify.py OUT_DIR"""
import json, os, sys
import bpy
out = os.path.abspath(sys.argv[1])
stats = json.load(open(os.path.join(out, "stats.json")))
res = {}
for cid in stats:
    for ext in ("glb", "fbx"):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        p = os.path.join(out, f"{cid}.{ext}")
        (bpy.ops.import_scene.gltf if ext == "glb" else bpy.ops.import_scene.fbx)(filepath=p)
        meshes = [o for o in bpy.data.objects if o.type == "MESH"]
        assert len(meshes) == 1, (cid, ext, len(meshes))
        ob = meshes[0]
        mw = ob.matrix_world
        co = [mw @ v.co for v in ob.data.vertices]
        mats = [m.name for m in ob.data.materials]
        imgs = sorted(i.name for i in bpy.data.images)
        # Blender's importers bring Y-up files back to Z-up, so Z is height here
        r = {"objects": len(meshes), "materials": mats, "images": imgs,
             "tris": sum(len(p.vertices) - 2 for p in ob.data.polygons),
             "size": [round(max(c[i] for c in co) - min(c[i] for c in co), 3) for i in range(3)],
             "min_z": round(min(c.z for c in co), 4),
             "center_xy": [round((max(c[i] for c in co) + min(c[i] for c in co)) / 2, 4) for i in range(2)],
             "uv_layers": len(ob.data.uv_layers)}
        res[f"{cid}.{ext}"] = r
        print(cid, ext, json.dumps(r))
json.dump(res, open(os.path.join(out, "verify.json"), "w"), indent=2)
