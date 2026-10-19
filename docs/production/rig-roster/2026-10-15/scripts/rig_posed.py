# AI-060-RIG-COMBAT: rig a posed (non-A-pose) Meshy humanoid from hand-placed joints, skin it through a
# voxel proxy, add rigid overrides for bases / weapons / shields, and bake the additive clip set from clips.py.
# Usage: python3 -I rig_posed.py -- <mesh.glb> <config.json> <outdir> <card_id>
import bpy, sys, json, math, os, bmesh
import numpy as np
from mathutils import Matrix, Vector, Quaternion, kdtree
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import clips as C
a = sys.argv[sys.argv.index('--') + 1:]
src, cfgp, outdir, name = a
cfg = json.load(open(cfgp)); os.makedirs(outdir, exist_ok=True)
VOX = cfg.get("vox", 0.008); HS = 10.0
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene; sc.render.fps = C.FPS
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
bpy.context.view_layer.update()
for m in meshes:
    mw = m.matrix_world.copy(); m.parent = None; m.data.transform(mw); m.matrix_world = Matrix.Identity(4)
for o in list(bpy.data.objects):
    if o.type != 'MESH': bpy.data.objects.remove(o)
if len(meshes) > 1:
    bpy.ops.object.select_all(action='DESELECT')
    for m in meshes: m.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]; bpy.ops.object.join()
mesh = bpy.context.view_layer.objects.active if len(meshes) > 1 else meshes[0]
mesh.name = name
V = np.array([tuple(v.co) for v in mesh.data.vertices])
lo, hi = V.min(0), V.max(0); H = float(hi[2] - lo[2])
off = np.array([-(lo[0] + hi[0]) / 2, -(lo[1] + hi[1]) / 2, -lo[2]])  # same frame as probe.py
mesh.data.transform(Matrix.Translation(tuple(off))); V = V + off
# ---------------------------------------------------------------- joints
V0 = V.copy()
def autoy(x, z, r=0.035):
    m = (np.hypot(V0[:, 0] - x * H, V0[:, 2] - z * H) < r * H)
    return float(np.median(V0[m][:, 1])) / H if m.sum() > 3 else 0.0
J = {}
def pt(v): return Vector((v[0], v[1], v[2])) * H if len(v) == 3 else Vector((v[0], autoy(v[0], v[1]), v[1])) * H
def L(p, q, t): return p.lerp(q, t)
CREATURE = cfg.get("kind") == "creature"
if CREATURE:  # body + spine chain to the head, tail chain, and free limb chains
    J["Hips"] = pt(cfg["body"]); CB = []
    sp = [pt(p) for p in cfg["spine"]]; J["_sp0"] = sp[0]
    CB.append(("Hips", "Root", "Hips", "_sp0"))
    for i in range(1, len(sp)):
        J[f"_sp{i}"] = sp[i]; CB.append((f"Spine{i}", "Hips" if i == 1 else f"Spine{i-1}", f"_sp{i-1}", f"_sp{i}"))
    tl = [pt(p) for p in cfg.get("tail", [])]
    for i, p in enumerate(tl): J[f"_tl{i}"] = p
    for i in range(1, len(tl)): CB.append((f"Tail{i}", "Hips" if i == 1 else f"Tail{i-1}", f"_tl{i-1}", f"_tl{i}"))
    for Lm in cfg.get("limbs", []):
        ps = [pt(p) for p in Lm["pts"]]
        for i, p in enumerate(ps): J[f"_{Lm['name']}{i}"] = p
        for i in range(1, len(ps)):
            CB.append((f"{Lm['name']}{i}", Lm.get("parent", "Hips") if i == 1 else f"{Lm['name']}{i-1}", f"_{Lm['name']}{i-1}", f"_{Lm['name']}{i}"))
else:
    for k, v in cfg["joints"].items(): J[k] = pt(v)
    J.setdefault("Spine", L(J["Hips"], J["Chest"], 0.36)); J.setdefault("Spine1", L(J["Hips"], J["Chest"], 0.70))
    J.setdefault("Head", L(J["Neck"], J["HeadTop"], 0.32))
    for s in ("Left", "Right"):
        if s + "Arm" in J: J.setdefault(s + "Shoulder", L(L(J["Chest"], J["Neck"], 0.5), J[s + "Arm"], 0.22))
        if s + "Toe" in J:
            J.setdefault(s + "ToeBase", L(J[s + "Foot"], J[s + "Toe"], 0.6))
