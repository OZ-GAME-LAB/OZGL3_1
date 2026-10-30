주인공 최종 사용 자산 — 2026-10-30

현재 선택: 치비용 검 공격3종 + 기존 v007 비공격15종 + 사망·사다리2종 = 20종.
선택된 모든 클립은 Chibi24 Generic 뼈 경로를 사용합니다. selection.json이 파일 목록입니다.
기존 이동·대기·점프 클립과 게임 코드·컨트롤러·플레이어 프리팹은 이번 작업에서 수정하지 않았습니다.

사용할 Unity 클립 폴더
Assets/Art/Characters/Protagonist01/ChibiMotion_v007/Clips

이번 추가 파일
- Longsword_Attack_Chop.anim: 내려베기, 0.7초, 비반복
- Longsword_Attack_Slice.anim: 가로베기, 0.6초, 비반복
- Longsword_Attack_Stab.anim: 찌르기, 0.7초, 비반복
- Death_Fall.anim: 사망, 1.0초, 비반복, 마지막0.2초 자세 유지
- Ladder_Climb_Loop.anim: 제자리 사다리 오르기, 0.8초, 반복

캐스팅 제외
Skill_Cast_Forward는 손 파지와 동작 품질 문제로2026-10-30 사용자 요청에 따라 폐기했다.
현재 최종 선택과 납품 목록에 캐스팅은 없다. 기존 캐스팅 클립을 게임에 연결하지 않는다.
스켈레톤 마법사 원본을 이용할 후속 캐스팅 후보는 이번20종과 별도로 검토한다.

공격 연결 시 주의
Combat/Clips에 있는 동명의 원본은 KayKit23 전용이며 Player_Chibi에 직접 재생되지 않습니다.
이번에 만든 ChibiMotion_v007/Clips 파일을 Attack 상태의 Motion으로 사용해야 합니다.
새 클립은 Visual/Armature/Hips/... 실제 Player_Chibi 경로로 베이크했으므로 Humanoid Avatar를 추가할 필요가 없습니다.
게임 컨트롤러의 공격 선택·전환·속도·히트 판정 로직은 담당자가 기존 규칙대로 연결합니다.

검 소품
ChibiMotion_v007/Prefabs/Longsword_Chibi_SocketProp.prefab
이 프리팹을 Player_Chibi의 RightHand/WeaponSocket 자식으로 두고 local position=0, rotation=identity, scale=1로 사용합니다.
해당 소켓은 실제 Player_Chibi에 이미 존재하며 새 공격 클립이 양손 파지에 필요한 소켓 위치·회전·크기를 재생합니다.
원본 Combat 검의 메시·재질을 재사용했습니다. 예전 +Y 축 검을 혼용하면 방향이 달라질 수 있습니다.
비공격 상태의 장착 여부·소켓 복귀·표시 정책은 게임 측에서 처리하며 이 작업은 런타임 장착 코드를 추가하지 않았습니다.

기존 클립과 점프 진단
기존 비공격15종은 그대로 유지했습니다. Rise/Fall은2026-10-27 버전이며 이번에는 수정하지 않았습니다.
2026-10-29 진단 기준 커밋c55551a3의 Player_Chibi.controller Air에는 Jump_Apex_Loop 하나만 연결되어 있었습니다.
해당 기준본에는 VelocityY를 이용하는 Rise/Apex/Fall BlendTree가 없었습니다. 최신develop 및 팀원 작업본은 담당자가 다시 확인해야 합니다.
클립 자체도 측면에서 자세 차이가 작습니다. 진단 수치와 상태 연결은 Validation/ChibiCompatibility20261029/Jump에 있습니다.
이번 GIF는 기존 클립의 Idle → Takeoff → Rise → Apex → Fall → Land → Idle 의도 순서를 보여줍니다.
높이0.66m·체공1.2초로 압축한 오프라인 자세 검수이며 실제 게임 물리/컨트롤러 재현 영상이 아닙니다.

GIF와 검수
모든 GIF/렌더 프레임은 프로젝트 밖 ~/Desktop/OZGL3_ArtReview에 저장합니다.
최신 갤러리: Characters/Protagonist01/Final/Review/Compatibility20261029/index.html
전체 선택 갤러리: Characters/Protagonist01/Final/Review/index.html
프로젝트의 Review/index.html은 외부 갤러리 안내용 파일입니다.
Validation/ChibiCompatibility20261029에 변환 소스, 파지 수치, 전체 메시 검수, 실제 Animator 재생 비교 및 인계 기록을 보관합니다.
검수용 임시 복제본에서만 런타임 스크립트/물리를 제거합니다. 실제 Player_Chibi와 Player_Chibi.controller는 변경하지 않습니다.

출처와 재현
- 검 공격: Combat 폴더에 보존한 승인된 KayKit23 공격3종 → 치비24본으로 재변환.
- 사망: 프로젝트의 KayKit Skeleton_Mage.fbx 내 Death_A.
- 사다리: 프로젝트에 이미 있는 Mixamo Climbing Ladder.fbx / Climb.anim. 원래 Mixamo Avatar에서 실제 관절 방향을 평가한 뒤 치비 Generic 곡선으로 베이크.
- Editor/CombatRetargetUtility.cs: 서로 다른 비율·기준 자세와 팔꿈치 굽힘 방향을 처리하는 오프라인 변환 도구.
- Editor/AdditionalMotionBuilder.cs: 사망·사다리 클립 생성.
- Editor/ChibiCompatibilityReview.cs 및 AnimatorCompatibilityValidator.cs: 실제 Player_Chibi 복제본의 저장 클립 검수.
라이선스와 원본 출처는 기존 Combat/Model 문서와 planning/Art_Assets_Provenance_and_QA_2026-10-27 보고서에도 기록되어 있습니다.

원본과 의존성 보존
Model의24본 GLB, LegacyMotions의 기존15종 목록, Combat의23본 Blender/클립/메시/재질을 재현용으로 보존합니다.
Combat 원본은 더 이상 최종 사용 클립 선택 목록에 포함하지 않습니다.
ChibiCap_v006 리그/손 morph/재질/텍스처, ChibiMotion_v007/SourceGait와 게임에서 사용하는 Runtime 자산은 유지합니다.
이전 정리·검수 기록은 Validation 아래 날짜별 폴더에 보존하며 현재 검수 증거와 혼동하지 않아야 합니다.
