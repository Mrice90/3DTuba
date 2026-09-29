# Blender headless (AI-080): import a staged Meshy GLB and write a playtest board token.
# Derived from docs/production/tools/glb_to_token_fbx.py (same axis/texture conventions), plus:
#   * decimation of very dense sculpts (Meshy untextured outputs reach 0.5-1.25M triangles),
#   * normalisation to a 1.0-unit-tall token with its origin at the bottom centre.
# The Unity side (PlaytestCatalog / TokenFactory) then applies the AI-063 per-type budget
# (height and footprint as a fraction of the hex), so this script never bakes a board scale.
# usage: blender -b -P glb_to_playtest_token.py -- in.glb out.fbx [max_triangles]
import bpy, sys, os, mathutils

args = sys.argv[sys.argv.index("--") + 1:]
src, dst = args[0], args[1]
max_tris = int(args[2]) if len(args) > 2 else 60000
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
if not meshes:
    raise SystemExit("no mesh in " + src)
bpy.ops.object.select_all(action="DESELECT")
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
obj.parent = None
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
if tris > max_tris:
    mod = obj.modifiers.new("Decimate", "DECIMATE")
    mod.ratio = max_tris / float(tris)
    bpy.ops.object.modifier_apply(modifier=mod.name)
    print("DECIMATED", tris, "->", sum(len(p.vertices) - 2 for p in obj.data.polygons))

pts = [obj.matrix_world @ v.co for v in obj.data.vertices]
mn = mathutils.Vector([min(p[i] for p in pts) for i in range(3)])
mx = mathutils.Vector([max(p[i] for p in pts) for i in range(3)])
size = mx - mn  # Blender is Z-up; glTF +Y becomes Blender +Z
s = 1.0 / size.z if size.z > 1e-6 else 1.0
off = mathutils.Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))
for v in obj.data.vertices:
    v.co = (v.co - off) * s
obj.location = (0, 0, 0)
obj.name = "Token"
for o in list(bpy.context.scene.objects):
    if o is not obj:
        bpy.data.objects.remove(o, do_unlink=True)
print("TOKEN_SIZE", tuple(round(c * s, 4) for c in size), "scale", round(s, 4))
os.makedirs(os.path.dirname(dst), exist_ok=True)
bpy.ops.export_scene.fbx(filepath=dst, use_selection=False, path_mode="COPY", embed_textures=False,
                         axis_forward="-Z", axis_up="Y", apply_unit_scale=True, bake_space_transform=True)

# Write the material's textures next to the FBX, named by the socket they feed, so Unity builds a
# URP Lit material (TokenPreview.BuildMaterial) without relying on FBX texture binding.
outdir = os.path.dirname(dst)
for mat in bpy.data.materials:
    if not mat.use_nodes:
        continue
    bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if not bsdf:
        continue

    def src_image(sock):
        stack = [l.from_node for l in bsdf.inputs[sock].links]
        while stack:
            n = stack.pop()
            if n.type == "TEX_IMAGE" and n.image:
                return n.image
            for i in n.inputs:
                stack += [l.from_node for l in i.links]

    for sock, tag in (("Base Color", "BaseColor"), ("Normal", "Normal"),
                      ("Metallic", "MetallicRoughness"), ("Roughness", "MetallicRoughness")):
        img = src_image(sock)
        if img is None:
            continue
        path = os.path.join(outdir, f"Token_{tag}.png")
        if os.path.exists(path):
            continue
        img.filepath_raw = path
        img.file_format = "PNG"
        img.save()
        print("TEXTURE", tag, img.size[0], img.size[1])
print("PLAYTEST_TOKEN_OK", dst)
