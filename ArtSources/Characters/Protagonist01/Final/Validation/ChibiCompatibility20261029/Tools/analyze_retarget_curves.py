from pathlib import Path
import json, re, math, sys
ROOT=Path(__file__).resolve().parents[7]
HERE=Path(__file__).resolve().parents[1]

def curves(path):
    text=path.read_text(encoding='utf-8-sig').split('  m_RotationCurves:',1)[1].split('  m_CompressedRotationCurves:',1)[0]
    result=[]
    for match in re.finditer(r'  - curve:\n(.*?)    path: (.*?)\n',text,re.S):
        keys=[]
        for key in re.finditer(r'        time: ([^\n]+)\n        value: \{x: ([^,]+), y: ([^,]+), z: ([^,]+), w: ([^}]+)\}',match[1]):
            nums=list(map(float,key.groups()));q=nums[1:];norm=math.sqrt(sum(v*v for v in q));keys.append((nums[0],[v/norm for v in q]))
        for (ta,qa),(tb,qb) in zip(keys,keys[1:]):
            angle=2*math.acos(min(1,abs(sum(a*b for a,b in zip(qa,qb)))))*180/math.pi
            result.append({'path':match[2],'from':ta,'to':tb,'angle':angle})
    return sorted(result,key=lambda x:x['angle'],reverse=True)

out={}
mapping={'LeftArm':'upperarm.l','RightArm':'upperarm.r','LeftForeArm':'lowerarm.l','RightForeArm':'lowerarm.r','LeftHand':'hand.l','RightHand':'hand.r','LeftUpLeg':'upperleg.l','RightUpLeg':'upperleg.r','LeftLeg':'lowerleg.l','RightLeg':'lowerleg.r','LeftFoot':'foot.l','RightFoot':'foot.r','Hips':'hips','Head':'head'}
comparisons={}
for name in ['Chop','Slice','Stab']:
    out[name]={}
    full={}
    for key,folder in [('source','Combat'),('target','ChibiMotion_v007')]:
        path=ROOT/f'Assets/Art/Characters/Protagonist01/{folder}/Clips/Longsword_Attack_{name}.anim'
        full[key]=curves(path)
        out[name][key]=full[key][:16]
    comparisons[name]=[]
    for targetBone,sourceBone in mapping.items():
        sourceRows=[r for r in full['source'] if r['path'].split('/')[-1]==sourceBone]
        targetRows=[r for r in full['target'] if r['path'].split('/')[-1]==targetBone]
        if not sourceRows or not targetRows: continue
        additions=[]
        for tr in targetRows:
            matching=[sr for sr in sourceRows if abs(sr['from']-tr['from'])<1e-5 and abs(sr['to']-tr['to'])<1e-5]
            if matching:
                additions.append({'from':tr['from'],'to':tr['to'],'source':matching[0]['angle'],'target':tr['angle'],'added':tr['angle']-matching[0]['angle']})
        comparisons[name].append({'targetBone':targetBone,'sourceBone':sourceBone,'sourcePeak':sourceRows[0],'targetPeak':targetRows[0],'largestAddedStep':max(additions,key=lambda r:r['added']) if additions else None})
filename=sys.argv[1] if len(sys.argv)>1 else 'retarget-key-step-diagnosis.json'
(HERE/'QA'/filename).write_text(json.dumps({'topSteps':out,'perBoneComparisons':comparisons},indent=2)+'\n',encoding='utf-8')
for name,data in out.items():
    print(name)
    for key,rows in data.items():
        print(key,[(row['path'].split('/')[-1],round(row['from'],4),round(row['to'],4),round(row['angle'],2)) for row in rows[:8]])
