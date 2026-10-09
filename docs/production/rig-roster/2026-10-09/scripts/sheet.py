import bpy, sys, math, os, json
from PIL import Image, ImageDraw
fbx, out, name = sys.argv[-3], sys.argv[-2], sys.argv[-1]; os.makedirs(out, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]; mesh=[o for o in bpy.data.objects if o.type=='MESH'][0]
info={"bones":len(arm.data.bones),"tris":sum(len(p.vertices)-2 for p in mesh.data.polygons),"vgroups":len(mesh.vertex_groups),
      "takes":{a.name:list(a.frame_range) for a in bpy.data.actions}}
sc=bpy.context.scene; sc.world=bpy.data.worlds.new("w"); sc.world.color=(0.05,0.06,0.09)
sc.render.engine='BLENDER_WORKBENCH'; sc.display.shading.light='STUDIO'; sc.display.shading.color_type='TEXTURE'
sc.render.resolution_x=300; sc.render.resolution_y=380
bpy.ops.mesh.primitive_plane_add(size=1.6); fl=bpy.context.object
cam=bpy.data.objects.new("cam",bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera=cam
cam.data.type='ORTHO'; cam.data.ortho_scale=2.3
def view(kind):
    if kind=="front": cam.location=(0,-8,0.95); cam.rotation_euler=(math.radians(90),0,0)
    else: cam.location=(8,0,0.95); cam.rotation_euler=(math.radians(90),0,math.radians(90))
tiles=[]; gif={}
for a in bpy.data.actions:
    arm.animation_data.action=a
    if a.slots: arm.animation_data.action_slot=a.slots[0]
    f0,f1=int(a.frame_range[0]),int(a.frame_range[1]); n=f1-f0
    zs=[]
    for f in range(f0,f1+1):
        sc.frame_set(f); dg=bpy.context.evaluated_depsgraph_get(); m=mesh.evaluated_get(dg); me=m.to_mesh()
        zs.append(min((m.matrix_world@v.co).z for v in me.vertices)); m.to_mesh_clear()
    info.setdefault("minZ",{})[a.name]=[round(min(zs),4),round(max(zs),4)]
    short=a.name.split('|')[-1]
    for kind in ("front","side"):
        view(kind)
        for lab,f in (("f0",f0),("q1",f0+n//4),("mid",f0+n//2),("q3",f0+3*n//4)):
            sc.frame_set(f); p=f"{out}/{short}_{kind}_{lab}.png"; sc.render.filepath=p; bpy.ops.render.render(write_still=True); tiles.append((f"{short} {kind} {lab}",p))
    view("front"); fr=[]
    for f in range(f0,f1+1,2 if 'Walk' in a.name else 3):
        sc.frame_set(f); p=f"{out}/_g_{short}_{f:03d}.png"; sc.render.filepath=p; bpy.ops.render.render(write_still=True); fr.append(Image.open(p).convert('P'))
    fr[0].save(f"{out}/../{name}_{short.lower()}.gif",save_all=True,append_images=fr[1:],duration=1000*(2 if 'Walk' in a.name else 3)//24,loop=0)
cols=8; W,Hh=300,380; img=Image.new('RGB',(cols*W,((len(tiles)+cols-1)//cols)*(Hh+20)),(10,12,18)); d=ImageDraw.Draw(img)
for i,(lab,p) in enumerate(tiles):
    x,y=(i%cols)*W,(i//cols)*(Hh+20); img.paste(Image.open(p).convert('RGB'),(x,y+20)); d.text((x+6,y+4),lab,fill=(255,220,120))
img.save(f"{out}/../{name}_sheet.png")
for f in os.listdir(out):
    if f.startswith('_g_'): os.remove(os.path.join(out,f))
json.dump(info,open(f"{out}/../{name}_fbx_verify.json","w"),indent=1); print(json.dumps(info))
