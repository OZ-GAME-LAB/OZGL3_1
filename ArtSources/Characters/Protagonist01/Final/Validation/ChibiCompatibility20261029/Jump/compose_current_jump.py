"""Compose external GIFs from saved current-clip review PNGs, without running Unity.

This script never edits game clips, models, controllers or gameplay code.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json, hashlib, math, sys

HERE = Path(__file__).resolve().parent
ROOT = next(p for p in HERE.parents if (p/'Assets').is_dir() and (p/'ProjectSettings').is_dir())
sys.path.insert(0,str(ROOT/'ArtSources'))
from review_storage import media_path, review_root
OUTPUT = media_path(ROOT/'ArtSources/Characters/Protagonist01/Final/Review/Compatibility20261029')
FONT_PATH = 'C:/Windows/Fonts/malgun.ttf'
TITLE_FONT = ImageFont.truetype(FONT_PATH, 19)
TEXT_FONT = ImageFont.truetype(FONT_PATH, 14)
SMALL_FONT = ImageFont.truetype(FONT_PATH, 12)
PHASES = {'IDLE':'대기','TAKEOFF':'점프 시전','RISE':'상승','APEX':'정점','FALL':'낙하','LAND':'착지'}
COLORS = {'IDLE':'#b8cad9','TAKEOFF':'#ebca85','RISE':'#86c9ed','APEX':'#cfadf2','FALL':'#f0b695','LAND':'#a8d8ad'}
BG = '#15202a'
NOTE = '오프라인 연결 / 높이 0.66m · 체공 1.2초로 축약 / 게임 녹화 아님'

def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()

def label_frame(sequence, row, size=560):
    path = Path(sequence['folder'])/(f"{row['frame']:04d}.png")
    raw = Image.open(path).convert('RGB')
    raw = raw.resize((size,round(raw.height*size/raw.width)), Image.Resampling.LANCZOS)
    result = Image.new('RGB',(size,raw.height+112),BG)
    draw = ImageDraw.Draw(result)
    intended = sequence['name'].startswith('Intended')
    title = '현재 클립의 의도된 연결' if intended else '저장된 Air 지정: 정점 클립만'
    view = '2.5D 측면' if sequence['view']=='Side' else '정면 검수'
    draw.text((12,10),f'{title} · {view}',font=TITLE_FONT,fill='#f3f6f9')
    draw.text((12,41),NOTE,font=TEXT_FONT,fill='#a9bbc9')
    result.paste(raw,(0,69))
    bottom = raw.height+78
    phase = row['phase']
    draw.rounded_rectangle((10,bottom-2,147,bottom+26),radius=5,fill=COLORS[phase])
    draw.text((20,bottom),f'{PHASES[phase]}  {phase}',font=TEXT_FONT,fill='#13202a')
    draw.text((167,bottom),f"{row['time']:.2f}s / 높이 {row['height']:.2f}m",font=TEXT_FONT,fill='#dce7ef')
    if not intended and phase in ('RISE','APEX','FALL'):
        draw.text((360,bottom),'재생: Apex만',font=TEXT_FONT,fill='#f0b695')
    return result

def encode_gif(images, path, fps):
    if not images: raise RuntimeError('No frames')
    samples = [images[round(i*(len(images)-1)/11)] for i in range(12)]
    swatch = Image.new('RGB',(images[0].width*4,images[0].height*3))
    for i,im in enumerate(samples): swatch.paste(im,(i%4*im.width,i//4*im.height))
    palette = swatch.quantize(colors=240,method=Image.Quantize.MEDIANCUT)
    encoded = [im.quantize(palette=palette,dither=Image.Dither.NONE) for im in images]
    durations = [round((i+1)*100/fps)*10-round(i*100/fps)*10 for i in range(len(images))]
    encoded[0].save(path,save_all=True,append_images=encoded[1:],duration=durations,loop=0,optimize=False,disposal=2)
    with Image.open(path) as im:
        count=im.n_frames
        actual=sum((im.seek(i),im.info.get('duration',0))[1] for i in range(count))
        width,height=im.size
    assert count==len(images),(path,count,len(images))
    assert actual==sum(durations),(path,actual,sum(durations))
    return {'file':path.relative_to(review_root()).as_posix(),'sha256':sha(path),'frames':count,'fps':fps,
        'durationMilliseconds':actual,'width':width,'height':height,'bytes':path.stat().st_size}

def contact(sequence):
    sample_times=[.33,.6667,.7667,.9,1.16,1.3667,1.5,1.7667,1.9667,2.0667,2.2667,2.8]
    rows=[min(sequence['frames'],key=lambda row:abs(row['time']-t)) for t in sample_times]
    cellw,cellh=260,296
    sheet=Image.new('RGB',(cellw*4,cellh*3+60),BG)
    draw=ImageDraw.Draw(sheet)
    draw.text((12,8),sequence['name']+' / '+sequence['view'],font=TITLE_FONT,fill='#edf3f8')
    draw.text((12,34),NOTE,font=TEXT_FONT,fill='#abc0cf')
    for i,row in enumerate(rows):
        raw=Image.open(Path(sequence['folder'])/f"{row['frame']:04d}.png").convert('RGB').resize((cellw,cellw),Image.Resampling.LANCZOS)
        x,y=i%4*cellw,60+i//4*cellh
        sheet.paste(raw,(x,y+32))
        draw.text((x+8,y+7),f"{row['time']:.3f}s · {PHASES[row['phase']]} · {row['phase']}",font=TEXT_FONT,fill=COLORS[row['phase']])
    path=OUTPUT/'Contacts'/f"{sequence['name']}_{sequence['view']}.jpg"
    path.parent.mkdir(parents=True,exist_ok=True)
    sheet.save(path,quality=93)
    return str(path)

def main():
    source=HERE/'sequence-render.json'
    if not source.is_file(): raise SystemExit('Render manifest not yet present; rerun after Unity review completes.')
    report=json.loads(source.read_text(encoding='utf-8-sig'))
    OUTPUT.mkdir(parents=True,exist_ok=True)
    if OUTPUT==ROOT or ROOT in OUTPUT.parents: raise RuntimeError('GIF output must be external')
    rows=report['sequences']
    assert len(rows)==4
    # This character has a colored outfit/skin. Two-color floor-only renders
    # can be technically valid images but must never be delivered as motion QA.
    raw_color_counts=[]
    for sequence in rows:
        for row in sequence['frames']:
            path=Path(sequence['folder'])/f"{row['frame']:04d}.png"
            with Image.open(path) as raw:
                rgb=raw.convert('RGB')
                colors=rgb.getcolors(rgb.width*rgb.height)
            count=len(colors)
            raw_color_counts.append(count)
            if count<16:
                raise RuntimeError(f'Character absent or suspiciously blank render: {path} ({count} colors). Re-render before composing.')
    sequences={}
    receipt=[]
    contacts=[]
    for sequence in rows:
        assert sequence['gameplaySimulation'] is False and sequence['clipsModified'] is False
        assert sequence['fps']==30
        assert all((Path(sequence['folder'])/f"{row['frame']:04d}.png").is_file() for row in sequence['frames'])
        images=[label_frame(sequence,row) for row in sequence['frames']]
        sequences[(sequence['name'],sequence['view'])]=(sequence,images)
        path=OUTPUT/(sequence['name']+'_'+sequence['view']+'.gif')
        receipt.append(encode_gif(images,path,sequence['fps']))
        contacts.append(contact(sequence))
    intended,ii=sequences[('Intended_Idle_Jump_Land','Side')]
    saved,ss=sequences[('Saved_ApexOnly_Idle_Jump_Land','Side')]
    assert len(ii)==len(ss)
    compare=[]
    for left,right in zip(ss,ii):
        frame=Image.new('RGB',(left.width+right.width,left.height),BG)
        frame.paste(left,(0,0));frame.paste(right,(left.width,0))
        ImageDraw.Draw(frame).line((left.width,0,left.width,left.height),fill='#536b7c',width=2)
        compare.append(frame)
    receipt.append(encode_gif(compare,OUTPUT/'Current_vs_Intended_Jump_Side.gif',30))
    result={'sourceManifest':str(source),'sourceManifestSHA256':sha(source),'externalDirectory':str(OUTPUT),
        'gameplayRecording':False,'clipsModifiedByComposition':False,
        'scope':'Unchanged saved clips on real Player_Chibi prefab. Offline transitions with authored clip timing; parent jump height and airtime compressed for review. Saved_ApexOnly reproduces only the saved Air clip choice, not the entire live Animator or physics.',
        'heightMetres':.66,'airDurationSeconds':1.2,'gifFiles':receipt,'phaseContactSheets':contacts,
        'allFrameCountsAndDurationsVerified':True,'gifFilesInsideProject':0,
        'rawRenderMinimumUniqueColors':min(raw_color_counts),'blankRenderGuardPassed':True,
        'visualReview':'Pending direct contact-sheet and GIF-frame inspection after composition.'}
    (HERE/'gif-receipt.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False,indent=2))

if __name__=='__main__': main()
