import bpy, sys, math
from mathutils import Vector
fbx,out=sys.argv[1],sys.argv[2]; shots=[s.split(':') for s in sys.argv[3:]]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=fbx)
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
bpy.context.view_layer.objects.active=arm; bpy.ops.object.mode_set(mode='EDIT')
for eb in arm.data.edit_bones: eb.use_connect=False
bpy.ops.object.mode_set(mode='OBJECT')
sc=bpy.context.scene; sc.world=bpy.data.worlds.new("w"); sc.world.color=(0.05,0.06,0.09)
sc.render.engine='BLENDER_WORKBENCH'; sc.display.shading.light='STUDIO'; sc.display.shading.color_type='TEXTURE'
sc.render.resolution_x=sc.render.resolution_y=560
cam=bpy.data.objects.new("c",bpy.data.cameras.new("c")); sc.collection.objects.link(cam); sc.camera=cam; cam.data.type='ORTHO'; cam.data.ortho_scale=2.6
acts={a.name.split('|')[-1]:a for a in bpy.data.actions}
for clip,f,az in shots:
    a=acts[clip]; arm.animation_data.action=a; arm.animation_data.action_slot=a.slots[0]; sc.frame_set(int(f))
    az=math.radians(float(az)); cam.location=(7*math.sin(az),-7*math.cos(az),1.6)
    cam.rotation_euler=(Vector((0,0,0.9))-cam.location).to_track_quat('-Z','Y').to_euler()
    sc.render.filepath=f"{out}_{clip}_{f}_{int(round(math.degrees(az)))}.png"; bpy.ops.render.render(write_still=True)
