# AI-060-RIG-ROSTER: build a clean mixamo-named skeleton on an A-pose Meshy mesh, skin it through a
# voxel proxy, retarget the Keraunos Prime Meshy Idle/Walk clips by joint direction, ground, export.
import bpy, sys, json, math, os
import numpy as np
from mathutils import Matrix, Vector
a=sys.argv[sys.argv.index('--')+1:]
tgt, fitjson, srcglb, outdir, name = a[0], a[1], a[2], a[3], a[4]
os.makedirs(outdir, exist_ok=True)
VOX=float(os.environ.get('VOX','0.008')); HS=float(os.environ.get('HS','10'))
fit=json.load(open(fitjson)); J={k:Vector(v) for k,v in fit["joints"].items()}; H=fit["H"]
bpy.ops.wm.read_factory_settings(use_empty=True)
sc=bpy.context.scene; sc.render.fps=24
# ---- source armature + clips
bpy.ops.import_scene.gltf(filepath=srcglb)
src=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]; src.name="SRC"
for o in list(bpy.data.objects):
    if o.type=='MESH': bpy.data.objects.remove(o)
clips={"Idle":bpy.data.actions["Idle_02"],"Walk":bpy.data.actions["Walking"]}
for k,ac in list(bpy.data.actions.items()):
    if ac not in clips.values(): bpy.data.actions.remove(ac)
# ---- target mesh
before=set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath=tgt)
new=[o for o in bpy.data.objects if o not in before]
mesh=[o for o in new if o.type=='MESH'][0]
bpy.context.view_layer.update()
mw=mesh.matrix_world.copy(); mesh.parent=None; mesh.data.transform(mw); mesh.matrix_world=Matrix.Identity(4)
mesh.data.transform(Matrix.Translation((-fit["xc"],0,-fit["zmin"])))
for o in new:
    if o!=mesh: bpy.data.objects.remove(o)
mesh.name=name
# ---- target skeleton
P="mixamorig:"
bones=[("Hips",None,"Hips","Spine",False),("Spine","Hips","Spine","Spine1",True),("Spine1","Spine","Spine1","Spine2",True),
 ("Spine2","Spine1","Spine2","Neck",True),("Neck","Spine2","Neck","Head",True),("Head","Neck","Head","HeadTop_End",True)]
for s in ("Left","Right"):
    bones+=[(s+"Shoulder","Spine2",s+"Shoulder",s+"Arm",False),(s+"Arm",s+"Shoulder",s+"Arm",s+"ForeArm",True),
      (s+"ForeArm",s+"Arm",s+"ForeArm",s+"Hand",True),(s+"Hand",s+"ForeArm",s+"Hand",s+"HandEnd",True),
      (s+"UpLeg","Hips",s+"UpLeg",s+"Leg",False),(s+"Leg",s+"UpLeg",s+"Leg",s+"Foot",True),
      (s+"Foot",s+"Leg",s+"Foot",s+"ToeBase",True),(s+"ToeBase",s+"Foot",s+"ToeBase",s+"Toe_End",True)]
# source segment for each target bone: (start bone head, end bone head, twist bone)
seg={b[0]:(b[0],b[3] if b[3]!="HandEnd" else None,b[0]) for b in bones}
for s in ("Left","Right"): seg[s+"Hand"]=(s+"Hand",s+"HandMiddle1",s+"Hand")
def frame(d,z):
    y=d.normalized(); z=(z-y*z.dot(y)); z.normalize(); x=y.cross(z)
    return Matrix((x,y,z)).transposed()
def src_rest(b): return src.data.bones[P+b]
def src_seg_rest(t):
    s0,s1,tw=seg[t]; d=src_rest(s1).head_local-src_rest(s0).head_local
    return frame(d,src_rest(tw).matrix_local.to_3x3().col[2])
arm_data=bpy.data.armatures.new(name+"_rig"); arm=bpy.data.objects.new(name+"_rig",arm_data); sc.collection.objects.link(arm)
bpy.context.view_layer.objects.active=arm; bpy.ops.object.mode_set(mode='EDIT')
for bn,par,h,t,conn in bones:
    eb=arm_data.edit_bones.new(P+bn); eb.head=J[h]
    eb.tail=J[t] if t!="HeadTop_End" else J[t]
    if par: eb.parent=arm_data.edit_bones[P+par]; eb.use_connect=conn and (eb.parent.tail-eb.head).length<1e-5
    # roll: carry the source segment's Z axis through the minimal swing source dir -> target dir
    Fs=src_seg_rest(bn); ds=Fs.col[1]; dt=(eb.tail-eb.head).normalized()
    eb.align_roll(ds.rotation_difference(dt).to_matrix()@Fs.col[2])
