# token FBX (+ optional Meshy GLB whose texture to borrow) -> GLB mesh with material, world transform applied
import bpy, sys
a=sys.argv[sys.argv.index('--')+1:]; src,dst=a[0],a[1]; tex=a[2] if len(a)>2 else None
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
tok=[o for o in bpy.data.objects if o.type=='MESH'][0]; bpy.context.view_layer.update()
mw=tok.matrix_world.copy(); tok.parent=None; tok.data.transform(mw); tok.matrix_world.identity()
for o in list(bpy.data.objects):
    if o is not tok: bpy.data.objects.remove(o)
if tex:
    n0=set(bpy.data.objects); (bpy.ops.import_scene.fbx if tex.endswith(".fbx") else bpy.ops.import_scene.gltf)(filepath=tex)
    g=[o for o in bpy.data.objects if o not in n0 and o.type=='MESH'][0]; mat=g.material_slots[0].material
    tok.data.materials.clear(); tok.data.materials.append(mat)
    for o in [o for o in bpy.data.objects if o not in n0]: bpy.data.objects.remove(o)
else:
    has=any(i.size[0]>0 for i in bpy.data.images); print("EMBEDDED", has)
tok.name=bpy.path.basename(src).rsplit('.',1)[0]
bpy.ops.export_scene.gltf(filepath=dst, export_format='GLB', export_animations=False)
print("OK", len(tok.data.vertices))
