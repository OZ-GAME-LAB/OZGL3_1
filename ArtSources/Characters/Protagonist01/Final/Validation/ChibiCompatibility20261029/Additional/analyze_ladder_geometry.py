"""Read-only exact surface diagnostic for Unity-exported deformed head/fist triangles."""
from pathlib import Path
import argparse, json, math
import numpy as np

EPS = 1e-7

def xyz(values):
    return np.asarray([[p['x'], p['y'], p['z']] for p in values], dtype=np.float64)

def ray_hits(origins, directions, triangles, segment=False):
    e1=triangles[:,1]-triangles[:,0]; e2=triangles[:,2]-triangles[:,0]
    h=np.cross(directions,e2); det=np.einsum('ij,ij->i',e1,h)
    good=np.abs(det)>1e-12; inv=np.zeros_like(det); inv[good]=1/det[good]
    s=origins-triangles[:,0]; u=np.einsum('ij,ij->i',s,h)*inv
    q=np.cross(s,e1); v=np.einsum('ij,ij->i',directions,q)*inv
    distance=np.einsum('ij,ij->i',e2,q)*inv
    hit=good & (u>=-EPS) & (v>=-EPS) & ((u+v)<=1+EPS) & (distance>EPS)
    if segment: hit &= distance<1-EPS
    return hit,distance

def surface_intersections(head, hand):
    hmin=head.min(axis=1); hmax=head.max(axis=1)
    pairs=set(); witnesses=[]
    for j, tri in enumerate(hand):
        indices=np.flatnonzero(np.all((hmax>=tri.min(axis=0)-EPS)&(hmin<=tri.max(axis=0)+EPS),axis=1))
        if not len(indices): continue
        subset=head[indices]
        for edge in range(3):
            origin=tri[edge]; direction=tri[(edge+1)%3]-origin
            hit,t=ray_hits(np.broadcast_to(origin,(len(indices),3)),np.broadcast_to(direction,(len(indices),3)),subset,True)
            for k in np.flatnonzero(hit):
                pairs.add((int(indices[k]),j))
                if len(witnesses)<64: witnesses.append((origin+direction*t[k]).tolist())
            origins=subset[:,edge]; directions=subset[:,(edge+1)%3]-origins
            hit,t=ray_hits(origins,directions,np.broadcast_to(tri,(len(indices),3,3)),True)
            for k in np.flatnonzero(hit):
                pairs.add((int(indices[k]),j))
                if len(witnesses)<64: witnesses.append((origins[k]+directions[k]*t[k]).tolist())
    return len(pairs),witnesses

def containment(head, points):
    points=np.unique(np.round(points,6),axis=0)
    directions=np.asarray([[1,.337,.171],[-.271,1,.413],[.193,.317,1]],dtype=float)
    directions/=np.linalg.norm(directions,axis=1)[:,None]
    votes=[]; windings=[]; inside=[]
    for point in points:
        count=0
        for d in directions:
            hit,t=ray_hits(np.broadcast_to(point,(len(head),3)),np.broadcast_to(d,(len(head),3)),head)
            distances=np.sort(t[hit]); unique=int(len(distances)>0)+int(np.sum(np.diff(distances)>1e-5))
            count+=unique%2
        a=head[:,0]-point; b=head[:,1]-point; c=head[:,2]-point
        la=np.linalg.norm(a,axis=1);lb=np.linalg.norm(b,axis=1);lc=np.linalg.norm(c,axis=1)
        numerator=np.einsum('ij,ij->i',a,np.cross(b,c))
        denominator=la*lb*lc+np.einsum('ij,ij->i',a,b)*lc+np.einsum('ij,ij->i',b,c)*la+np.einsum('ij,ij->i',c,a)*lb
        winding=float(np.sum(2*np.arctan2(numerator,denominator))/(4*math.pi))
        votes.append(count);windings.append(abs(winding))
        if count>=2 and abs(winding)>.45: inside.append(point.tolist())
    return {'uniqueVertexCount':len(points),'rayMajorityInsideCount':int(np.sum(np.asarray(votes)>=2)),
            'solidAngleInsideCount':int(np.sum(np.asarray(windings)>.5)),
            'rayAndWindingInsideCount':len(inside),'maximumAbsoluteWinding':max(windings,default=0),
            'insideWitnesses':inside[:32]}

def bounds(points):
    return {'count':len(points),'minimum':points.min(axis=0).tolist(),'maximum':points.max(axis=0).tolist()}

