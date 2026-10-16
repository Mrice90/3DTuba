# Find faces that stretch a lot in a clip frame and report which bones their vertices follow.
import bpy, sys, numpy as np, collections
bpy.ops.wm.open_mainfile(filepath=sys.argv[1]); clip=sys.argv[2]; f=int(sys.argv[3])
m=[o for o in bpy.data.objects if o.type=='MESH'][0]; arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
names={g.index:g.name.split(':')[-1] for g in m.vertex_groups}
dom=[names[max(v.groups,key=lambda g:g.weight).group] for v in m.data.vertices]
E=np.array([e.vertices[:] for e in m.data.edges]); V0=np.array([tuple(v.co) for v in m.data.vertices])
for tr in arm.animation_data.nla_tracks: tr.mute = tr.name!=clip
bpy.context.scene.frame_set(f); dg=bpy.context.evaluated_depsgraph_get(); me=m.evaluated_get(dg).to_mesh()
V1=np.array([tuple(v.co) for v in me.vertices])
l0=np.linalg.norm(V0[E[:,0]]-V0[E[:,1]],axis=1); l1=np.linalg.norm(V1[E[:,0]]-V1[E[:,1]],axis=1)
bad=np.where(l1>np.maximum(l0*4,0.03))[0]
print("edges stretched",len(bad),"of",len(E))
c=collections.Counter(tuple(sorted((dom[E[i,0]],dom[E[i,1]]))) for i in bad); print(c.most_common(12))
H=V0[:,2].max()
for i in bad[:8]: print(np.round(V0[E[i,0]]/H,3), dom[E[i,0]], np.round(V0[E[i,1]]/H,3), dom[E[i,1]])
