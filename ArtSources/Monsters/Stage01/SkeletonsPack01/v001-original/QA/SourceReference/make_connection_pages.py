from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import math
root=Path('C:/Users/me/Desktop/OZGL3_ArtReview/Monsters/Stage01/SkeletonsPack01/v001-original')
out=root/'Independent'/'Connections';out.mkdir(exist_ok=True)
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',13)
for role in sorted((root/'Connections').iterdir()):
    groups=[]
    for folder in sorted(role.iterdir()):
        paths=[folder/'raw_end.png',folder/'raw_start.png']+sorted((folder/'Blend').glob('*.png'))
        paths=[p for p in paths if p.exists()]
        if paths:groups.append((folder.name,paths))
    for start in range(0,len(groups),3):
        subset=groups[start:start+3]
        height=sum(28+196*math.ceil(len(paths)/8) for _,paths in subset)
        sheet=Image.new('RGB',(1536,height),'#20252a');draw=ImageDraw.Draw(sheet);y=0
        for name,paths in subset:
            draw.text((3,y+3),f'{role.name} {name} | raw endpoints then ALL blend frames',font=font,fill='white');y+=28
            for i,path in enumerate(paths):
                with Image.open(path) as src:
                    crop=src.crop((440,0,960,540)).convert('RGB');crop.thumbnail((192,177),Image.Resampling.LANCZOS)
                    x=i%8*192;yy=y+i//8*196;sheet.paste(crop,(x+(192-crop.width)//2,yy+19))
                draw.text((x+4,yy+2),path.stem,font=font,fill='white')
            y+=196*math.ceil(len(paths)/8)
        sheet.save(out/f'{role.name}_{start//3+1:02d}.jpg',quality=94)
print('Connection pages complete')
