"""Read-only FBX hand-space silhouette diagnostic for the v004 mesh morph.

This is an untextured geometric inspection, not a Unity/game render. No assets change.
"""
from pathlib import Path
import importlib.util
import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE=Path(__file__).resolve().parent
WORKSPACE=HERE.parents[3]
spec=importlib.util.spec_from_file_location('fbx',HERE.parent/'v001'/'inspect_fbx.py')
fbx=importlib.util.module_from_spec(spec);spec.loader.exec_module(fbx)
nodes,_=fbx.binary_nodes((WORKSPACE/'Assets/Art/Characters/Protagonist01/Runtime/Protagonist01_Rig.fbx').read_bytes())
objects=next(n for n in nodes if n['name']=='Objects')['children']
geometry=next(n for n in objects if n['name']=='Geometry')
vertices=np.array(fbx.array_values(geometry,'Vertices')).reshape(-1,3)
triangles=[]; polygon=[]
for number in fbx.array_values(geometry,'PolygonVertexIndex'):
    polygon.append(number if number>=0 else -number-1)
    if number<0:
        triangles.extend((polygon[0],polygon[i],polygon[i+1]) for i in range(1,len(polygon)-1));polygon=[]
triangles=np.array(triangles)
smooth=lambda x:np.clip(x,0,1)**2*(3-2*np.clip(x,0,1))

def morph(points,weights,sign,new=True,fist=True):
    pivot=.082 if new else .112
    columns=np.array([-.024,-.008,.013,.033])
    selected=(points[:,1]>pivot)&(points[:,2]<.047)&(weights>.9)
    nearest=np.argmin(abs(points[:,2,None]-columns),axis=1)
    lengths=np.array([max(points[selected&(nearest==i),1])-pivot for i in range(4)])
    region=(weights>=.55)&(points[:,2]>.065)&(points[:,1]>.065)&(points[:,1]<.15)
    tip=points[region][np.argmax(points[region,2])]; thumbpivot=np.array([0,.06,.036])
    axis=tip-thumbpivot;axis/=np.linalg.norm(axis)
    target=np.array([sign*(.025 if fist else .043),.055 if fist else .09,.005 if fist else .02]) if new else np.array([sign*(.057 if fist else .05),.107 if fist else .11,.006 if fist else .02])
    direction=target-thumbpivot;scale=np.linalg.norm(direction)/np.linalg.norm(tip-thumbpivot);direction/=np.linalg.norm(direction)
    cross=np.cross(axis,direction);cos=axis@direction;K=np.array([[0,-cross[2],cross[1]],[cross[2],0,-cross[0]],[-cross[1],cross[0],0]])
    rot=np.eye(3)+K+K@K/(1+cos)
    a=np.linspace(0,1,129);m,p,d=(85,105,70) if new and fist else (65,70,25) if new else (75,95,45) if fist else (58,73,34)
    angle=np.radians(m*smooth(a/.20)+p*smooth((a-.25)/.35)+d*smooth((a-.6)/.4))
    cx=np.r_[0,np.cumsum(np.sin((angle[1:]+angle[:-1])*.5)/128)]
    cy=np.r_[0,np.cumsum(np.cos((angle[1:]+angle[:-1])*.5)/128)]
    output=points.copy()
    for i,(p,w) in enumerate(zip(points,weights)):
        if w<.55:continue
        thumb=smooth((p[2]-.038)/.027)*(1-smooth((p[1]-.135)/.025))
        full=.98+(.78-.98)*thumb;skin=smooth((w-.55)/(full-.55))*smooth((p[1]-.025)/.035)
        finger=(1-thumb)*smooth((p[1]-(.065 if new else .103))/(.022 if new else .019))
        q=p.copy()
        if finger>0 and p[1]>pivot:
            length=np.interp(p[2],columns,lengths);t=(p[1]-pivot)/length
            closed=length+(.095-length)*(.65 if fist else .25) if new else length
            x,y,ang=[np.interp(t,a,v) for v in (cx,cy,angle)]
            curl=np.array([sign*x*closed+p[0]*np.cos(ang),pivot+y*closed-sign*p[0]*np.sin(ang),p[2]])
            curl[2]=p[2]*(1+(((.88 if fist else .95) if new else .95)-1)*smooth(t))
            q+=(curl-p)*finger
        if thumb>0:
            offset=p-thumbpivot;axial=axis*(offset@axis)
            opposed=thumbpivot+rot@(offset-axial+axial*scale if new else offset)
            q+=(opposed-p)*thumb
        output[i]=p+(q-p)*skin
    return output

W,H=400,480;canvas=Image.new('RGB',(W*6,H*2),'#252b33');draw=ImageDraw.Draw(canvas)
for row,bone in enumerate(['RightHand','LeftHand']):
    cluster=next(n for n in objects if n['name']=='Deformer' and bone in str(n['properties']))
    transform=np.array(fbx.array_values(cluster,'Transform')).reshape(4,4)
    points=(np.c_[vertices,np.ones(len(vertices))]@transform)[:,:3]*.01;points[:,0]*=-1
    weight=np.zeros(len(points));weight[fbx.array_values(cluster,'Indexes')]=fbx.array_values(cluster,'Weights')
    faces=triangles[(weight[triangles].min(1)>=.5)&(points[triangles,1].min(1)>.025)]
    for variant,positions in enumerate([points,morph(points,weight,-1 if row==0 else 1,False),morph(points,weight,-1 if row==0 else 1,True)]):
        for view,(horiz,depth) in enumerate([(2,0),(0,2)]):
            column=variant*2+view;offset=np.array([column*W,row*H]);projection=np.c_[positions[:,horiz],-positions[:,1]]*2200+offset+[W*.5,H*.72]
            for tri in faces[np.argsort(positions[faces,depth].mean(1))]:
                n=np.cross(positions[tri[1]]-positions[tri[0]],positions[tri[2]]-positions[tri[0]]);n/=max(np.linalg.norm(n),1e-10)
                brightness=.65+.35*abs(n[depth]);color=tuple(int(c*brightness) for c in [249,219,195])
                draw.polygon([tuple(p) for p in projection[tri]],fill=color,outline='#c4a993')
            draw.text(tuple(offset+[10,10]),f'{bone} '+['Original','Old Curl','Early MCP + Thumb'][variant]+' '+['Palm','Side'][view],fill='white')
canvas.save(HERE/'audit-evidence/hand-geometry-comparison.png')
print(HERE/'audit-evidence/hand-geometry-comparison.png')
