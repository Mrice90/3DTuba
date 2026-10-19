"""Re-imports every exported GLB and FBX and checks the contracts (Blender 5.2, headless bpy).

  python3 -I verify.py <models_dir>
"""
import json
import os
import sys

import bpy

out = sys.argv[1]
report = []
for name in sorted(os.listdir(out)):
    if not name.endswith((".glb", ".fbx")):
        continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    path = os.path.join(out, name)
    if name.endswith(".glb"):
        bpy.ops.import_scene.gltf(filepath=path)
    else:
        bpy.ops.import_scene.fbx(filepath=path)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    o = meshes[0]
    acts = sorted(a.name for a in bpy.data.actions)
    for ob in bpy.context.scene.objects:  # measure the rest pose, not an imported clip's frame
        if ob.animation_data:
            ob.animation_data.action = None
            ob.location, ob.rotation_euler, ob.rotation_quaternion = (0, 0, 0), (0, 0, 0), (1, 0, 0, 0)
    bpy.context.view_layer.update()
    zs = [(o.matrix_world @ v.co).z for v in o.data.vertices]
    ys = [(o.matrix_world @ v.co).y for v in o.data.vertices]
    xs = [(o.matrix_world @ v.co).x for v in o.data.vertices]
    r = {"file": name, "meshes": len(meshes), "materials": [m.name for m in o.data.materials],
         "size_xyz": [round(max(a) - min(a), 3) for a in (xs, ys, zs)],
         "centre_z": round((max(zs) + min(zs)) / 2, 3),
         "actions": acts}
    if name.startswith("coin_"):
        r["ok"] = (len(meshes) == 1 and r["materials"][:3] == ["Coin_Rim", "Coin_Heads", "Coin_Tails"]
                   and abs(r["size_xyz"][0] - 1.0) < 0.01 and abs(r["centre_z"]) < 0.01 and len(acts) >= 2)
    else:
        r["ok"] = len(meshes) == 1 and len(r["materials"]) == 1 and abs(r["size_xyz"][0] - 2.0) < 0.01
    report.append(r)
    print("VERIFY", json.dumps(r))
with open(os.path.join(out, "verify.json"), "w") as f:
    json.dump(report, f, indent=1)
print("ALL OK" if all(r["ok"] for r in report) else "SOME FAILED")