# shift so the hips stand over the origin (game rotates tokens about their origin)
shift = Vector((-J["Hips"].x, -J["Hips"].y, 0)); mesh.data.transform(Matrix.Translation(shift)); V = V + np.array(shift)
for k in J: J[k] = J[k] + shift
yaw = math.radians(cfg.get("facing", 0.0)); RF = Matrix.Rotation(yaw, 3, 'Z')  # facing frame: 0 = faces -Y
AX = {"X": RF @ Vector((1, 0, 0)), "Y": RF @ Vector((0, 1, 0)), "Z": Vector((0, 0, 1))}
# ---------------------------------------------------------------- skeleton (mixamo names + Root)
P = "mixamorig:"
if CREATURE: B = CB
else:
    B = [("Hips", "Root", "Hips", "Spine"), ("Spine", "Hips", "Spine", "Spine1"), ("Spine1", "Spine", "Spine1", "Chest"),
         ("Spine2", "Spine1", "Chest", "Neck"), ("Neck", "Spine2", "Neck", "Head"), ("Head", "Neck", "Head", "HeadTop")]
    for s in ("Left", "Right"):  # limbs are optional: mounted riders and robed casters may have no legs
        if s + "Arm" in J:
            B += [(s + "Shoulder", "Spine2", s + "Shoulder", s + "Arm"), (s + "Arm", s + "Shoulder", s + "Arm", s + "ForeArm"),
                  (s + "ForeArm", s + "Arm", s + "ForeArm", s + "Hand"), (s + "Hand", s + "ForeArm", s + "Hand", s + "HandEnd")]
        if s + "UpLeg" in J:
            B += [(s + "UpLeg", "Hips", s + "UpLeg", s + "Leg"), (s + "Leg", s + "UpLeg", s + "Leg", s + "Foot")]
            if s + "Toe" in J: B += [(s + "Foot", s + "Leg", s + "Foot", s + "ToeBase"), (s + "ToeBase", s + "Foot", s + "ToeBase", s + "Toe")]
            else: B += [(s + "Foot", s + "Leg", s + "Foot", None)]
for e in cfg.get("extra_bones", []):  # e.g. tails, cloth, mounts: [name, parent, head, tail]
    J["_h_" + e[0]] = Vector(e[2]) * H + shift; J["_t_" + e[0]] = Vector(e[3]) * H + shift
    B.append((e[0], e[1], "_h_" + e[0], "_t_" + e[0]))
ad = bpy.data.armatures.new(name + "_rig"); arm = bpy.data.objects.new(name + "_rig", ad); sc.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm; bpy.ops.object.mode_set(mode='EDIT')
rb = ad.edit_bones.new(P + "Root"); rb.head = (0, 0, 0); rb.tail = (0, 0, 0.08 * H); rb.use_deform = True
for bn, par, h, t in B:
    eb = ad.edit_bones.new(P + bn); eb.head = J[h]
    if t is None:
        tl = J[h] + RF @ Vector((0, -0.07 * H, -J[h].z * 0.6))
    else: tl = J[t]
    if (tl - eb.head).length < 1e-4: tl = eb.head + Vector((0, 0, 0.03 * H))
    eb.tail = tl; eb.parent = ad.edit_bones[P + par]; eb.use_connect = False
    eb.align_roll(RF @ Vector((0, -1, 0)) if abs((tl - eb.head).normalized().dot(RF @ Vector((0, -1, 0)))) < 0.9 else Vector((0, 0, 1)))
bpy.ops.object.mode_set(mode='OBJECT')
order = [P + "Root"] + [P + b[0] for b in B]; parent = {P + b[0]: P + b[1] for b in B}
# ---------------------------------------------------------------- skin
ad.bones[P + "Root"].use_deform = False
proxy = mesh.copy(); proxy.data = mesh.data.copy(); sc.collection.objects.link(proxy)
r = proxy.modifiers.new("vox", 'REMESH'); r.mode = 'VOXEL'; r.voxel_size = VOX * H
bpy.context.view_layer.objects.active = proxy; bpy.ops.object.modifier_apply(modifier="vox")
bm = bmesh.new(); bm.from_mesh(proxy.data); bm.verts.ensure_lookup_table(); seen = set(); isl = []
for v in bm.verts:
    if v.index in seen: continue
    st = [v]; cur = []; seen.add(v.index)
    while st:
        x = st.pop(); cur.append(x)
        for e in x.link_edges:
            o = e.other_vert(x)
            if o.index not in seen: seen.add(o.index); st.append(o)
    isl.append(cur)
