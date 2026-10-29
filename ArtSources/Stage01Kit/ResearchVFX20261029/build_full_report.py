import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[2]
u = json.loads((ROOT/'unity-research.json').read_text(encoding='utf-8-sig'))
e = json.loads((ROOT/'external-research.json').read_text(encoding='utf-8-sig'))
s = json.loads((ROOT/'supplementary-research.json').read_text(encoding='utf-8-sig'))
p = json.loads((ROOT/'strategy.json').read_text(encoding='utf-8-sig'))
audit = json.loads((ROOT/'project-audit.json').read_text(encoding='utf-8-sig'))
lines = []
def line(t=''): lines.append(str(t))
def sec(t): line(); line(t); line('='*68); line()
def item(k,v):
    if v is None or v == '' or v == []: return
    if isinstance(v,list): v = ' / '.join(map(str,v))
    if isinstance(v,dict): v = '; '.join(f'{a}: {b}' for a,b in v.items())
    line(f'{k}: {v}')

line(p['title'])
line('기준일 2026년 10월 29일 Asia/Seoul')
line('프로젝트 Unity 6000.3.21f1 / URP 17.3.0 / 정면 직교 카메라의 2.5D 전투')
line(p['status'])
line('공식 무료표기 17개 Unity Store 패키지와 외부 무료 재료·도구를 조사했다. 중복 4건을 합치면 43개 제품군이다. 이 수치는 모두를 도입하거나 모두가 무조건 상업 이용 가능한 완성 VFX라는 뜻이 아니다.')

sec('1 추천 방향과 우리 게임의 기준')
line(p['recommendation']); line(); line(p['why'])
line(); line('게임 스타일 판단은 저장된 최신 치비 캐릭터/롱소드 공격 검수 이미지와 도시 이미지, 현재 카메라·재질 설정을 대조한 결과다. 공식 이미지 직접 시각 확인은 CFX/Free Slash/Simple FX의 정지 프리뷰에 한정하며 나머지 후보의 스타일 적합도는 제품 설명과 기술정보 기반의 잠정 판단이다.')
for k,v in p['style'].items(): item({'physical':'물리','magic':'마법','explosion':'폭발','ground':'지면과 범위','enemy':'적 공격','avoid':'낮출 요소','timing_starting_values':'시간 시작안'}.get(k,k),v)
line(); line('현재 환경 팔레트 참고: '+str(audit['context']['base_environment_palette']))

sec('2 먼저 비교할 조합')
for x in p['first_ab_test']:
    line(x['slot']); item('우선',x['first']); item('대안',x['alternative']); item('비교 기준',x['compare']); line()
line('Vefects Flipbook Lite는 Store판의 무료·Standard EULA를 확인했다. 다만 무료판의 색상 선택/RGBA 마스크/Emission 편집 기능 제한으로 우리 팔레트 통일에는 불리하므로 최종 우선순위는 예비 비교군이다. 일부 개별 조사에서의 상위 A/B 제안보다 기능 제약을 무겁게 반영했다.')
line('가장 적게 섞는 안은 CFX+Free Slash 2팩이다. 실제로 꼬리가 부족한 경우 Trails URP를 추가한다. Magic Effects FREE 한 팩으로 넓게 맞추는 안도 대조할 수 있으나 Unity6·재질 변환은 먼저 검증한다.')

sec('3 스킬별 적용 계획')
for x in p['skill_mapping']:
    line(x['skill']); item('형태',x['visual']); item('후보',x['source']); item('현재 구현과 연결',x['integration']); line()
line('스킬의 수치/개수/틱/속도는 조사 시점의 직렬화 값과 코드를 설명한다. 이 보고서가 밸런스를 확정하거나 바꾼 것은 아니다. 모든 SkillData의 CastDuration=.4초이며 연쇄 마지막 생성의 명목시간=.48초와 차이가 있어 취소·시전 종료 시 연출을 어떻게 처리할지 팀이 일치시켜야 한다.')

sec('4 기존 CombatVFX 샘플의 처리')
for k,v in p['existing_sample_decision'].items():
    item({'keep':'보존','adapt':'가공','pitfalls':'연결 시 확인할 점'}.get(k,k),v)
line('샘플 구성값: DashBurst .60초, SlashWave .95초, MagicMissile1.05초, Explosion1.55초. ParticleSystem MaxParticles 합계는 각각14/12/16/61. 이는 살아 있는 입자 수·드로콜·GPU실측값이 아니다.')
line('현재 6개 스킬은 이미 판정과 프리팹 연결을 갖고 있다. 2026-10-19 vfx-plan.txt의 TestLogSkill만 존재한다는 설명은 오래되었다. 기존 문서는 수정하지 않고 이 보고서에 현재 상태를 기록했다.')

