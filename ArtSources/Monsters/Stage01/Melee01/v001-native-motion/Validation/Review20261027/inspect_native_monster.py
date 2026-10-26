import bpy,json,pathlib,hashlib,math
import numpy as np
O=pathlib.Path(__file__).resolve().parent;P=O.parents[1];source=P/'Stage01_Melee01.blend'
original_sha=hashlib.sha256(source.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(source));s=bpy.context.scene
rig=next(o for o in s.objects if o.type=='ARMATURE');meshes=[o for o in s.objects if o.type=='MESH' and (o.parent==rig or any(m.type=='ARMATURE' and m.object==rig for m in o.modifiers))]
body=[o for o in meshes if o.name!='Melee01_Blade'];feet=[o for o in body if 'leg' in o.name.lower()]
def eval_pts(objs):
    dg=bpy.context.evaluated_depsgraph_get();pts=[]
    for o in objs:
        e=o.evaluated_get(dg);m=e.to_mesh();pts.extend(list(e.matrix_world@v.co) for v in m.vertices);e.to_mesh_clear()
    return np.asarray(pts,dtype=np.float32)
meshdata=[]
for o in meshes:
    bad_weights=0
    for v in o.data.vertices:
        w=sum(g.weight for g in v.groups)
        if v.groups and abs(w-1)>1e-4:bad_weights+=1
    meshdata.append({'name':o.name,'vertices':len(o.data.vertices),'triangles':sum(len(p.vertices)-2 for p in o.data.polygons),'materials':[m.name if m else None for m in o.data.materials],'modifiers':[{'type':m.type,'target':m.object.name if m.type=='ARMATURE' and m.object else None} for m in o.modifiers],'nonUnitWeightSumVertices':bad_weights})
clips=[]
for name in ['Idle','Move','Attack','Hit']:
    a=bpy.data.actions[name];rig.animation_data_create();rig.animation_data.action=a
    if a.slots:rig.animation_data.action_slot=a.slots[0]
    for tr in rig.animation_data.nla_tracks:tr.mute=True
    lo,hi=a.frame_range;rows=[];first=None;last=None;bone_bad=0
    for k in range(round((hi-lo)*2)+1):
        f=lo+k/2;s.frame_set(math.floor(f),subframe=f%1);bpy.context.view_layer.update();p=eval_pts(body);ft=eval_pts(feet)
        if first is None:first=p.copy()
        last=p.copy();finite=bool(np.isfinite(p).all());bone_values=np.asarray([v for b in rig.pose.bones for r in b.matrix for v in r]);bone_bad+=int(not np.isfinite(bone_values).all())
        rows.append({'frame':f,'time':(f-lo)/s.render.fps,'finite':finite,'minimumFootZ':float(ft[:,2].min()),'minimumBodyZ':float(p[:,2].min()),'maximumBodyZ':float(p[:,2].max())})
    clips.append({'name':name,'frameRange':[float(lo),float(hi)],'seconds':float((hi-lo)/s.render.fps),'samples60Hz':len(rows),'allSkinAndBoneValuesFinite':all(r['finite'] for r in rows) and bone_bad==0,'minimumFootZ':min(r['minimumFootZ'] for r in rows),'maximumBodyZ':max(r['maximumBodyZ'] for r in rows),'startEndMaxWorldVertexDifferenceM':float(np.max(np.abs(first-last))),'rows':rows})
assert hashlib.sha256(source.read_bytes()).hexdigest()==original_sha
out={'sourceBlendSHA256':original_sha,'method':'Read-only reopened saved native monster source. Evaluated whole skinned body and feet at 60 Hz, all four selected clips including endpoints. No pose edits or render-time repairs.','rig':rig.name,'bones':len(rig.data.bones),'fps':s.render.fps,'meshCount':len(meshes),'meshes':meshdata,'clips':clips,'sourceUnchanged':True,'scope':'Finite data, geometry existence, native clip timing, loop endpoints and ground reference. This is numerical evidence, not a naturalness score or a runtime collision/transition test.'}
(O/'native-monster-audit.json').write_text(json.dumps(out,indent=2),encoding='utf-8')
print(json.dumps({'rig':out['rig'],'bones':out['bones'],'meshCount':len(meshes),'clips':[{k:v for k,v in c.items() if k!='rows'} for c in clips]},indent=2))
