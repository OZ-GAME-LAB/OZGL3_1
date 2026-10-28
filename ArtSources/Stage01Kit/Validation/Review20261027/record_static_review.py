import json, hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parent
REPO=ROOT.parents[3]
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
report=json.loads((ROOT/'unity-static-audit.json').read_text(encoding='utf-8-sig'))
summary=json.loads((ROOT/'numerical-summary.json').read_text())
inv=json.loads((ROOT/'nonhero-source-inventory.json').read_text(encoding='utf-8-sig'))
identity=[]
for field in ('prefabs','sourceFBX','generatedMeshes','localAuthoringTools'):
 for x in inv['stage01'][field]:
  p=REPO/x['path']; identity.append({'path':x['path'],'before':x['sha256'],'after':sha(p),'identical':sha(p)==x['sha256']})
assert all(x['identical'] for x in identity)
review={
 'date':'2026-10-27',
 'method':'Actual view_image examination of eight labelled contact sheets compiled from freshly rendered Unity prefabs, every prefab in side and three-quarter view, followed by the full-size orb transparency witness. No image content was retouched; contact sheets only resize/place the original captures.',
 'unityVersion':report['unity'],
 'coverage':{'retainedPrefabs':31,'sideViews':31,'threeQuarterViews':31,'transparencyWitness':1,'reviewedContactSheets':8,'uniqueMeshesNumericallyInspected':34,'inventoryMeshAssets':35,'meshCountExplanation':'The source inventory retains 35 generated mesh .asset files; 34 unique mesh objects are referenced by the 31 delivered prefabs. The remaining Weapons/Staff.asset is the original pre-orb source mesh retained for the editable orb builder, not another active staff head. Prefabs include variants and assembled blocks; they are not 31 unique models.'},
 'numericalSummary':summary,
 'visualFindings':[
  'All 31 prefab silhouettes and assigned materials render. No missing/pink material, invisible asset, disconnected platform wall/roof mass, or gross corruption was observed in these two views.',
  'The 16 modular pieces and the two assembled tutorial/stage blocks retain their subdued gray-blue urban palette. Static assembly review does not validate player collision or traversability.',
  'The seven Meshy environmental props retain their intentionally faceted source shapes. The tree foliage and fine AC/vending-machine lines remain coarse low-poly detail; no additional mesh defect was established that warrants altering previously approved geometry.',
  'All four potion variants render distinct palette values; health uses its cross symbol. The three buff bottles retain the simple existing mark. No design or icon changes were made.',
  'The retained short Sword is straight with coherent grip/guard/blade. It is a separate source prop from the hero approved combat longsword.',
  'The Staff has a smaller faceted sphere with a visible gap above the shaft. The yellow/blue witness remains clearly visible through the sphere with the expected tinted alpha blend; this verifies practical transparency rather than merely checking material alpha.'
 ],
 'blockingDefectsFound':[],
 'assetFixes':[],
 'unchangedAssetProof':identity,
 'reviewedContacts':[{'path':p.relative_to(ROOT).as_posix(),'sha256':sha(p)} for p in sorted((ROOT/'StaticContacts').glob('*.jpg'))],
 'renderEvidence':[{'path':p.relative_to(ROOT).as_posix(),'sha256':sha(p)} for p in sorted((ROOT/'StaticFrames').glob('*.png'))],
 'limitations':['Two static views per prefab at LOD0; hidden interior faces and all possible camera angles are not certified.','All referenced mesh objects were numerically inspected, including LOD variants; LOD transitions were not played or performance-profiled.','No target-device GPU/frame-time assessment, lighting integration, gameplay collision/path traversal, weapon equip logic, or VFX audit.','This is current approved asset review, not a new style redesign.']
}
(ROOT/'visual-review.json').write_text(json.dumps(review,ensure_ascii=False,indent=2),encoding='utf-8')
text='''1스테이지 환경·소품·무기 검수 (2026-10-27)

실제 Unity 프로젝트에서 현재 프리팹 31개를 모두 측면/3분면으로 새로 렌더하고 62개 모델 이미지를 확인했다. 구성은 모듈16, 소품11(환경7+포션4), 무기2, 조립 블록2다. 이는 고유 모델31종이라는 뜻은 아니다.

현재 프리팹에서 참조하는 고유 메시34개(총49,202정점,20,458삼각형)를 검사했다. NaN/무한값, 잘못된 인덱스, 퇴화 삼각형, 읽을 수 없는 메시, 누락 스크립트·재질·셰이더는 모두0건이다. 보존된 메시 .asset35개 중 현재 프리팹이 참조하는 것은34개다. 나머지 Weapons/Staff.asset은 구형 머리로 바꾸기 전의 원본이며, 편집 가능한 스태프 제작 도구의 입력으로 보존된 것이다.

8개 시트로 전체 모델의 실루엣과 면·재질을 직접 확인했다. 추가 수정이 필요한 확정적 결함은 발견하지 못했다. 기존 승인된 회청색 도시 팔레트, 로우폴리 면, 건물과 플랫폼 형태를 유지했다. 원본 AI 소품의 나뭇잎 및 자판기/실외기 세부 선은 단순하고 거친 표현으로 남아 있다. 승인된 스타일을 임의로 다시 만들지 않았다.

스태프는 작은 구형 머리와 몸체 사이의 간격이 보인다. 별도 노랑/파랑 배경판을 구 뒤에 두고 촬영하여 실제 구를 통과하는 배경색과 반투명 색 혼합을 확인했다. 알파 숫자만 읽고 투명하다고 판정하지 않았다. 기존 스태프의 구 지름0.19m/간격0.045m는 변경하지 않았다.

검은 Stage01Kit의 기존 한손검 소품이며, 주인공 Final/Combat의 롱소드와 별도다. 포션4종, 튜토리얼/1스테이지 조립 블록도 검사했다.

모델·프리팹·메시·제작 스크립트 원본의 전후 해시를 비교하여 변경 없음을 확인했다. 이번 작업은 검수 자료와 재실행 가능한 임시 Editor 도구만 추가했다. LOD0 두 시점에서의 시각 검수이며, 플레이어 충돌/경사/이동 경로, 실제 장착 로직, 게임 조명, 기기별 성능, LOD 전환과 VFX 검수는 포함하지 않는다.
'''
(ROOT/'FINDINGS_ko.txt').write_text(text,encoding='utf-8')
print(json.dumps({'verifiedUnchangedFiles':len(identity),'renderEvidence':len(review['renderEvidence']),'visualReview':str(ROOT/'visual-review.json')},ensure_ascii=False))
