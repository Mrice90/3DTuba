# Fit a thin prop (spear/lance shaft) near a rough front-view line. Prints 3D endpoints (probe frame, H units).
# Usage: fitline.py <glb> x0 z0 x1 z1 [r=0.03] [view=xz|yz]
import bpy, sys, numpy as np
a=sys.argv[1:]; src=a[0]; x0,z0,x1,z1=map(float,a[1:5]); r=float(a[5]) if len(a)>5 else 0.03; view=a[6] if len(a)>6 else "xz"
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src)
ms=[o for o in bpy.data.objects if o.type=='MESH']; bpy.context.view_layer.update()
V=np.concatenate([np.array([tuple(o.matrix_world@v.co) for v in o.data.vertices]) for o in ms]); lo,hi=V.min(0),V.max(0); H=hi[2]-lo[2]
V=(V-np.array([(lo[0]+hi[0])/2,(lo[1]+hi[1])/2,lo[2]]))/H
ax=0 if view=="xz" else 1
P2=V[:,[ax,2]]; A=np.array([x0,z0]); B=np.array([x1,z1]); d=B-A; L=np.linalg.norm(d); u=d/L
t=(P2-A)@u; perp=np.abs((P2-A)@np.array([-u[1],u[0]]))
m=(perp<r)&(t>-0.05)&(t<L+0.05); P=V[m]; t=t[m]
# depth (the hidden axis) per bin along the line: take the densest thin cluster
oth=1-ax if ax in (0,1) else 0; oth=1 if ax==0 else 0
bins=np.linspace(t.min(),t.max(),12); mids=[]
for b0,b1 in zip(bins,bins[1:]):
    s=(t>=b0)&(t<b1)
    if s.sum()<4: continue
    h,e=np.histogram(P[s][:,oth],bins=40); k=np.argmax(h); dep=(e[k]+e[k+1])/2
    q=P[s][np.abs(P[s][:,oth]-dep)<0.02]
    if len(q): mids.append(q.mean(0))
M=np.array(mids); c=M.mean(0); _,_,vt=np.linalg.svd(M-c); dirv=vt[0]
proj=(P-c)@dirv; inl=np.linalg.norm((P-c)-np.outer(proj,dirv),axis=1)<r
e0=c+dirv*proj[inl].min(); e1=c+dirv*proj[inl].max()
print("inliers",inl.sum(),"of",len(P)); print("A",np.round(e0,3).tolist()); print("B",np.round(e1,3).tolist())
