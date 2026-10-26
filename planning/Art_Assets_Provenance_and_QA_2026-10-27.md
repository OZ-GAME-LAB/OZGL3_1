# Project M 아트 에셋 출처와 검수 보고서

2026년 10월 27일 | 개발팀 인수인계용 | 작성 Codex

현재 보존된 주인공은 기존 이동·생활 동작 15개와 최신 롱소드 공격 3개, 총 18개 클립으로 구성된다. 두 묶음은 서로 다른 Generic 리그를 사용하므로, 지금 상태에서 하나의 캐릭터 Animator에 모두 연결할 수는 없다. 이번 보고서는 에셋이 어디서 왔고 어떻게 가공되었는지, 어느 파일이 정본인지, 무엇을 검수했고 무엇이 아직 통합되지 않았는지를 구분한다.

조사 대상은 주인공 Final 정본과 보존 중인 Runtime 비주얼, 튜토리얼·1스테이지 환경 키트, Melee01 몬스터다. 기존 VFX는 목록만 확인한다. 기존 게임 시스템·UI·외부 패키지 전체의 라이선스 감사나 실제 게임 플레이 성능 검증을 수행했다는 뜻은 아니다. 기획 기준은 같은 폴더의 Project M 상세기획서 0.5와 기획데이터이며, 현재 에셋 목록이 기획상 모든 몬스터와 스테이지 콘텐츠를 충족하는 것은 아니다.

## 1 현재 보존된 결과물

| 분류 | 현재 수량 | 제작 방식 | 사용 상태 |
| --- | --- | --- | --- |
| 주인공 원본 모델 | 1 | 생성 콘셉트 이미지 → Meshy 모델·리그 → 로컬 가공 | 3등신 chibi 정본 |
| 기존 Runtime 비주얼 | 1 | 이전 비주얼과 동작 전달 파일 보존 | 현재 Player 의존성 |
| 기존 주인공 동작 | 15 | 기존 AI 보행 입력과 Unity Editor 곡선·포즈 가공 | Chibi24 전용 |
| 최신 롱소드 공격 | 3 | KayKit 실제 동작 → 체형 맞춤 리타게팅·양손 접촉 보정 | KayKit23 전용 |
| 도시 환경 모듈 | 16 | Unity Editor에서 로컬 메시 제작 | 독립 프리팹 |
| 환경 소품 | 7 | Meshy 생성 후 팔레트·피벗·프리팹 가공 | 독립 프리팹 |
| 포션 | 4 | Unity Editor에서 로컬 메시·색상 변형 제작 | 독립 프리팹 |
| 원래 검과 스태프 | 2 | Meshy 원형과 로컬 수정 | 최신 롱소드와 별개 |
| 튜토리얼 및 스테이지 블록 | 2 | 위 모듈·소품의 조립 배치 | 완성 게임 씬과 별개 |
| Melee01 몬스터 | 1종 4클립 | KayKit Skeletons 1.0의 동일 모델·원래 리그·내장 동작 | 별도 프리팹 |
| 전투 VFX 샘플 | 4 | Unity 메시·ParticleSystem 기반 | 보류 중인 기존 샘플 |

Stage01Kit의 31개 프리팹은 모듈 16개, 소품 11개인 환경 소품 7개와 포션 4개, 무기 2개, 조립 블록 2개를 합한 수이다. 같은 에셋의 LOD나 조립 블록 내부 인스턴스를 새 에셋으로 중복 계산하지 않았다.

### 정본 위치

주인공 저작 원본은 `ArtSources/Characters/Protagonist01/Final/`이다. 이 보고서에서 Final은 이 경로를 뜻한다. 전체 선택 목록은 [selection.json](../ArtSources/Characters/Protagonist01/Final/selection.json), GIF 모음은 [Review/index.html](../ArtSources/Characters/Protagonist01/Final/Review/index.html)에 있다.

- 원본 모델: `Final/Model/Protagonist_ChibiCap_Rig.glb`
- 기존 동작의 GIF·제작 기록: `Final/LegacyMotions/`
- 최신 공격 편집 원본: `Final/Combat/Protagonist_Combat.blend`
- 기존 동작 Unity 폴더: `Assets/Art/Characters/Protagonist01/ChibiMotion_v007/`
- 최신 공격 Unity 폴더: `Assets/Art/Characters/Protagonist01/Combat/`
- 환경·소품·원래 무기: `Assets/Art/Stage01Kit/`
- 몬스터 Unity 폴더: `Assets/Art/Monsters/Stage01/Melee01/v001/`

