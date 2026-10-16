import bpy, sys, numpy as np
bpy.ops.wm.open_mainfile(filepath=sys.argv[1]); clip=sys.argv[2]
m=[o for o in bpy.data.objects if o.type=='MESH'][0]; arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
for tr in arm.animation_data.nla_tracks: tr.mute = tr.name!=clip
out=[]
for f in range(1,int(sys.argv[3])+1):
    bpy.context.scene.frame_set(f); dg=bpy.context.evaluated_depsgraph_get(); me=m.evaluated_get(dg).to_mesh()
    co=np.empty(len(me.vertices)*3); me.vertices.foreach_get("co",co); out.append(round(co[2::3].min(),3))
print(clip,out)
