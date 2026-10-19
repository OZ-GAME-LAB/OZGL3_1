Project M — Motion Polish v004 인계
작성일: 2026-10-19 / Unity 6000.3.21f1 / URP 17.3

범위
이 폴더는 모션 원본, 보정 클립, 별도 손 메시와 검수용 프리팹입니다. 기존 플레이어·Animator Controller·전투 스크립트·게임 씬·환경 자산에는 적용하지 않습니다. 사망 모션은 이번 수정 범위에서 제외했습니다.

최종 English 클립 — Clips/
Idle_Fists                 기본 대기, 양손 주먹, 반복
Walk_Fists                 걷기, 양손 주먹, 반복
Run_Fists                  달리기, 양손 주먹, 반복
Punch_Right_Jab            오른손 주먹 공격
Jump_Full                  점프 전 과정
Dash_Forward               진행 방향 대시
Hit_Reaction               피격 반응
Potion_Drink               포션 섭취
Item_Throw_Overhand        머리 위 투척
Interact_Reach             손 뻗기 상호작용
Pickup_Low                 낮은 위치 줍기
Sword_Horizontal_Slash     검 횡베기
Sword_Aerial_Plunge        공중 수직 낙하 내려찍기
Staff_Cast_Forward         전방 지팡이 시전

Sources와 Clips의 구분
Sources/*.fbx는 Meshy Prime으로 생성한 Humanoid 후보 14개입니다. 후보 생성에는 총 140크레딧(각 10크레딧)을 사용했습니다. 생성 기록은 ArtSources/Characters/Protagonist01/v004-motion-polish/prime-generation-final.json에 있습니다.
Clips/*.anim은 주인공의 고정 24본 리그와 원본 계층에 맞춰 로컬 Transform 곡선으로 베이크한 최종 Generic 클립입니다. Sources의 Humanoid 후보와 같은 파일이 아니며, 동일한 본 이름·계층·스킨을 기준으로 재생합니다. 최종 곡선은 60Hz로 베이크합니다. 기존 Humanoid Controller의 클립 슬롯에 그대로 대체하는 용도는 아니며, 포함된 Generic 검수 프리팹과 동일 계층을 기준으로 사용합니다.
검 횡베기·공중 내려찍기·대시·지팡이 시전·주먹 공격·포션 섭취의 6개 클립은 기존 리그에서 포즈와 타이밍을 새로 편집해 베이크했습니다. 다른 클립도 접지·보행 주기·방향·반복 구간을 검토해 정리했습니다. 로컬 편집과 VFX 샘플에는 추가 생성 크레딧을 사용하지 않았습니다.

이동과 후속 적용 담당자 참고
Walk_Fists/Run_Fists/Dash_Forward는 이동 입력이나 게임 오브젝트의 전진을 포함하지 않는 제자리 동작입니다. 검수 바닥은 고정되어 있습니다. 캐릭터의 로컬 +Z가 전방이며, 검수 장면에서는 부모를 회전시켜 화면 오른쪽을 향하게 했습니다.
Jump_Full은 준비→이륙→상승→정점→하강→착지→회복을 담은 유한 클립입니다. 이륙은 약0.60초, 착지는 약1.35초입니다. Sword_Aerial_Plunge는 공중에서 시작해 약0.58초에 지면에 도달합니다. 두 클립은 Hips에 수직 궤적을 포함합니다. 물리 이동과 함께 적용할 담당자는 수직 궤적을 분리하거나 제거해 이중 상승/낙하가 발생하지 않게 해야 합니다. 별도 물리 제어·Animator 전환·공격 판정·이벤트는 이번 패키지에 연결하지 않았습니다.
GIF의 투척 물체 비행은 손에서 놓는 시점을 확인하기 위한 샘플 궤적이며 게임 투사체 구현은 아닙니다. 캐릭터 클립 자체는 물체를 생성하거나 던지지 않습니다.

손 메시와 프리팹
Meshes/Protagonist01_PolishedHands.asset은 원본 스킨의 복사본입니다. Grip_R와 Grip_L은 각각 100에서 닫힌 주먹, 75에서 별도로 만든 무기 그립 형태를 사용합니다. 75는 주먹 변위를 단순히 75%만 적용한 모양이 아닙니다. 손가락 굽힘과 엄지 맞물림을 포함하고 원본 리그·UV·스키닝·재질을 보존합니다.
기본 Idle/Walk/Run은 양손 주먹을 유지합니다. 무기와 아이템 동작은 그립·펴기 곡선을 사용합니다. 최종 blendshape 곡선과 실제 손 모양을 함께 확인하세요.
Prefabs/Protagonist01_MotionReview.prefab은 검수 전용입니다. 최종 Generic 평가를 위해 Animator Avatar와 Runtime Controller를 비워 두며 자동 재생·게임 입력·전투 컴포넌트를 연결하지 않습니다.

편집·검수 도구
MotionPolishBuilder: 새 Sources 임포트와 별도 검수 프리팹 준비
MotionPolishBake: 원본 계층의 포즈 편집·접지·루프 구간 정리와 Generic 곡선 베이크
MotionPolishHandBuilder: 별도 손 메시와 75/100 그립 프레임 제작
MotionPolishReviewCapture: 임시 검수 장면에서 실제 스킨 포즈를 평가·렌더
모든 편집·캡처 도구는 Editor 전용이며 기존 런타임 동작에 연결하지 않습니다.

렌더와 보고서
ArtSources/Characters/Protagonist01/v004-motion-polish/
60Hz 포즈 평가에서 30fps PNG/GIF를 만듭니다. ReviewOutput은 최종 보정 Clips의 렌더이며 RawCandidateReview는 별도 보관한 원본 후보 검수입니다. 영상 파일명의 _SourceOnly는 해당 클립을 원래 속도 그대로 평가한 캡처 조건을 뜻하며, ReviewOutput에서는 Sources FBX가 아닌 최종 Generic Clips를 가리킵니다.
최신 재생 길이·접지·루프·손·경로 지표는 최종 캡처와 분석 보고서를 기준으로 확인합니다. 이 인계 문서 자체가 품질 합격 판정은 아닙니다. 60Hz/30fps는 검수 시간축 설정이며 실제 게임 CPU/GPU 성능 벤치마크나 목표 기기 프레임 보장 수치가 아닙니다. 이번 샘플의 런타임 성능 측정은 수행하지 않았습니다.
주요 기록: import-report.txt / polish-bake-report.txt / hand-polish-build.json / motion-review.json
최종 GIF 갤러리: ReviewOutput/index.html. GIF/에 개별 동작과 VFX, Groups/에 이동·전투·아이템·VFX 묶음이 있습니다. PNG와 GIF는 실제 Unity 메시·클립·VFX를 렌더한 결과이며, 게임 플레이 화면 녹화는 아닙니다. 바닥의 타원은 접지 판독을 위한 검수 표시입니다.

VFX 샘플
Assets/Art/CombatVFX_v1/Prefabs/의 DashBurst / SlashWave / MagicMissile / Explosion은 별도 샘플입니다. ProjectM.CombatVfxPreview.CombatVfxSample의 Play() 또는 SetPreviewTime()을 명시적으로 호출해 확인합니다. ParticleSystem.playOnAwake는 꺼져 있으며 기존 씬·전투·풀링에는 자동 연결하지 않습니다.
VFX 상세 사용법과 구성 예산은 Assets/Art/CombatVFX_v1/Samples/README_ko.txt 및 BuildReport.json을 확인하세요.
