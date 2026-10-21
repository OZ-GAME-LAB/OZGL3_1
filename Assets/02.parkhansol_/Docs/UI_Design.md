# OZGL3_1 (Project M) — UI/UX 설계 v0.9

작성: 박한솔 (UI/UX) · 2026-10-15 · 공유 문서: https://claude.ai/code/artifact/f8497896-e61f-4266-a194-7db47a969bdb
기준: 기획서 v0.2 (10/15 재배포본) · Unity 6000.3 · URP 17.3 · uGUI 2.0(TMP) · Input System 1.20 · DOTween 무료판

UI 화면 13종 + 계열별 스킬 트리 + 게임 흐름(로딩·페이드·스테이지 띠), HUD 양손 분리·퀘스트 목록·지도 아래 시스템 알림·UI 효과음. 브랜치 `parkhansol_ui_261015` + `develop`.

## 0. v0.9 — 기획서 기준으로 맞춤 (2026-10-15)
| 영역 | 바뀐 점 | 기획서 근거 |
|---|---|---|
| 스킬 트리 (K) | 검술·마법 섞는 공용 트리 → **계열마다 트리 하나** (`ClassData.skillTree`: `SkillTree_Sword` / `SkillTree_Magic`). 노드 9개 = Q/E/R × 1~3단계 | 3장 "각 계열의 액티브 스킬 3개는 Q/E/R에 고정" |
| 습득·강화 규칙 | Q는 Lv.1부터 1단계 보유, E는 Lv.2·R은 Lv.4부터 1P로 습득, 강화도 단계당 1P (습득한 뒤 바로 가능) | 3장 스킬 포인트 초기 검증값 |
| 최대 레벨 | 15 → **7** (6P, 전부 찍으려면 8P → 투자처 선택) | 3장 "데모의 목표 상한을 레벨 7" |
| 패시브 · 택1 · 초기화 | 데이터에서 뺌. 초기화 버튼은 `SkillTreeWindow.allowReset`(기본 꺼짐). 기능 코드는 남겨 둠 | 기획서에 없음 |
| 스킬 창 설명 칸 | 강화 노드에 **다음 효과** (이전 단계 → 이 단계, 대기시간 변화) + 비용 | 3장 "다음 효과와 투자 비용" |
| 스킬 창 시간 정지 | 열려 있는 동안 `Time.timeScale = 0` (기존 동작, 셀프 테스트 추가) | 3장 기본안 |
| 인벤토리 (I) | **UIRoot에서 제외**, I 키 기본 없음, 일시정지 메뉴 버튼 제거. 코드(`InventoryWindow`·`IInventorySource`)는 남겨 둠 | 4장 "복잡한 인벤토리 범위 밖", 5장 1~4 고정 슬롯 |
| 아이템 | 4종 1~4 고정 슬롯 + 수량 + 버프 남은 시간(같은 버프는 시간 갱신) 유지. 예시 열쇠 아이템 삭제 | 5장 |
| 보스 진입 가능 | 게이트 목표 달성 순간 SYSTEM 알림 "보스 구역 개방" + 알림 소리 (퀘스트 목록) | 5장 "보스 진입 가능 상태에 시각·사운드 피드백" |
| 보스 구역 안내 화살표 | `ObjectiveMarkerLink.onlyAfterBossUnlock` — 보스 구역 입구에 붙이면 목표 달성 뒤에만 화면 밖 화살표 | 1장 "목표 봉쇄 수를 채운 뒤 보스 구역으로 안내" |
| 확인용 씬 | 샌드박스·쇼케이스 플레이어가 계열 트리 사용, 안내 문구에서 I 키 제거 | — |

적용: Unity에서 **OZ > UI > Setup - Run All** (계열 트리 생성·연결, 예전 `SkillTree_Main`·`Item_GateKey` 삭제, UIRoot·씬 재생성) → **OZ > UI > Run Self Test**.

