Chibi Motion v007 — Compact Action

대상: v006 ChibiCap 모델의 동일한 24본 계층. v007에 별도 복제한 CylinderGrip 메시를 사용합니다.
원본 v006 모델은 보존하고, 오른손의 무기용 Grip_R 150/175 형상을 추가하고 손끝 스키닝을 교정했습니다.
기존 Grip_R 75/100 형상은 보존했습니다. 75는 기존 소품용, 150은 지팡이, 175는 가는 검 손잡이 전용 그립입니다.
원본 7,101개 정점과 삼각형 구성을 유지합니다. 무기용 손가락·엄지 형상과 손끝 스키닝을 보정하며, 손목을 강제로 밀어내는 보정과 세분화는 적용하지 않았습니다.
검수 프리팹: Prefabs/Protagonist_Chibi_MotionReview.prefab
전 클립은 60Hz Generic Transform 곡선과 손 Grip blendshape 곡선으로 저장되어 있습니다.
모델의 뼈 길이를 늘리지 않고 새 비율에 맞춰 팔·다리 IK와 몸의 무게 이동을 편집했습니다.
Idle / Walk / Run은 직립, 전방 시선, 닫힌 주먹 상태입니다.
지팡이는 오른손 Grip 150, 검은 175, 맨손은 100이며, 섭취·투척·줍기 중에는 기존 0~100 범위에서만 손을 엽니다.

기본 13개 (초, 정확한 타이밍은 검수 폴더의 build-report / clip-timing.csv 참고)
Idle_Fists                 2.800  대기, 반복
Walk_Fists                 0.867  제자리 걷기, 반복
Run_Fists                  0.633  제자리 달리기, 반복
Jump_Full                  1.200  준비 → 상승 → 정점 → 하강 → 착지 → 회복
Dash_Forward               0.667  작은 준비 → 전방 체중 이동 → 짧은 유지 → 회복
Punch_Right_Jab            0.533  준비 → 빠른 잽 → 타격 유지 → 회수
Sword_Horizontal_Slash     0.667  안쪽 → 바깥쪽 단 한 번의 횡베기 → 팔/손잡이 회수 → 칼끝 상향 가드
Staff_Cast_Forward         0.667  짧은 준비 → 연속적인 전방 캐스팅과 작은 반동 → 회수
Potion_Drink               2.000  병 올리기 → 입 접촉 / 기울이기 → 내리기
Item_Throw_Overhand        1.300  머리 옆 준비 → 전방 투척 → 회수
Hit_Reaction               0.667  짧은 피격 반동 → 감쇠 회복
Interact_Reach             0.933  가슴 높이 접촉 → 짧은 유지 → 회수
Pickup_Low                 1.333  쪼그려 앉기 → 바닥에 손가락 접촉 → 쥐기 → 일어서기

공중 / 착지 보조 5개
Jump_Takeoff / Jump_Rise_Loop / Jump_Apex_Loop / Jump_Fall_Loop / Jump_Land
Rise/Apex/Fall 유지 클립은 고정된 짧은 포즈 루프입니다. 모든 높이·속도 조건에서의 상태 전환을 자동 구현하지 않습니다.

이동 구분
Walk / Run / Dash는 캐릭터 루트의 수평 이동을 포함하지 않는 제자리 클립입니다.
Jump_Full은 전체 동작을 보여주기 위한 Hips의 수직 궤적을 포함합니다.
보조 5개는 공중 궤적 높이를 제거했습니다. 담당자가 물리 이동을 제어할 경우 이 보조 클립을 사용하고
Full의 Hips 높이와 캐릭터 물리 높이를 중복 적용하지 않아야 합니다.
Air 유지 클립에서 접지 IK를 강제하면 공중 포즈가 망가지므로 공중/지상 구분은 담당자가 연결해야 합니다.

