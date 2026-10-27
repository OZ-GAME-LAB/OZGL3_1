주인공 상승 / 낙하 보완 — 2026-10-27

변경 대상
Jump_Rise_Loop: 0.8초 반복. 앞무릎과 뒤발에 작은 높이/앞뒤 차이를 둔 상승 유지 자세.
Jump_Fall_Loop: 1.0초 반복. 두 발을 아래로 준비하고 팔을 조금 벌려 균형을 잡는 낙하 유지 자세.
기존 Chibi24 Generic 리그, 원본 모델과 닫힌 주먹을 사용합니다. 새 리그나 모델은 만들지 않았습니다.
점프 시전/착지/정점/Jump_Full을 포함한 나머지 선택 클립 16개는 SHA256으로 보존 확인합니다.

원인과 제작
이전 Rise/Fall은 Jump_Full의 한 프레임을 복제한 완전 고정 포즈였습니다.
관찰한 기본 공중 자세를 3등신 주인공의 짧은 팔/다리에 맞춰 기존 Unity Editor 제작 도구에서 직접 편집했습니다.
손목은 전완 방향으로 정렬하고 양팔은 안정된 팔꿈치 굽힘 평면을 사용합니다. 큰 팔 휘두름이나 공중 달리기는 넣지 않았습니다.
60Hz Generic 곡선으로 저장하고 반복 시작/끝 자세와 곡선 접선을 일치시켰습니다.
중심 골반에는 이동 궤적이 없으며, 고도와 속도는 인게임 담당자가 제어해야 합니다. 게임 코드는 수정하지 않았습니다.
재생성: Unity 메뉴 Project M > Chibi Compact v007 > Build Airborne Loops Only.
Editor/ChibiAirborneMotion.cs가 이 두 클립만 저장합니다. ChibiMotionBuild.cs 전체 제작 경로에도 같은 정의를 연결했습니다.

참고 출처
Adobe Mixamo 공식 사용법: https://helpx.adobe.com/creative-cloud/help/animate-characters-mixamo.html
캐릭터 위에서 포즈를 보고 속도/팔 간격을 조정하는 공식 작업 흐름을 참고했습니다. JS 기반 Mixamo 개별 Falling Idle 영상을 직접 재생한 것으로 주장하지 않습니다.
KayKit 공식 모션 팩: https://kaylousberg.itch.io/kaykit-character-animations
실제 관찰 원본: ArtSources/Monsters/Stage01/Melee01/v001-native-motion/SourceAssets/Skeleton_Minion.glb
기존 source-provenance.json에 기록된 KayKit Skeletons1.0 CC0, 커밋15b62b9bad122f72926c10fb14d622c73819fa54.
Jump_Idle(1.0667초), Jump_Full_Short(1.1667초)를 각각8시점·측면/정면 총32컷 렌더하여 확인했습니다.
KayKit의 큰 팔 벌림을 그대로 복사하지 않고 주인공의 짧은 팔과 액션게임용 실루엣에 맞춰 줄였습니다.
Animation Mentor: https://www.animationmentor.com/blog/tutorial-animate-a-jump-and-land/
몸의 시간차, 다리의 작은 비대칭, 정면/측면 교차 검수와 궤적 검토 참고.
Crytek: https://www.cryengine.com/docs/static/engines/cryengine-5/categories/23756816/pages/69468585
Mixamo Jump/Falling Idle을 별도 공중 동작으로 사용하는 공식 구성 예제 참고.
Epic: https://dev.epicgames.com/documentation/unreal-engine/adding-character-animation-in-unreal-engine?lang=en-US
점프 시작·공중 루프·착지를 분리하는 구조 참고.
로컬 Mixamo X Bot@Jumping.fbx도 확인했으나 원본이 약0.5초 구간이므로 긴 공중 루프의 직접 소스로 사용하지 않았습니다.
이번 두 클립은 레퍼런스를 보고 직접 만든 것이며 Mixamo/KayKit 클립을 다운로드·직접 리타기팅한 결과물이 아닙니다.

검수
1차: 기존 고정 포즈 원인 확인, 새 상승/낙하 실루엣과 실제 변형 메시 확인.
2차: 독립 검수자가 4방향 및 연결 시퀀스와 손 메시 변형을 교차 확인. 명확한 역꺾임/찢어짐/몸통 관통/다리 교차를 관찰하지 못했습니다.
3차: 루프 접선 보완 후 재저장된 클립을 다시 읽어 3프리팹 ×2클립 총654포즈를120Hz로 검사. 306이미지 재렌더.
수치/마커: unity-review.json. 이전 1차 수치는 pass1-unity-review.json.
보존/최종 파일 해시: before-clips.json, delivery-verification.json. GIF: gif-receipt.json.
Contacts는 전체 측면 루프와 연결 시퀀스, 다른 시점의 위상별 검수 시트입니다.

표시 범위
Review/Airborne/Jump_Sequence.gif는 기존 시전·착지에 새 Rise/Fall을 오프라인으로 혼합한 미리보기입니다.
높이 궤적은 렌더용 부모에만 적용했습니다. 기존 Jump_Full 파일이나 인게임 전환/물리/Animator에는 반영하지 않았습니다.
단독 GIF의 팔다리 변화량은 의도적으로 작습니다. 모든 미래 게임 전환에서 관통이 없음을 보장하는 검사는 아닙니다.
Final/Validation/Review20261027은 이번 상승/낙하 수정 전의 역사적 검수 기록입니다. planning의 종합 출처 보고서는 이번 변경과 외부GIF저장 정책까지 갱신했습니다.
GIF기본위치: ~/Desktop/OZGL3_ArtReview/Characters/Protagonist01/Final. 프로젝트 안의 Review HTML은 외부 갤러리 포털입니다.
프로젝트 안의 구GIF 삭제는 자동 승인 검토가 차단하여 미완료이며, 새GIF는 외부에만 게시하고 Git 대상에서 제외합니다.
