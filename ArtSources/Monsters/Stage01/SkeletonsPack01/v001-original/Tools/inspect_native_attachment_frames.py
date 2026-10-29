"""Read matching native socket/accessory frames in a common Blender world basis.

No weapon is attached and no transform is edited; this is evidence for the
preview wrapper author, not a guessed attachment rotation.
"""
from pathlib import Path
import bpy,json,math
from mathutils import Matrix,Vector
from mathutils.kdtree import KDTree
ROOT=Path(__file__).resolve().parents[1]
def vec(v):return [float(x) for x in v]
def mat(m):return [vec(row) for row in m]
def load(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if path.suffix=='.fbx':bpy.ops.import_scene.fbx(filepath=str(path))
    else:bpy.ops.import_scene.gltf(filepath=str(path))
    bpy.context.view_layer.update()

def character(kind):
    ext='fbx' if kind=='fbx' else 'glb'
    load(ROOT/f'SourceAssets/Characters/{kind}/Skeleton_Rogue.{ext}')
    rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    sockets={}
    for name in ['handslot.r','handslot.l']:
        b=rig.data.bones[name];world=rig.matrix_world@b.matrix_local
        sockets[name]={'bone_parent':b.parent.name,'head_world':vec(rig.matrix_world@b.head_local),'tail_world':vec(rig.matrix_world@b.tail_local),'head_frame_world':mat(world),'local_axes_world':{axis:vec(world.to_3x3()@Vector(basis)) for axis,basis in [('X',(1,0,0)),('Y',(0,1,0)),('Z',(0,0,1))]}}
    helper=[]
    for o in bpy.context.scene.objects:
        if 'icosphere' in o.name.lower():helper.append({'name':o.name,'type':o.type,'parent':o.parent.name if o.parent else None,'parent_type':o.parent_type,'location':vec(o.location),'scale':vec(o.scale),'vertices':len(o.data.vertices) if o.type=='MESH' else None,'hide_render':o.hide_render,'armature_modifier':any(m.type=='ARMATURE' for m in o.modifiers),'materials':[m.name if m else None for m in o.data.materials] if o.type=='MESH' else []})
    return {'format':kind,'rig_rotation_euler':vec(rig.rotation_euler),'rig_scale':vec(rig.scale),'sockets':sockets,'icosphere_objects':helper}

def accessory(name,kind):
    extension='fbx' if kind=='fbx' else 'gltf'
    load(ROOT/f'SourceAssets/Accessories/{kind}/{name}.{extension}')
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
    points=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
    lo=[min(p[i] for p in points) for i in range(3)];hi=[max(p[i] for p in points) for i in range(3)]
    return {'format':kind,'objects':[{'name':o.name,'parent':o.parent.name if o.parent else None,'root_matrix_world':mat(o.matrix_world),'location':vec(o.location),'rotation_euler':vec(o.rotation_euler),'scale':vec(o.scale)} for o in objects],'bounds_min':lo,'bounds_max':hi,'size':[hi[i]-lo[i] for i in range(3)],'world_positions':[vec(p) for p in points],'unique_rounded_world_positions':sorted(set(tuple(round(float(x),5) for x in p) for p in points))}

def nearest_max(points,target):
    kd=KDTree(len(target))
    for i,p in enumerate(target):kd.insert(Vector(p),i)
    kd.balance()
    return max(kd.find(Vector(p))[2] for p in points)

characters={kind:character(kind) for kind in ['fbx','gltf']}
socket_differences={name:max(abs(characters['fbx']['sockets'][name]['head_frame_world'][i][j]-characters['gltf']['sockets'][name]['head_frame_world'][i][j]) for i in range(4) for j in range(4)) for name in ['handslot.r','handslot.l']}
accessories=[]
for name in ['Skeleton_Crossbow','Skeleton_Axe','Skeleton_Staff','Skeleton_Quiver','Skeleton_Arrow']:
    formats={kind:accessory(name,kind) for kind in ['fbx','gltf']}
    positions={k:formats[k].pop('unique_rounded_world_positions') for k in formats}
    actual={k:formats[k].pop('world_positions') for k in formats}
    difference=max(nearest_max(actual['fbx'],actual['gltf']),nearest_max(actual['gltf'],actual['fbx']))
    accessories.append({'name':name,'formats':formats,'same_unique_positions_at_1e_5m':positions['fbx']==positions['gltf'],'max_bidirectional_vertex_position_difference_m':difference,'rounded_unique_position_counts':{k:len(v) for k,v in positions.items()}})
report={'method':'Default FBX and GLTF import, read native rest bone head matrices and accessory root matrices in Blender Z-up world. No asset data or attachment transform edited.','characters':characters,'socket_head_frame_maximum_component_difference':socket_differences,'accessories':accessories,'interpretation':['Original Rogue FBX contains no Icosphere; original GLB contains an unparented, unskinned Icosphere. Intent as a rig-shape/helper is an inference, not explicit author metadata.','Character rigid Hood/Cape/Helmet/Hat parts must not be excluded by a skinned-only filter.','Blender bone-parenting may introduce a tail/axis convention different from a Unity Transform. Do not transfer a previous Blender-only -90 degree correction to the original Unity FBX socket without inspecting its actual imported head frame.','No canonical assembled weapon-to-socket transform is provided in these separate source files. This report does not claim a visually approved or source-authored grip angle.']}
(ROOT/'QA/native-attachment-frames.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('NATIVE_ATTACHMENT_FRAMES_READY',json.dumps(socket_differences),flush=True)
