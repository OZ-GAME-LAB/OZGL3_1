"""Orthographic textured views of the delivered Meshy FBX for visual QA."""
from pathlib import Path
import math
import sys
sys.dont_write_bytecode = True
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from inspect_fbx import binary_nodes

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
MODEL = ROOT / 'Assets/Art/Characters/Protagonist01/protagonist01-v001.fbx'
TEXTURE = MODEL.with_name('protagonist01-v001_base_color.png')

def child(node, name):
    return next(n for n in node['children'] if n['name'] == name)

nodes, _ = binary_nodes(MODEL.read_bytes())
objects = next(n for n in nodes if n['name'] == 'Objects')
geo = child(objects, 'Geometry')
vertices = np.array(child(geo, 'Vertices')['properties'][0]).reshape(-1, 3)
indices = np.array(child(geo, 'PolygonVertexIndex')['properties'][0])
assert np.all(indices[2::3] < 0) and np.all(indices.reshape(-1,3)[:,:2] >= 0)
indices = np.where(indices < 0, -indices - 1, indices).reshape(-1, 3)
uv_node = child(geo, 'LayerElementUV')
assert child(uv_node, 'MappingInformationType')['properties'][0] == 'ByPolygonVertex'
assert child(uv_node, 'ReferenceInformationType')['properties'][0] == 'IndexToDirect'
uv = np.array(child(uv_node, 'UV')['properties'][0]).reshape(-1, 2)
uv_indices = np.array(child(uv_node, 'UVIndex')['properties'][0]).reshape(-1,3)
texture = np.asarray(Image.open(TEXTURE).convert('RGB'))
height = np.ptp(vertices[:,1])
center = (vertices.min(axis=0) + vertices.max(axis=0)) / 2
vertices -= center

def render(angle, width=560, canvas_height=650):
    c, s = math.cos(math.radians(angle)), math.sin(math.radians(angle))
    rotation = np.array([[c,0,-s],[0,1,0],[s,0,c]])
    positions = vertices @ rotation.T
    scale = min((canvas_height-70)/height, (width-35)/np.ptp(positions[:,0]))
    xy = np.stack([positions[:,0]*scale+width/2, -positions[:,1]*scale+canvas_height/2],axis=1)
    pixels = np.full((canvas_height,width,3),239,dtype=np.uint8)
    depth = np.full((canvas_height,width),-np.inf)
    light = np.array([-0.35,0.7,0.6]); light /= np.linalg.norm(light)
    for ids, texture_ids in zip(indices, uv_indices):
        points = xy[ids]
        x0,y0 = np.maximum(np.floor(points.min(axis=0)).astype(int),0)
        x1,y1 = np.minimum(np.ceil(points.max(axis=0)).astype(int),[width-1,canvas_height-1])
        if x0>x1 or y0>y1:
            continue
        a,b,d = points
        den = (b[1]-d[1])*(a[0]-d[0])+(d[0]-b[0])*(a[1]-d[1])
        if abs(den)<1e-10:
            continue
        yy,xx = np.mgrid[y0:y1+1,x0:x1+1]
        xx=xx+0.5; yy=yy+0.5
        w0=((b[1]-d[1])*(xx-d[0])+(d[0]-b[0])*(yy-d[1]))/den
        w1=((d[1]-a[1])*(xx-d[0])+(a[0]-d[0])*(yy-d[1]))/den
        w2=1-w0-w1
        z=w0*positions[ids[0],2]+w1*positions[ids[1],2]+w2*positions[ids[2],2]
        mask=(w0>=-1e-8)&(w1>=-1e-8)&(w2>=-1e-8)&(z>depth[y0:y1+1,x0:x1+1])
        if not mask.any():
            continue
        coords=w0[...,None]*uv[texture_ids[0]]+w1[...,None]*uv[texture_ids[1]]+w2[...,None]*uv[texture_ids[2]]
        tx=np.clip(np.rint(coords[:,:,0]*(texture.shape[1]-1)).astype(int),0,texture.shape[1]-1)
        ty=np.clip(np.rint((1-coords[:,:,1])*(texture.shape[0]-1)).astype(int),0,texture.shape[0]-1)
        normal=np.cross(positions[ids[1]]-positions[ids[0]],positions[ids[2]]-positions[ids[0]])
        length=np.linalg.norm(normal)
        shade=0.88+0.12*max(0,float(np.dot(normal/max(length,1e-12),light)))
        colors=np.clip(texture[ty,tx]*shade,0,255).astype(np.uint8)
        pixels[y0:y1+1,x0:x1+1][mask]=colors[mask]
        depth[y0:y1+1,x0:x1+1][mask]=z[mask]
    return Image.fromarray(pixels)

views=[('FRONT',0),('3/4',32),('SIDE',90),('BACK',180)]
sheet=Image.new('RGB',(1120,1380),(239,239,239))
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',22)
for i,(name,angle) in enumerate(views):
    im=render(angle)
    x=(i%2)*560; y=(i//2)*690
    sheet.paste(im,(x,y+40))
    draw=ImageDraw.Draw(sheet)
    draw.text((x+24,y+12),name,font=font,fill=(50,56,65))
output=HERE/'protagonist01-review.png'
sheet.save(output)
print(output)
