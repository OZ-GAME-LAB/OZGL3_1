"""Read-only actual Unity Run skin-surface audit; writes JSON, never changes assets."""
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
import numpy as np

ROOT = Path(__file__).resolve().parents[4]
REPORT = Path(__file__).resolve().parent
INFO = json.loads((REPORT / 'run-only-review.json').read_text(encoding='utf-8-sig'))
PROBE = Path(INFO['geometryDirectory'])
SCHEMA = json.loads((PROBE / 'schema.json').read_text())['skins'][0]
FACES = np.array(SCHEMA['triangles']).reshape(-1, 3)
WEIGHTS = np.zeros((SCHEMA['vertexCount'], len(SCHEMA['bones'])))
for i, row in enumerate(SCHEMA['weights']):
    for bone, weight in zip(row['bones'], row['weights']): WEIGHTS[i, bone] += weight
def influence(names):
    return WEIGHTS[:, [i for i, name in enumerate(SCHEMA['bones']) if name in names]].sum(axis=1)
ARM = influence(['LeftArm','LeftForeArm','LeftHand','RightArm','RightForeArm','RightHand'])
LEFT = influence(['LeftForeArm','LeftHand']) > .65
RIGHT = influence(['RightForeArm','RightHand']) > .65
BODY_FACES = np.all(ARM[FACES] < .2, axis=1)
ARM_FACES = np.all(LEFT[FACES], axis=1) | np.all(RIGHT[FACES], axis=1)

def vectors(rows): return np.array([[row[k] for k in 'xyz'] for row in rows])
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()

def outer_hits(origins, direction, faces):
    a = faces[:,0]; e1=faces[:,1]-a; e2=faces[:,2]-a
    h=np.cross(direction,e2); det=np.einsum('ij,ij->i',e1,h)
    inv=np.divide(1.,det,out=np.zeros_like(det),where=np.abs(det)>1e-12)
    result=np.full(len(origins),np.nan)
    for start in range(0,len(origins),128):
        s=origins[start:start+128,None,:]-a[None,:,:]
        u=np.einsum('ijk,jk->ij',s,h)*inv; q=np.cross(s,e1)
        v=np.einsum('ijk,k->ij',q,direction)*inv
        t=np.einsum('ijk,jk->ij',q,e2)*inv
        valid=(np.abs(det)>1e-12)&(u>=-1e-7)&(v>=-1e-7)&(u+v<=1+1e-7)&(t>1e-7)
        hit=np.where(valid,t,-np.inf).max(axis=1);hit[~np.isfinite(hit)]=np.nan
        result[start:start+len(hit)]=hit
    return result

def segment_hits(start,end,triangle):
    d=end-start;a=triangle[:,0];e1=triangle[:,1]-a;e2=triangle[:,2]-a
    h=np.cross(d,e2);det=np.einsum('ij,ij->i',e1,h)
    inv=np.divide(1.,det,out=np.zeros_like(det),where=np.abs(det)>1e-12)
    s=start-a;u=np.einsum('ij,ij->i',s,h)*inv;q=np.cross(s,e1)
    v=np.einsum('ij,ij->i',d,q)*inv;t=np.einsum('ij,ij->i',e2,q)*inv
    return (np.abs(det)>1e-12)&(u>1e-7)&(v>1e-7)&(u+v<1-1e-7)&(t>1e-7)&(t<1-1e-7)

