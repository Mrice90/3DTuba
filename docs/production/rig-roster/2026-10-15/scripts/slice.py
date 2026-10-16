# print vertex clusters per z slice inside an x/y window (probe frame, H units) to locate props
import bpy, sys, numpy as np
src=sys.argv[1]; x0,x1,y0,y1=map(float,sys.argv[2:6])
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src)
ms=[o for o in bpy.data.objects if o.type=='MESH']; bpy.context.view_layer.update()
V=np.concatenate([np.array([tuple(o.matrix_world@v.co) for v in o.data.vertices]) for o in ms]); lo,hi=V.min(0),V.max(0); H=hi[2]-lo[2]
V=(V-np.array([(lo[0]+hi[0])/2,(lo[1]+hi[1])/2,lo[2]]))/H
for z in np.arange(0,1.0,0.04):
    s=(V[:,2]>=z)&(V[:,2]<z+0.04)&(V[:,0]>x0)&(V[:,0]<x1)&(V[:,1]>y0)&(V[:,1]<y1)
    if s.sum(): P=V[s]; print(f"z{z:.2f} n{s.sum():4d} x[{P[:,0].min():+.3f},{P[:,0].max():+.3f}] y[{P[:,1].min():+.3f},{P[:,1].max():+.3f}]")
