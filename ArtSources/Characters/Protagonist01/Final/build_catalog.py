from pathlib import Path
import json,html
P=Path(__file__).resolve().parent;W=next(p for p in P.parents if (p/'Assets').is_dir() and (p/'ProjectSettings').is_dir())
old=json.loads((P/'LegacyMotions/manifest.json').read_text())
attack_names=['Longsword_Attack_Chop','Longsword_Attack_Slice','Longsword_Attack_Stab']
labels={'Longsword_Attack_Chop':'내려베기','Longsword_Attack_Slice':'가로베기','Longsword_Attack_Stab':'찌르기','Idle_Fists':'대기','Walk_Fists':'걷기','Run_Fists':'달리기','Jump_Full':'점프','Dash_Forward':'대시','Potion_Drink':'포션 섭취','Item_Throw_Overhand':'투척','Hit_Reaction':'피격','Interact_Reach':'상호작용','Pickup_Low':'줍기','Jump_Takeoff':'도약','Jump_Rise_Loop':'상승','Jump_Apex_Loop':'정점','Jump_Fall_Loop':'낙하','Jump_Land':'착지'}
combat_prefab='Assets/Art/Characters/Protagonist01/Combat/Prefabs/Protagonist_Combat.prefab'
records=[];cards=[]
for name in attack_names:
 records.append({'name':name,'group':'attack','unityClip':'Assets/Art/Characters/Protagonist01/Combat/Clips/'+name+'.anim','compatiblePrefab':combat_prefab,'rig':'KayKit23','gif':'Combat/'+name+'_Side.gif'})
for c in old['clips']:
 group='air' if c['auxiliary'] else 'movement' if c['name'] in ['Idle_Fists','Walk_Fists','Run_Fists','Jump_Full','Dash_Forward'] else 'utility'
 records.append({'name':c['name'],'group':group,'unityClip':c['unityClip'],'compatiblePrefab':c['compatiblePrefab'],'rig':'Chibi24','gif':'../LegacyMotions/'+c['gif']})
for r in records:
 name=r['name'];source='최신 검 공격 · 전용 23본 리그' if r['group']=='attack' else '기존 v007 승인본 · 24본 리그'
 cards.append(f'<article data-group="{r["group"]}"><h2>{labels[name]}</h2><img loading="lazy" src="{r["gif"]}" alt="{labels[name]} 사이드뷰 GIF"><p>{name}</p><small>{source}</small></article>')
doc='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>주인공 · 최종 사용 모션</title><style>
*{box-sizing:border-box}body{margin:0;background:#111820;color:#edf1f5;font:15px/1.6 system-ui,sans-serif}main{max-width:1450px;margin:auto;padding:32px 24px 60px}h1{margin:0 0 8px;font-size:30px}h2{margin:12px 16px;font-size:18px}.lead,small{color:#b7c6d5}.notice{padding:16px 20px;background:#253240;border-radius:10px;margin:20px 0}.toolbar{display:flex;gap:9px;flex-wrap:wrap;margin:22px 0}button{background:#263543;border:1px solid #4a6174;border-radius:8px;color:white;padding:9px 16px;cursor:pointer}button.on{background:#4a6174}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(300px,1fr));gap:20px}article{background:#1b2631;border:1px solid #334554;border-radius:12px;overflow:hidden}img{display:block;width:100%;height:auto}article p{margin:12px 16px 3px;font-size:13px;font-weight:bold}small{display:block;margin:0 16px 16px}a{color:#9bd4ff}.footer{margin:25px 0;color:#b5c3cf;font-size:13px}
</style><main><h1>주인공 · 최종 사용 모션</h1><p class="lead">검 공격 3종 + 기존 v007 비공격 15종. 2026년 10월 27일 전체 검수 및 공격 모델의 팔꿈치 스킨 보정.</p>
<div class="notice">공격 타이밍과 관절 움직임은 유지하고, 팔꿈치 주변 소매의 과도한 접힘을 국소적으로 완화했습니다. 기존 모션은 24본 리그, 검 공격은 23본 리그의 전용 프리팹에 대응합니다. 두 세트는 같은 Animator에 바로 혼용할 수 없으며, 한 캐릭터로 연결하려면 별도 리타기팅 작업이 필요합니다.</div>
<div class="toolbar"><button class="on" onclick="filter('all',this)">전체 18종</button><button onclick="filter('attack',this)">검 공격 3종</button><button onclick="filter('movement',this)">이동·대기</button><button onclick="filter('utility',this)">상호작용·피격</button><button onclick="filter('air',this)">점프 단계</button></div><div class="grid">'''+''.join(cards)+'''</div>
<p class="footer"><a href="../README.txt">최종 파일 안내</a> · <a href="../Combat/Protagonist_Combat.blend">공격 Blender 원본</a> · <a href="../Model/Protagonist_ChibiCap_Rig.glb">기존 캐릭터 모델 원본</a><br>현재 게임 씬에서 사용 중인 런타임 자산과 비공격 모션 제작에 필요한 원본 의존성은 보존했습니다.</p></main><script>function filter(g,b){document.querySelectorAll('article').forEach(a=>a.style.display=g==='all'||a.dataset.group===g?'':'none');document.querySelectorAll('button').forEach(x=>x.classList.remove('on'));b.classList.add('on')}</script></html>'''
(P/'Review/index.html').write_text(doc,encoding='utf8')
(P/'selection.json').write_text(json.dumps({'userSelection':'3 latest sword attacks + unchanged existing v007 nonattack clips','count':18,'runtimeIntegrated':False,'rigCompatibility':'Two native Generic rigs. No direct common Animator compatibility; future retargeting is required for a single rig.','clips':records},ensure_ascii=False,indent=2),encoding='utf8')
print('FINAL_CATALOG_READY',len(records))