폴더 이름에 v006·v007이 남아 있는 일부 Unity 파일은 승인된 동작과 제작 도구가 실제 참조하는 의존성이다. 오래된 이름이라는 이유만으로 삭제하면 기존 프리팹이나 재편집 경로가 깨질 수 있다. 현재 게임 씬에 사용되는 기존 플레이어 자료도 별도이며, Final 선택 작업이 그 플레이어를 교체한 것은 아니다.

### 현재 Player에 연결된 기존 비주얼

`Assets/2.Prefab/Player/Player.prefab`은 `Assets/Art/Characters/Protagonist01/Runtime/Protagonist01_Visual.prefab`의 GUID를 실제로 참조한다. Runtime 폴더에는 Rig·Walk·Run·Actions FBX와 Unarmed Animator Controller, 재질 및 보조 코드가 남아 있다. 이 묶음은 선택된 Final 18개와 구분하며 기존 게임 의존성을 보존하기 위해 유지한다. 현재 남은 기록만으로 이 이전 비주얼의 정확한 최초 생성 작업과 입력 출처를 확정하지 못했으므로, Final chibi의 Meshy 작업 ID를 이 파일들에 대신 붙이지 않는다. 이번 Unity 정적 검사에서 24본, 7,427정점, 6,087삼각형을 확인했다. 기존 Controller의 모든 런타임 동작을 시험했다는 뜻은 아니다.

## 2 주인공 모델과 두 리그의 제작 과정

### 모델과 최초 리그

보존된 생성 기록에 따르면, 2026년 10월 21일 내장 image_gen으로 모자 쓴 약 3등신 캐릭터 콘셉트를 만든 뒤 Meshy image-to-3D에 입력했다. Meshy T2.5 Smart Topology, 목표 8,000면, T-pose, 2K 컬러 텍스처, 비 PBR 설정이다. 모델 결과는 8,079삼각형과 1개 재질이며, 이후 높이 1.2m 기준의 24본 리깅을 수행했다. 생성 15크레딧과 리깅 5크레딧, 합계 20크레딧은 이 모델·리그에 한정된 영수증 수치다.

- 모델 작업 ID: `01a15d2b-ee91-7588-8dd9-041192a87f0b`
- 리깅 작업 ID: `01a15d31-2ce9-7463-a3d7-392b1ba82785`
- 근거: [generation.json](../ArtSources/Characters/Protagonist01/Final/Model/Provenance/generation.json), 보존된 콘셉트 PNG와 concept-prompt.txt

GLB에는 텍스처와 24본 스킨이 함께 들어 있어 외부 이미지 경로 없이 열 수 있다. 이후 Unity 쪽에서 주먹 형태, 손의 스킨 영향, Grip_R·Grip_L 블렌드셰이프를 가공했다. 따라서 원본 GLB만 다시 임포트한 결과가 승인된 Unity 동작 프리팹과 완전히 같지는 않다. 원본의 고유 기하 정점 4,225개와 임포트된 메시의 정점 7,101개 차이는 UV·노멀 경계 등으로 분리되는 정점을 포함하는 집계 기준 차이다.

