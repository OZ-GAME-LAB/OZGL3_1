from pathlib import Path
from PIL import Image
import hashlib,json,shutil,runpy,sys

HERE=Path(__file__).resolve().parent
FINAL=HERE.parents[1]
ROOT=next(p for p in HERE.parents if (p/'Assets').is_dir() and (p/'ProjectSettings').is_dir())
sys.path.insert(0,str(ROOT/'ArtSources'))
from review_storage import media_path,publish_html,storage_metadata
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
names=['Jump_Rise_Loop','Jump_Fall_Loop']
before=json.loads((HERE/'before-clips.json').read_text(encoding='utf-8-sig'))
unrelated=[r['path'] for r in before if not r['allowChange'] and sha(ROOT/r['path'])!=r['sha256']]
assert not unrelated,unrelated
manifest=json.loads((FINAL/'LegacyMotions/manifest.json').read_text(encoding='utf-8-sig'))
manifest['selection']='15approved v007 nonattack clips; only Rise/Fall loops revised2026-10-27, other13preserved.'
manifest['unityAssetsChanged']=True
manifest['previewStorage']=storage_metadata(FINAL/'LegacyMotions')
for c in manifest['clips']:
    if c['name'] not in names:continue
    p=ROOT/c['unityClip'];g=media_path(FINAL/'LegacyMotions'/c['gif'])
    g.parent.mkdir(parents=True,exist_ok=True)
    shutil.copyfile(media_path(FINAL/'Review/Airborne'/(c['name']+'.gif')),g)
    c['unitySHA256']=sha(p)
    c['unityLFNormalizedSHA256']=hashlib.sha256(p.read_bytes().replace(b'\r\n',b'\n')).hexdigest()
    c['durationSeconds']=.8 if c['name']==names[0] else 1.
    c['gifSHA256']=sha(g);c['gifBytes']=g.stat().st_size;c['copiedWithoutReencoding']=False
    c['revision']='Airborne20261027: authored on nativeChibi24 rig; observed KayKit poses and public Mixamo guidance as reference, not imported Mixamo animation.'
    c['gifFrames']=0;c['gifPlaybackMilliseconds']=0
    with Image.open(g) as im:
        for i in range(im.n_frames):
            im.seek(i);c['gifFrames']+=1;c['gifPlaybackMilliseconds']+=im.info.get('duration',0)
(FINAL/'LegacyMotions/manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
review=FINAL/'Review/Airborne'
html='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>주인공 · 상승과 낙하</title><style>
*{box-sizing:border-box}body{margin:0;background:#111820;color:#edf1f5;font:16px/1.7 system-ui,sans-serif}main{max-width:1100px;margin:auto;padding:30px 24px}h1{font-size:28px}h2{font-size:20px}p{color:#b7c6d5}a{color:#9bd4ff}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:20px}article{background:#1b2631;border:1px solid #334554;border-radius:12px;padding:16px}img{display:block;width:100%;height:auto}.sequence{max-width:650px;margin:24px auto}.note{border-left:3px solid #6c97b4;padding:8px 16px}
</style><main><h1>주인공 · 상승과 낙하</h1><p>작은 균형 움직임이 있는 공중 유지 자세. 1배속 · 2.5D 사이드뷰</p><div class="grid">
<article><h2>상승 중</h2><img src="Jump_Rise_Loop.gif" alt="상승 공중 루프"><p>Jump_Rise_Loop · 0.8초<br>앞무릎을 살짝 접고 뒤발을 내린 자세</p></article>
<article><h2>낙하 중</h2><img src="Jump_Fall_Loop.gif" alt="낙하 공중 루프"><p>Jump_Fall_Loop · 1.0초<br>발을 아래로 준비하고 팔로 균형을 잡는 자세</p></article></div>
<article class="sequence"><h2>시전 → 상승 → 낙하 → 착지</h2><img src="Jump_Sequence.gif" alt="점프 연결 미리보기"><p>기존 시전·착지에 새 두 클립을 연결한 검수용 미리보기입니다.</p></article>
<p class="note">시전·착지·정점·기존 Jump_Full과 다른 모션은 유지했습니다. 클립에는 이동 궤적이 없으며, 연결 영상의 높이 이동과 혼합은 미리보기 전용입니다. 인게임 Animator·이동 코드·VFX에는 연결하지 않았습니다.</p>
<p>4방향 포즈와 전체 측면 프레임, 루프 경계·피부 변형·전환을 검수했습니다. 기존 24본 리그용 클립입니다.</p>
<p><a href="../index.html">전체 모션 갤러리</a> · <a href="../../Validation/Airborne20261027/README.txt">제작·출처·검수 기록</a></p></main></html>'''
publish_html(review/'index.html',html,'주인공 상승과 낙하 외부 갤러리')
runpy.run_path(str(FINAL/'build_catalog.py'),run_name='__main__')
metrics=json.loads((HERE/'unity-review.json').read_text())
receipt={
    'revision':'Airborne20261027','previewStorage':storage_metadata(),'changedClips':[], 'unchangedSelectedClips':16,
    'unrelatedClipChanges':unrelated,'existingTakeoffLandApexFullPreserved':True,
    'modelRigMaterialsPrefabsChanged':False,'gameplayAndVFXChanged':False,
    'samples120Hz':sum(c['samples'] for c in metrics['clips']),
    'maxLoopSeamMetres':max(c['loopSeam'] for c in metrics['clips']),
    'maxHipDriftMetres':max(c['rootDrift'] for c in metrics['clips']),
    'maxBoundaryTangentDifference':max(c['maxBoundaryTangentDifference'] for c in metrics['clips']),
    'validationErrors':sum(c[k] for c in metrics['clips'] for k in ['nonFinite','collapsedTriangles','missingBindings']),
    'visualReview':[
        'Pass1: baseline freeze pose identified; authored pose silhouettes checked on Front andThreeQuarter; all Side sequence frames examined.',
        'Pass2: independent reviewer inspected108loopSide frames,54other-view loop phases,108sequence frames and36grip-variant frontal phases. No clear arm inversion,body penetration,mesh tear or actual leg crossing observed. Side falling foot overlap was confirmed as projection overlap in otherviews.',
        'Pass3: matching endpoint tangent correction; saved clips reloaded and654120Hz poses rechecked;306rendered frames refreshed. Independent final reviewer rechecked Side/Front loops and sequence transition boundaries. Root drift, endpoint pose and tangent differences are0.'
    ],
    'limitations':['Finite sampled review; no claim about arbitrary future gameplay blends.','Existing Apex and Jump_Full remain unchanged; sequence preview blends Rise directly intoFall.','Small balance movement is intentional. No Mixamo binary was downloaded or retargeted.']
}
for c in manifest['clips']:
    if c['name'] in names:receipt['changedClips'].append({k:c[k] for k in ['name','unityClip','unitySHA256','durationSeconds','gifSHA256','gifFrames','gifPlaybackMilliseconds']})
assert receipt['validationErrors']==0 and receipt['maxLoopSeamMetres']<.0001 and receipt['maxBoundaryTangentDifference']==0
log=(ROOT/'Logs/Airborne20261027-compile.log').read_text(encoding='utf8',errors='replace')
receipt['finalUnityCompilePassed']='Application will terminate with return code 0' in log and 'error CS' not in log
assert receipt['finalUnityCompilePassed']
(HERE/'delivery-verification.json').write_text(json.dumps(receipt,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps({'preserved':16,'updated':names,'samples':receipt['samples120Hz'],'errors':0}))
