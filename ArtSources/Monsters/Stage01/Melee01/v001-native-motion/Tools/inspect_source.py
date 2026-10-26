import bpy, json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(ROOT/'SourceAssets/Skeleton_Minion.glb'))
report={'objects':[],'actions':[]}
for o in bpy.context.scene.objects:
    item={'name':o.name,'type':o.type,'parent':o.parent.name if o.parent else None,'location':list(o.location),'rotation':list(o.rotation_euler),'scale':list(o.scale),'dimensions':list(o.dimensions)}
    if o.type=='ARMATURE': item['bones']=[{'name':b.name,'head':list(b.head_local),'tail':list(b.tail_local),'parent':b.parent.name if b.parent else None} for b in o.data.bones]
    if o.type=='MESH': item.update(vertices=len(o.data.vertices),groups=[v.name for v in o.vertex_groups],materials=[m.name if m else None for m in o.data.materials])
    report['objects'].append(item)
for a in bpy.data.actions: report['actions'].append({'name':a.name,'range':list(a.frame_range),'slots':[s.identifier for s in a.slots]})
for m in bpy.data.materials:
    print('MATERIAL',m.name,[(n.type,n.name) for n in m.node_tree.nodes] if m.use_nodes else '')
(ROOT/'QA/source-inspection.json').write_text(json.dumps(report,indent=2))
print(json.dumps({'objects':report['objects'],'actions':report['actions'][:6]},indent=2))