def sample(path):
    row=json.loads(path.read_text());p=vectors(row['skins'][0]['worldVertices']);m=row['markers']
    hip_y=m['hips']['y'];top=max(m['leftShoulder']['y'],m['rightShoulder']['y'])+.035
    tri=p[FACES];centres=tri.mean(axis=1)
    body_ids=np.where(BODY_FACES&(centres[:,1]>=hip_y-.065)&(centres[:,1]<=top))[0]
    body=tri[body_ids];arms=tri[ARM_FACES]
    if not len(body) or not len(arms): raise RuntimeError('Empty actual surface selection')
    overlap=np.all(arms.max(1)[:,None,:]>=body.min(1)[None,:,:]-1e-7,axis=2)&np.all(body.max(1)[None,:,:]>=arms.min(1)[:,None,:]-1e-7,axis=2)
    ai,bi=np.where(overlap);cross=np.zeros(len(ai),dtype=bool)
    for k in range(3):
        cross|=segment_hits(arms[ai,k],arms[ai,(k+1)%3],body[bi])
        cross|=segment_hits(body[bi,k],body[bi,(k+1)%3],arms[ai])
    coplanar=0
    if len(ai):
        normal=np.cross(arms[ai,1]-arms[ai,0],arms[ai,2]-arms[ai,0]);length=np.linalg.norm(normal,axis=1)
        dist=np.abs(np.einsum('ij,ij->i',body[bi,0]-arms[ai,0],normal))/np.maximum(length,1e-20)
        bn=np.cross(body[bi,1]-body[bi,0],body[bi,2]-body[bi,0]);parallel=np.linalg.norm(np.cross(normal,bn),axis=1)<1e-9
        coplanar=int(((dist<1e-7)&parallel).sum())
    gaps={}
    for side,mask in [('left',LEFT),('right',RIGHT)]:
        points=p[mask];sign=np.sign(-m[side+'Hand']['z']+m['hips']['z'])
        origins=points.copy();origins[:,2]=m['hips']['z'];direction=np.array([0.,0.,-sign])
        hits=outer_hits(origins,direction,body);distance=(points[:,2]-m['hips']['z'])*(-sign)
        values=distance-hits;finite=np.isfinite(values)
        gaps[side]={'selectedVertices':int(mask.sum()),'matchingBodyRays':int(finite.sum()),'minimumLateralGapM':float(values[finite].min()) if finite.any() else None,'negativeRaysBelowMinus0p1mm':int((values[finite]<-.0001).sum())}
    return {'frame':row['frame'],'time':row['time'],'probeSHA256':sha(path),'bodyTriangleCount':len(body),'distalArmTriangleCount':len(arms),'aabbCandidatePairs':len(ai),'noncoplanarTriangleCrossingPairs':int(cross.sum()),'coplanarCandidatePairsUnresolved':coplanar,'crossingBodyTriangleIds':body_ids[bi[cross]].tolist(),'crossingArmTriangleIds':np.where(ARM_FACES)[0][ai[cross]].tolist(),'gaps':gaps}

def main():
    files=sorted(PROBE.glob('[0-9]*.json'))
    with ThreadPoolExecutor(max_workers=4) as pool: rows=list(pool.map(sample,files))
    result={'method':'Actual complete Unity BakeMesh world triangles, actor lateral=-worldZ. Non-arm body faces require every vertex combined upper/lower/hand arm influence<.2, with centroidY from hips-.065 to max shoulderY+.035. Distal arm faces require every vertex forearm+hand influence>.65. Exact noncoplanar edge/triangle tests use both triangle edge sets after3D AABB broad phase; same-height/depth rays measure outermost actual body-face lateral clearance at each selected distal arm vertex.','limits':'A validated selected-surface scope, not a whole-character or continuous-time guarantee. Intentional shoulder attachment and transition-weight boundary faces are outside this distal-arm test. Empty selections raise errors. Coplanar candidates are reported unresolved. Positive ray gaps are lateral distances, not Euclidean distances. Arms outside bodyY/depth may have no matching ray; they still undergo the triangle-crossing test.','probeDirectory':str(PROBE),'runOnlyReviewSHA256':sha(REPORT/'run-only-review.json'),'captureReportSHA256':sha(REPORT/'review-report.json'),'schemaSHA256':sha(PROBE/'schema.json'),'clipSHA256':INFO['side']['sourceClipSHA256'],'frameCount':len(rows),'rows':rows}
    (REPORT/'run-actual-lateral-audit.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf8')
    for side in ['left','right']:
        values=[r['gaps'][side]['minimumLateralGapM'] for r in rows if r['gaps'][side]['minimumLateralGapM'] is not None]
        print(side,'minimum',min(values) if values else None,'negative',sum(r['gaps'][side]['negativeRaysBelowMinus0p1mm'] for r in rows))
    print('frames',len(rows),'crossings',sum(r['noncoplanarTriangleCrossingPairs'] for r in rows),'coplanarUnresolved',sum(r['coplanarCandidatePairsUnresolved'] for r in rows))
if __name__=='__main__':main()
