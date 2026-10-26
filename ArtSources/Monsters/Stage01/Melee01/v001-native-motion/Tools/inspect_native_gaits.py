import bpy,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.context.scene.render.fps=30
bpy.ops.import_scene.gltf(filepath=str(ROOT/'SourceAssets/Skeleton_Minion.glb'))
r=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
for t in list(r.animation_data.nla_tracks):r.animation_data.nla_tracks.remove(t)
scale=json.loads((ROOT/'QA/build-manifest.json').read_text())['scale'];r.scale=(scale,)*3
feet=[o for o in r.children if 'Leg' in o.name]
def minz():
 dg=bpy.context.evaluated_depsgraph_get();v=[]
 for o in feet:
  e=o.evaluated_get(dg);m=e.to_mesh();v.extend((e.matrix_world@x.co).z for x in m.vertices);e.to_mesh_clear()
 return min(v)
rows=[]
for name in ['Idle_Combat','Running_A','Running_B','Running_C','Walking_A','Walking_B','Walking_C','Walking_D_Skeletons']:
 a=bpy.data.actions[name];r.animation_data.action=a;r.animation_data.action_slot=a.slots[0];values=[]
 for f in range(int(a.frame_range[1])+1):bpy.context.scene.frame_set(f);values.append(minz())
 rows.append({'clip':name,'min':min(values),'max':max(values),'at_frame':values.index(min(values))})
print('GAITS',json.dumps(rows));(ROOT/'QA/native-gait-ground.json').write_text(json.dumps(rows,indent=2))
