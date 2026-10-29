"""External review composition for saved native-Chibi clips. Does not run Unity."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import argparse, hashlib, json, math, sys, html

HERE=Path(__file__).resolve().parent
ROOT=next(p for p in HERE.parents if (p/'Assets').is_dir() and (p/'ProjectSettings').is_dir())
sys.path.insert(0,str(ROOT/'ArtSources'))
from review_storage import media_path,review_root
OUT=media_path(ROOT/'ArtSources/Characters/Protagonist01/Final/Review/Compatibility20261029')
BG='#15202a'; FG='#e8f1f6'; MUTED='#aec1ce'
FONT='C:/Windows/Fonts/malgun.ttf'
TITLE=ImageFont.truetype(FONT,18); BODY=ImageFont.truetype(FONT,14); SMALL=ImageFont.truetype(FONT,12)
SPECS={
 'Longsword_Attack_Chop':('검 내려베기','단발 검 공격 · 시작/끝 정지는 검수용'),
 'Longsword_Attack_Slice':('검 가로베기','단발 검 공격 · 시작/끝 정지는 검수용'),
 'Longsword_Attack_Stab':('검 찌르기','단발 검 공격 · 시작/끝 정지는 검수용'),
 'Death_Fall':('사망','비반복 클립 · 끝 자세 유지 · GIF만 다시보기'),
 'Ladder_Climb_Loop':('사다리 오르기','제자리 루프 2주기 · 높이 이동은 게임 제어')}

def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def read_report():
    report=json.loads((HERE/'saved-clip-audit.json').read_text(encoding='utf-8-sig'))
    rows={r['name']:r for r in report['clips']}
    missing=set(SPECS)-set(rows)
    if missing: raise RuntimeError('Wait for complete5clip render: '+repr(missing))
    return rows

def frame_paths(row,view):
    folder=next(Path(f) for f in row['frameDirectories'] if Path(f).name==view)
    count=round(row['duration']*30)+1 if view=='Side' else 13
    paths=[folder/f'{i:04d}.png' for i in range(count)]
    assert all(p.is_file() for p in paths),(row['name'],view,'missing frame')
    for path in paths:
        with Image.open(path) as im:
            colors=im.convert('RGB').getcolors(im.width*im.height)
        if len(colors)<16: raise RuntimeError('Blank character render: '+str(path))
    return paths

def make_contacts(row,view):
    paths=frame_paths(row,view)
    # Side sheets show every30Hz frame. Other views show all13 phase renders.
    pages=[]; per_page=24; cellw=240; cellh=272; cols=4
    for page in range(math.ceil(len(paths)/per_page)):
        subset=paths[page*per_page:(page+1)*per_page]
        im=Image.new('RGB',(cols*cellw,65+math.ceil(len(subset)/cols)*cellh),BG)
        draw=ImageDraw.Draw(im)
        draw.text((12,8),f'{row["name"]} / {view} / {page+1}',font=TITLE,fill=FG)
        draw.text((12,35),'실제 저장 클립 · Player_Chibi 메시 · 검수용 장착 소품 · 게임 녹화 아님',font=BODY,fill=MUTED)
        for local,path in enumerate(subset):
            idx=page*per_page+local
            t=row['duration']*idx/(len(paths)-1)
            raw=Image.open(path).convert('RGB').resize((cellw,cellw),Image.Resampling.LANCZOS)
            x=(local%cols)*cellw; y=65+(local//cols)*cellh
            im.paste(raw,(x,y+27))
            draw.text((x+7,y+4),f'{idx:03d} · {t:.3f}s',font=BODY,fill=FG)
        output=OUT/'Contacts'/f'{row["name"]}_{view}_{page+1:02d}.jpg'
        output.parent.mkdir(parents=True,exist_ok=True)
        im.save(output,quality=94)
        pages.append(output)
    return pages

def label(raw,name,clip_t,state,viewer_t,size=560):
    image=raw.resize((size,size),Image.Resampling.LANCZOS)
    result=Image.new('RGB',(size,size+108),BG)
    draw=ImageDraw.Draw(result)
    draw.text((12,9),SPECS[name][0]+' · 2.5D 사이드뷰',font=TITLE,fill=FG)
    draw.text((12,37),SPECS[name][1],font=BODY,fill=MUTED)
    result.paste(image,(0,64))
    draw.text((12,size+74),f'{clip_t:.3f}s · {state}',font=BODY,fill=FG)
    draw.text((size-130,size+74),f'검수 {viewer_t:.2f}s',font=SMALL,fill=MUTED)
    return result

def timeline(row):
    name=row['name']; paths=frame_paths(row,'Side')
    frames=[Image.open(p).convert('RGB') for p in paths]
    intervals=len(frames)-1
    assert abs(intervals/30-row['duration'])<.001,(name,'Unsupported non30Hz clip')
    items=[]
    if name=='Ladder_Climb_Loop':
        # Do not duplicate the end key. It equals the start key of the next cycle.
        for cycle in range(2):
            for idx in range(intervals): items.append((frames[idx],idx/30,f'반복 {cycle+1}/2'))
        start_hold=end_hold=0
    else:
        start_hold=6; end_hold=30 if name=='Death_Fall' else 12
        items.extend((frames[0],0,'검수용 시작 정지') for _ in range(start_hold))
        items.extend((frames[idx],idx/30,'클립 재생') for idx in range(intervals))
        items.extend((frames[-1],row['duration'],'마지막 자세 유지' if name=='Death_Fall' else '검수용 끝 정지') for _ in range(end_hold))
    result=[label(raw,name,t,state,i/30) for i,(raw,t,state) in enumerate(items)]
    return result,{'clipDurationSeconds':row['duration'],'sourceFrames':len(paths),'sourceSampleRate':30,
        'firstFrameHoldSeconds':start_hold/30,'lastFrameHoldSeconds':end_hold/30,'cycles':2 if name=='Ladder_Climb_Loop' else 1,
        'clipLooping':name=='Ladder_Climb_Loop','previewLooping':True,'loopEndpointDuplicateRemoved':name=='Ladder_Climb_Loop'}

def encode(images,path):
    palette_source=Image.new('RGB',(images[0].width*4,images[0].height*3))
    for i in range(12): palette_source.paste(images[round(i*(len(images)-1)/11)],(i%4*images[0].width,i//4*images[0].height))
    palette=palette_source.quantize(colors=240,method=Image.Quantize.MEDIANCUT)
    encoded=[im.quantize(palette=palette,dither=Image.Dither.NONE) for im in images]
    durations=[round((i+1)*100/30)*10-round(i*100/30)*10 for i in range(len(encoded))]
    encoded[0].save(path,save_all=True,append_images=encoded[1:],loop=0,duration=durations,disposal=2,optimize=False)
    with Image.open(path) as im:
        count=im.n_frames; duration=sum((im.seek(i),im.info.get('duration',0))[1] for i in range(count)); dims=im.size
    assert count==len(images),(path,count,len(images))
    assert duration==sum(durations)
    return {'file':path.relative_to(review_root()).as_posix(),'sha256':sha(path),'encodedFrames':count,'durationMilliseconds':duration,'width':dims[0],'height':dims[1],'bytes':path.stat().st_size}

def gallery(rows,contacts):
    cards=[]
    for name in SPECS:
        links=' · '.join(f'<a href="{html.escape(p.relative_to(OUT).as_posix())}">{view} {i+1}</a>' for view in ['Side','Front','Opposite','ThreeQuarter'] for i,p in enumerate(contacts[(name,view)]))
        uri=(ROOT/rows[name]['path']).as_uri()
        cards.append(f'<article><h2>{html.escape(SPECS[name][0])}</h2><p class="name">{name} · {rows[name]["duration"]:.3f}초</p><img src="{name}_Side.gif" alt="{name}"><p>{SPECS[name][1]}</p><details><summary>다시점·전 프레임 검수 시트</summary><p>{links}</p></details><p><a href="{uri}">Unity 클립 파일</a></p></article>')
    doc='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>치비 리그 모션 검수</title>
<style>*{box-sizing:border-box}body{margin:0;background:#101820;color:#edf3f8;font:16px/1.65 system-ui,sans-serif}main{max-width:1240px;margin:auto;padding:28px 22px}h1{font-size:28px;margin:0 0 10px}h2{font-size:21px;margin:0}p{color:#bbcad5}a{color:#8fcdf2}img{display:block;width:100%;height:auto}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(330px,1fr));gap:18px}article{padding:16px;background:#1a2631;border:1px solid #344958;border-radius:10px}.name{font:13px/1.6 ui-monospace,monospace;word-break:break-word}.wide{margin:22px 0}.jump{max-width:700px;margin:auto}.subtle{font-size:14px}details{margin-top:12px}summary{cursor:pointer;color:#b9dbef}</style>
<main><h1>치비 리그 모션 검수</h1><p>현재 Player_Chibi 모델에 저장된 클립을 재생한 오프라인 검수입니다. 검 공격 3종과 사망·사다리 동작을 동일한 치비 리그로 확인합니다. 게임 플레이 녹화는 아닙니다.</p>
<p class="subtle">클립 구간은 원래 속도(30fps)입니다. 공격은 앞 0.2초/뒤 0.4초, 사망은 앞 0.2초/뒤 1초를 검수용으로 정지합니다. 사망 클립은 비반복이며 GIF만 다시 재생됩니다. 사다리는 정지 없이 2주기를 반복합니다.</p>
<div class="grid">'''+''.join(cards)+'''</div>
<article class="wide"><h2>현재 점프 클립의 의도된 연결</h2><p>대기 → 점프 시전 → 상승 → 정점 → 낙하 → 착지 → 대기. 기존 점프 클립은 수정하지 않았습니다.</p><div class="jump"><img src="Intended_Idle_Jump_Land_Side.gif" alt="현재 점프 클립 의도 연결"></div><p class="subtle">포즈 확인을 위한 오프라인 혼합이며 높이 0.66m·체공 1.2초로 축약했습니다. 실제 게임 물리나 Animator 전환을 녹화한 영상이 아닙니다.</p><details><summary>저장된 Air 클립 지정과 의도된 연결 비교</summary><p>현재 저장된 Player_Chibi.controller의 Air는 정점 클립 하나만 지정되어 있습니다. 왼쪽은 그 Air 클립 선택을, 오른쪽은 Rise/Apex/Fall을 연결한 의도를 같은 검수 궤적으로 비교합니다.</p><img src="Current_vs_Intended_Jump_Side.gif" alt="정점 단독과 상승 정점 낙하 연결 비교"></details></article>
<p><a href="../index.html">기존 전체 모션 갤러리</a></p></main></html>'''
    (OUT/'index.html').write_text(doc,encoding='utf-8')

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--contacts-only',action='store_true');args=parser.parse_args()
    rows=read_report();OUT.mkdir(parents=True,exist_ok=True)
    assert ROOT not in OUT.parents and OUT!=ROOT
    contacts={}
    for name in SPECS:
        for view in ['Side','Front','Opposite','ThreeQuarter']: contacts[(name,view)]=make_contacts(rows[name],view)
    contact_report={'sourceAuditSHA256':sha(HERE/'saved-clip-audit.json'),'pages':[str(p) for pages in contacts.values() for p in pages],'allSideFramesIncluded':True,'otherViewPhaseCount':13,'sourceClipsModified':False}
    (HERE/'action-contact-receipt.json').write_text(json.dumps(contact_report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'contactPages':len(contact_report['pages']),'directory':str(OUT/'Contacts')},ensure_ascii=False),flush=True)
    if args.contacts_only:return
    receipt=[]
    for name in SPECS:
        frames,metadata=timeline(rows[name]);entry=encode(frames,OUT/(name+'_Side.gif'));entry.update(metadata);entry['name']=name;receipt.append(entry)
    gallery(rows,contacts)
    result={'sourceAuditSHA256':sha(HERE/'saved-clip-audit.json'),'externalDirectory':str(OUT),'gifFiles':receipt,'sourceClipsModified':False,'gameplayRecording':False,'allFrameCountsAndDurationsVerified':True,'gallery':str(OUT/'index.html'),'visualReview':'Pending direct visual inspection of final rendered poses and GIFs.'}
    (HERE/'actions-gif-receipt.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False,indent=2))

if __name__=='__main__':main()
