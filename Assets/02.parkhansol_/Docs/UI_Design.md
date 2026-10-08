# OZGL3_1 (Project M) — UI/UX 설계 v0.6

작성: 박한솔 (UI/UX) · 2026-10-08 · 공유 문서: https://claude.ai/code/artifact/f8497896-e61f-4266-a194-7db47a969bdb
기준: 기획서 v0.2 · Unity 6000.3 · URP 17.3 · uGUI 2.0(TMP) · Input System 1.20 · DOTween 무료판

UI 화면 14종 + 노드형 스킬 트리 + 게임 흐름(로딩·페이드·스테이지 띠), HUD 양손 분리·퀘스트 목록·지도 아래 시스템 알림·UI 효과음(v0.6). 브랜치 `parkhansol_ui_261008` + `develop`.

## 1. 범위
| 화면 | 내용 | 상태 |
|---|---|---|
| 전투 HUD | 좌상단: 초상화·HP·Lv·랭크 + **퀘스트 목록** / 우상단: 미니맵 + **SYSTEM 알림** / 좌하단: 1~4 아이템·버프 / 우하단: Q/E/R 스킬 / 보스 바 상단 가운데 | v0.6 개편 |
| 게이트 파괴 띠 | 게이트 봉쇄 시 "게이트 파괴 n/목표" 2초 표시. 스테이지 클리어 창 대체 | 구현 |
| 피해 숫자·타격 이펙트·적 체력바 | 일반·치명타·약점·처치·지속 피해·플레이어 피격·회복·빗나감 | v0.4 치명타 글자 제거 |
| 설정 창 | 시작 화면 '설정', 일시정지 '옵션'. 사운드·화면·게임 탭 | 구현 |
| 스킬 트리 (K) | 노드형. Q/E/R 버튼마다 검술·마법 택1 → 2·3단계, 공용 패시브 4개, 초기화 | v0.4 신규 (카드형 스킬 창 대체) |
| 인벤토리 (I) | 6×4 칸 36px(아이콘 2배), 집기/놓기, 우클릭 사용, 사용 칸 수, 퀵슬롯 번호 | v0.4 품질 개선 |
| 지도 (Tab·M) | 방 단위 미니맵·전체 지도 | 구현 |
| 대화창 | 좌우 초상화, 화자 강조, 타이핑 | 구현 |
| 보스 HUD | 등장 연출, 페이즈 눈금, 격파. 상시 체력바는 HUD 레이어(창 아래) | v0.4 레이어 수정 |
| 시작·계열 선택·일시정지·사망·데모 종료 | 버튼 → `UIRequests` (인스펙터 연결: `UIRequestEvents`) | 구현 |
| 로딩 화면 · 페이드 · 스테이지 시작 띠 | `GameUI.Flow` — 진행 막대·%·TIP, 검은 화면 동안 교체, "STAGE n + 이름" 띠(페이드 뒤 자동 대기) | v0.5 신규 |

## 2. 구조
- `OZ.UI.Contracts` — 팀원이 참조하는 유일한 어셈블리. `GameUI` 창구, Source 인터페이스 9종(+`IEnemyHealthSource`), `UIRequests`, `UIState`, `UISettings`, `HitFlash`, 데이터 SO, `SkillTreeState`(규칙 계산기)
- `OZ.UI` — 구현: `UIManager`, HUD, `DamageFxController`, 설정 창, 대화·지도·보스, `SkillTreeWindow`
- `OZ.UI.Editor` — `OZ > UI > Setup - Run All`(데이터·UIRoot·샌드박스·쇼케이스), Self Test, Git Push(오늘 브랜치 자동)
- `OZ.UI.Samples` — 가상 플레이어·적·보스, `SandboxCombat`, `SandboxFeel`, 지하철역 쇼케이스(`Showcase*`)

## 3. v0.6 HUD 개편 · 효과음
| 항목 | 내용 |
|---|---|
| 배치 (양손 분리) | 아이템 1~4 좌하단, 스킬 Q/E/R 우하단 (36px — 16px 아이콘 2배라 픽셀 유지). 일반 알림은 우하단 스킬 위 |
| 퀘스트 목록 | 좌상단 체력 아래, 최대 4줄. 게이트 봉쇄 n/목표 + 보스 처치(게이트 후 잠김) 자동. `GameUI.Quest.Set / SetProgress / Complete / Remove` |
| SYSTEM 알림 | 지도와 같은 폭(104px), 지도 밑에서 내려옴, 최대 3장, 2.5초 뒤 지도 뒤로. 연속 레벨업은 한 장으로 합침. `GameUI.Notify.System(종류, 본문, 보조)` |
| 보스 바 | 상단 가운데 게이트 패널이 빠져 맨 위로 |
| 효과음 | Kenney Interface Sounds (CC0) 28종 — 짧고 맑은 휴대폰 UI 톤. 버튼·창·알림·퀘스트·게이트·스테이지·보스·스킬트리·인벤토리 자동. `GameUI.Sound.Play(UISound.X)`, 교체는 `UI/Audio/UISoundSet_Default` |
| 문구 정리 | 사망 화면 안내 문구, 스킬 초기화 알림 제거 |

## 4. 팀 설계 연결 (v0.5)
팀 설계: `GameManager → StageManager / SpawnManager / PoolManager`, `StageManager → GateManager → Gate → GateSpawner → SpawnManager`.