## 1. 범위
| 화면 | 내용 | 상태 |
|---|---|---|
| 전투 HUD | 좌상단: 초상화·HP·Lv·랭크 + **퀘스트 목록** / 우상단: 미니맵 + **SYSTEM 알림** / 좌하단: 1~4 아이템·버프 / 우하단: Q/E/R 스킬 / 보스 바 상단 가운데 | v0.6 개편 |
| 게이트 파괴 띠 | 게이트 봉쇄 시 "게이트 파괴 n/목표" 2초 표시. 스테이지 클리어 창 대체 | 구현 |
| 보스 구역 개방 알림 | 목표 달성 순간 SYSTEM 알림 + 소리, 보스 줄 활성 | v0.9 |
| 피해 숫자·타격 이펙트·적 체력바 | 일반·치명타·약점·처치·지속 피해·플레이어 피격·회복·빗나감 | v0.4 치명타 글자 제거 |
| 설정 창 | 시작 화면 '설정', 일시정지 '옵션'. 사운드·화면·게임 탭 | 구현 |
| 스킬 창 (K) | 계열 트리: Q/E/R × 1~3단계, 보유 포인트·습득·단계·다음 효과·비용. 열면 시간 정지 | v0.9 기획서 기준 |
| 지도 (Tab·M) | 방 단위 미니맵·전체 지도 | 구현 |
| 대화창 | 좌우 초상화, 화자 강조, 타이핑 | 구현 |
| 보스 HUD | 등장 연출, 페이즈 눈금, 격파. 상시 체력바는 HUD 레이어(창 아래) | v0.4 레이어 수정 |
| 시작·계열 선택·일시정지·사망·데모 종료 | 새 게임·이어하기(저장 있을 때만)·설정·종료 / 계열 전투 특성·Q/E/R 안내 / 버튼 → `UIRequests` (인스펙터 연결: `UIRequestEvents`) | 구현 |
| 랭크 승급 | F → E(1스테이지 보스) → D(2스테이지 보스) 팡파레 + SYSTEM 알림 | v0.7 |
| 지역 이름 | 화면 위쪽 가운데, 부드럽게 떴다 사라짐. `AreaNameTrigger` / `GameUI.Area.Show` | v0.7 밑작업 (레벨 배치 대기) |
| 저체력 경고 | 체력 30% 이하 → 가장자리 붉은 비네트 + 심장 박동 소리 | v0.7 |
| 화면 밖 목표 화살표 | 가장자리 화살표 + 아이콘 + 거리. 게이트 자동(`GateIndicatorBridge`), 보스 구역은 `ObjectiveMarkerLink`(목표 달성 뒤) | v0.7 · v0.9 |
| 상호작용 안내 | 대상 머리 위 "[F] 조사" (표시만). `InteractPromptLink` / `GameUI.Prompt` | v0.7 |
| 로딩 화면 · 페이드 · 스테이지 시작 띠 | `GameUI.Flow` — 진행 막대·%·TIP, 검은 화면 동안 교체, "STAGE n + 이름" 띠 | v0.5 |
| ~~인벤토리 (I)~~ | v0.9 제외 (기획서 범위 밖). 코드만 보관 | 제외 |

## 2. 구조
- `OZ.UI.Contracts` — 팀원이 참조하는 유일한 어셈블리. `GameUI` 창구, Source 인터페이스, `UIRequests`, `UIState`, `UISettings`, `HitFlash`, 데이터 SO, `SkillTreeState`(규칙 계산기)
- `OZ.UI` — 구현: `UIManager`, HUD, `DamageFxController`, 설정 창, 대화·지도·보스, `SkillTreeWindow`
- `OZ.UI.Editor` — `OZ > UI > Setup - Run All`(데이터·UIRoot·샌드박스·쇼케이스), Self Test, Git Push(오늘 브랜치 자동)
- `OZ.UI.Samples` — 가상 플레이어·적·보스, `SandboxCombat`, `SandboxFeel`, 지하철역 쇼케이스(`Showcase*`)

