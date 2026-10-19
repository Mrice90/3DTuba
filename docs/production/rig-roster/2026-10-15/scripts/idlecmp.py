# How far is each clip's first frame from the in-game token pose? Re-imports the rigged FBX, evaluates frame 1 of
# every take, and reports the mean / 95th percentile vertex offset in units of model height (same vertex order).
import bpy, sys, json, numpy as np
fbx = sys.argv[sys.argv.index('--') + 1]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=fbx)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]; mesh = [o for o in bpy.data.objects if o.type == 'MESH'][0]
bpy.context.view_layer.objects.active = arm; bpy.ops.object.mode_set(mode='EDIT')
for eb in arm.data.edit_bones: eb.use_connect = False
bpy.ops.object.mode_set(mode='OBJECT')
def ev():
    dg = bpy.context.evaluated_depsgraph_get(); m = mesh.evaluated_get(dg); me = m.to_mesh()
    co = np.empty(len(me.vertices) * 3); me.vertices.foreach_get("co", co); m.to_mesh_clear()
    return co.reshape(-1, 3) @ np.array(mesh.matrix_world.to_3x3()).T + np.array(mesh.matrix_world.translation)
arm.animation_data.action = None
for t in arm.animation_data.nla_tracks: t.mute = True
for pb in arm.pose.bones: pb.matrix_basis.identity()
bpy.context.view_layer.update(); R = ev(); H = np.ptp(R[:, 2]); R0 = R - [0, 0, R[:, 2].min()]
out = {}
for a in bpy.data.actions:
    arm.animation_data.action = a
    if a.slots: arm.animation_data.action_slot = a.slots[0]
    bpy.context.scene.frame_set(int(a.frame_range[0])); V = ev()
    d = np.linalg.norm(V - R0, axis=1) / H
    out[a.name.split('|')[-1]] = {"mean": round(float(d.mean()), 4), "p95": round(float(np.percentile(d, 95)), 4), "startZ": round(float(V[:, 2].min() / H), 4)}
print("IDLECMP", json.dumps(out))