def plot_projection(data, destination):
    from PIL import Image,ImageDraw
    points=[xyz(data['head']),xyz(data['leftFist']),xyz(data['rightFist'])]
    canvas=Image.new('RGB',(1500,760),'#1f2630');draw=ImageDraw.Draw(canvas)
    colors=['#8896a8','#f6a24b','#60d4ec']
    bones=xyz(data['bonePositions'])
    for panel,(axis,label) in enumerate([(2,'Side: forward Z / up Y'),(0,'Front: X / up Y')]):
        all_points=np.vstack(points+[bones]);lo=all_points[:,[axis,1]].min(axis=0);hi=all_points[:,[axis,1]].max(axis=0)
        scale=min(650/max(hi[0]-lo[0],.01),640/max(hi[1]-lo[1],.01));offset=np.array([panel*750+60,700])
        def xy(p):return (float(offset[0]+(p[axis]-lo[0])*scale),float(offset[1]-(p[1]-lo[1])*scale))
        draw.text((panel*750+30,20),label+' / t='+str(round(data['time'],4)),fill='white')
        for group,color in zip(points,colors):
            for p in group:
                x,y=xy(p);draw.ellipse((x-1,y-1,x+1,y+1),fill=color)
        for name,p in zip(data['boneNames'],bones):
            x,y=xy(p);draw.ellipse((x-3,y-3,x+3,y+3),fill='#8cff8a');draw.text((x+4,y+2),name,fill='#8cff8a')
    canvas.save(destination)

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--directory',type=Path,default=Path.home()/'Desktop/OZGL3_ArtReview/Characters/Protagonist01/Final/Review/Compatibility20261029/Diagnostics/LadderNativeGeometry');parser.add_argument('--report',type=Path,default=Path(__file__).with_name('ladder-triangle-contact-audit.json'));args=parser.parse_args()
    rows=[];previous_rotations=None;rotation_steps={}
    files=sorted(args.directory.glob('*.json'))
    if not files: raise RuntimeError('No exported native ladder geometry found.')
    for path in files:
        data=json.loads(path.read_text(encoding='utf-8-sig'));head=xyz(data['head']).reshape(-1,3,3)
        row={'file':path.name,'time':data['time'],'head':bounds(head.reshape(-1,3)),'headTriangles':len(head),'hands':{}}
        for side,key in [('left','leftFist'),('right','rightFist')]:
            vertices=xyz(data[key]);triangles=xyz(data[side+'Triangles']).reshape(-1,3,3)
            intersections,witnesses=surface_intersections(head,triangles)
            detail={'vertices':bounds(vertices),'triangles':len(triangles),'intersectingTrianglePairs':intersections,'intersectionWitnesses':witnesses}
            detail.update(containment(head,vertices));row['hands'][side]=detail
        rows.append(row)
        if data.get('boneRotations'):
            rotations=np.asarray([[q[a] for a in 'xyzw'] for q in data['boneRotations']],dtype=float)
            rotations/=np.linalg.norm(rotations,axis=1)[:,None]
            if previous_rotations is not None:
                angles=np.degrees(2*np.arccos(np.clip(np.abs(np.sum(rotations*previous_rotations,axis=1)),0,1)))
                for name,angle in zip(data['boneNames'],angles):rotation_steps[name]=max(rotation_steps.get(name,0),float(angle))
            previous_rotations=rotations
        print(json.dumps({'frame':path.name,'time':data['time'],'leftPairs':row['hands']['left']['intersectingTrianglePairs'],'rightPairs':row['hands']['right']['intersectingTrianglePairs'],'leftInside':row['hands']['left']['rayAndWindingInsideCount'],'rightInside':row['hands']['right']['rayAndWindingInsideCount']}),flush=True)
        if path.stem in ('0000','0008','0012','0016'):plot_projection(data,args.directory/(path.stem+'-projection.png'))
    result={'method':'Actual deformed triangle edges vs triangles in both directions after AABB pruning; three non-axis ray parity tests and solid-angle winding on fist vertices. Dominant-bone triangle groups can have open seams: containment is corroborative, triangle intersections provide explicit surface witnesses. Negative convex-plane score alone is not a collision verdict.','frames':rows,
            'sampledFrames':len(rows),'surfaceIntersections':sum(h['intersectingTrianglePairs'] for r in rows for h in r['hands'].values()),'corroboratedInsideVertices':sum(h['rayAndWindingInsideCount'] for r in rows for h in r['hands'].values()),
            'maximumAdjacentWorldBoneRotationDegreesAt30Hz':rotation_steps}
    args.report.write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k not in ('frames','method')}),flush=True)

if __name__=='__main__':main()