무기 / 소품 검수
Staff.prefab의 머리는 기존 수정보다 작은 반투명 수정구로 교체했습니다. 손잡이와 떠 있는 간격, 소켓 메타데이터는 유지합니다.
검수 장면에서 Sword / 수정된 Staff / Potion_Health를 장착합니다. 게임 코드와 VFX는 변경하지 않았습니다.
검 / 지팡이: 검수 scale .70, 손 기준 미터 오프셋 (-.024, .068, -.0205), localRotation X 45도.
손잡이는 모델 +Y, 손아귀 원통은 손 (0, .7071, .7071) 대각 축이며 실제 손가락과 엄지가 둘러싸도록 보정했습니다.
무기 클립은 장비를 쥔 가드에서 시작하고 같은 가드로 회복합니다. 맨손 대기와 오른손 포즈는 다릅니다.
검의 회수에서 칼날을 아래로 접는 동작은 제거했습니다. 베는 동안 칼날의 넓은 면은 수평면을 유지합니다.
포션: 검수 scale .85, 손 기준 미터 오프셋 (.040, .025, .045), localRotation Z 90도.
실제 Transform.localPosition / Scale에는 손 bone.lossyScale 보정이 필요합니다. 재현 구현은 Editor/ChibiMotionCapture.cs의 ReviewProp 참고.
장착 / 해제 / 투척 물리 / 판정 이벤트는 클립에 심지 않았습니다. 투척 시각은 clip-timing.csv의 releaseSeconds 참고.
WeaponSocket / WeaponSocket_Left는 검수 프리팹의 각 손 아래에 존재하며 게임 장착 로직은 연결하지 않았습니다.
오른손 WeaponSocket에는 위 위치·회전·scale 보정이 이미 들어 있습니다. 소켓 자식으로 렌더 소품을 넣을 때 localPosition=0 / localRotation=identity / localScale=1로 두며 보정을 중복 적용하지 않습니다.
기존 Stage01Kit의 SocketWeapon 컴포넌트는 이전 캐릭터용 설정을 보존했습니다. 이번 검수는 그 게임 장착 메서드를 호출하지 않고, 실제 렌더 소품을 새 v007 장착 프레임으로 놓은 결과입니다.

제작 / 검수 범위
전투·유틸리티는 새 모델에서 동작별 준비·타격/접촉·회수를 편집했습니다.
걷기·달리기는 보존된 v005 AI 보행의 지지·스윙 구조를 새 다리 길이에 맞춰 각도와 발 궤적을 재적용했습니다.
손/발은 실제 변형 메시를 측정하고, 전체 모션 카메라는 고정된 2.5D 사이드뷰를 사용했습니다.
카메라는 정측면에서 수평 약 6도, 위에서 약 11.25도입니다. 수평 베기의 깊이와 날 면을 읽히게 하는 동일한 검수 카메라이며 게임 카메라 코드는 변경하지 않았습니다.
손잡이 확대 GIF 2개는 같은 각도에서 손잡이 중심만 따라갑니다. 별도 모션이나 다른 속도를 사용하지 않습니다.
GIF는 Unity에 저장된 .anim을 다시 읽어 실제 skin을 렌더한 720x720 / 30fps / 1배속입니다.
유한 기본 동작에는 끝자세 확인용 8프레임(약 .267초)을 더 표시하며, .anim 자체의 길이는 늘리지 않았습니다.
2개 전환 GIF는 별도 검수 장면에서 저장 클립을 혼합한 결과입니다. 지상 보행 전환에는 혼합 중의 발 접지 보정도 사용했습니다.
이 접지 보정은 검수 스크립트에만 있으며 게임에는 적용하지 않았습니다. 플레이어 입력, 충돌, 공격 캔슬과 물리 처리는 검수 대상이 아닙니다.
프리팹의 Animator는 비활성화되어 있고 Controller / Avatar가 지정되지 않았습니다. 현재 게임에 자동 적용되지 않습니다.
새 스크립트는 전부 Editor 폴더 + UNITY_EDITOR 안에 있으며 게임 플레이 코드나 VFX를 변경하지 않습니다.
추가 Meshy / Comfy 생성 크레딧은 사용하지 않았습니다.

검수 결과 / GIF:
ArtSources/Characters/Protagonist01/v007-compact-action/ReviewOutput/index.html
실제 캡처: review-report.json / 빌드: build-report.json / 검수 기록: qa-review.txt
조사 근거와 관찰 범위: research/sword-reference-research.txt / research/animation-reference-crosscheck.txt
