from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json, math, hashlib, sys

ROOT=next(p for p in Path(__file__).resolve().parents if (p/'Assets').is_dir() and (p/'ProjectSettings').is_dir())
sys.path.insert(0,str(ROOT/'ArtSources'))
from review_storage import media_path,review_root,storage_metadata
RAW=ROOT/'Logs/Airborne20261027/Frames'
OUT=Path(__file__).resolve().parent
FINAL=OUT.parents[1]
REVIEW=media_path(FINAL/'Review/Airborne')
REVIEW.mkdir(exist_ok=True,parents=True)
font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',17)
small=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',13)

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()

def frames(folder,label,size=480):
    result=[]
    for p in sorted(folder.glob('*.png')):
        im=Image.open(p).convert('RGB').resize((size,size),Image.Resampling.LANCZOS)
        tile=Image.new('RGB',(size,size+34),'#17212a');tile.paste(im,(0,34))
        ImageDraw.Draw(tile).text((12,8),label,font=font,fill='#e7edf4')
        result.append(tile)
    return result

def gif(ims,path):
    swatch=Image.new('RGB',(ims[0].width*4,ims[0].height*3))
    for i in range(12):swatch.paste(ims[round(i*(len(ims)-1)/11)],((i%4)*ims[0].width,(i//4)*ims[0].height))
    palette=swatch.quantize(colors=240,method=Image.Quantize.MEDIANCUT)
    encoded=[im.quantize(palette=palette,dither=Image.Dither.NONE) for im in ims]
    # GIF centiseconds: distribute 30/40ms at exactly 30fps average.
    durations=[round((i+1)*10/3)*10-round(i*10/3)*10 for i in range(len(ims))]
    encoded[0].save(path,save_all=True,append_images=encoded[1:],duration=durations,loop=0,optimize=False,disposal=2)

if __name__=='__main__':
    receipt=[]
    for folder in sorted(RAW.glob('*/*/*')):
        if not folder.is_dir():continue
        files=sorted(folder.glob('*.png'))
        if not files:continue
        rel=folder.relative_to(RAW);label=' / '.join(rel.parts)
        contact=Image.new('RGB',(4*250,math.ceil(len(files)/4)*272),'#111820')
        for i,p in enumerate(files):
            tile=Image.open(p).convert('RGB').resize((250,250),Image.Resampling.LANCZOS)
            x=(i%4)*250;y=(i//4)*272
            contact.paste(tile,(x,y+22));ImageDraw.Draw(contact).text((x+5,y+4),f'{rel.parts[1]} {i:03}',font=small,fill='white')
        contacts=OUT/'Contacts';contacts.mkdir(exist_ok=True)
        contact.save(contacts/('_'.join(rel.parts)+'.jpg'),quality=90)
        if rel.parts[0]=='MotionReview' and rel.parts[-1]=='Side':
            output=REVIEW/(rel.parts[1]+'.gif');gif(frames(folder,rel.parts[1]+' | 1x / 2.5D'),output)
            receipt.append(dict(name=rel.parts[1],file=output.relative_to(review_root()).as_posix(),previewStorage=storage_metadata(),sha256=sha(output),frames=len(files)))
    seq=RAW/'Sequence/Side'
    if seq.exists():
        gif(frames(seq,'Takeoff > Rise > Fall > Land | Preview'),REVIEW/'Jump_Sequence.gif')
        for view in ['Side','Front']:
            files=sorted((RAW/'Sequence'/view).glob('*.png'))
            contact=Image.new('RGB',(4*250,math.ceil(len(files)/4)*272),'#111820')
            for i,p in enumerate(files):
                x=i%4*250;y=i//4*272
                contact.paste(Image.open(p).convert('RGB').resize((250,250),Image.Resampling.LANCZOS),(x,y+22))
                ImageDraw.Draw(contact).text((x+6,y+4),f'{view} {i/30:.3f}s',font=small,fill='white')
            contact.save(OUT/'Contacts'/('Sequence_'+view+'.jpg'),quality=90)
    left=frames(RAW/'MotionReview/Jump_Rise_Loop/Side','ASCENDING | Jump_Rise_Loop')
    right=frames(RAW/'MotionReview/Jump_Fall_Loop/Side','FALLING | Jump_Fall_Loop')
    combined=[]
    for i in range(120):
        im=Image.new('RGB',(960,514),'#17212a');im.paste(left[i%24],(0,0));im.paste(right[i%30],(480,0));combined.append(im)
    gif(combined,REVIEW/'Rise_Fall_Side.gif')
    (OUT/'gif-receipt.json').write_text(json.dumps(receipt,indent=2),encoding='utf8')
    print('Composed',len(receipt),'loops and sequence')
