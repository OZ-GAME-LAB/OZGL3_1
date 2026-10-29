"""Read saved Unity assets only; do not alter controllers, clips or runtime code."""
from pathlib import Path
import re, json, math, hashlib, subprocess

ROOT = next(p for p in Path(__file__).resolve().parents if (p/'Assets').is_dir())
OUT = Path(__file__).parent
CLIPS = ROOT/'Assets/Art/Characters/Protagonist01/ChibiMotion_v007/Clips'
def numbers(s): return {k:float(v) for k,v in re.findall(r'([xyzw]): ([^,}]+)',s)}
def curve_first(text, key):
    part = text.split('  '+key+':',1)[1].split('\n  m_',1)[0]
    result = {}
    for block in part.split('\n  - curve:')[1:]:
        path = re.search(r'\n    path: (.*)',block).group(1)
        vals = numbers(re.search(r'\n        value: (\{.*?\})',block).group(1))
        result[path] = vals
    return result
def qmul(a,b):
    x,y,z,w=a; X,Y,Z,W=b
    return (w*X+x*W+y*Z-z*Y,w*Y-x*Z+y*W+z*X,w*Z+x*Y-y*X+z*W,w*W-x*X-y*Y-z*Z)
def qrotate(q,v): return qmul(qmul(q,(*v,0)),(-q[0],-q[1],-q[2],q[3]))[:3]
def world_pose(text):
    rot,pos,scale=[curve_first(text,k) for k in ['m_RotationCurves','m_PositionCurves','m_ScaleCurves']]
    world={'':((0,0,0),(0,0,0,1),(1,1,1))}
    for path in sorted(pos,key=lambda x:(x.count('/'),x)):
        parent=path.rpartition('/')[0]
        pp,pq,ps=world[parent]
        lp=tuple(pos[path][k] for k in 'xyz'); lq=tuple(rot[path][k] for k in 'xyzw'); ls=tuple(scale[path][k] for k in 'xyz')
        d=qrotate(pq,tuple(lp[i]*ps[i] for i in range(3)))
        world[path]=(tuple(pp[i]+d[i] for i in range(3)),qmul(pq,lq),tuple(ps[i]*ls[i] for i in range(3)))
    return {path.rsplit('/',1)[-1]:{'position':list(value[0]),'rotation':list(value[1])} for path,value in world.items() if path}

clips={}
names=['Idle_Fists','Jump_Takeoff','Jump_Rise_Loop','Jump_Apex_Loop','Jump_Fall_Loop','Jump_Land','Jump_Full']
for name in names:
    file=CLIPS/(name+'.anim'); text=file.read_text(encoding='utf-8-sig')
    clips[name]={'path':file.relative_to(ROOT).as_posix(),'sha256':hashlib.sha256(file.read_bytes()).hexdigest(),
        'duration':float(re.search(r'm_StopTime: (\S+)',text).group(1)),
        'loop':bool(int(re.search(r'm_LoopTime: (\d)',text).group(1))), 'firstFrameWorldPose':world_pose(text)}

controller_path=ROOT/'Assets/Anim/Player_Chibi.controller'
text=controller_path.read_text(encoding='utf-8-sig')
docs={m.group(2):(m.group(1),m.group(3)) for m in re.finditer(r'^--- !u!(\d+) &(-?\d+)\n(.*?)(?=^---|\Z)',text,re.M|re.S)}
states={}
for fid,(type_id,block) in docs.items():
    if type_id!='1102': continue
    name=re.search(r'  m_Name: (.*)',block).group(1)
    states[fid]={'name':name,'speed':float(re.search(r'  m_Speed: (\S+)',block).group(1)),'motion':re.search(r'  m_Motion: (.*)',block).group(1),'transitions':re.findall(r'- \{fileID: (-?\d+)\}',block.split('m_Transitions:',1)[1].split('m_StateMachineBehaviours:',1)[0])}
for row in states.values():
    ts=[]
    for tid in row['transitions']:
        b=docs[tid][1]; dest=re.search(r'm_DstState: \{fileID: (-?\d+)\}',b).group(1)
        ts.append({'id':tid,'destination':states.get(dest,{}).get('name',dest),'conditions':re.findall(r'm_ConditionEvent: (.*)',b),
            'duration':float(re.search(r'm_TransitionDuration: (\S+)',b).group(1)),
            'hasExitTime':bool(int(re.search(r'm_HasExitTime: (\S+)',b).group(1))),
            'exitTime':float(re.search(r'm_ExitTime: (\S+)',b).group(1))})
    row['transitions']=ts

markers=['Hips','Head','LeftHand','RightHand','LeftLeg','RightLeg','LeftFoot','RightFoot']
differences={}
for a,b in [('Jump_Rise_Loop','Jump_Fall_Loop'),('Jump_Rise_Loop','Jump_Apex_Loop'),('Jump_Fall_Loop','Jump_Apex_Loop')]:
    rows={}
    for mark in markers:
        pa=clips[a]['firstFrameWorldPose'][mark]['position']; pb=clips[b]['firstFrameWorldPose'][mark]['position']
        rows[mark]={'deltaXYZ':[pb[i]-pa[i] for i in range(3)],'distance':math.dist(pa,pb),'sideViewDistanceYZ':math.hypot(pa[1]-pb[1],pa[2]-pb[2])}
    differences[a+'_vs_'+b]=rows

result={'baselineCommit':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
    'controller':controller_path.relative_to(ROOT).as_posix(),'controllerSHA256':hashlib.sha256(controller_path.read_bytes()).hexdigest(),
    'blendTrees':len([1 for kind,_ in docs.values() if kind=='206']),
    'states':{row['name']:row for row in states.values()},'clips':clips,'firstFrameMarkerDifferences':differences,
    'physics':{'jumpVelocity':12,'gravityY':-9.81,'linearDamping':0,'approxApexTimeFromImpulse':12/9.81,'approxApexHeightAboveTakeoff':12**2/(2*9.81),'approxAirTimeSameLandingHeight':24/9.81,'landStateGameplayDuration':0.1},
    'note':'Transform math is offline numerical evidence from serialized first-key curves, not a Unity Animator evaluation or gameplay recording.'}
(OUT/'saved-state-diagnosis.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'blendTrees':result['blendTrees'],'states':{k:v for k,v in result['states'].items() if k in ['Idle','Jump','Air','Land']},'clipDuration':{k:v['duration'] for k,v in clips.items()},'differences':differences['Jump_Rise_Loop_vs_Jump_Fall_Loop'],'physics':result['physics']},indent=2))
