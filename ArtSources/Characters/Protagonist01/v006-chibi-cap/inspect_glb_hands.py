"""Read-only bind-space diagnostics for the new generated hands."""
from pathlib import Path
import json, struct
import numpy as np

HERE = Path(__file__).resolve().parent
data = (HERE / 'rigging-bundle/Protagonist_ChibiCap_Rig.glb').read_bytes()
size, kind = struct.unpack_from('<II', data, 12)
doc = json.loads(data[20:20+size])
offset = 20+size
bin_size, bin_kind = struct.unpack_from('<II', data, offset)
blob = data[offset+8:offset+8+bin_size]

def accessor(index):
    a = doc['accessors'][index]; v = doc['bufferViews'][a['bufferView']]
    dt = np.dtype({5120:'i1',5121:'u1',5122:'<i2',5123:'<u2',5125:'<u4',5126:'<f4'}[a['componentType']])
    n = {'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}[a['type']]
    start = v.get('byteOffset',0)+a.get('byteOffset',0)
    return np.ndarray((a['count'],n), dtype=dt, buffer=blob, offset=start,
                      strides=(v.get('byteStride',dt.itemsize*n), dt.itemsize)).copy()

def local(node):
    if 'matrix' in node: return np.array(node['matrix']).reshape(4,4).T
    x,y,z,w = node.get('rotation',[0,0,0,1])
    m = np.eye(4)
    m[:3,:3] = [[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],
                [2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],
                [2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]]
    m[:3,:3] = m[:3,:3] @ np.diag(node.get('scale',[1,1,1]))
    m[:3,3] = node.get('translation',[0,0,0]); return m

parents = {c:i for i,n in enumerate(doc['nodes']) for c in n.get('children',[])}
def world(i):
    m = local(doc['nodes'][i]); return world(parents[i]) @ m if i in parents else m

skin_node = next(i for i,n in enumerate(doc['nodes']) if 'skin' in n and 'mesh' in n)
skin = doc['skins'][doc['nodes'][skin_node]['skin']]
primitive = doc['meshes'][doc['nodes'][skin_node]['mesh']]['primitives'][0]
attr = primitive['attributes']
points = accessor(attr['POSITION']); joints = accessor(attr['JOINTS_0']); weights = accessor(attr['WEIGHTS_0'])
inverse = accessor(skin['inverseBindMatrices']).reshape(-1,4,4).transpose(0,2,1)
report = []
for name in ['RightHand','LeftHand']:
    index = next(j for j,n in enumerate(skin['joints']) if doc['nodes'][n]['name']==name)
    influence = (weights*(joints==index)).sum(axis=1)
    hand = np.c_[points,np.ones(len(points))] @ inverse[index].T
    unit = np.linalg.norm(world(skin['joints'][index])[:3,1])
    physical = hand[:,:3]*unit
    selected = influence>.55
    out = {'name':name,'metresPerBoneUnit':float(unit),'count':int(selected.sum()),
           'minimum':physical[selected].min(axis=0).tolist(),'maximum':physical[selected].max(axis=0).tolist(),
           'quantiles':np.quantile(physical[selected],[0,.1,.25,.5,.75,.9,1],axis=0).tolist(),
           'points':[{'p':p.tolist(),'influence':float(i)} for p,i in zip(physical[selected],influence[selected])]}
    report.append(out)
(HERE/'hand-bind-inspection.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps([{k:v for k,v in h.items() if k!='points'} for h in report],indent=2))