isl.sort(key=len, reverse=True)
keep = cfg.get("keep_islands", 1)
bmesh.ops.delete(bm, geom=[v for i in isl[keep:] for v in i], context='VERTS'); bm.to_mesh(proxy.data); bm.free()
for o in (proxy, arm): o.scale = (HS, HS, HS)
bpy.ops.object.select_all(action='DESELECT'); proxy.select_set(True); arm.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
for o in (proxy, arm): o.scale = (1, 1, 1)
proxy.parent = None; proxy.matrix_world = Matrix.Identity(4)
proxy_unw = sum(1 for v in proxy.data.vertices if not v.groups)
for b in ad.bones: mesh.vertex_groups.new(name=b.name)
dt = mesh.modifiers.new("dt", 'DATA_TRANSFER'); dt.object = proxy; dt.use_vert_data = True; dt.data_types_verts = {'VGROUP_WEIGHTS'}
dt.vert_mapping = 'POLYINTERP_NEAREST'; dt.layers_vgroup_select_src = 'ALL'; dt.layers_vgroup_select_dst = 'NAME'
bpy.context.view_layer.objects.active = mesh; bpy.ops.object.modifier_apply(modifier="dt")
bpy.data.objects.remove(proxy)
ad.bones[P + "Root"].use_deform = True
# rigid overrides: base disc -> Root, props (weapons, shields) -> one bone, capsules in H units
Vn = V / H; forced = {}
if "base_z" in cfg:
    for i in np.where(Vn[:, 2] < cfg["base_z"])[0]: forced[int(i)] = "Root"
def capsule(a_, b_, r_):
    a_ = np.array(a_) + np.array(shift) / H; b_ = np.array(b_) + np.array(shift) / H
    d = b_ - a_; t = np.clip(((Vn - a_) @ d) / (d @ d), 0, 1); return np.linalg.norm(Vn - (a_ + t[:, None] * d), axis=1) < r_
adj = [[] for _ in range(len(Vn))]
for e in mesh.data.edges: a0, b0 = e.vertices; adj[a0].append(b0); adj[b0].append(a0)
propv = {}
for pr in cfg.get("props", []):
    if "disc_box" in pr:  # shields: RANSAC a plane inside the box, keep the thin slab around it
        Vp = Vn - np.array(shift) / H
        inb = np.all((Vp >= np.array(pr["disc_box"][0])) & (Vp <= np.array(pr["disc_box"][1])), axis=1); idx = np.where(inb)[0]
        rng = np.random.default_rng(7); best = (0, None)
        for _ in range(400):
            p3 = Vp[rng.choice(idx, 3, replace=False)]; nv = np.cross(p3[1] - p3[0], p3[2] - p3[0])
            if np.linalg.norm(nv) < 1e-9: continue
            nv /= np.linalg.norm(nv); c_ = (np.abs((Vp[idx] - p3[0]) @ nv) < pr.get("thick", 0.03)).sum()
            if c_ > best[0]: best = (c_, (p3[0], nv))
        p0, nv = best[1]; m = inb & (np.abs((Vp - p0) @ nv) < pr.get("thick", 0.03))
        print("disc plane normal", np.round(nv, 3), "verts", int(m.sum()))
    elif "box" in pr:
        m = np.all((Vn - np.array(shift) / H >= np.array(pr["box"][0])) & (Vn - np.array(shift) / H <= np.array(pr["box"][1])), axis=1)
    else:
        a_, b_ = pr["a"], pr["b"]
        if len(a_) == 2: a_ = [a_[0], pr.get("y", autoy(a_[0], a_[1])), a_[1]]
        if len(b_) == 2: b_ = [b_[0], pr.get("y", autoy(b_[0], b_[1])), b_[1]]
        m = capsule(a_, b_, pr["r"])
        # grow across mesh edges inside a wider capsule so the whole weapon surface is caught, not just its core
        wide = capsule(a_, b_, pr["r"] * pr.get("grow", 1.8))
        if "zmin" in pr: wide &= Vn[:, 2] >= pr["zmin"]
        cur = set(np.where(m & wide)[0].tolist()); front = list(cur)
        while front:
            nxt = []
            for vi in front:
                for wj in adj[vi]:
                    if wj not in cur and wide[wj]: cur.add(wj); nxt.append(wj)
            front = nxt
        m = np.zeros(len(Vn), bool); m[list(cur)] = True
    if "zmin" in pr: m &= Vn[:, 2] >= pr["zmin"]
    for i in np.where(m)[0]: forced[int(i)] = pr["bone"]; propv[int(i)] = pr["bone"]
for vi, bn in forced.items():
    for gi in [g.group for g in mesh.data.vertices[vi].groups]: mesh.vertex_groups[gi].remove([vi])  # ids first: removing invalidates the element refs
    mesh.vertex_groups[P + bn].add([vi], 1.0, 'REPLACE')
