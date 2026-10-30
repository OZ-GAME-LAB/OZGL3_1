"""Read default native imports and save import-only editable copies.

Never changes objects, rest bones, vertex groups, materials, animation keys,
scale, world position or floor offsets. Raw inputs remain byte-identical.
"""
from pathlib import Path
import bpy,json,sys,hashlib
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[1]

def vector(v):return [float(n) for n in v]
def matrix(m):return [vector(row) for row in m]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def bounds(objects):
    pts=[o.matrix_world@v.co for o in objects if o.type=='MESH' for v in o.data.vertices]
    return {'min':[min(p[k] for p in pts) for k in range(3)],'max':[max(p[k] for p in pts) for k in range(3)],'size':[max(p[k] for p in pts)-min(p[k] for p in pts) for k in range(3)]} if pts else None
def inspect(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if path.suffix.lower()=='.blend':bpy.ops.wm.open_mainfile(filepath=str(path))
    elif path.suffix.lower()=='.fbx':bpy.ops.import_scene.fbx(filepath=str(path))
    else:bpy.ops.import_scene.gltf(filepath=str(path))
    fps=bpy.context.scene.render.fps/bpy.context.scene.render.fps_base
    rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE']
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    actions=[]
    for a in bpy.data.actions:
        keys=0;curves=0
        for layer in a.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    curves+=len(bag.fcurves);keys+=sum(len(fc.keyframe_points) for fc in bag.fcurves)
        lo,hi=a.frame_range
        actions.append({'name':a.name,'frame_start':float(lo),'frame_end':float(hi),'duration_seconds':float((hi-lo)/fps),'slots':[s.identifier for s in a.slots],'fcurves':curves,'keyframe_points':keys})
    skinned=[o for o in meshes if any(m.type=='ARMATURE' for m in o.modifiers)]
    return {'source_file':str(path.relative_to(ROOT)),'source_sha256':sha(path),'blender':bpy.app.version_string,'native_import_settings':'Default Blender importer, no post-import changes to asset data. Blender performs standard file-axis and unit conversion.','fps':fps,'fps_integer':bpy.context.scene.render.fps,'fps_base':bpy.context.scene.render.fps_base,'unit_scale_length':bpy.context.scene.unit_settings.scale_length,'mesh_count':len(meshes),'raw_bind_mesh_world_bounds':bounds(meshes),'character_bind_mesh_world_bounds':bounds(skinned),'unbound_meshes':[o.name for o in meshes if o not in skinned],'objects':[{'name':o.name,'type':o.type,'parent':o.parent.name if o.parent else None,'hide_render':o.hide_render,'location':vector(o.location),'rotation_euler':vector(o.rotation_euler),'scale':vector(o.scale),'matrix_world':matrix(o.matrix_world),'vertices':len(o.data.vertices) if o.type=='MESH' else None,'materials':[m.name if m else None for m in o.data.materials] if o.type=='MESH' else []} for o in bpy.context.scene.objects],'rigs':[{'name':o.name,'bone_count':len(o.data.bones),'active_action':o.animation_data.action.name if o.animation_data and o.animation_data.action else None,'bones':[{'name':b.name,'parent':b.parent.name if b.parent else None,'head_local':vector(b.head_local),'tail_local':vector(b.tail_local),'matrix_local':matrix(b.matrix_local),'use_deform':b.use_deform} for b in o.data.bones]} for o in rigs],'action_count':len(actions),'actions':actions,'images':[{'name':im.name,'filepath':im.filepath,'packed':bool(im.packed_file),'size':list(im.size)} for im in bpy.data.images]}

mode=sys.argv[sys.argv.index('--')+1] if '--' in sys.argv else 'fbx'
if mode=='fbx':
    path=ROOT/'SourceAssets/Characters/fbx/Skeleton_Rogue.fbx'
    report=inspect(path)
    (ROOT/'QA/original-fbx-rogue-import.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print('ORIGINAL_FBX_IMPORTED',report['fps'],report['action_count'],report['raw_bind_mesh_world_bounds'],flush=True)
    for name in ['Skeleton_Crossbow','Skeleton_Axe','Skeleton_Staff','Skeleton_Quiver','Skeleton_Arrow']:
        accessory=inspect(ROOT/f'SourceAssets/Accessories/fbx/{name}.fbx')
        (ROOT/f'QA/original-{name}-import.json').write_text(json.dumps(accessory,indent=2),encoding='utf-8')
        print('ORIGINAL_ACCESSORY_IMPORTED',name,accessory['raw_bind_mesh_world_bounds'],flush=True)
elif mode=='editable':
    for name in ['Skeleton_Rogue','Skeleton_Warrior','Skeleton_Mage']:
        path=ROOT/f'SourceAssets/Characters/gltf/{name}.glb'
        report=inspect(path)
        target=ROOT/f'Editable/{name}_Original_GLTF.blend'
        if target.exists():raise FileExistsError('Refusing to overwrite import-only editable copy')
        bpy.ops.wm.save_as_mainfile(filepath=str(target),compress=True)
        report['editable_file']=str(target.relative_to(ROOT));report['editable_sha256']=sha(target)
        report['post_import_model_changes']=False;report['animation_key_changes']=False
        (ROOT/f'QA/original-{name}-gltf-import.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
        print('IMPORT_ONLY_EDITABLE_SAVED',name,report['fps'],report['action_count'],report['raw_bind_mesh_world_bounds'],flush=True)
elif mode=='reopen':
    rows=[]
    for name in ['Skeleton_Rogue','Skeleton_Warrior','Skeleton_Mage']:
        report=inspect(ROOT/f'Editable/{name}_Original_GLTF.blend')
        previous=json.loads((ROOT/f'QA/original-{name}-gltf-import.json').read_text())
        checks={'same_actions':report['actions']==previous['actions'],'same_rest_bones':report['rigs']==previous['rigs'],'same_fps':report['fps']==previous['fps'],'same_images':report['images']==previous['images']}
        if not all(checks.values()):raise RuntimeError(f'Saved import fidelity mismatch: {name}, {checks}')
        summary={'character':name,'saved_blend':report['source_file'],'sha256':report['source_sha256'],'action_count':report['action_count'],'bone_count':report['rigs'][0]['bone_count'],'character_bind_mesh_world_bounds':report['character_bind_mesh_world_bounds'],'unbound_meshes':report['unbound_meshes'],'checks':checks,'asset_data_modified':False}
        rows.append(summary)
        print('IMPORT_ONLY_REOPEN_VERIFIED',json.dumps(summary),flush=True)
    (ROOT/'QA/import-only-reopen-verification.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
else:raise ValueError(mode)
