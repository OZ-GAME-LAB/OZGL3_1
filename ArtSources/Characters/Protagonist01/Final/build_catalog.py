from pathlib import Path
import json,html,sys
P=Path(__file__).resolve().parent;W=next(p for p in P.parents if (p/'Assets').is_dir() and (p/'ProjectSettings').is_dir())
sys.path.insert(0,str(W/'ArtSources'))
from review_storage import publish_html,storage_metadata,media_path
old=json.loads((P/'LegacyMotions/manifest.json').read_text())
attack_names=['Longsword_Attack_Chop','Longsword_Attack_Slice','Longsword_Attack_Stab']
labels={'Longsword_Attack_Chop':'내려베기','Longsword_Attack_Slice':'가로베기','Longsword_Attack_Stab':'찌르기','Idle_Fists':'대기','Walk_Fists':'걷기','Run_Fists':'달리기','Jump_Full':'점프','Dash_Forward':'대시','Potion_Drink':'포션 섭취','Item_Throw_Overhand':'투척','Hit_Reaction':'피격','Interact_Reach':'상호작용','Pickup_Low':'줍기','Jump_Takeoff':'도약','Jump_Rise_Loop':'상승','Jump_Apex_Loop':'정점','Jump_Fall_Loop':'낙하','Jump_Land':'착지','Death_Fall':'사망','Ladder_Climb_Loop':'사다리 오르기'}
chibi_root='Assets/Art/Characters/Protagonist01/ChibiMotion_v007'
player_prefab='Assets/2.Prefab/Player/Player_Chibi.prefab'
records=[];cards=[]
for name in attack_names:
 records.append({'name':name,'group':'attack','unityClip':chibi_root+'/Clips/'+name+'.anim','compatiblePrefab':player_prefab,'rig':'Chibi24','equippedPropPrefab':chibi_root+'/Prefabs/Longsword_Chibi_SocketProp.prefab','socket':'Visual/Armature/Hips/Spine02/Spine01/Spine/RightShoulder/RightArm/RightForeArm/RightHand/WeaponSocket','sourceClip':'Assets/Art/Characters/Protagonist01/Combat/Clips/'+name+'.anim','gif':'Compatibility20261029/'+name+'_Side.gif'})
for name in ['Death_Fall','Ladder_Climb_Loop']:
 records.append({'name':name,'group':'utility','unityClip':chibi_root+'/Clips/'+name+'.anim','compatiblePrefab':player_prefab,'rig':'Chibi24','gif':'Compatibility20261029/'+name+'_Side.gif'})
for c in old['clips']:
 group='air' if c['auxiliary'] else 'movement' if c['name'] in ['Idle_Fists','Walk_Fists','Run_Fists','Jump_Full','Dash_Forward'] else 'utility'
 records.append({'name':c['name'],'group':group,'unityClip':c['unityClip'],'compatiblePrefab':c['compatiblePrefab'],'rig':'Chibi24','gif':'../LegacyMotions/'+c['gif']})
for r in records:
 name=r['name'];source='KayKit 공격 → Player_Chibi 24본 변환' if r['group']=='attack' else '새 치비용 클립 · 24본 리그' if name in ['Death_Fall','Ladder_Climb_Loop'] else '공중 자세 루프 · 이번 작업에서는 원본 유지' if name in ['Jump_Rise_Loop','Jump_Fall_Loop'] else '기존 v007 승인본 · 24본 리그'
 cards.append(f'<article data-group="{r["group"]}"><h2>{labels[name]}</h2><img loading="lazy" src="{r["gif"]}" alt="{labels[name]} 사이드뷰 GIF"><p>{name}</p><small>{source}</small></article>')
