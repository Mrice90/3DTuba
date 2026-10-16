import bpy, sys
bpy.ops.wm.open_mainfile(filepath=sys.argv[1])
for a in bpy.data.actions:
    fcs = a.fcurves if hasattr(a,'fcurves') and len(a.fcurves) else [fc for l in a.layers for s in l.strips for cb in s.channelbags for fc in cb.fcurves]
    paths=set(fc.data_path.split('"')[1] if '"' in fc.data_path else fc.data_path for fc in fcs)
    loc=[(fc.data_path,fc.array_index,round(fc.evaluate(a.frame_range[0]),4),round(fc.evaluate(a.frame_range[1]),4)) for fc in fcs if fc.data_path.endswith('location')]
    print(a.name, a.frame_range[:], len(fcs), len(paths), loc[:6])
