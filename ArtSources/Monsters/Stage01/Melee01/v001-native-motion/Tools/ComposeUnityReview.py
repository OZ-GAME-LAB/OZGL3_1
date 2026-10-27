"""Compose actual Unity Camera.Render frames; never generates or changes motion."""
from pathlib import Path
import json,sys
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parent.parent
PROJECT=next(p for p in ROOT.parents if (p/'Assets').is_dir() and (p/'ProjectSettings').is_dir())
sys.path.insert(0,str(PROJECT/'ArtSources'))
from review_storage import media_path,publish_html
OUT=ROOT/'QA/UnityRendered'
report=json.loads((OUT/'report.json').read_text(encoding='utf-8'))
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',17)
small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',14)

def gif(folder,target,speed=1,frame_count=None):
    target=media_path(target);target.parent.mkdir(parents=True,exist_ok=True)
    paths=[folder/f'{i:04}.png' for i in range(frame_count)] if frame_count is not None else sorted(folder.glob('[0-9][0-9][0-9][0-9].png'))
    if not paths: raise ValueError(f'No actual Unity frames: {folder}')
    frames=[Image.open(p).convert('RGB') for p in paths]
    # GIF stores centiseconds. Preserve 30fps with a 3/3/4cs pattern.
    rate=report['rate']*speed
    duration=[10*(round((i+1)*100/rate)-round(i*100/rate)) for i in range(len(frames))]
    frames[0].save(target,save_all=True,append_images=frames[1:],duration=duration,loop=0,optimize=False,disposal=2)
    for frame in frames:frame.close()

def contact(name,info):
    frame_count=info['frames'];indices=sorted(set(round((frame_count-1)*i/7) for i in range(8)))
    width,height=240,135;pad=6;rowh=height+29
    sheet=Image.new('RGB',(pad+(width+pad)*len(indices),45+rowh*len(report['views'])),(19,24,31))
    draw=ImageDraw.Draw(sheet);draw.text((10,10),f'{name}  actual saved Unity prefab and native clip  30fps',(232,239,247),font=font)
    for row,view in enumerate(report['views']):
        for col,index in enumerate(indices):
            p=OUT/name/view/f'{index:04}.png'
            image=Image.open(p).convert('RGB');image.thumbnail((width,height));x=pad+col*(width+pad);y=45+row*rowh
            sheet.paste(image,(x,y));draw.text((x,y+height+3),f'{view}  f{index:03d}  {min(index/report["rate"],info["seconds"]):.2f}s',(220,227,239),font=small);image.close()
    sheet.save(OUT/f'{name}_Contact.png')

def every_frame_contact(name,view,info):
    paths=[OUT/name/view/f'{i:04}.png' for i in range(info['frames'])]
    columns=8;width,height=240,135;pad=6;rowh=height+26
    sheet=Image.new('RGB',(pad+(width+pad)*columns,45+rowh*((len(paths)+columns-1)//columns)),(19,24,31))
    draw=ImageDraw.Draw(sheet);draw.text((10,10),f'{name} / {view}  all {len(paths)} rendered frames in time order',(232,239,247),font=font)
    for i,p in enumerate(paths):
        image=Image.open(p).convert('RGB');image.thumbnail((width,height));x=pad+(i%columns)*(width+pad);y=45+(i//columns)*rowh
        sheet.paste(image,(x,y));draw.text((x,y+height+2),f'f{i:03d}  {min(i/report["rate"],info["seconds"]):.3f}s',(220,227,239),font=small);image.close()
    sheet.save(OUT/f'{name}_{view}_EveryFrame.png')

for info in report['clips']:
    name=info['name'];contact(name,info)
    for view in report['views']:
        every_frame_contact(name,view,info)
        # PNG/contact review retains the inclusive terminal sample. Playback
        # omits that duplicate endpoint so it adds no extra 1/30s loop dwell.
        gif(OUT/name/view,OUT/f'{name}_{view}.gif',frame_count=info['frames']-1)
    gif(OUT/name/'Side',OUT/f'{name}_Side_Slow.gif',.5,frame_count=info['frames']-1)

for info in report['connections']:
    name=info['first']+'_to_'+info['second']
    gif(OUT/'Connections'/name/'Blend',OUT/f'{name}_PreviewBlend.gif',frame_count=info['frames'])

html=['<!doctype html><meta charset="utf-8"><title>Native monster Unity review</title><style>body{font:16px/1.6 system-ui;max-width:1300px;margin:30px auto;background:#141a22;color:#e5edf5}img{max-width:100%}a{color:#b5d4ed}article{margin:36px 0}</style><h1>Actual imported Unity model review</h1><p>Saved and reloaded Generic FBX clips and independent prefab. Every frame is rendered by Unity at 30 fps. No gameplay Animator controller or VFX integration. Amber markers show intended ground Y=0; the opaque floor is deliberately lowered to -0.05m so toe penetration and running airborne phases remain visible. Inspect contact sheets and complete original/slow clips; diagnostic numbers do not certify motion quality.</p>']
for info in report['clips']:
    name=info['name'];html.append(f'<article><h2>{name}</h2><img src="{name}_Side.gif"><p><a href="{name}_Side_Slow.gif">0.5x side</a> | <a href="{name}_Front.gif">Front</a> | <a href="{name}_Opposite.gif">Opposite</a> | <a href="{name}_Back.gif">Back</a></p><img src="{name}_Contact.png"></article>')
html.append('<h2>Preview connections</h2><p>Each preview uses a 0.12 second offline Playables blend. Raw end and start stills are also retained in Connections. These previews do not verify the game Animator.</p>')
for info in report['connections']:
    name=info['first']+'_to_'+info['second'];html.append(f'<details><summary>{name}</summary><img src="{name}_PreviewBlend.gif"></details>')
publish_html(OUT/'index.html',''.join(html),'몬스터 Unity 검수 외부 갤러리')
print('UNITY_REVIEW_COMPOSED',OUT/'index.html')