bpy.ops.object.mode_set(mode='OBJECT')
# ---- skin via voxel proxy
proxy=mesh.copy(); proxy.data=mesh.data.copy(); sc.collection.objects.link(proxy)
for m in list(proxy.data.materials): pass
r=proxy.modifiers.new("vox",'REMESH'); r.mode='VOXEL'; r.voxel_size=VOX*H
bpy.context.view_layer.objects.active=proxy; bpy.ops.object.modifier_apply(modifier="vox")
# keep only the largest connected island; loose islands with no bone inside make bone heat fail
import bmesh
bm=bmesh.new(); bm.from_mesh(proxy.data); bm.verts.ensure_lookup_table()
seen=set(); islands=[]
for v in bm.verts:
    if v.index in seen: continue
    st=[v]; isl=[]; seen.add(v.index)
    while st:
        x=st.pop(); isl.append(x)
        for e in x.link_edges:
            o=e.other_vert(x)
            if o.index not in seen: seen.add(o.index); st.append(o)
    islands.append(isl)
islands.sort(key=len,reverse=True)
drop=[v for isl in islands[1:] for v in isl]
bmesh.ops.delete(bm,geom=drop,context='VERTS'); bm.to_mesh(proxy.data); bm.free()
print("proxy islands",len(islands),"kept",len(islands[0]),"dropped verts",len(drop))
print("proxy faces",len(proxy.data.polygons))
# bone heat is unreliable at ~2 m scale; solve at 10x and scale back
for o in (proxy,arm): o.scale=(HS,HS,HS)
bpy.ops.object.select_all(action='DESELECT'); proxy.select_set(True); arm.select_set(True); bpy.context.view_layer.objects.active=arm
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
for o in (proxy,arm): o.scale=(1,1,1)
proxy.parent=None; proxy.matrix_world=Matrix.Identity(4)
nw=sum(1 for v in proxy.data.vertices if not v.groups)
print("proxy unweighted verts",nw,"/",len(proxy.data.vertices))
for b in arm_data.bones: mesh.vertex_groups.new(name=b.name)
dt=mesh.modifiers.new("dt",'DATA_TRANSFER'); dt.object=proxy; dt.use_vert_data=True; dt.data_types_verts={'VGROUP_WEIGHTS'}
dt.vert_mapping='POLYINTERP_NEAREST'; dt.layers_vgroup_select_src='ALL'; dt.layers_vgroup_select_dst='NAME'
bpy.context.view_layer.objects.active=mesh; bpy.ops.object.modifier_apply(modifier="dt")
bpy.data.objects.remove(proxy)
bpy.ops.object.select_all(action='DESELECT'); mesh.select_set(True)
bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL',limit=4)
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL',lock_active=False)
from mathutils import kdtree
unw=[v.index for v in mesh.data.vertices if not v.groups or sum(g.weight for g in v.groups)<1e-4]
okv=[v for v in mesh.data.vertices if v.index not in set(unw)]
kd=kdtree.KDTree(len(okv))
for i,v in enumerate(okv): kd.insert(v.co,i)
kd.balance()
for vi in unw:
    _,i,_=kd.find(mesh.data.vertices[vi].co)
    for g in okv[i].groups: mesh.vertex_groups[g.group].add([vi],g.weight,'REPLACE')
print("mesh unweighted fixed by nearest neighbour",len(unw),"/",len(mesh.data.vertices))
REPORT_UNW=len(unw)
mesh.parent=arm; mod=mesh.modifiers.new("Armature",'ARMATURE'); mod.object=arm
# ---- retarget
order=[b[0] for b in bones]; par={b[0]:b[1] for b in bones}
src_hip_rest=src_rest("Hips").head_local; hip_scale=J["Hips"].z/src_hip_rest.z
rest={bn:arm_data.bones[P+bn].matrix_local.copy() for bn in order}
corr={}
for bn in order:
    Fs_rest=src_seg_rest(bn); Ft=rest[bn].to_3x3()
    sw=Ft.col[1].rotation_difference(Fs_rest.col[1]).to_matrix()
    corr[bn]=Fs_rest.inverted()@(sw@Ft)
def minz():
    dg=bpy.context.evaluated_depsgraph_get(); m=mesh.evaluated_get(dg); me=m.to_mesh()
    co=np.empty(len(me.vertices)*3); me.vertices.foreach_get("co",co); m.to_mesh_clear(); return co[2::3].min()
