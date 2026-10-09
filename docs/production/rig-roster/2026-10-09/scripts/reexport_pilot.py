# Re-export the Keraunos pilot FBX with extension-bearing embedded texture names (Unity texture fix).
import bpy, sys
blend, out = sys.argv[-2], sys.argv[-1]
bpy.ops.wm.open_mainfile(filepath=blend)
for img in bpy.data.images:
    ext={'JPEG':'.jpg','PNG':'.png'}.get(img.file_format,'.png')
    base=bpy.path.clean_name(img.name.rsplit('.',1)[0] if img.name.lower().endswith(('.jpg','.png')) else img.name)
    img.name=base+ext; img.filepath_raw="//"+base+ext
bpy.ops.export_scene.fbx(filepath=out, object_types={'ARMATURE','MESH'},
    use_selection=False, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
    add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
    bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=True, bake_anim_use_all_actions=False,
    bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, path_mode='COPY', embed_textures=True)
