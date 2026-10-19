# compare rest mesh of a rigged FBX against a token GLB (both normalised to height 1, grounded, centred)
import bpy, sys, numpy as np
from mathutils import kdtree
a=sys.argv[sys.argv.index('--')+1:]
def load(p):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    (bpy.ops.import_scene.fbx if p.endswith('fbx') else bpy.ops.import_scene.gltf)(filepath=p)
    for o in bpy.data.objects:
        if o.type=='MESH':
            for m in o.modifiers: m.show_viewport=False
    bpy.context.view_layer.update()
    V=np.concatenate([np.array([tuple(o.matrix_world@v.co) for v in o.data.vertices]) for o in bpy.data.objects if o.type=='MESH'])
    lo,hi=V.min(0),V.max(0); V=(V-np.array([(lo[0]+hi[0])/2,(lo[1]+hi[1])/2,lo[2]]))/(hi[2]-lo[2]); return V
A=load(a[0]); B=load(a[1])
kd=kdtree.KDTree(len(B))
for i,v in enumerate(B): kd.insert(v,i)
kd.balance()
d=np.array([kd.find(v)[2] for v in A[::5]])
print("CMP dimsA",np.ptp(A,0).round(3),"dimsB",np.ptp(B,0).round(3),"nn median",round(float(np.median(d)),4),"p95",round(float(np.percentile(d,95)),4))
