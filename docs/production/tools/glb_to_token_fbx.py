# Blender headless: import a Meshy GLB, normalize to a board token (bottom-center origin,
# longest horizontal side = target footprint), export FBX with embedded textures.
# usage: blender -b -P glb_to_token_fbx.py -- in.glb out.fbx footprint
import bpy, sys, mathutils
args = sys.argv[sys.argv.index("--") + 1:]
src, dst, footprint = args[0], args[1], float(args[2])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
bpy.ops.object.select_all(action="DESELECT")
for o in meshes: o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1: bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
obj.parent = None
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
pts = [obj.matrix_world @ v.co for v in obj.data.vertices]
mn = mathutils.Vector([min(p[i] for p in pts) for i in range(3)])
mx = mathutils.Vector([max(p[i] for p in pts) for i in range(3)])
size = mx - mn  # Blender is Z-up
s = footprint / max(size.x, size.y)
off = mathutils.Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))
for v in obj.data.vertices: v.co = (v.co - off) * s
obj.location = (0, 0, 0); obj.name = "Token"
for o in list(bpy.context.scene.objects):
    if o is not obj: bpy.data.objects.remove(o, do_unlink=True)
print("TOKEN_SIZE", tuple(round(c * s, 4) for c in size), "scale", round(s, 4))
bpy.ops.export_scene.fbx(filepath=dst, use_selection=False, path_mode="COPY", embed_textures=True,
                         axis_forward="-Z", axis_up="Y", apply_unit_scale=True, bake_space_transform=True)
# Also write the material's textures next to the FBX, named by the socket they feed,
# so Unity can build a URP Lit material without relying on FBX embedded-texture binding.
import os
outdir = os.path.dirname(dst)
for mat in bpy.data.materials:
    if not mat.use_nodes: continue
    bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if not bsdf: continue
    def src_image(sock):
        stack = [l.from_node for l in bsdf.inputs[sock].links]
        while stack:
            n = stack.pop()
            if n.type == "TEX_IMAGE" and n.image: return n.image
            for i in n.inputs: stack += [l.from_node for l in i.links]
    for sock, tag in (("Base Color", "BaseColor"), ("Normal", "Normal"), ("Metallic", "MetallicRoughness"), ("Roughness", "MetallicRoughness")):
        img = src_image(sock)
        if img is None: continue
        path = os.path.join(outdir, f"Token_{tag}.png")
        if os.path.exists(path): continue
        img.filepath_raw = path; img.file_format = "PNG"; img.save()
        print("TEXTURE", tag, img.size[0], img.size[1])