참고한 작가 자료는 [YouTube 영상](https://www.youtube.com/shorts/ujVLoPdd1Pg)과 [cartridge102 페이지](https://x.com/cartridge102)다. 기록상 이 링크들은 비율·실루엣·로우폴리 스타일 참고 자료이며, 작가의 모델이나 애니메이션을 내려받아 사용했다는 증거는 없다. 참고 링크를 원본 저작물 사용허락이나 에셋 라이선스로 해석해서는 안 된다.

### 기존 동작용 Chibi24

승인된 15개는 원래 24본 계층에 맞춘 Unity Generic 클립이다. 이동 계열은 보존된 SourceGait 입력을 사용하고, 자세·손 모양·타이밍과 보조 동작은 Unity Editor 제작 코드에서 가공했다. 기타 동작은 포즈, IK와 시간 곡선으로 저작한 부분을 포함한다. 이 15개 전체를 KayKit 동작이나 모션캡처라고 부를 근거는 없다. 오래된 AI 보행 입력의 제공자별 생성 작업 ID는 현재 보존 기록만으로 완전하게 역추적되지 않는다.

런타임 바인딩에는 `Visual/Armature/Hips` 계층과 `Visual/char1`의 손 블렌드셰이프가 사용된다. 제작 의존성은 [authoring-dependencies.json](../ArtSources/Characters/Protagonist01/Final/LegacyMotions/authoring-dependencies.json)에 기록되어 있다. SourceGait는 재편집 입력이며 최종 15클립의 대체본이 아니다. 보관된 Run_Fists는 팔과 몸의 간격을 수정해 승인받은 버전이다.

### 공격용 KayKit23

최신 공격은 KayKit Character Animations 1.1의 Rig_Medium 실제 동작에서 가져왔다. 23본 KayKit 계층을 주인공 체형에 맞추고 기존 몸체의 가중치를 이식했으며, 별도의 단순 주먹 메시와 무기 루트를 사용한다. 원래 동작의 몸통·골반·사지 회전을 기반으로 재생 속도를 조정하고, 짧은 팔 체형에 맞춰 양손의 손잡이 접촉을 보정했다. Chop과 Slice에는 승인된 칼날 방향 수정이 포함되며, Stab은 해당 수정 대상이 아니었다.

Blender 원본에는 선택된 공격 3종 각각의 리그 액션과 WeaponRoot 액션, 총 6개가 있다. 편집 시 두 액션을 같은 클립 조합으로 함께 적용해야 한다. 텍스처는 blend 안에 패킹되어 있고 동일 JPEG도 보존했다. 원본 라이브러리 전체나 중간 실험 동작은 정본에 포함하지 않는다.

두 리그는 계층·기준 자세·메시·손 표현·무기 바인딩이 달라 이름이나 Avatar 변경만으로 통합되지 않는다. Combat 내부의 `Protagonist_KayKit_v018`은 기존 클립 경로를 유지하기 위한 이름이다.

## 3 보존된 주인공 클립 18개

아래 길이는 Unity 클립의 동작 길이이다. GIF에는 반복·마지막 정지 구간이 들어갈 수 있어 파일 재생 시간이 같다는 뜻은 아니다. L은 Chibi24 기존 동작 프리팹, K는 KayKit23 공격 프리팹을 뜻한다.

| 클립 | 초 | 반복 | 리그 | 출처와 용도 |
| --- | --- | --- | --- | --- |
| Idle_Fists | 2.800 | 예 | L | 기존 승인 대기 |
| Walk_Fists | 0.867 | 예 | L | 기존 승인 걷기 |
| Run_Fists | 0.633 | 예 | L | 팔 간격 수정 승인 달리기 |
| Jump_Full | 1.200 | 아니오 | L | 전체 점프와 골반 높이 궤적 |
| Dash_Forward | 0.667 | 아니오 | L | 대시 자세 |
| Potion_Drink | 2.000 | 아니오 | L | 포션 섭취 |
| Item_Throw_Overhand | 1.300 | 아니오 | L | 투척 기준 시점 0.52초 |
| Hit_Reaction | 0.667 | 아니오 | L | 피격 |
| Interact_Reach | 0.933 | 아니오 | L | 상호작용 |
| Pickup_Low | 1.333 | 아니오 | L | 낮은 물건 줍기 |
| Jump_Takeoff | 0.167 | 아니오 | L | 점프 보조 이륙 |
| Jump_Rise_Loop | 0.300 | 예 | L | 점프 보조 상승 |
| Jump_Apex_Loop | 0.200 | 예 | L | 점프 보조 정점 |
| Jump_Fall_Loop | 0.300 | 예 | L | 점프 보조 하강 |
| Jump_Land | 0.433 | 아니오 | L | 점프 보조 착지 |
| Longsword_Attack_Chop | 0.700 | 아니오 | K | Melee_2H_Attack_Chop |
| Longsword_Attack_Slice | 0.600 | 아니오 | K | Melee_2H_Attack_Slice |
| Longsword_Attack_Stab | 0.700 | 아니오 | K | Melee_2H_Attack_Stab |

L의 호환 프리팹은 `ChibiMotion_v007/Prefabs/Protagonist_Chibi_MotionReview.prefab`, K는 `Combat/Prefabs/Protagonist_Combat.prefab`이며, 두 경로의 앞에는 `Assets/Art/Characters/Protagonist01/`이 붙는다. 각 동작은 해당 폴더의 `Clips/<클립명>.anim`이다. 정확한 파일별 SHA256과 GIF 대응은 Final의 [LegacyMotions/manifest.json](../ArtSources/Characters/Protagonist01/Final/LegacyMotions/manifest.json) 및 [Combat/clips.json](../ArtSources/Characters/Protagonist01/Final/Combat/clips.json)에 있다.

공격 원본 길이는 Chop 1.633초, Slice 1.100초, Stab 1.600초이며 결과는 60Hz로 베이크했다. 걷기·달리기·대시와 공격은 게임 이동을 자동 구현하는 클립이 아니다. Jump_Full의 골반 높이 궤적과 물리 점프를 함께 적용하면 높이가 중복될 수 있으므로, 실제 통합 시에는 전체 점프 또는 높이 궤적을 분리한 보조 5클립 중 방식에 맞는 것을 사용해야 한다.

Move_Dash_Recover.gif는 이동 전환을 보여주는 보관 시연으로, 19번째 납품 클립이 아니다. 삭제 요청된 과거 주먹 공격·스태프 캐스팅·옛 검 공격과 죽음 모션은 이 선택 목록에 없다.

## 4 무기와 도시 환경의 출처

### 두 종류의 검과 스태프

Stage01Kit의 Sword와 Staff는 Meshy로 생성한 원래 별도 소품이다. 최신 공격 프리팹의 롱소드는 그 짧은 Sword와 별개이며, Final Combat의 Blender 편집 원본과 Unity 소켓 프리팹에 보존되어 있다. 최신 롱소드를 KayKit에서 제공한 검 메시나 Meshy 원형 그대로라고 표시할 근거는 없다. 현재 원본에서 편집 가능한 로컬 제작 메시와 재질을 확인할 수 있으나, 초창기 중간 제작 스크립트 전체를 정본에 보관한 것은 아니다.

공격용 별도 소켓 프리팹은 `Combat/Prefabs/Longsword_KayKitSocket.prefab`이다. `Combat/Weapon_Attachment.json`에는 오른손의 정확한 부모 경로와 초기 로컬 변환이 기록되어 있다. 클립은 WeaponRoot를 직접 활성화·애니메이션하므로, 나중에 무기를 교체할 때는 같은 이름·부모·초기 변환을 유지하고 두 번째 검을 중복 생성하지 않아야 한다.

스태프는 생성된 원래 수정 머리를 로컬 수정한 작은 반투명 수정구로 교체했다. 현재 구체는 80삼각형, 지름 약 0.19m, 몸체 위 간격 약 0.045m이며, 단일 투명 패스를 쓴다. 화면 굴절이나 전체 화면 효과를 쓰는 구조는 아니다. 손으로 잡는 동작의 적합성은 개별 캐릭터 소켓과 동작에서 다시 확인해야 한다.

### 환경과 포션

Meshy T2.5 Smart Topology 영수증이 있는 9종은 Sword, Staff, StreetLamp, StreetTree, VendingMachine, Bench, TrashBin, ACUnit, GatePylon이다. 이 가운데 무기를 제외한 7종이 환경 소품이다. 보존된 영수증은 각 5크레딧, 합계 45크레딧을 기록한다. 이는 과거 해당 9작업의 수치이며 현재 가격이나 전체 프로젝트 누적 비용을 나타내지 않는다.

건물·도로·인도·옥상 연결부·사다리 등 16개 모듈과 4색 포션은 Unity Editor 제작 도구로 로컬 메시를 만들었다. 생성 소품에는 색상 정리와 프리팹 구성이 추가되며, 모듈은 공유 팔레트와 필요한 LOD를 사용한다. 어두운 도시 표현은 메시 자체뿐 아니라 재질 팔레트와 조립 배치의 결과다.

| 종류 | 원본 또는 제작 도구 | 보존 자료 |
| --- | --- | --- |
| 생성 소품과 원래 무기 | Meshy 작업 9개와 FBX 9개 | meshy-generation.json |
| 도시 모듈 | UrbanModuleBuilder | 공유 팔레트와 LOD 메시 |
| 소품 프리팹 정리 | UrbanPropBuilder | 피벗·재질·프리팹 |
| 검과 스태프 프리팹 | UrbanWeaponBuilder | 독립 무기 프리팹 |
| 수정구 | ChibiStaffOrbBuilder | 작은 부유 구체와 재질 |
| 포션 4종 | UrbanConsumableBuilder | 로컬 병 메시와 색상 변형 |

제작 도구는 `Assets/Art/Stage01Kit/Editor/`에 있다. 작업 ID, 파일 경로, SHA256, 프리팹 참조는 [nonhero-source-inventory.json](../ArtSources/Stage01Kit/Validation/Review20261027/nonhero-source-inventory.json)에 정리했다. Tutorial_Block과 Stage01_Block은 에셋 조립 예시이며, 메트로이드바니아 이동 동선·충돌·게이트 진행을 실제로 플레이해 검증한 레벨이라는 뜻은 아니다.

## 5 몬스터와 보류된 VFX

Melee01은 KayKit Skeletons 1.0의 원래 모델·리그와 그 모델에 포함된 동작을 사용한다. 주인공처럼 다른 체형에 리타게팅한 작업이 아니다. 균일한 크기 조절, 도시 색상과 망토 가공, 무기 축 정렬, 클립별 일정한 바닥 높이 보정을 적용했다. 원래 자세를 AI로 다시 생성하지 않았다.

| 납품 클립 | 원래 동작 |
| --- | --- |
| Idle | Idle_Combat |
| Move | Running_A |
| Attack | 1H_Melee_Attack_Chop |
| Hit | Hit_A |

몬스터 원본은 `ArtSources/Monsters/Stage01/Melee01/v001-native-motion/Stage01_Melee01.blend`, Unity 프리팹은 `Assets/Art/Monsters/Stage01/Melee01/v001/Prefabs/Stage01_Melee01.prefab`이다. 원본 저장소는 [KayKit Skeletons 1.0](https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0)이며 고정 커밋은 `15b62b9bad122f72926c10fb14d622c73819fa54`다. 현재 웹 배포 페이지의 Skeletons 1.1과 이 몬스터의 1.0 출처를 혼동하면 안 된다. 원거리형·돌진형·중장갑형과 보스가 이 한 프리팹으로 완성된 것은 아니다.

VFX는 `Assets/Art/CombatVFX_v1/Samples/`에 DashBurst, SlashWave, MagicMissile, Explosion 샘플 4개가 남아 있다. README상 Unity 메시·입자·리본을 로컬 제작한 시연이며 외부 텍스처, VFX Graph, 전체 화면 왜곡을 사용하지 않는다. 과거 추가 Meshy 크레딧 사용도 없다. 사용자가 보류한 영역이므로 이번에는 새로 만들거나 품질을 개선하지 않는다. 이 보고서의 검수 통과 대상에서도 제외한다.

## 6 출처와 라이선스 확인

2026년 10월 27일 Kay Lousberg의 Skeletons와 Character Animations 공식 배포 페이지를 확인했고, 저장된 라이선스 문구와 대조했다. 두 팩은 CC0로 개인·상업 사용을 허용하며 필수 출처 표시를 요구하지 않는다. 원작자의 수정 없는 재판매나 본인 작품으로의 주장에 관한 요청은 페이지에 별도로 적혀 있다. 프로젝트에서는 출처 추적을 위해 이름과 라이선스 파일을 계속 보관한다. [Skeletons 공식 페이지](https://kaylousberg.itch.io/kaykit-skeletons), [Character Animations 공식 페이지](https://kaylousberg.itch.io/kaykit-character-animations), [CC0 원문](https://creativecommons.org/publicdomain/zero/1.0/).

| 대상 | 확인된 근거 | 권리 상태와 주의점 |
| --- | --- | --- |
| 최신 주인공 공격의 KayKit 동작·원본 리그 | Character Animations 1.1 및 Skeletons 라이선스 파일 | CC0 확인 |
| Melee01 모델·리그·동작 | 고정 커밋과 KayKit_CC0.txt | CC0 확인 |
| 주인공 모델과 생성 소품 | Meshy 생성 작업 영수증 | 당시 계정 플랜·공개 설정은 영수증만으로 확정 불가 |
| 콘셉트 이미지 | image_gen 제공자·프롬프트·PNG 기록 | KayKit CC0가 적용되는 자료가 아님 |
| 작가 영상과 페이지 | 스타일 참고 링크 | 모델 배포 허락이나 프로젝트 에셋 라이선스가 아님 |
| 로컬 메시·코드 가공 | Unity Editor 코드와 Blender 정본 | 원본 외부 에셋의 조건과 별도로 출처 구분 |

Meshy 공식 약관은 유료 플랜 고객의 생성 결과 권리와 무료 플랜의 CC BY 4.0 조건을 구분하고, 커뮤니티에 공개한 자료에는 별도 조항을 둔다. 보존된 작업 ID와 소비 크레딧은 생성 출처를 증명하지만 당시 구독·공개 상태를 증명하지 않는다. 따라서 생성 에셋을 모두 CC0 또는 무조건 독점 소유라고 표시하지 않았다. 출시 자료를 확정할 때는 생성 시점 계정 기록과 공개 여부를 대조하고 필요한 표시를 적용해야 한다. [Meshy 이용약관](https://www.meshy.ai/terms-of-use), [공식 소유권 안내](https://help.meshy.ai/en/articles/10137554-what-is-the-ownership-of-the-generated-models).

라이선스 파일은 `Final/Combat/Licenses/`의 KayKit_Character_Animations_CC0.txt와 KayKit_Skeletons_CC0.txt, 몬스터 Unity 폴더 `Licenses/KayKit_CC0.txt`에 있다. CC0는 KayKit 원본 부분의 조건이며 주인공 생성 모델·별도 무기·레퍼런스 이미지 전체로 자동 확장되는 허가는 아니다.

## 7 이번 검수의 결과와 범위

이번 출처 조사에서는 선택된 18개 이름·호환 프리팹·원본 동작 매핑, 모델 생성 기록, Stage01Kit 작업 영수증, 몬스터 고정 출처, 보존된 라이선스를 대조했다. 과거의 통과 기록과 오늘 새로 실행한 검사 결과는 구분한다. 같은 파일 해시라는 사실은 이전 결과 보존을 입증하지만, 그 자체로 새 동작 품질 검사를 수행했다는 뜻은 아니다.

| 대상 | 이번 검사 | 결과 |
| --- | --- | --- |
| 최신 공격 3종 | 수정 전후 각각 123프레임 3시점과 120Hz 243포즈 | 소매 스킨 보정과 Unity 123포즈 회귀검사 완료 |
| 기존 15종 | 45조합 2529포즈와 전체 30Hz 측면 프레임 | 확정적 신규 결함 미발견과 승인본 보존 |
| 환경·소품·무기 | 31프리팹 62이미지와 고유 메시 34개 | 확정적 결함 미발견과 원본 80파일 해시 보존 |
| Melee01 4종 | 60Hz 412포즈와 기존 실제 Unity 34개 시트 대조 | 명확한 스킨·관절·파지 결함 미발견 |
| 출처와 라이선스 | 공식 배포 페이지·보존 영수증 대조 | KayKit CC0 확인과 Meshy 계정 조건 미확정 구분 |
| 기존 VFX | 보존 위치·README만 확인 | 품질 검사 제외 |

이번에 확인한 수정 사항은 공격 소매의 스킨 가중치와 제작 도구의 오래된 보고서 출력 경로다. 기존 15개 동작과 환경·몬스터 모델은 유지했다. 아래 결과는 명시한 표본·시점·환경에서의 에셋 검사이며 실제 게임 플레이 전체를 보증하지 않는다.

### 주인공 공격 검사와 수정

Blender의 실제 스킨 결과를 수정 전후 각각 123개 베이크 프레임과 3개 시점, 총 369장씩 새로 렌더했다. 각 버전에서 120Hz의 243개 포즈를 수치 검사했고, 최종 9개의 전체 프레임 시트와 확대 이미지도 검토했다. 이 과정에서 팔꿈치 부근 소매의 국소적인 눌림을 확인했다. 넓은 영역을 평활화한 첫 후보는 메시가 늘어지는 문제로 채택하지 않았다.

최종 수정은 몸체의 275개 원본 정점에 있는 위팔·아래팔 가중치 분배에만 적용했다. 123포즈에서 최대 표면 위치 변화는 약 13.915mm이며, 기준 정점·UV·리그와 6개 액션의 곡선은 그대로다. 243포즈에서 검과 주먹의 변환·형상이 정확히 보존되는지도 대조했다. 새 동작을 만들거나 공격 타이밍을 변경한 작업은 아니다.

| 클립 | 정지 메시 대비 변 길이 비율 99백분위 수정 전 | 수정 후 |
| --- | --- | --- |
| Chop | 1.5037 | 1.4685 |
| Slice | 1.6261 | 1.5969 |
| Stab | 1.5623 | 1.5464 |

위 수치는 선택한 스킨 변형 측정치의 소폭 개선을 뜻하며 모든 주름이나 눌림이 사라졌다는 보장은 아니다. 근거는 [source-retouch-receipt.json](../ArtSources/Characters/Protagonist01/Final/Validation/Review20261027/Combat/source-retouch-receipt.json)과 같은 폴더의 skin-candidate-comparison.json이다.

수정한 몸체를 Unity 메시로 전달한 뒤 세 공격의 123개 포즈를 다시 대조했다. 원본과 Unity의 변경 정점 수는 정확히 275개로 대응했고, 애니메이션 해시·표본 시각·삼각형 구성은 유지되었다. 몸체 최대 위치 변화는 13.91546mm, 영향 밖 정점의 부동소수점 차이는 최대 0.000246mm였으며, 검과 양쪽 주먹 정점은 정확히 같았다. 비유한 좌표와 누락 바인딩은 없었다. [unity-skin-regression.json](../ArtSources/Characters/Protagonist01/Final/Validation/Review20261027/Combat/unity-skin-regression.json).

최종 선택 검증에서 18개 Unity AnimationClip과 메타 파일의 해시, 기존 15개 GIF가 모두 보존되었다. 몸체 가중치만 바뀐 공격은 실제 Unity 프리팹에서 GIF 6개인 일반·느린 재생 각 3개를 다시 만들었고, 미디어 manifest와 대조했다. 일반 GIF는 Chop·Slice·Stab 순서로 21·18·21프레임, 0.7·0.6·0.7초이며 추가 정지 구간이 없다. 느린 버전은 재생 시간이 두 배다. 카탈로그 링크 21개와 현재 blend 해시도 확인했다. [delivery-verification.json](../ArtSources/Characters/Protagonist01/Final/Validation/Review20261027/delivery-verification.json).

### 몬스터 검사

Melee01은 오늘 Blender에서 전체 4클립의 412개 포즈를 60Hz로 다시 평가했다. 41본, 몸체 메시 9개와 칼날 1개의 수치가 유한하고 가중치 합이 유효했으며, 네 클립의 시작·끝 스킨 위치가 일치했다. 현재 blend·FBX·프리팹 해시가 이전 실제 Unity 렌더에 사용한 파일과 같은 것을 확인한 후, 그 렌더의 34개 시트를 다시 눈으로 검토했다. 네 동작의 측면 208프레임 전체, 공격의 정면·반대편·후면 전체, 나머지 보조 시점과 오프라인 전환 샘플을 포함한다. 오늘 모든 Unity 이미지를 새로 렌더했다고 표시하지 않는다.

손잡이 이탈, 관절 뒤집힘, 큰 스킨 붕괴나 누락 재질은 발견하지 못했다. 발 최저점은 Hit 약 -0.436mm, Idle 약 -0.0345mm로 측정되었지만, 검토 이미지에서 승인 모션을 수정해야 할 정도의 침하로 보이지 않아 보존했다. 관련 근거는 몬스터 원본 폴더의 `Validation/Review20261027/native-monster-audit.json`과 `visual-review.json`이다. 0.12초 전환 시연은 오프라인 검사이며 실제 AI 전환 결과가 아니다.

### 기존 동작의 Unity 검사

Unity 6000.3.21f1에서 저장된 기존 15클립을 실제 MotionReview·SwordGrip·StaffGrip 프리팹에 적용해 45개 조합, 총 2,529개 포즈를 60Hz로 새로 평가했다. 누락된 바인딩, 비유한 좌표, 영 스케일, 붕괴한 삼각형은 검출되지 않았다. 모든 정점은 실제 BakeMesh 출력으로 검사했으며, 검토용 이미지는 그 포즈를 임시 CPU 스킨 메시로 옮겨 렌더했다. 근거는 [hero-audit.json](../ArtSources/Characters/Protagonist01/Final/Validation/Review20261027/Legacy/hero-audit.json)이다.

15개 MotionReview 동작의 30Hz 측면 프레임 전체인 41개 시트, 반대편 위상 시트 15개와 정적 모델 시트 4개를 직접 검토했다. 별도 검토자는 15종의 정면·3분의 4 시점 위상 시트 30장과 7종의 생활·점프·대시 동작에 대한 두 주먹 메시 변형 시트 14장, 총 44장을 확인했다. 검토 범위에서 확정적인 신규 메시 파열, 손 분리·역관절, Run 팔의 몸통 매몰 또는 루프 경계의 튐을 발견하지 못했다. 기존 15개 애니메이션과 모델은 수정하지 않았다.

Potion·Pickup·Throw의 손 접근은 의도된 동작으로 판단했다. 다만 소품을 장착하지 않은 검사이므로 포션·무기의 실제 손 접촉까지 통과했다고 해석하지 않는다. 정확한 파일 목록과 한계는 [independent-secondary-visual-review.json](../ArtSources/Characters/Protagonist01/Final/Validation/Review20261027/Legacy/independent-secondary-visual-review.json)에 있으며, 전체 측면 검수 시트와 수치 요약은 같은 폴더의 final-audit-summary.json에 기록했다.

유지보수로 Editor 제작 도구 3개의 진단 출력 경로 4곳만 `Final/Validation/LegacyAuthoring`으로 정리했다. 클립을 재생성하지 않았으며, 변경 파일과 전후 해시는 같은 검수 폴더의 authoring-path-repairs.json에 있다.

### 환경과 소품 검사

실제 Unity 프로젝트에서 31개 프리팹을 측면과 3분의 4 시점으로 새로 렌더한 62개 이미지, 총 8개 접촉시트를 직접 확인했다. 현재 프리팹이 참조하는 고유 메시 34개는 LOD 포함 합계 49,202정점·20,458삼각형이며, 비유한 좌표, 잘못된 인덱스, 퇴화 삼각형, 읽기 불가 메시, 누락 스크립트·재질·셰이더는 모두 0건이었다. 시각 검수는 LOD0 두 시점에 한정된다.

확정적인 누락 재질이나 큰 메시 붕괴를 발견하지 못해 모델을 다시 만들지 않았다. 작은 스태프 구와 몸체의 간격을 확인했고, 구 뒤에 노랑·파랑 배경판을 놓은 별도 이미지로 배경이 투과하고 색이 섞이는 실제 반투명 표현도 확인했다. 알파 설정값만으로 판단한 것은 아니다. 원본 80개 파일의 전후 해시는 같았다. 제작 도구가 입력으로 사용하는 구형 `Weapons/Staff.asset`은 현재 프리팹의 활성 메시와 달라도 필요한 보존 자료다.

근거는 [환경 시각 검수 기록](../ArtSources/Stage01Kit/Validation/Review20261027/visual-review.json)과 [수치 요약](../ArtSources/Stage01Kit/Validation/Review20261027/numerical-summary.json)이다. 실제 플레이어 충돌, 레벨 이동 경로, 게임 조명, LOD 전환, 목표 기기 성능은 별도 검증이 필요하다.

### 이전 보존 검증과 구분

Final Combat의 `Validation/source-integrity.json`은 이전 정본 정리 과정에서 123개 베이크 프레임과 정리 전후 이미지를 대조한 역사적 기록이다. 오늘은 의도적으로 소매 가중치를 수정했으므로, 그 옛 전체 파일 해시가 현재 파일과 동일하다는 증거로 사용할 수 없다. 현재 Combat 결과는 2026년 10월 27일의 수정 영수증과 Unity 전달 검증을 우선한다. Legacy manifest의 해시는 그대로 보존한 기존 클립을 비교하는 별도 기준이다. 리그 유지와 시각적으로 어떤 결함도 없다는 판단은 별개다. 이번 검사에 실제 게임의 입력 지연, 공격 판정, 취소 타이밍, Animator 전환, 물리 점프·경사면 이동, 목표 기기 GPU·CPU 측정은 포함하지 않는다.

## 8 다음 담당자가 알아야 할 제한

우선 두 리그를 섞기 전에 사용할 런타임 캐릭터 구조를 결정해야 한다. 승인된 모습을 그대로 보존한 현재 18개 선택은 단일 Animator 전달을 완료한 상태가 아니다. 통합을 진행한다면 본 계층·손 표현·검 소켓의 차이를 처리하고, 18개 전체를 같은 실제 게임 카메라에서 다시 확인해야 한다.

기존 동작의 Build 메뉴는 보존 파일을 다시 생성할 수 있다. 검토만 할 때는 기존 프리팹과 클립을 직접 열고, 제작 도구를 무심코 재실행하지 않는다. Combat 편집에서는 몸체 액션만 바꾸지 말고 clips.json에 지정된 무기 액션을 함께 적용한다. 주인공 ComparisonReference.blend는 몬스터 크기·외형 비교용 정적 원본이며 애니메이션 소스가 아니다.

모든 원본 라이브러리와 중간 실험 파일이 정본에 들어 있지는 않다. 현재 보존 범위는 승인된 결과와 필요한 편집 의존성, 출처·라이선스·검수 기록이다. 옛 AI 보행 원본의 제공자별 영수증과 최신 롱소드 초창기 제작 스크립트 전체까지 복원할 수 있다고 보장하지 않는다. 현재 파일과 공개 1차 출처로 확인한 사실, 보존 기록만 남아 있는 사실을 구분해 후속 변경 기록을 이어가는 것이 필요하다.

### 최종 작업 범위

출력 경로 수정 후 실제 Unity 프로젝트의 최종 컴파일은 종료 코드 0이며 C# 컴파일 오류가 없었다. 근거는 Legacy 검수 폴더의 final-compile.json이다.

인게임 코드·Animator·VFX는 변경하지 않았다. 검수 실행용 임시 Editor 파일 7개의 삭제는 자동 승인 검토에서 정책상 차단되어 로컬에 남아 있으며, `Assets/Editor/AssetAudit20261027`과 관련 메타는 커밋에서 제외한다. 재실행 가능한 검사 도구의 정본과 결과 기록은 ArtSources에 보존한다.

### 주요 증거 파일

- [최종 선택 18개](../ArtSources/Characters/Protagonist01/Final/selection.json)
- [모델 생성 기록](../ArtSources/Characters/Protagonist01/Final/Model/Provenance/generation.json)
- [기존 동작의 파일별 해시와 GIF 대응](../ArtSources/Characters/Protagonist01/Final/LegacyMotions/manifest.json)
- [기존 동작의 제작 의존성](../ArtSources/Characters/Protagonist01/Final/LegacyMotions/authoring-dependencies.json)
- [공격 동작의 원본 이름과 길이](../ArtSources/Characters/Protagonist01/Final/Combat/clips.json)
- [공격 원본 출처와 해시](../ArtSources/Characters/Protagonist01/Final/Combat/Provenance/source-provenance.json)
- [환경과 몬스터 출처 목록](../ArtSources/Stage01Kit/Validation/Review20261027/nonhero-source-inventory.json)
- [몬스터 원본 출처](../ArtSources/Monsters/Stage01/Melee01/v001-native-motion/Research/source-provenance.json)
