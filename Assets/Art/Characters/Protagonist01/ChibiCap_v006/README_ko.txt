Project M — Chibi Cap v006 / 2026-10-21

새 주인공 한 개의 모델링 버전이다. 기존 v001~v005 주인공, 환경, 무기, VFX와 게임 플레이어 연결은 바꾸지 않는다.

외형
사용자 제공 Shorts와 cartridge102의 공식 공개 작품을 다시 시각 확인했다. 낮고 넓은 cap, 큰 둥근 얼굴, 짧고 넓은 몸통/팔다리, 큰 신발의 형태를 새 도시형 남자 주인공에 반영했다. 기존 navy/ivory/charcoal/teal 의상 방향을 이어간다.
약3등신의 제작 기준은 모자를 제외한 hair crown~턱을1, hair crown~신발 바닥을3으로 잡았다. 모자 높이까지 머리에 포함하면 더 짧은 비율로 보인다. 애니메이션용 모델 높이는 모자를 포함해1.2m로 균등 정규화한다.

전달 자산
Model/Protagonist_ChibiCap.fbx: 생성 원본 정적 T-pose FBX.
Textures/Protagonist_ChibiCap_BaseColor.png: 2K 컬러 텍스처.
Materials/Protagonist_ChibiCap_Unlit.mat: Unity URP Unlit 재질.
Prefabs/Protagonist_ChibiCap_Model.prefab: 정적 모델, 발바닥 원점·1.2m.
Rig/Protagonist_ChibiCap_Rig.fbx: 별도로 생성한24본 스킨 리그.
Meshes/Protagonist_ChibiCap_Fists.asset: 원본 스킨 스트림을 보존한 별도 손 morph 메시. Grip_R/L=100은 주먹,75는 무기 그립용 형태,0은 원본 열린 손이다.
Prefabs/Protagonist_ChibiCap_Rig.prefab: 새 체형·손 morph용 검수 프리팹. 컨트롤러나 게임 스크립트는 연결하지 않는다.
검수 프리팹의 Animator는 비활성 상태이며 Avatar/Runtime Controller는 비어 있다. 실제 적용 담당자가 사용할 재생 방식에 맞춰 활성화/설정해야 한다.
Clips/Punch_Elastic_Preview.anim: 새 체형에서 탄성 액션 방향을 확인하는 단일 Generic 샘플 클립. 실제 최종 경로/길이는 ElasticReview 기록을 우선한다.

모델 수치
생성 원본4225 geometry vertices·8079삼각형·재질1개. Unity에서는 UV와 normal 경계가 나뉘어 렌더 정점 수가 더 많다. 원본 정점 수와 렌더 정점 수를 혼동하지 않는다. 텍스처와 프리팹 외형은 실제 Unity 정면·측면·사선·후면으로 검수한다.

모션 범위
압축된 준비 → 빠른 타격 → 짧은 impact hold → 감쇠된 탄성 회수의 방향을 단일 샘플로 확인한다. 실제 공격 판정·히트스톱·카메라·입력 버퍼는 구현하지 않는다.
기존14개 모션 전체를 새 리그에 이식한 버전이 아니다. 클립은 해당 프리팹의 고정 계층용 Generic이며 Humanoid muscle 클립을 뜻하지 않는다. 기존 리그와 본 이름이 같더라도 새 체형의 모든 접촉/타이밍이 자동으로 맞는다고 가정하지 않는다.
검수 스크립트는 Editor 전용이다. 게임 런타임 코드, Animator Controller, 씬에는 적용하지 않는다. VFX는 작업 대상에서 제외한다.

원본과 검수 기록
ArtSources/Characters/Protagonist01/v006-chibi-cap/
  Protagonist_ChibiCap_Concept.png / concept-prompt.txt: image_gen으로 생성한3D 제작용 기준 이미지와 실제 프롬프트.
  Protagonist_ChibiCap.glb: 텍스처 포함 정적 교환 파일.
  rigging-bundle/: 스킨 리그GLB와 리깅에 무료 포함된Walk/Run 원본. 후자는 새 탄성 모션으로 승인한 클립이 아니다.
  generation.json: 생성 모델·설정·비용·작업ID.
  research/reference-analysis-2026-10-21.txt: 두 링크의 실제 확인 범위와 디자인 분석.
  research/visual-audit.txt: 기준 이미지 및 실제3D 시각 검수.
  ReviewOutput/index.html: 실제 Unity 모델 턴테이블 및 뷰별 검수 갤러리.
  ElasticReview/: 새 리그의 직립 주먹 스탠스와 펀치 샘플 실제30fps 원본, 저장 클립·접지·시선 검수 기록.
  vfx-unchanged-final.json: 기존 VFX54개 파일의 작업 전후SHA256 일치 확인.

생성 비용
Meshy T2.5 Smart Topology 컬러 모델15크레딧 + 리깅5크레딧, 합계20크레딧. 새 모션의 추가 유료 생성은 없다. 기준 이미지는 built-in image_gen으로 생성했다.