| 팀 설계 | UI |
|---|---|
| GameManager.InitializeAsync | `GameUI.Flow.ShowLoading` → `SetLoadingProgress` → `HideLoading` |
| GameManager.RestartStage / ChangeStage | `GameUI.Flow.Transition(교체)` ← `UIRequests.Retry / NextStage` |
| StageManager.StartStage | `GameUI.Flow.StageIntro(n, 이름)` |
| StageManager.StartBossphase | `BossUILink.Begin(maxHP, 콜백)` → `SetHP` |
| StageManager.ClearStage | `GameUI.Screens.ShowStageClear` / 마지막 `ShowDemoEnd` |
| GateManager | `GateUILink.BeginStage / GateOpened / GateSealed` (또는 `IGateSource` 구현) |
| Spawn·Pool 적 | `EnemyUILink` (`Init` · `Hit`) — 풀 꺼냄·반납 시 체력바 자동 등록·해제 |
| 플레이어 | `PlayerUILink` (`SetHP` · `SetExp` · `SetLevel` · `SetRank`) |

- 로딩·페이드 중에는 `UIState.IsGameplayInputBlocked = true`
- `Assets/Scenes/SampleScene`: UIRoot + EventSystem + `UI Links (팀원 연결용)` (Link 컴포넌트 + 안내 메모)
- 상세: `Docs/UI_Integration.md` · 동작 예시: `Samples/Flow/` (쇼케이스 씬에서 실제로 돌아감)

## 5. 스킬 트리 (v0.4)
| 항목 | 값 |
|---|---|
| 최대 레벨 · 포인트 | Lv.15 (임시) · 레벨당 1P → 14P |
| 구성 | Q/E/R 버튼마다 검술·마법 Skill 노드 택1(`exclusiveGroup`) → Upgrade 2단계(배우는 Lv+1) → 3단계(배우는 Lv+4). 공용 패시브 4개(체력 +10% / 치명타 +5% / 대기시간 -8% / 이동 +8%) |
| 시작 | 계열 선택 = 그 계열 Q 노드 무료(초기화해도 유지). E·R은 트리에서 선택 |
| 총 비용 | 시작 Q 제외 14P = Lv.15에서 한쪽 빌드 완성 |
| 초기화 | 두 번 눌러 확정, 쓴 포인트 전부 환급 |
| 데이터 | `SkillTree_Main` 에셋 하나 (노드 위치·선행·비용·필요 레벨). `OZ > UI > Validate Data`로 검사 |

팀원 연동: `ISkillTreeSource` 구현 (규칙은 `SkillTreeState`를 그대로 감싸면 됨 — `Samples/DummyPlayer` 참고). 기존 `ISkillSource`(아이콘·쿨타임·단계)는 그대로이고, 단계 값은 트리에서 계산. 패시브 수치는 `SkillTreeState.GetStat("max_hp_pct")` 등으로 읽음.

## 6. v0.4 변경
| 영역 | 바뀐 점 | 팀원 코드 |
|---|---|---|
| 단축키 | Q/E/R·1~4 칸 22 → 36px, 아이콘 16px 2배, 키 글자 12px, 수량 그림자 | — |
| 버프 | 단축키 줄 위(y 80)로 이동 — 키 글자와 안 겹침, 아이콘 20px | — |
| 좌상단 HUD | 반투명 바탕 추가 (밝은 배경 위에서도 체력바가 읽히게) | — |
| 치명타 | "치명타" 글자 제거 → 노랑 24px + 큰 튐 + 흔들림 + 2배 금색 타격 이펙트, 일반 숫자보다 위 줄 | — |
| 인벤토리 | 어두운 칸(GridPanelIndent) + 강조 테두리, 사용 칸 수, 퀵슬롯 번호, 설명 칸 색 띠·아이콘 칸·구분선 | — |
| 보스 체력바 | Cinematic → HUD 레이어 (인벤토리·스킬·지도 창 아래로) | — |
| 스킬 창 | 카드형 → 노드형 스킬 트리 | `ISkillTreeSource` 추가 |

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
- `UI_Showcase_Subway` — 로우폴리 지하철역 임시 맵 + 블록 인형 플레이어·적. 팀 설계 모양 샘플 매니저로 로딩 → STAGE 띠 → 게이트 웨이브 → 보스 → 클리어 → 다음 스테이지. A/D 이동 · Space 2단 점프 · Z/마우스 3타 · Q/E/R 스킬 · F6 재시작 · F7 다음 스테이지 · F8 로딩. UI 크기·위치를 2.5D 게임 화면에서 확인하는 용도 (실제 맵·캐릭터 나오면 폐기)

## 9. 입력·화면
K 스킬 트리 · I 인벤토리 · Tab/M 지도 · ESC 닫기/일시정지 · Space 대화. 640×360 × 정수배. 레이어 HUD 0 · Screen 10 · Popup 20 · Cinematic 30 · Fullscreen 40 · Modal 45 · Toast 50 · Overlay 100. 글꼴 Galmuri 11·9·14·11 Bold.

## 10. 남은 결정
- 효과음 최종 선정(사운드 담당 확인) · 퀘스트 목록에 넣을 목표 종류
- 팀원 매니저 실제 코드 올라오면 Link → 인터페이스 직접 구현으로 바꿀지
- 최대 레벨 확정(현재 15) · 계열 선택 화면 유지 여부(Q도 트리에서 고를지) · 패시브 종류·수치
- 장비 화면 포함 여부 · 히트스톱·흔들림 수치 채택(전투·카메라) · 볼륨 연결(사운드) · 인벤토리·지도 시간 정지 · 지도 세이브 · 아트 교체 담당
