# Stretch QA on the rigged .blend: for each clip, worst frame share of face edges stretched > 2.5x rest
# and the bone pairs responsible. Usage: qa.py <rigged.blend>
import bpy, sys, numpy as np, collections, json
bpy.ops.wm.open_mainfile(filepath=sys.argv[1])
m=[o for o in bpy.data.objects if o.type=='MESH'][0]; arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
names={g.index:g.name.split(':')[-1] for g in m.vertex_groups}
dom=[names[max(v.groups,key=lambda g:g.weight).group] for v in m.data.vertices]
E=np.array(sorted({tuple(sorted(k)) for p in m.data.polygons for k in p.edge_keys}))
V0=np.array([tuple(v.co) for v in m.data.vertices]); l0=np.linalg.norm(V0[E[:,0]]-V0[E[:,1]],axis=1)+1e-6
H=V0[:,2].max(); res={}
for t in arm.animation_data.nla_tracks: t.mute = True
for tr in list(arm.animation_data.nla_tracks):
    act=tr.strips[0].action; arm.animation_data.action=act; arm.animation_data.action_slot=act.slots[0]
    n=int(act.frame_range[1]); worst=(0,0,None)
    for f in range(1,n+1,2):
        bpy.context.scene.frame_set(f); me=m.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh()
        co=np.empty(len(me.vertices)*3); me.vertices.foreach_get("co",co); V1=co.reshape(-1,3)
        l1=np.linalg.norm(V1[E[:,0]]-V1[E[:,1]],axis=1); bad=(l1/l0>2.5)&(l1>0.01*H)
        if bad.sum()>worst[0]: worst=(int(bad.sum()),f,bad)
    c=collections.Counter(tuple(sorted((dom[a],dom[b]))) for a,b in E[worst[2]]) if worst[2] is not None else collections.Counter()
    res[tr.name]={"worst_frame":worst[1],"stretched_edges":worst[0],"share_pct":round(100*worst[0]/len(E),3),"pairs":[f"{a}-{b}:{k}" for (a,b),k in c.most_common(4)]}
print(json.dumps(res))