arm.animation_data_create(); src.animation_data_create() if src.animation_data is None else None
report={}
for cname,sa in clips.items():
    src.animation_data.action=sa; src.animation_data.action_slot=sa.slots[0]
    f0,f1=int(sa.frame_range[0]),int(sa.frame_range[1])
    ta=bpy.data.actions.new(cname); ta.use_fake_user=True; arm.animation_data.action=ta
    for f in range(f0,f1+1):
        sc.frame_set(f); R={}
        for bn in order:
            s0,s1,tw=seg[bn]; pb=src.pose.bones
            d=pb[P+s1].head-pb[P+s0].head
            Fs=frame(d,pb[P+tw].matrix.to_3x3().col[2])
            R[bn]=Fs@corr[bn]
            rr=rest[bn].to_3x3()
            if par[bn] is None: basis=rr.inverted()@R[bn]
            else:
                pr=rest[par[bn]].to_3x3()
                basis=(R[par[bn]]@pr.inverted()@rr).inverted()@R[bn]
            tb=arm.pose.bones[P+bn]; tb.rotation_mode='QUATERNION'; tb.rotation_quaternion=basis.to_quaternion()
            if par[bn] is None:
                head=J["Hips"]+(pb[P+"Hips"].head-src_hip_rest)*hip_scale
                tb.location=rr.inverted()@(head-J["Hips"])
                tb.keyframe_insert("location",frame=f-f0+1)
            tb.keyframe_insert("rotation_quaternion",frame=f-f0+1)
    # ground: offset hips so the lowest point of the cycle sits at Z=0
    n=f1-f0+1; zs=[]
    for f in range(1,n+1): sc.frame_set(f); zs.append(minz())
    low=min(zs); off=rest["Hips"].to_3x3().inverted()@Vector((0,0,-low))
    fcs=[fc for l in ta.layers for s in l.strips for cb in s.channelbags for fc in cb.fcurves]
    for fc in fcs:
        if fc.data_path==f'pose.bones["{P}Hips"].location':
            for k in fc.keyframe_points: k.co[1]+=off[fc.array_index]; k.handle_left[1]+=off[fc.array_index]; k.handle_right[1]+=off[fc.array_index]
            fc.update()
    zs2=[]
    for f in range(1,n+1): sc.frame_set(f); zs2.append(minz())
    report[cname]={"frames":n,"seconds":round((n-1)/24,3),"minZ_before":round(low,4),"minZ_after_range":[round(min(zs2),4),round(max(zs2),4)]}
    print(cname,report[cname])
    tr=arm.animation_data.nla_tracks.new(); tr.name=cname; st=tr.strips.new(cname,1,ta); st.action_slot=ta.slots[0]
    arm.animation_data.action=None
arm.animation_data.action=None
for pb in arm.pose.bones: pb.matrix_basis.identity()
bpy.data.objects.remove(src)
for ac in list(clips.values()): bpy.data.actions.remove(ac)
sc.frame_set(0)
vs=[v.co for v in mesh.data.vertices]
report["rest_bbox"]=[[round(min(v[i] for v in vs),3) for i in range(3)],[round(max(v[i] for v in vs),3) for i in range(3)]]
report["bones"]=len(arm_data.bones); report["tris"]=sum(len(p.vertices)-2 for p in mesh.data.polygons)
report["unweighted_fixed"]=REPORT_UNW; report["max_influences"]=max(len(v.groups) for v in mesh.data.vertices)
json.dump(report,open(f"{outdir}/{name}_rig_report.json","w"),indent=1)
bpy.ops.wm.save_as_mainfile(filepath=f"{outdir}/{name}_rigged.blend")
# Unity only makes Texture2D assets from embedded images whose names carry an extension
for img in bpy.data.images:
    ext={'JPEG':'.jpg','PNG':'.png'}.get(img.file_format,'.png')
    base=bpy.path.clean_name(img.name.rsplit('.',1)[0] if img.name.lower().endswith(('.jpg','.png')) else img.name)
    img.name=base+ext; img.filepath_raw="//"+base+ext
bpy.ops.export_scene.fbx(filepath=f"{outdir}/{name}_rigged.fbx", object_types={'ARMATURE','MESH'},
    use_selection=False, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
    add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
    bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=True, bake_anim_use_all_actions=False,
    bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, path_mode='COPY', embed_textures=True)
bpy.ops.export_scene.gltf(filepath=f"{outdir}/{name}_rigged.glb", export_format='GLB', export_animations=True, export_animation_mode='NLA_TRACKS')
print("DONE")
