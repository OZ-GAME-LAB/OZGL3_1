Project M — 주인공 자연스러운 모션 v005
2026-10-19 / Unity 6000.3.21f1 / URP 17.3

대상
별도 검수 프리팹과 14개 영문 클립이다. 기존 플레이어·게임 씬·Animator Controller·입력·공격 판정·아이템 코드에는 연결하지 않았다. VFX는 이번 작업 대상에 포함하지 않는다.

v004에서 확인된 원인과 수정
v004는 AI Idle의 1초 자세를 여러 행동의 neutral로 복제했다. 실제 모델의 기준 몸통은 거의 수직인데, 그 Idle에는 허리 뒤기울기와 고개 숙임이 있었다. 접지와 루프 수치가 정상이라는 이유로 자연스러운 연기까지 승인한 이전 판단은 적절하지 않았다.
v005는 모델의 직립 몸통과 실제 발바닥을 기준으로 별도 stance를 만들었다. 기본상태의 허리·어깨·목·머리를 재정렬하고, 이동 원본의 상수 자세 편향과 시간에 따른 변화량을 분리했다. 골반→하부 척추→흉곽의 전달 시점을 나눴으며 무기·포션·바닥 줍기는 실제 캐릭터의 도달 거리와 접촉 위치를 교정했다.

Clips/ — 고정 24본 계층의 Generic AnimationClip
Idle_Fists                 4.000s   직립 기본 대기, 닫힌 주먹, 호흡·느린 체중 이동, 반복
Walk_Fists                  .983s   새 Prime 원본의 보행 한 주기, 반복
Run_Fists                   .750s   새 Prime 원본의 달리기 한 주기, 반복
Jump_Full                  2.717s   준비·이륙·상승·정점·하강·착지·직립 복귀
Dash_Forward                .850s   골반부터 전경사, 압축·앞뒤 다리 drive·회복
Punch_Right_Jab            1.050s   오른손 직선 공격, 가드·신전·회수, 반대 손 가드
Sword_Horizontal_Slash     1.350s   허리 높이 횡베기, 몸통 전달·체중 이동·후속 회복
Sword_Aerial_Plunge        1.550s   공중에서 수직 하강, 검 내려찍기·착지 압축·회복
Staff_Cast_Forward         1.500s   전방 지팡이 시전과 회복
Hit_Reaction                .900s   짧은 recoil·압축·직립 복귀
Potion_Drink               2.800s   실제 얼굴의 입 높이에 병을 가져가 마신 후 내리기
Item_Throw_Overhand        2.967s   머리 위 투척, 손 놓기·전방 시선·회복
Interact_Reach             1.700s   가슴 높이 대상에 손 뻗기·펴기·회수
Pickup_Low                 2.150s   무릎·골반을 충분히 낮춰 지면 소품 높이에 손 도달·회수
사망 클립은 만들지 않았다.

AI 원본과 후보 선별
Sources/에는 신규 Meshy Text-to-Motion Prime FBX 후보 8개가 있다. 이번 추가 생성은 80크레딧이다. 후보를 모두 최종 결과로 채택하지 않았다. Walk/Run의 유효한 주기를 선택했고, 큰 고개 동작·원하지 않는 회전·다른 방향의 베기/시전·불완전한 시작 자세가 있는 후보는 제외했다. 후보 원본과 제외 근거는 ArtSources/Characters/Protagonist01/v005-natural-motion/RawCandidateReview 및 prime-generation-all.json을 참고한다.
Jump/Throw/Hit에는 기존 v004의 생성 원본을 재사용하되 새 기준 자세와 동작 의도에 맞춰 다시 편집했다. Hit는 원본의 긴 복통 자세를 사용하지 않고 첫 반응만 짧게 혼합했다. 소품 접촉과 게임용 단일 행동은 원래 리그의 제어 가능한 포즈/IK 곡선으로 편집한 뒤 베이크했다.
Sources의 Humanoid FBX와 Clips의 Generic 최종 클립은 서로 다른 단계의 자료다. 후보의 오류를 최종 동작의 오류와 혼동하지 말 것.

리그·손·프리팹
Prefabs/Protagonist01_MotionReview.prefab은 원래 모델/재질과 v004의 별도 손 메시를 참조한다. Grip_R/Grip_L=100은 닫힌 주먹, 75는 별도 무기 그립이다. 기존 모델의 손가락 본을 추가한 것은 아니다.
최종 클립은 동일한 본 이름과 부모 계층에 로컬 Transform 곡선을 60Hz로 기록한 Generic 클립이다. 포함된 프리팹과 같은 계층을 기준으로 사용한다. 기존 Humanoid Controller의 muscle 클립 슬롯에 그대로 대체하는 형태와 구분해야 한다. 검수 프리팹의 Avatar와 Runtime Controller는 비어 있다.
검/지팡이/포션은 기존 별도 프리팹을 검수 중 손 소켓에 임시 장착한다. 이번 작업은 무기 모델이나 환경 모델을 변경하지 않는다.

후속 적용 담당자 참고
캐릭터의 로컬 +Z가 전방이다. 검수에서는 부모를 회전해 화면 오른쪽을 바라보게 한다. 보행·달리기·대시는 제자리 모션이며 실제 게임 오브젝트의 이동은 포함하지 않는다.
Jump_Full과 Sword_Aerial_Plunge는 Hips에 수직 궤적을 포함한다. Jump의 이륙은 약 .60s, 착지는 약1.35s이며 Plunge의 지면 도달은 약 .58s다. 물리 Y 이동을 사용하는 담당자는 수직 궤적을 분리하거나 제거해 두 번 이동시키지 않도록 해야 한다. 이번 작업에는 물리 제어·Animator 전환·타격 이벤트를 연결하지 않았다.
GIF의 투척 물체 비행은 release 확인용 샘플이며 게임 투사체 구현이 아니다. 유한 행동의 GIF는 끝나면 처음으로 돌아가며 그 재시작은 Animator 상태 전환을 뜻하지 않는다.

검수 자료와 조사
ArtSources/Characters/Protagonist01/v005-natural-motion/ReviewOutput/index.html
개별 14개 GIF, 3개 묶음, 손 확대와 기존 v004 비교를 담는다. 실제 Unity 메시/클립의 1x 재생을 60Hz 평가하고 30fps로 렌더한 샘플이다. GIF의 30/40ms 시간 단위를 누적 보정해 원래 평균 속도를 유지한다.
진단 PNG/숫자는 관통·튀는 회전·경계 단절을 찾는 보조 수단이며 자연스러움 점수나 게임 플레이 성능 측정값이 아니다. 접지 판독용 바닥과 타원 표시를 사용한다.
research/korean-research.txt       국내 공개 실무 사례·공식 문서·커뮤니티 접근 기록
research/international-research.txt 해외 포럼·AI 제작 워크플로·공식 교차 근거
research/reference-breakdowns.txt  동작별 레퍼런스와 확인 수준·적용 범위
research/posture-root-cause.txt     실제 Unity/FK에서 확인한 자세 원인
research/applied-method.txt        이번 제작에서 적용한 방법

Editor 도구
NaturalMotionBuilder: 신규 후보 임포트와 별도 검수 프리팹
NaturalMotionBake: 자세 교정·동작 편집·최종 Generic 곡선 생성
NaturalMotionCapture: 임시 중립 장면의 실제 스킨/클립 렌더와 계측
NaturalMotionProbe: 기존 기준 자세와 원본/리타깃 상태의 읽기 전용 진단
모두 Editor 전용이며 게임 Update 경로가 없다. BuildFinalReview는 캐릭터만 렌더한다.
