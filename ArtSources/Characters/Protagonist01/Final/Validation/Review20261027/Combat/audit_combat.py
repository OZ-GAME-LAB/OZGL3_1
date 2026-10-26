"""Read-only evaluated geometry audit and full-frame review; never saves source .blend."""
import bpy, json, math, sys, hashlib
import numpy as np
from pathlib import Path
from mathutils import Vector

OUT = Path(__file__).parent
ROOT = OUT.parents[2]
SCENE = bpy.context.scene
RIG = bpy.data.objects['Hero_KayKit_Rig']
MESHES = [bpy.data.objects[n] for n in ('Chibi_Body_v009','Mitten_R','Mitten_L','Longsword')]
CLIPS = json.loads((ROOT/'Combat/clips.json').read_text(encoding='utf8'))
CAPTURE = OUT.parents[6]/'Logs'/'AssetAudit20261027'/'CombatFrames'
# A CLI argument can override the ignored capture folder; retained outputs are summary and contact sheets.
if '--' in sys.argv:
    args = sys.argv[sys.argv.index('--')+1:]
    CAPTURE = Path(args[0])
    if len(args)>1:
        OUT=Path(args[1]);OUT.mkdir(parents=True,exist_ok=True)
CAPTURE.mkdir(parents=True, exist_ok=True)

def bind(c):
    for objname, actionname in c['actions'].items():
        o = bpy.data.objects[objname]
        o.animation_data_create()
        a = bpy.data.actions[actionname]
        o.animation_data.action = a
        o.animation_data.action_slot = a.slots[0]
    SCENE.frame_set(c['start'])

def coords(o):
    e = o.evaluated_get(bpy.context.evaluated_depsgraph_get())
    m = e.to_mesh()
    a = np.array([e.matrix_world @ v.co for v in m.vertices],dtype=float)
    e.to_mesh_clear()
    return a

static = []
edges = {}
for o in MESHES:
    edges[o.name] = np.array([e.vertices[:] for e in o.data.edges],dtype=int)
    weights = [sum(g.weight for g in v.groups) for v in o.data.vertices]
    static.append({'mesh':o.name, 'vertices':len(o.data.vertices),'faces':len(o.data.polygons),
                   'weightMin':min(weights),'weightMax':max(weights),
                   'unweightedVertices':sum(w < 1e-6 for w in weights) if o.modifiers else 0,
                   'armatureModifiers':[m.object.name if m.object else None for m in o.modifiers if m.type=='ARMATURE']})

results=[]
for c in CLIPS:
    bind(c)
    rows=[]; prev=None; first=None; last=None
    refedges={}
    for o in MESHES:
        a=coords(o); ed=edges[o.name];refedges[o.name]=np.linalg.norm(a[ed[:,0]]-a[ed[:,1]],axis=1)
    for frame in np.arange(c['start'],c['end']+0.01,0.5):
        whole=int(frame);SCENE.frame_set(whole,subframe=float(frame-whole))
        row={'frame':float(frame),'meshes':{},'jointAngles':{}}
        current={}
        for o in MESHES:
            a=coords(o); current[o.name]=a; ed=edges[o.name]
            lengths=np.linalg.norm(a[ed[:,0]]-a[ed[:,1]],axis=1)
            valid=refedges[o.name]>1e-5
            ratio=lengths[valid]/refedges[o.name][valid]
            row['meshes'][o.name]={'finite':bool(np.isfinite(a).all()),'boundsMin':a.min(0).tolist(),'boundsMax':a.max(0).tolist(),
                                  'maxVertexStep':float(np.linalg.norm(a-prev[o.name],axis=1).max()) if prev else 0,
                                  'minEdgeRatioToStart':float(ratio.min()),'maxEdgeRatioToStart':float(ratio.max())}
        for side in ('l','r'):
            b=RIG.pose.bones
            u=b['upperarm.'+side].head; e=b['lowerarm.'+side].head; w=b['wrist.'+side].head; h=b['hand.'+side].tail
            row['jointAngles']['elbow.'+side]=math.degrees((u-e).angle(w-e))
            row['jointAngles']['wristDeflection.'+side]=math.degrees((w-e).angle(h-w))
            handmid=(current['Mitten_'+side.upper()].min(0)+current['Mitten_'+side.upper()].max(0))/2
            grip=bpy.data.objects['Grip_'+side.upper()].matrix_world.translation
            row['jointAngles']['handCenterToGrip.'+side]=float(np.linalg.norm(handmid-np.array(grip)))
        if first is None:first=current
        last=current;prev=current;rows.append(row)
    results.append({'clip':c['name'],'samples120Hz':len(rows),
                    'endpointMaxVertexDifference':{n:float(np.linalg.norm(last[n]-first[n],axis=1).max()) for n in first},
                    'samples':rows})
(OUT/'combat-numeric-audit.json').write_text(json.dumps({'sourceBlendSHA256':hashlib.sha256(Path(bpy.data.filepath).read_bytes()).hexdigest(),'method':'Evaluate all four visible meshes at 120Hz; edge ratios are relative to clip start, not anatomical quality verdicts. Joint angles and grip distances are diagnostic, not standalone pass criteria.','staticMeshes':static,'clips':results},indent=2),encoding='utf8')
if '--numeric-only' in sys.argv:
    print('COMBAT_NUMERIC_AUDIT_COMPLETE',flush=True)
    sys.exit(0)

# Textured workbench provides direct evaluated mesh evidence independent of Unity render setup.
SCENE.render.engine='BLENDER_WORKBENCH'
SCENE.render.resolution_x=360;SCENE.render.resolution_y=360;SCENE.render.resolution_percentage=100
SCENE.render.image_settings.file_format='PNG'
SCENE.render.film_transparent=False
SCENE.display.shading.light='STUDIO'
SCENE.display.shading.color_type='TEXTURE'
SCENE.display.shading.show_shadows=True
SCENE.display.shading.show_cavity=True
SCENE.display.shading.cavity_type='BOTH'
SCENE.display.shading.background_type='WORLD'
SCENE.world.color=(0.075,0.085,0.10)
views=['Camera_Side','Camera_Front','Camera_Opposite']
for c in CLIPS:
    bind(c)
    for view in views:
        SCENE.camera=bpy.data.objects[view]
        directory=CAPTURE/c['name']/view;directory.mkdir(parents=True,exist_ok=True)
        for frame in range(c['start'],c['end']+1):
            SCENE.frame_set(frame)
            SCENE.render.filepath=str(directory/f'{frame:04d}.png')
            bpy.ops.render.render(write_still=True)
    print('AUDITED_AND_CAPTURED '+c['name'],flush=True)
print('COMBAT_AUDIT_COMPLETE',flush=True)
