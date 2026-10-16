import bpy, sys, numpy as np
bpy.ops.wm.read_factory_settings(use_empty=True)
p=sys.argv[1]
(bpy.ops.import_scene.fbx if p.endswith('.fbx') else bpy.ops.import_scene.gltf)(filepath=p)
m=[o for o in bpy.data.objects if o.type=='MESH'][0]; arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
print([a.name for a in bpy.data.actions])
a=[a for a in bpy.data.actions if a.name.split('|')[-1].startswith(sys.argv[2])][0]
if arm.animation_data is None: arm.animation_data_create()
for t in list(arm.animation_data.nla_tracks): arm.animation_data.nla_tracks.remove(t)
arm.animation_data.action=a; arm.animation_data.action_slot=a.slots[0]
out=[]
for f in range(int(a.frame_range[0]),int(a.frame_range[1])+1,3):
    bpy.context.scene.frame_set(f); dg=bpy.context.evaluated_depsgraph_get(); mm=m.evaluated_get(dg); me=mm.to_mesh()
    co=np.array([tuple(mm.matrix_world@v.co) for v in me.vertices]); out.append(round(co[:,2].min(),3))
print(sys.argv[2],a.frame_range[:],out)