## 3. 스킬 트리 (v0.9)
| 항목 | 값 |
|---|---|
| 데이터 | 계열마다 `SkillTree_Sword` / `SkillTree_Magic` (`ClassData.skillTree`에 연결). Setup을 다시 돌리면 기획 규칙으로 다시 채워짐 |
| 구성 | Q/E/R 열마다 Skill 노드(1단계) → Upgrade 2단계 → 3단계. 열 위에 Q/E/R, 옆에 습득 레벨 |
| 습득 | Q: Lv.1 보유(무료) · E: Lv.2부터 1P · R: Lv.4부터 1P |
| 강화 | 단계당 1P, 최대 3단계, 습득 레벨 외 추가 조건 없음 |
| 포인트 | 레벨당 1P, 최대 Lv.7 → 6P. 전부 찍으려면 8P(시작 Q 제외) → 선택 |
| 설명 칸 | 스킬: 설명 + 1단계 효과·대기 / 강화: 다음 효과(이전 → 이번 단계, 대기시간 변화) / 상태: 비용·사유("Lv.4 필요", "포인트 부족") |
| 대기시간 (1단계) | 검술 Q5 · E8 · R16초 / 마법 Q6 · E10 · R20초 (2·3단계는 10%씩 감소 임시값) |

팀원 연동: `ISkillTreeSource` 구현 (규칙은 `SkillTreeState`를 그대로 감싸면 됨 — `Samples/DummyPlayer` 참고, 트리는 `classData.skillTree`). 기존 `ISkillSource`(아이콘·쿨타임·단계)는 그대로이고, 단계 값은 트리에서 계산.

## 4. HUD 배치·효과음 (v0.6~)
| 항목 | 내용 |
|---|---|
| 배치 (양손 분리) | 아이템 1~4 좌하단, 스킬 Q/E/R 우하단 (36px). 일반 알림은 우하단 스킬 위 |
| 퀘스트 목록 | 좌상단 체력 아래, 최대 4줄. 게이트 봉쇄 n/목표 + 보스 처치(게이트 후 잠김) 자동. `GameUI.Quest.Set / SetProgress / Complete / Remove` |
| SYSTEM 알림 | 지도와 같은 폭(104px), 지도 밑에서 내려옴, 최대 3장, 2.5초 뒤 지도 뒤로. 연속 레벨업은 한 장. `GameUI.Notify.System(종류, 본문, 보조)` |
| 효과음 | Kenney Interface Sounds (CC0) 28종 + 심장 박동 자체 합성. `GameUI.Sound.Play(UISound.X)`, 교체는 `UI/Audio/UISoundSet_Default` |

## 5. 플레이어 체력 연결 (v0.7~v0.8)
- `GameBridge/PlayerHealthBridge` (팀원 코드 수정 없이 Play 시 자동 부착)
  - **플레이어 체력 모드** (플레이어에 IHealthSource 컴포넌트 있을 때): 체력 기준 = 정명진 님 `PlayerHealth`. 피격·회복은 그쪽 `TakeDamage/Heal`로 넘김. 0 → `deathScreenDelay`(1초) 뒤 사망 화면, 재도전 → `ResetHealth()`
  - **임시 모드** (체력 컴포넌트 없음): 브리지가 체력 관리, `Monster` 접촉 10 피해
- 남은 연결: `Monster`가 `IDamageable` 미구현 → 몬스터 체력 생기면 `EnemyUILink`(`Init`·`Hit`)로 적 체력바·피해 숫자 연결

## 6. 팀 설계 연결 (v0.5)
팀 설계: `GameManager → StageManager / SpawnManager / PoolManager`, `StageManager → GateManager → Gate → GateSpawner → SpawnManager`.