sec('5 직접 제작할 경우의 현실적인 방식')
for k,v in p['self_authoring'].items():
    label={'recommended':'제작 방향','blocks':'공용 블록','workflow':'작업 순서','ai_role':'AI의 역할','tool_options':'무료 도구','effort_estimates':'예상 공수'}.get(k,k)
    if k in ('blocks','workflow'):
        line(label)
        for i,z in enumerate(v,1): line(f'  {i}. {z}')
    else: item(label,v)
    line()
line('텍스처의 시트 크기와 재생 길이를 구분한다. 예를 들어 16프레임을16fps로 재생하면1초가 되어 .1초대 참격에는 길다. 프레임 수를 줄이거나 재생 속도를 조절하고 마지막 프레임의 알파를 확인한다. 플립북이 예쁘다는 이유로 타격 프레임을 지연시키지 않는다.')

sec('6 팀원에게 전달할 규격과 일정')
for k,v in p['handoff_contract'].items(): item({'prefabs':'전달할 9개 variant','transform':'좌표','lifecycle':'수명','pool':'풀링','metadata':'첨부 정보','team_reply_draft':'팀원 전달 문안 초안'}.get(k,k),v); line()
line('실행 순서')
for z in p['execution_plan']: line(z)
line(); line('초기 성능 목표는 일반단발12~48입자, 큰폭발48~96입자·MaxParticles합128 이하, 공유재질3~4개에서 시작할 수 있다. 이전 연구의 설계 목표를 출발값으로만 사용하고 실제 전체화면 투명면적/겹침·CPU/GPU를 보고 조절한다. 현재 보고서에는 성능 측정 결과가 없다.')

sec('7 무료와 라이선스를 확인하는 기준')
for z in p['free_and_license_rules']: line('- '+z)
line('Unity 표준 EULA: https://unity.com/legal/as-terms')
line('팀 안에서도 해당 Asset의 사용자·seat 조건을 확인하고 각자 무료 취득 기록을 남기는 편이 단순하다. 원본 패키지와 바뀐 프리팹을 공개적으로 재배포하는 일은 게임 실행본에 포함해 배포하는 것과 다르다.')

sec('8 Unity Asset Store 무료 패키지 전체 17개')
for x in u['entries']:
    line(f"{x['id']} {x['title']} — {x['publisher']}")
    line(x['url'])
    for a,b in [('priority','권장 순위'),('price','현재 가격'),('contents','무료 포함 범위'),('compatibility','공식 호환 표기'),('latest_version','버전'),('latest_release','기록된 갱신일'),('size','패키지 크기'),('visual_review','시각 확인 범위'),('style_fit','우리 스타일'),('adaptation_cost','수정 비용'),('risk','제한'),('license','라이선스')]: item(b,x.get(a))
    if x['id']=='U17': line('날짜 보충: root의 직접 웹 조회는 Jul27,2026, 다른 지역 응답에는7월28일. 동일v1.0.1이며 하루 차이를 별도 버전으로 계산하지 않는다.')
    line()

sec('9 다른 배포처의 무료 에셋과 재료')
duplicate_external={'E19':'U17','E20':'U03'}
for x in e['entries']:
    x = dict(x)
    if x['id']=='E19':
        x['recommendation']='최종 B 예비 비교군. Store판 무료·상업 라이선스는 확인했지만 Lite의 색상 편집 기능 제한으로 팔레트 통일 비용이 있다. 유료 Full 기능은 무료판의 장점으로 계산하지 않는다.'
        x['style_fit']='중간 잠정. 2.5D 방향은 맞지만 무료판 색상 제약과 픽셀 변형을 실제 화면에서 확인해야 함.'
    line(f"{x['id']} {x['name']} — {x['creator']}")
    line(x['url'])
    if x['id'] in duplicate_external: line('중복 제품군: '+duplicate_external[x['id']]+'와 같은 제품군. 별도 효과 수로 추가 집계하지 않음. 배포판별 약관과 바이너리는 별도 확인.')
    for a,b in [('site','배포처'),('free','무료 범위'),('license','라이선스'),('commercial','상업 게임'),('credit','표시'),('redistribution','원본 배포'),('format','형식'),('contents','내용'),('compatibility','Unity 적용'),('work','필요 작업'),('style_fit','적합도'),('fit_reason','판단 근거'),('skill_use','사용처'),('recommendation','판단'),('verification','확인 범위')]: item(b,x.get(a))
    for z in x.get('evidence_urls',[]): line('추가 근거 '+z)
    line()

sec('10 추가 제작 도구와 배포처')
duplicate_supp={"LintyTextures":"E10","JangaFX Free VDB Volumes":"E17"}
for i,x in enumerate(s['candidates'],1):
    line(f"S{i:02} {x['name']} — {x['author']}"); line(x['url'])
    if x['name'] in duplicate_supp: line('중복 제품군: '+duplicate_supp[x['name']]+'와 같은 재료. 별도 집계하지 않음.')
    for a,b in [('site','배포처'),('price','무료 범위'),('license','라이선스'),('format','형식'),('compatibility','호환과 작업'),('contents','제공 범위'),('fit','추천'),('work','주의할 작업'),('status','확인 상태')]: item(b,x.get(a))
    item('라이선스 근거',x.get('license_url')); line()