# faces bridging the static base and a moving prop (a spear butt resting on the plinth) would stretch: cut them
hand_pts = {bn: np.array(ad.bones[P + bn].head_local) / H for bn in set(propv.values())}
def far_from_grip(vs, bn): return min(np.linalg.norm(Vn[v] - hand_pts[bn]) for v in vs) > cfg.get("grip_r", 0.07)
cut = []
for p in mesh.data.polygons:
    vs = list(p.vertices); fz = {forced.get(v, "-") for v in vs}
    if "Root" in fz and any(forced.get(v, "Root") != "Root" for v in vs): cut.append(p.index); continue
    pb = {propv[v] for v in vs if v in propv}
    if len(pb) == 1 and len(pb) < len({propv.get(v, "-") for v in vs}) and far_from_grip(vs, next(iter(pb))): cut.append(p.index)
if cut:
    bm = bmesh.new(); bm.from_mesh(mesh.data); bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.faces[i] for i in cut], context='FACES_ONLY'); bm.to_mesh(mesh.data); bm.free()
bpy.ops.object.select_all(action='DESELECT'); mesh.select_set(True)
bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
unw = [v.index for v in mesh.data.vertices if not v.groups or sum(g.weight for g in v.groups) < 1e-4]
okv = [v for v in mesh.data.vertices if v.groups and sum(g.weight for g in v.groups) >= 1e-4]
kd = kdtree.KDTree(len(okv))
for i, v in enumerate(okv): kd.insert(v.co, i)
kd.balance()
for vi in unw:
    _, i, _ = kd.find(mesh.data.vertices[vi].co)
    for g in okv[i].groups: mesh.vertex_groups[g.group].add([vi], g.weight, 'REPLACE')
if "base_z" in cfg:  # re-pin the base by position: the passes above can leave blended weights on duplicate seam verts
    for v in mesh.data.vertices:
        if v.co.z < cfg["base_z"] * H and not (v.index in propv and propv[v.index] != "Root"):
            for gi in [g.group for g in v.groups]: mesh.vertex_groups[gi].remove([v.index])
            mesh.vertex_groups[P + "Root"].add([v.index], 1.0, 'REPLACE')
mesh.parent = arm; mod = mesh.modifiers.new("Armature", 'ARMATURE'); mod.object = arm
# ---------------------------------------------------------------- clips
rest = {b.name: b.matrix_local.copy() for b in ad.bones}
def lq(bn, axis, deg):
    la = rest[bn].to_3x3().inverted() @ AX[axis]; return Quaternion(la.normalized(), math.radians(deg))
def minz():
    dg = bpy.context.evaluated_depsgraph_get(); m = mesh.evaluated_get(dg); me = m.to_mesh()
    co = np.empty(len(me.vertices) * 3); me.vertices.foreach_get("co", co); m.to_mesh_clear(); return co[2::3].min()