| 팀 설계 | UI |
|---|---|
| GameManager.InitializeAsync | `GameUI.Flow.ShowLoading` → `SetLoadingProgress` → `HideLoading` |
| GameManager.RestartStage / ChangeStage | `GameUI.Flow.Transition(교체)` ← `UIRequests.Retry / NextStage` |
| StageManager.StartStage | `GameUI.Flow.StageIntro(n, 이름)` |
| StageManager.StartBossphase | `BossUILink.Begin(maxHP, 콜백)` → `SetHP` |
| StageManager.ClearStage | 랭크 승급(`PlayerUILink.SetRank`) / 마지막 `GameUI.Screens.ShowDemoEnd` |
| GateManager | `GateUILink.BeginStage / GateOpened / GateSealed` (또는 `IGateSource` 구현) → 목표 달성 시 보스 구역 알림 자동 |
| 보스 구역 입구 | `ObjectiveMarkerLink` (Kind Boss, Only After Boss Unlock) |
| Spawn·Pool 적 | `EnemyUILink` (`Init` · `Hit`) — 풀 꺼냄·반납 시 체력바 자동 등록·해제 |
| 플레이어 | `PlayerHealth`(IHealthSource) 있으면 자동 사용, 없으면 `PlayerUILink` (`SetHP` · `SetExp` · `SetLevel` · `SetRank`) |
| 저장·이어하기 | `GameUI.Screens.ShowTitle(canContinue)` — 저장이 있을 때만 이어하기 버튼 활성 |

- 로딩·페이드 중에는 `UIState.IsGameplayInputBlocked = true`
- 상세: `Docs/UI_Integration.md` · 동작 예시: `Samples/Flow/`

## 7. 피해 숫자·타격감 기준
| 종류 | 피해 숫자 | 히트스톱 | 흔들림(UI px) | 흰 번쩍임 | 이펙트 |
|---|---|---|---|---|---|
| Normal | 흰 12px | 0.03초 | 1px | 0.05초 | 작은 하늘색 |
| Critical | 노랑 24px, 글자 없음, 위 줄 | 0.07초 | 3px | 0.07초 | 금색 2배 |
| Weakness | 주황 굵은 24px, 그림자 2px | 0.08초 | 3px | 0.08초 | 큰 주황 |
| Finisher | 빨강 30px, 그림자 2px (마지막 일격 자동) | 0.12초 | 4px | 0.10초 | 큰 빨강 |
| DamageOverTime | 보라 10px, 아래, 0.4초 합산 | — | — | 0.03초 | — |
| PlayerHurt / Heal / Miss | 빨강 / 초록 + / 회색 | 0.06초 / — / — | 3px / — / — | — | — |

상세: `Docs/CombatFeedback.md`

## 8. 확인용 씬
- `UI_Sandbox` — 2D 더미로 모든 UI를 키로 띄워 보는 씬 (도움말 F1)
- `UI_Showcase_Subway` — 로우폴리 지하철역 임시 맵 + 블록 인형. 로딩 → STAGE 띠 → 게이트 웨이브 → 보스 → 클리어 → 다음 스테이지. A/D · Space · Z/마우스 · Q/E/R · K 스킬 · Tab 지도 · F6 재시작 · F7 다음 스테이지 · F8 로딩

## 9. 입력·화면
K 스킬 창 · Tab/M 지도 · ESC 닫기/일시정지 · Space 대화. 640×360 × 정수배. 레이어 HUD 0 · Screen 10 · Popup 20 · Cinematic 30 · Fullscreen 40 · Modal 45 · Toast 50 · Overlay 100. 글꼴 Galmuri 11·9·14·11 Bold.
플레이어 조작(기획서 2장): A/D 이동 · W/S 사다리 · Space 점프 · Shift 대시(짧은 무적) · 좌클릭 공격(커서 방향) · Q/E/R 스킬 · 1~4 아이템.

## 10. 남은 결정
- 효과음 최종 선정(사운드 담당 확인)
- 팀원 매니저 실제 코드 올라오면 Link → 인터페이스 직접 구현으로 바꿀지
- 스킬 2·3단계 대기시간·효과 수치 (전투 검증 후)
- 히트스톱·흔들림 수치 채택(전투·카메라) · 볼륨 연결(사운드) · 지도 시간 정지 · 아트 교체 담당
- 확정됨(v0.9): 장비 화면 없음 · 인벤토리 창 없음 · 최대 Lv.7 · 계열 선택 화면 유지 · 패시브 없음
