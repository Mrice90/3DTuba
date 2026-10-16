# Evaluate each baked action on its own (as Unity samples it) and print min z range per clip, in units of H
import bpy, sys, json, numpy as np
bpy.ops.wm.open_mainfile(filepath=sys.argv[1])
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]; mesh=[o for o in bpy.data.objects if o.type=='MESH'][0]
for t in arm.animation_data.nla_tracks: t.mute=True
H=max(mesh.dimensions); out={}
for a in bpy.data.actions:
    arm.animation_data.action=a
    if a.slots: arm.animation_data.action_slot=a.slots[0]
    zs=[]
    for f in range(int(a.frame_range[0]), int(a.frame_range[1])+1):
        bpy.context.scene.frame_set(f); dg=bpy.context.evaluated_depsgraph_get(); m=mesh.evaluated_get(dg); me=m.to_mesh()
        co=np.empty(len(me.vertices)*3); me.vertices.foreach_get("co",co); m.to_mesh_clear()
        zs.append(float((co.reshape(-1,3)@np.array(mesh.matrix_world.to_3x3()).T)[:,2].min()+mesh.matrix_world.translation.z))
    out[a.name]=[round(min(zs),4),round(max(zs),4)]
print("ACTZ", json.dumps(out))
