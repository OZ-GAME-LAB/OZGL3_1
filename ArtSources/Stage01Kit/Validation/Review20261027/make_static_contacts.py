import json, math
from pathlib import Path
from PIL import Image, ImageOps, ImageDraw, ImageFont
ROOT=Path(__file__).resolve().parent
report=json.loads((ROOT/'unity-static-audit.json').read_text(encoding='utf-8-sig'))
out=ROOT/'StaticContacts';out.mkdir(exist_ok=True)
try: font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',17)
except: font=ImageFont.load_default()
rows=report['prefabs']
for start in range(0,len(rows),4):
    batch=rows[start:start+4];sheet=Image.new('RGB',(960,380*len(batch)),(23,29,40));d=ImageDraw.Draw(sheet)
    for row,a in enumerate(batch):
        for col,view in enumerate(('Side','ThreeQuarter')):
            im=Image.open(ROOT/'StaticFrames'/f"{a['name']}_{view}.png").convert('RGB');im=ImageOps.contain(im,(460,340))
            sheet.paste(im,(col*480+(480-im.width)//2,row*380+35))
            d.text((col*480+10,row*380+9),a['name']+' | '+view,font=font,fill='white')
    target=out/f'Stage01_All_{start//4+1:02d}.jpg';sheet.save(target,quality=93)
print(json.dumps({'assets':len(rows),'sheets':math.ceil(len(rows)/4),'output':str(out)}))
