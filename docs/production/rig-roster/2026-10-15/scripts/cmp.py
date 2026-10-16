import bpy, sys, numpy as np
d=sys.argv[1]; n=sys.argv[2]; clip=sys.argv[3]; f=int(sys.argv[4])
bpy.ops.wm.open_mainfile(filepath=f"{d}/{n}_rigged.blend")
m=[o for o in bpy.data.objects if o.type=='MESH'][0]; arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
for tr in arm.animation_data.nla_tracks: tr.mute = tr.name!=clip
bpy.context.scene.frame_set(f); me=m.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
A=np.array([tuple(m.matrix_world@v.co) for v in me.vertices])
bones={b.name:(np.array(arm.matrix_world@pb.head)) for b,pb in zip(arm.data.bones,arm.pose.bones)}
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=f"{d}/{n}_rigged.fbx")
m=[o for o in bpy.data.objects if o.type=='MESH'][0]; arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
a=[a for a in bpy.data.actions if a.name.endswith(clip)][0]; arm.animation_data.action=a; arm.animation_data.action_slot=a.slots[0]
bpy.context.scene.frame_set(f); me=m.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
B=np.array([tuple(m.matrix_world@v.co) for v in me.vertices])
print("nverts",len(A),len(B), "minz", A[:,2].min(), B[:,2].min())
if len(A)==len(B): dd=np.linalg.norm(A-B,axis=1); print("diff max/mean",dd.max(),dd.mean())
for b,pb in zip(arm.data.bones,arm.pose.bones):
    h=np.array(arm.matrix_world@pb.head)
    if b.name in bones: print(b.name, np.round(bones[b.name],3), np.round(h,3))
