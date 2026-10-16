# List connected mesh islands (probe frame, H units): vertex count and bbox. Weapons/bases are often their own islands.
import bpy, sys, numpy as np, bmesh
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=sys.argv[1])
ms=[o for o in bpy.data.objects if o.type=='MESH']; bpy.context.view_layer.update()
out=[]
allV=[]
for o in ms:
    bm=bmesh.new(); bm.from_mesh(o.data); bm.transform(o.matrix_world); bm.verts.ensure_lookup_table()
    # merge by distance to join seams split by UV
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5); bm.verts.ensure_lookup_table()
    seen=set()
    for v in bm.verts:
        if v.index in seen: continue
        st=[v]; cur=[]; seen.add(v.index)
        while st:
            x=st.pop(); cur.append(tuple(x.co))
            for e in x.link_edges:
                w=e.other_vert(x)
                if w.index not in seen: seen.add(w.index); st.append(w)
        out.append(np.array(cur))
    allV+= [np.array([tuple(v.co) for v in bm.verts])]
V=np.concatenate(allV); lo,hi=V.min(0),V.max(0); H=hi[2]-lo[2]; c=np.array([(lo[0]+hi[0])/2,(lo[1]+hi[1])/2,lo[2]])
out.sort(key=len,reverse=True)
print("islands",len(out))
for I in out[:12]:
    I=(I-c)/H; print(len(I), np.round(I.min(0),3), np.round(I.max(0),3))
