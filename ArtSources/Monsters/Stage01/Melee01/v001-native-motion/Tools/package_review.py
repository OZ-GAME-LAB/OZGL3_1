"""Package actual rendered frames; no pose generation or image synthesis."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json, hashlib, html, sys
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'Review'
PROJECT=next(p for p in ROOT.parents if (p/'Assets').is_dir() and (p/'ProjectSettings').is_dir())
sys.path.insert(0,str(PROJECT/'ArtSources'))
from review_storage import media_path,publish_html,storage_metadata
media_path(OUT).mkdir(parents=True,exist_ok=True)
manifest=json.loads((ROOT/'QA/build-manifest.json').read_text())
font_path='C:/Windows/Fonts/malgun.ttf'
font=ImageFont.truetype(font_path,18);small=ImageFont.truetype(font_path,13)
labels={'Idle':'대기','Move':'제자리 달리기','Attack':'한손 내려치기','Hit':'피격'}
summaries=[]
for clip,seconds in manifest['durations_seconds'].items():
    n=round(seconds*30)
    frames=[]
    # Last duplicate endpoint is in the PNG sequence for loop inspection,
    # but omitted from GIF playback to preserve exact source cycle duration.
    for f in range(n):
        source=OUT/'Frames'/clip/f'{f:04}.png'
        if not source.exists():raise RuntimeError(f'Missing rendered frame {source}')
        im=Image.open(source).convert('RGB')
        canvas=Image.new('RGB',(im.width,im.height+64),'#17232d');canvas.paste(im,(0,64));d=ImageDraw.Draw(canvas)
        d.text((18,8),f'{labels[clip]} · {clip} · 원본 시간 {f/30:.3f}s',font=font,fill='#f1f3ee')
        d.text((18,36),'주인공 v016 참고 자세  |  근접형 샘플 1종  |  동일 단위·직교 측면 카메라',font=small,fill='#b8c7cd')
        frames.append(canvas)
    palette=frames[0].quantize(colors=256)
    q=[im.quantize(palette=palette,dither=Image.Dither.NONE)for im in frames]
    # GIF delays are centiseconds: distribute rounding instead of changing FPS.
    for speed,suffix in [(1,'1x'),(.5,'05x')]:
        delays=[(round((i+1)*100/(30*speed))-round(i*100/(30*speed)))*10 for i in range(n)]
        q[0].save(media_path(OUT/f'{clip}_{suffix}.gif'),save_all=True,append_images=q[1:],duration=delays,loop=0,disposal=2,optimize=False)
    sheets=[]
    paths=[OUT/'Frames'/clip/f'{f:04}.png'for f in range(n+1)]
    for page in range((len(paths)+35)//36):
        batch=paths[page*36:(page+1)*36];cols=6;rows=(len(batch)+cols-1)//cols
        sheet=Image.new('RGB',(1200,rows*132),'#17232d');d=ImageDraw.Draw(sheet)
        for i,p in enumerate(batch):
            x=(i%cols)*200;y=(i//cols)*132
            img=Image.open(p).convert('RGB').resize((200,112));sheet.paste(img,(x,y+20));d.text((x+6,y+2),f'{clip} f{int(p.stem):03}  {int(p.stem)/30:.3f}s',font=small,fill='white')
        name=f'{clip}_AllFrames_{page+1:02}.jpg';sheet.save(OUT/name,quality=90);sheets.append(name)
    summaries.append({'clip':clip,'seconds':seconds,'frames':n+1,'gifCycleMs':sum((round((i+1)*100/30)-round(i*100/30))*10 for i in range(n)),'allFrameSheets':sheets})

keycontact=Image.new('RGB',(1440,4*260),'#17232d');d=ImageDraw.Draw(keycontact)
for row,view in enumerate(['Side','Front','Opposite','Back']):
    for f in range(6):
        path=OUT/'Keys'/f'Attack_{view}_{f:02}.png'
        im=Image.open(path).convert('RGB').resize((240,240));keycontact.paste(im,(f*240,row*260+20));d.text((f*240+8,row*260+2),f'{view} · pose{f+1}',font=small,fill='white')
keycontact.save(OUT/'Attack_KeyPoses.jpg',quality=92)

body='''<!doctype html><html lang="ko"><meta charset="utf-8"><title>1스테이지 근접형 한 종 검수</title>
<style>body{max-width:1060px;margin:32px auto;padding:0 24px;background:#14212b;color:#eef1ec;font:16px/1.7 system-ui}h1{font-size:30px}h2{margin-top:36px}a{color:#91d5dc}img{width:100%;height:auto;border-radius:5px}article{margin:38px 0}summary{cursor:pointer;padding:10px 0}.note{color:#bdc9cc}.links{display:flex;gap:24px;flex-wrap:wrap}code{color:#d1ddc7}</style>
<h1>1스테이지 근접형 샘플</h1>
<p>청록 망토 해골 근접병 한 종입니다. KayKit의 원래 모델·리그·동작을 사용했고 주인공 v016과 동일 단위의 직교 측면 카메라에 배치했습니다. 종족과 한손 칼은 이번 샘플의 외형·공격 제안입니다.</p>
<p class="note">왼쪽 주인공은 크기 비교용 정지 자세입니다. 오른쪽 몬스터는 Idle / Move / Attack / Hit 네 클립입니다. 뼈 궤적을 새로 합성하지 않았으며 원본 동작의 속도를 유지했습니다.</p>
<div class="links"><a href="../Stage01_Melee01.blend">편집용 Blender</a><a href="../Exports/Stage01_Melee01.fbx">Unity FBX</a><a href="../QA/UnityRendered/index.html">실제 Unity 스킨·네 방향·클립 연결</a><a href="../FINAL_REVIEW.txt">검수 기록과 한계</a><a href="../Research/asset-comparison.txt">에셋 비교</a><a href="../Research/source-provenance.json">출처·라이선스</a></div>
'''
for row in summaries:
    clip=row['clip'];body+=f'<article><h2>{labels[clip]} <code>{clip}</code></h2><p>{row["seconds"]:.3f}초 · '+('제자리 반복'if clip in ['Idle','Move'] else'단발 동작')+f'</p><img src="{clip}_1x.gif" alt="{labels[clip]} 동일 배율 원속도"><details><summary>0.5배속 전체 동작</summary><img src="{clip}_05x.gif" alt="{labels[clip]} 느린 재생"></details><details><summary>모든 프레임 펼쳐 보기</summary>'
    for name in row['allFrameSheets']:body+=f'<img src="{name}" alt="{clip} 전체 프레임">'
    body+='</details></article>'
body+='''<h2>공격 주요 자세 네 방향</h2><img src="Attack_KeyPoses.jpg" alt="공격 측면 정면 반대 후면 주요 자세">
<p>원본의 준비·타격·회수 구조를 유지했습니다. 공격 판정 이벤트·게임 이동 속도·실제 Animator 전환은 연결하지 않았습니다. 제자리 달리기의 발 접지는 최종 게임 이동 속도와 함께 확인해야 합니다. Unity 페이지의 0.12초 연결은 검수용 미리보기입니다.</p></html>'''
publish_html(OUT/'index.html',body,'1스테이지 몬스터 외부 갤러리')
(OUT/'presentation-manifest.json').write_text(json.dumps({'previewStorage':storage_metadata(OUT),'source_blend_sha256':hashlib.sha256((ROOT/'Stage01_Melee01.blend').read_bytes()).hexdigest(),'source_fbx_sha256':hashlib.sha256((ROOT/'Exports/Stage01_Melee01.fbx').read_bytes()).hexdigest(),'clips':summaries,'camera':'same orthographic camera and world units for both characters','protagonist':'static evaluated mesh copied read-only from v016','rendering':'Blender Cycles actual evaluated skin; Unity render separately linked'},indent=2))
print('PACKAGED',len(summaries),'clips')
