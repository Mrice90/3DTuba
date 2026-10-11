# Blender headless (AI-080): import a staged Meshy GLB and write a playtest board token.
# Derived from docs/production/tools/glb_to_token_fbx.py (same axis/texture conventions), plus:
#   * decimation of very dense sculpts (Meshy untextured outputs reach 0.5-1.25M triangles),
#   * normalisation to a 1.0-unit-tall token with its origin at the bottom centre.
# The Unity side (PlaytestCatalog / TokenFactory) then applies the AI-063 per-type budget
# (height and footprint as a fraction of the hex), so this script never bakes a board scale.
# usage: blender -b -P glb_to_playtest_token.py -- in.glb out.fbx [max_triangles] [--static]
# A rigged GLB with actions keeps its armature and clips (AI-060b); --static forces the old baked token.
import bpy, sys, os, mathutils

args = sys.argv[sys.argv.index("--") + 1:]
flags = [a for a in args if a.startswith("--")]
args = [a for a in args if not a.startswith("--")]
src, dst = args[0], args[1]
max_tris = int(args[2]) if len(args) > 2 else 60000
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
if not meshes:
    raise SystemExit("no mesh in " + src)
arms = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]


def export_rigged(arm):
    # AI-060b: a rigged Meshy GLB keeps its skeleton and every action, so Unity can play the clips
    # (UnitAnimator matches them by name: idle / walk / attack / hit / death). Normalisation scales the
    # armature root instead of baking vertices, and decimation runs ahead of the Armature modifier.
    total = sum(len(p.vertices) - 2 for o in meshes for p in o.data.polygons)
    for o in meshes:
        tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
        if total > max_tris:
            bpy.context.view_layer.objects.active = o
            mod = o.modifiers.new("Decimate", "DECIMATE")
            mod.ratio = max_tris / float(total)
            bpy.ops.object.modifier_move_to_index(modifier=mod.name, index=0)
            bpy.ops.object.modifier_apply(modifier=mod.name)
            print("DECIMATED", o.name, tris, "->", sum(len(p.vertices) - 2 for p in o.data.polygons))
    root = arm
    while root.parent is not None:
        root = root.parent
    for a in arms:
        a.animation_data_create()
    if arm.animation_data:
        arm.animation_data.action = None  # bounds from the rest pose, not the first clip's frame
    bpy.context.view_layer.update()
    pts = [o.matrix_world @ v.co for o in meshes for v in o.data.vertices]
    mn = mathutils.Vector([min(p[i] for p in pts) for i in range(3)])
    mx = mathutils.Vector([max(p[i] for p in pts) for i in range(3)])
    size = mx - mn
    s = 1.0 / size.z if size.z > 1e-6 else 1.0
    off = mathutils.Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))
    root.location = (root.location - off) * s
    root.scale = root.scale * s
    keep = set(meshes) | set(arms) | {root}
    for o in list(bpy.context.scene.objects):
        if o not in keep:
            bpy.data.objects.remove(o, do_unlink=True)
    actions = [a.name for a in bpy.data.actions]
    print("TOKEN_SIZE", tuple(round(c * s, 4) for c in size), "scale", round(s, 4), "RIGGED actions", actions)
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=dst, use_selection=False, path_mode="COPY", embed_textures=False,
                             object_types={"ARMATURE", "MESH", "EMPTY"}, add_leaf_bones=False,
                             bake_anim=bool(actions), bake_anim_use_all_actions=True,
                             bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True,
                             axis_forward="-Z", axis_up="Y", apply_unit_scale=True, bake_space_transform=False)

def export_static():
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


if arms and bpy.data.actions and "--static" not in flags:
    export_rigged(arms[0])
else:
    export_static()

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