sec('11 조사했지만 우선 도입에서 제외한 자료')
for x in s['excluded_or_reference_only']:
    line(x['name']+' / '+x['site']); line(x['url']); line(x['reason']); line()
line('Unity 유료 및 필수 유료 의존성')
line(json.dumps(u.get('paid_exclusion',[]),ensure_ascii=False,indent=2))
line(); line('추가 제외 기준')
for x in e.get('additional_exclusions',[]): line('- '+x)

sec('12 배포처별 조사 결론')
for x in e.get('sites_surveyed',[]):
    x = dict(x)
    if x['name'].startswith('Vefects'):
        x['result']='itch 별도 배포판에는 상업 계약이 미기재된 경우가 있다. Lite395420와 Trails242574는 Unity Store판의 무료·Standard EULA를 확인했다. Wind/Shockwaves itch판은 조건부다. 직접 웹스토어의 유료 상품과 구분한다.'
    item(x['name'],x['result']); line(x['url']); line()
for title,result in [
 ('Unity Asset Store','무료17개 상세 확인. 공식 URP/Unity6 지원과 제작자 설명·버전 조건을 분리.'),
 ('Gumroad','LeLu 무료 텍스처의 상업 사용 가능. CC0와 pack 재배포 금지 표현이 병기되어 단순 CC0로 재해석하지 않음.'),
 ('Fab','무료 Niagara 샘플은 UnrealEngine형식이라 현재 Unity 직접 적용에서 제외. 라이선스와 엔진 호환은 별개.'),
 ('CGTrader 및 Free3D','VFX 명칭으로 검색되는 모델·Alembic·IP기반 효과가 많아 현재 스타일의 무료 URP 완성품을 확정하지 못함.'),
 ('GDevelop','CC0 픽셀마법20개는 확인했지만 포장이 GDevelop객체여서 Unity재구성 필요.'),
 ('CrunchyVFX','무료CC0출력 웹도구. 픽셀/UI보조 제작 후보이며 실출력과Unity재생은 미실시.')]: item(title,result)

sec('13 검증의 범위와 근거 파일')
line(p['research_limits'])
line('현재 페이지를 확인한 증거와 프로젝트의 파일 감사는 별개다. 공식 호환표를 읽은 후보를 인게임 테스트 완료라고 부르지 않았다. 수량을 알 수 없는 무료판에 유료 전체판의 프리팹 수를 붙이지 않았다. 조건부 후보와 도구를 완성 효과 개수로 세지 않았다.')
line('43개 계산: Unity17+외부22+추가8−중복4(Linty/JangaFX/Trails/Lite). 이 중 코드툴·학습용 편집샘플·이미지재료·조건부 자료도 포함하며, 11절에 별도로 모은 제외 목록은 이 숫자에 더하지 않았다.')
line('직접 수정한 것은 이 조사 문서/데이터/작성도구뿐이다. 런타임 코드·씬·프리팹·패키지 설정·기존 VFX는 이번 조사에서 변경하지 않았다.')
for file in ['strategy.json','unity-research.json','external-research.json','supplementary-research.json','project-audit.txt','project-audit.json']:
    line(str(ROOT/file))
line(); line('공식 기술 문서')
for x in s['technical_sources']: item(x['supports'],x['url'])

out=PROJECT/'planning'/'Project_M_무료VFX_전체조사_2026-10-29.txt'
out.write_text('\n'.join(lines)+'\n',encoding='utf-8')
index={
 'date':'2026-10-29','timezone':'Asia/Seoul','unique_product_families_and_tools':43,
 'raw_rows':47,'deduplicated_pairs':[['U17','E19'],['U03','E20'],['E10','S02'],['E17','S05']],
 'not_a_claim':'43개 모두 완성 VFX/상업허가확정/인게임검증완료라는 의미가 아님',
 'report':str(out),'asset_imported':False,'runtime_modified':False,
 'sources':{'unity':len(u['entries']),'external':len(e['entries']),'supplementary':len(s['candidates'])},
 'primary_recommendation':['U01','U02'],'optional_complement':['U03','E01','E02'],
 'ab_alternatives':['U06','U07','U04','U05'],'lite_final_rank':'B; color editing restrictions',
 'document_status':'TXT complete; separate DOCX digest in preparation'
}
(ROOT/'research-index.json').write_text(json.dumps(index,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'output':str(out),'characters':sum(map(len,lines)),'lines':len(lines),'unique_families':43},ensure_ascii=False))