doc='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>주인공 · 최종 사용 모션</title><style>
*{box-sizing:border-box}body{margin:0;background:#111820;color:#edf1f5;font:15px/1.6 system-ui,sans-serif}main{max-width:1450px;margin:auto;padding:32px 24px 60px}h1{margin:0 0 8px;font-size:30px}h2{margin:12px 16px;font-size:18px}.lead,small{color:#b7c6d5}.notice{padding:16px 20px;background:#253240;border-radius:10px;margin:20px 0}.toolbar{display:flex;gap:9px;flex-wrap:wrap;margin:22px 0}button{background:#263543;border:1px solid #4a6174;border-radius:8px;color:white;padding:9px 16px;cursor:pointer}button.on{background:#4a6174}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(300px,1fr));gap:20px}article{background:#1b2631;border:1px solid #334554;border-radius:12px;overflow:hidden}img{display:block;width:100%;height:auto}article p{margin:12px 16px 3px;font-size:13px;font-weight:bold}small{display:block;margin:0 16px 16px}a{color:#9bd4ff}.footer{margin:25px 0;color:#b5c3cf;font-size:13px}
</style><main><h1>주인공 · 최종 사용 모션</h1><p class="lead">치비용 검 공격 3종 + 기존 비공격 15종 + 사망·사다리 2종. 캐스팅은2026-10-30 폐기 요청에 따라 제외했습니다. <a href="Compatibility20261029/index.html">새 클립 및 점프 연결 검수</a></p>
<div class="notice">검 공격을 실제 Player_Chibi의 Visual/Armature 뼈 경로에 맞는 24본 Generic 클립으로 변환했습니다. Combat 폴더의 23본 원본 대신 ChibiMotion_v007/Clips 파일을 사용합니다. 검은 Longsword_Chibi_SocketProp을 WeaponSocket에 장착합니다. 게임 컨트롤러·코드 연결은 이번 작업에서 변경하지 않았습니다. 점프 GIF는 기존 클립으로 만든 오프라인 자세 검수이며 게임 플레이 녹화가 아닙니다.</div>
<div class="toolbar"><button class="on" onclick="filter('all',this)">전체 20종</button><button onclick="filter('attack',this)">검 공격 3종</button><button onclick="filter('movement',this)">이동·대기</button><button onclick="filter('utility',this)">사망·사다리·보조</button><button onclick="filter('air',this)">점프 단계</button></div><div class="grid">'''+''.join(cards)+'''</div>
<p class="footer"><a href="../README.txt">최종 파일 안내</a> · <a href="../Combat/Protagonist_Combat.blend">공격 Blender 원본</a> · <a href="../Model/Protagonist_ChibiCap_Rig.glb">기존 캐릭터 모델 원본</a><br>현재 게임 씬에서 사용 중인 런타임 자산과 비공격 모션 제작에 필요한 원본 의존성은 보존했습니다.</p></main><script>function filter(g,b){document.querySelectorAll('article').forEach(a=>a.style.display=g==='all'||a.dataset.group===g?'':'none');document.querySelectorAll('button').forEach(x=>x.classList.remove('on'));b.classList.add('on')}</script></html>'''
publish_html(P/'Review/index.html',doc,'주인공 최종 모션 외부 갤러리')
compatibility_portal=P/'Review/Compatibility20261029/index.html'
compatibility_portal.parent.mkdir(parents=True,exist_ok=True)
compatibility_url=html.escape(media_path(compatibility_portal).as_uri(),quote=True)
compatibility_portal.write_text('<!doctype html><html lang="ko"><meta charset="utf-8"><title>치비 리그 모션 검수</title><body><h1>치비 리그 모션 검수</h1><p>GIF는 프로젝트 외부에 저장됩니다.</p><p><a href="'+compatibility_url+'">외부 검수 갤러리 열기</a></p></body></html>',encoding='utf8')
(P/'selection.json').write_text(json.dumps({'userSelection':'3 approved sword attacks retargeted to Chibi24 +15 unchanged v007 nonattack clips +2 new Chibi24 utility clips; rejected casting excluded, 2026-10-30','count':20,'runtimeIntegrationChangedThisTask':False,'existingGameClipReferences':['Hit_Reaction','Walk_Fists','Idle_Fists','Dash_Forward','Jump_Apex_Loop','Jump_Takeoff','Jump_Land','Run_Fists'],'newAirborneClipsIntegrated':False,'previewStorage':storage_metadata(P/'Review'),'rigCompatibility':'All selected clips now use native Chibi24 Generic paths. Original KayKit23 Combat clips remain retarget source material. Assign the new ChibiMotion_v007 clips to the gameplay controller; no controller asset was edited.','clips':records},ensure_ascii=False,indent=2),encoding='utf8')
print('FINAL_CATALOG_READY',len(records))
