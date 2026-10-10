"""AI-094 reskinnable coin (Blender 5.2, headless bpy).

  python3 -I coin.py <skins_dir> <out_dir>

One low-poly coin mesh with three material slots, in this order:
  0 Coin_Rim    edge, bevels and the raised lip (a flat metal colour)
  1 Coin_Heads  the top face disc
  2 Coin_Tails  the bottom face disc
Each face disc is UV-mapped to the whole 0..1 square (the inscribed circle), so a
new skin is just two square images. The tails UVs are set so its image reads the
right way round when the coin is flipped over its X axis.

Size: 1.0 across, 0.08 thick, origin at the centre (Unity scales it in CoinFlip).
Blender is Z-up with heads facing +Z; the exports turn that into Unity +Y.
"""
import math
import os
import sys

import bpy  # noqa: I001 (bpy must load before bmesh)
import bmesh

R = 0.5          # outer radius
RF = 0.45        # face disc radius (inside the lip)
ZF = 0.030       # face height (half thickness at the face)
ZL = 0.040       # lip height (half thickness)
SEG = 64
# rim profile from the top face edge, over the lip, down the edge, to the bottom face edge
PROFILE = [(RF, ZF), (0.462, ZL), (0.488, ZL), (R, 0.028), (R, -0.028), (0.488, -ZL), (0.462, -ZL), (RF, -ZF)]


def material(name, image=None, color=(0.8, 0.6, 0.25, 1), metallic=0.0, roughness=0.4):
    m = bpy.data.materials.new(name)
    nt = m.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if image:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(image)
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return m


def build(skins):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    me = bpy.data.meshes.new("Coin")
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    rings = []
    for r, z in PROFILE:
        rings.append([bm.verts.new((r * math.cos(2 * math.pi * i / SEG), r * math.sin(2 * math.pi * i / SEG), z)) for i in range(SEG)])
    # rim quads (material 0), u around the edge, v down the profile
    for j in range(len(PROFILE) - 1):
        for i in range(SEG):
            k = (i + 1) % SEG
            f = bm.faces.new((rings[j][i], rings[j][k], rings[j + 1][k], rings[j + 1][i]))
            f.material_index = 0
            f.smooth = True
            v0, v1 = 1 - j / (len(PROFILE) - 1), 1 - (j + 1) / (len(PROFILE) - 1)
            u0, u1 = i / SEG, (i + 1) / SEG
            for loop, (u, v) in zip(f.loops, ((u0, v0), (u1, v0), (u1, v1), (u0, v1))):
                loop[uv].uv = (u, v)
    # face discs as triangle fans around a centre vertex (clean UVs, even shading)
    for ring, z, mat, flip in ((rings[0], ZF, 1, False), (rings[-1], -ZF, 2, True)):
        c = bm.verts.new((0, 0, z))
        for i in range(SEG):
            k = (i + 1) % SEG
            f = bm.faces.new((c, ring[i], ring[k]) if not flip else (c, ring[k], ring[i]))
            f.material_index = mat
            f.smooth = False
            for loop in f.loops:
                x, y, _ = loop.vert.co
                loop[uv].uv = (0.5 + x / (2 * RF), 0.5 + (-y if flip else y) / (2 * RF))
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new("Coin", me)
    bpy.context.scene.collection.objects.link(obj)
    # Smooth around the circumference, hard where the profile turns a corner.
    me.set_sharp_from_angle(angle=math.radians(30))
    obj.data.materials.append(material("Coin_Rim", color=(0.85, 0.62, 0.22, 1), metallic=1.0, roughness=0.3))
    obj.data.materials.append(material("Coin_Heads", image=os.path.join(skins, "olympus_heads.png"), metallic=0.6, roughness=0.35))
    obj.data.materials.append(material("Coin_Tails", image=os.path.join(skins, "olympus_tails.png"), metallic=0.6, roughness=0.35))
    return obj


def export(obj, out):
    os.makedirs(out, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, "coin.blend"))
    bpy.ops.export_scene.gltf(filepath=os.path.join(out, "coin.glb"), export_format="GLB")
    # FBX for Unity: no embedded textures; CoinFlip assigns the skin images at runtime.
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, "coin.fbx"), use_selection=False, apply_scale_options="FBX_SCALE_UNITS",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=True, mesh_smooth_type="FACE", path_mode="STRIP", add_leaf_bones=False,
                             bake_anim=False)
    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    print("COIN tris", tris, "verts", len(obj.data.vertices), "materials", [m.name for m in obj.data.materials])


if __name__ == "__main__":
    export(build(sys.argv[1]), sys.argv[2])