hipH = J["Hips"]; pivot = Vector((hipH.x, hipH.y, 0)) + RF @ Vector((0, 0.06 * H, 0))
arm.animation_data_create(); report = {"clips": {}}
for cname, fn, n, loop, ground, ev in (C.creature_clip_set(cfg) if CREATURE else C.clip_set(cfg)):
    frames = list(range(0, n + 1)) if loop else list(range(0, n))
    keys = []; lifts = []
    # Earlier clips' NLA tracks would otherwise be evaluated on every depsgraph update and leak into the ground solve
    for tr_ in arm.animation_data.nla_tracks: tr_.mute = True
    arm.animation_data.action = None
    for t in frames:
        pose, hd = fn(t, n, cfg)
        if cfg.get("arm_down"):  # A-pose statues: lower the arms toward the body in every clip (applied before the clip)
            for s_, sg_ in (("Left", 1), ("Right", -1)):
                pose[s_ + "Arm"] = [("Y", sg_ * cfg["arm_down"])] + list(pose.get(s_ + "Arm", []))
        for pb in arm.pose.bones: pb.rotation_mode = 'QUATERNION'; pb.matrix_basis = Matrix.Identity(4)
        qs = {}
        for bone, lst in pose.items():
            if bone.startswith("_") or P + bone not in rest: continue
            q = Quaternion()
            for ax_, dg in lst: q = q @ lq(P + bone, ax_, dg)
            qs[P + bone] = q
        # hover clips: ground every frame like the rest, then add the clip's lift on top (lift(0) = 0), so no clip
        # starts above the tile or pops when it blends in from Idle, and none can sink
        lifts.append(hd[2]); loc = RF @ Vector((hd[0], hd[1], hd[2] if ground else 0.0)) * H
        if "_root" in pose:
            Rr = Matrix.Identity(3)
            for ax_, dg in pose["_root"]: Rr = Rr @ Matrix.Rotation(math.radians(dg), 3, AX[ax_])
            qs[P + "Hips"] = (rest[P + "Hips"].to_3x3().inverted() @ Rr @ rest[P + "Hips"].to_3x3()).to_quaternion() @ qs.get(P + "Hips", Quaternion())
            loc = loc + (pivot + Rr @ (hipH - pivot)) - hipH
        for bn, q in qs.items(): arm.pose.bones[bn].rotation_quaternion = q
        if cfg.get("anchored"): loc = Vector((0, 0, 0))  # seated on a fixed throne/base: rotate only, never translate the hips
        hb = arm.pose.bones[P + "Hips"]; hb.location = rest[P + "Hips"].to_3x3().inverted() @ loc
        if not cfg.get("anchored"):  # iterate: verts blended with the static Root follow the hips only partly
            for _ in range(6):
                arm.update_tag(); mesh.update_tag(); bpy.context.view_layer.update(); z = minz()
                if abs(z) < 1e-4 * H: break
                loc = loc - Vector((0, 0, z)); hb.location = rest[P + "Hips"].to_3x3().inverted() @ loc
        keys.append(({bn: q.copy() for bn, q in qs.items()}, hb.location.copy()))
    if not ground and not cfg.get("anchored"):
        l0 = min(lifts)
        keys = [(qs, lc + rest[P + "Hips"].to_3x3().inverted() @ Vector((0, 0, (lz - l0) * H))) for (qs, lc), lz in zip(keys, lifts)]
    act = bpy.data.actions.new(cname); act.use_fake_user = True; arm.animation_data.action = act
    for i, (qs, lc) in enumerate(keys):
        for pb in arm.pose.bones:
            pb.matrix_basis = Matrix.Identity(4)
        for bn, q in qs.items(): arm.pose.bones[bn].rotation_quaternion = q
        arm.pose.bones[P + "Hips"].location = lc
        for pb in arm.pose.bones:
            pb.keyframe_insert("rotation_quaternion", frame=i + 1)
            if pb.name == P + "Hips": pb.keyframe_insert("location", frame=i + 1)
    for l in act.layers:
        for s in l.strips:
            for cb in s.channelbags:
                for fc in cb.fcurves:
                    for kp in fc.keyframe_points: kp.interpolation = 'LINEAR'
    report["clips"][cname] = {"frames": len(keys), "seconds": round((len(keys) - 1) / C.FPS, 3), "loop": loop,
                              "events": {k: v + 1 for k, v in ev.items()}}
    tr = arm.animation_data.nla_tracks.new(); tr.name = cname; st = tr.strips.new(cname, 1, act); st.action_slot = act.slots[0]
    arm.animation_data.action = None
for tr_ in arm.animation_data.nla_tracks: tr_.mute = False
for pb in arm.pose.bones: pb.matrix_basis = Matrix.Identity(4)
report.update({"card": name, "H": H, "bones": len(ad.bones), "tris": sum(len(p.vertices) - 2 for p in mesh.data.polygons),
               "forced_rigid_verts": len(forced), "cut_bridge_faces": len(cut), "nearest_fixed": len(unw), "proxy_unweighted": proxy_unw,
               "max_influences": max(len(v.groups) for v in mesh.data.vertices), "attack_style": cfg.get("attack"),
               "loco": cfg.get("loco", "walk"), "facing_deg": cfg.get("facing", 0.0), "source": os.path.basename(src)})
json.dump(report, open(f"{outdir}/{name}_rig_report.json", "w"), indent=1)
bpy.ops.wm.save_as_mainfile(filepath=f"{outdir}/{name}_rigged.blend", compress=True)
for img in bpy.data.images:
    ext = {'JPEG': '.jpg', 'PNG': '.png'}.get(img.file_format, '.png')
    base = bpy.path.clean_name(img.name.rsplit('.', 1)[0] if img.name.lower().endswith(('.jpg', '.png')) else img.name)
    img.name = base + ext; img.filepath_raw = "//" + base + ext
bpy.ops.export_scene.fbx(filepath=f"{outdir}/{name}_rigged.fbx", object_types={'ARMATURE', 'MESH'}, use_selection=False,
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', add_leaf_bones=False,
    primary_bone_axis='Y', secondary_bone_axis='X', bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=True,
    bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, path_mode='COPY', embed_textures=True)
bpy.ops.export_scene.gltf(filepath=f"{outdir}/{name}_rigged.glb", export_format='GLB', export_animations=True, export_animation_mode='NLA_TRACKS')
print("DONE", json.dumps(report["clips"]))
