# Fit a clean mixamo-style skeleton to an A-pose Meshy mesh; print joints; render overlay.
import bpy, sys, math, json
import numpy as np
from mathutils import Vector
args=sys.argv[sys.argv.index('--')+1:]
src, out, overrides = args[0], args[1], (json.loads(args[2]) if len(args)>2 else {})
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
mesh=[o for o in bpy.data.objects if o.type=='MESH'][0]
bpy.context.view_layer.update()
from mathutils import Matrix
mw=mesh.matrix_world.copy(); mesh.parent=None; mesh.data.transform(mw); mesh.matrix_world=Matrix.Identity(4)
V=np.array([tuple(v.co) for v in mesh.data.vertices])
zmin=V[:,2].min(); H=V[:,2].max()-zmin
def slab(z,h=0.012): return np.abs(V[:,2]-z)<h*H
V=V-np.array([0,0,zmin])
hm=np.abs(V[:,2]-0.86*H)<0.02*H; xc=float((V[hm][:,0].min()+V[hm][:,0].max())/2); V[:,0]-=xc
mesh.data.transform(Matrix.Translation((-xc,0,-zmin)))
def front(z,w=0.05*H): m=slab(z)&(np.abs(V[:,0])<w); return V[m][:,1].min()
def ms(guess, radius, mask=None, iters=12, axes=(0,1,2)):
    g=np.array(guess,float); P=V if mask is None else V[mask]
    for _ in range(iters):
        d=np.linalg.norm(P-g,axis=1); sel=P[d<radius]
        if len(sel)<5: break
        c=sel.mean(0)
        for a in axes: g[a]=c[a]
    return g
S=1.0/1.8  # source ratios measured on Keraunos (height 1.8)
J={}
# torso column
for name,zs,ys in [("Hips",0.888,-0.03),("Spine",1.002,-0.02),("Spine1",1.115,0),("Spine2",1.229,0.0),("Neck",1.302,0.0),("Head",1.376,0.0),("HeadTop_End",1.647,0.0)]:
    z=zs*S*H
    if name=="HeadTop_End": J[name]=np.array([0,J["Head"][1],z]); continue
    k={"Hips":0.065,"Spine":0.06,"Spine1":0.065,"Spine2":0.07,"Neck":0.045,"Head":0.05}[name]
    J[name]=np.array([0,front(z)+k*H,z])
for side,sg in (("Left",1),("Right",-1)):
    # legs: mean-shift in slabs from source-ratio guesses
    for name,(x,z) in {"UpLeg":(0.10,0.813),"Leg":(0.11,0.53),"Foot":(0.12,0.16)}.items():
        zz=z*S*H; g=[sg*x*H*0.95, 0.0, zz]
        g[1]=front(zz,0.2*H)+0.04*H; g=ms(g,0.06*H,mask=slab(zz)&(sg*V[:,0]>0.01*H)&(V[:,1]<front(zz,0.2*H)+0.12*H)&(np.abs(V[:,0])<0.17*H),axes=(0,1))
        J[side+name]=np.array([g[0],g[1],zz])
    if True:
        J[side+"UpLeg"][0]=sg*max(abs(J[side+"UpLeg"][0]),0.05*H)
    foot=J[side+"Foot"]
    m=(V[:,2]<0.06*H)&(np.abs(V[:,0]-foot[0])<0.08*H)&(V[:,1]<foot[1])
    toe=V[m][np.argmin(V[m][:,1])] if m.sum() else foot+np.array([0,-0.07*H,-foot[2]])
    J[side+"ToeBase"]=np.array([foot[0]+(toe[0]-foot[0])*0.6, foot[1]+(toe[1]-foot[1])*0.6, 0.03*H])
    J[side+"Toe_End"]=np.array([toe[0],toe[1],0.025*H])
    # arms
    sh=np.array([sg*0.035*H, J["Spine2"][1], 0.72*H]); J[side+"Shoulder"]=sh
    arm0=np.array([sg*0.115*H, J["Spine2"][1], 0.735*H])
    best=None
    for ang in range(15,80,2):
        a=math.radians(ang); d=np.array([sg*math.cos(a),0,-math.sin(a)])
        score=0
        for t in np.linspace(0.08,0.34,14):
            p=arm0+d*t*H; dd=np.linalg.norm((V-p)[:,[0,2]],axis=1)
            score+= (dd<0.02*H).sum()>0
        if best is None or score>best[0]: best=(score,ang,d)
    ang=overrides.get('_armangle',50); a=math.radians(ang); d=np.array([sg*math.cos(a),0,-math.sin(a)])
    J[side+"Arm"]=ms(arm0+d*0.0,0.04*H,iters=6,axes=(1,))
    el=ms(arm0+d*0.135*H,0.03*H,axes=(0,1,2)); J[side+"ForeArm"]=el
    wr=ms(arm0+d*0.265*H,0.025*H,axes=(0,1,2)); J[side+"Hand"]=wr
    J[side+"HandEnd"]=wr+(wr-el)/np.linalg.norm(wr-el)*0.055*H
    print(side,"arm angle",ang,"score",best[0])
mir=overrides.get('_mirror')
if mir:
    o='Right' if mir=='Left' else 'Left'
    for n in ("Arm","ForeArm","Hand","HandEnd","Shoulder"): J[o+n]=J[mir+n]*np.array([-1,1,1])
for n in ("UpLeg","Leg","Foot","ToeBase","Toe_End","Arm","ForeArm","Hand","HandEnd","Shoulder"):
    l,r=J["Left"+n],J["Right"+n]; ax=(abs(l[0])+abs(r[0]))/2; ay=(l[1]+r[1])/2; az=(l[2]+r[2])/2
    J["Left"+n]=np.array([ax,ay,az]); J["Right"+n]=np.array([-ax,ay,az])
for k,v in overrides.items():
    if not k.startswith('_'): J[k]=np.array(v)*H
print(json.dumps({k:[round(float(x)/H,4) for x in v] for k,v in J.items()}))
json.dump({"xc":xc,"H":float(H),"zmin":float(zmin),"joints":{k:[float(x) for x in v] for k,v in J.items()}},open(out+".json","w"),indent=1)
# overlay render
pass
mat=bpy.data.materials.new("j"); mat.diffuse_color=(1,0.1,0.1,1)
for k,v in J.items():
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.012*H,location=tuple(v)); o=bpy.context.object; o.data.materials.append(mat); o.show_in_front=True
sc=bpy.context.scene; sc.world=bpy.data.worlds.new("w"); sc.render.engine='BLENDER_WORKBENCH'; sc.display.shading.light='FLAT'; sc.display.shading.color_type='MATERIAL'
mesh.active_material.diffuse_color=(0.8,0.8,0.8,1) if mesh.active_material else None
sc.display.shading.show_xray=True; sc.display.shading.xray_alpha=0.6
cam=bpy.data.objects.new("cam",bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera=cam
cam.data.type='ORTHO'; cam.data.ortho_scale=1.15*H
for nm,rot,loc in [("front",(math.radians(90),0,0),(0,-10,H/2)),("side",(math.radians(90),0,math.radians(90)),(10,0,H/2))]:
    cam.rotation_euler=rot; cam.location=loc; sc.render.resolution_x=520; sc.render.resolution_y=560
    sc.render.filepath=f"{out}_{nm}.png"; bpy.ops.render.render(write_still=True)
