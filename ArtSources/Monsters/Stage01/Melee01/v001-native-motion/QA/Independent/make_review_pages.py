"""Read-only motion review: crop rendered images into ordered contact pages."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,hashlib
BASE=Path(__file__).resolve().parents[2]
SRC=BASE/'QA/UnityRendered'
OUT=Path(__file__).parent
report=json.loads((SRC/'report.json').read_text())
actual=hashlib.sha256((BASE/'Exports/Stage01_Melee01.fbx').read_bytes()).hexdigest()
assert report['fbxSHA256']==actual,'Unity rendered a different FBX'
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',15)
manifest={'reviewed_export_sha256':actual,'pages':[]}
for clip in report['clips']:
    name=clip['name']
    for view in report['views']:
        frame_count=clip['frames']
        paths=[SRC/name/view/f'{i:04}.png' for i in range(frame_count)]
        for page,start in enumerate(range(0,frame_count,18)):
            canvas=Image.new('RGB',(1800,906),'#19232f');d=ImageDraw.Draw(canvas)
            d.text((10,8),f'{name} {view}  frames {start}-{min(start+17,frame_count-1)} | 30fps | export {actual[:10]}',font=font,fill='white')
            for n,p in enumerate(paths[start:start+18]):
                im=Image.open(p).convert('RGB').crop((190,0,790,540)).resize((300,270))
                x=(n%6)*300;y=30+(n//6)*292;canvas.paste(im,(x,y));d.text((x+5,y+270),f'f{start+n:03}  {(start+n)/30:.3f}s',font=font,fill='white')
            target=OUT/f'{name}_{view}_p{page}.jpg';canvas.save(target,quality=90)
            manifest['pages'].append(str(target.name))
(OUT/'page-manifest.json').write_text(json.dumps(manifest,indent=2))
print('REVIEW_PAGES',len(manifest['pages']))
