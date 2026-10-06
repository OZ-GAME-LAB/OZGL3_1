# OZGL3_1 (Project M) — UI/UX 설계 v0.3

작성: 박한솔 (UI/UX) · 2026-10-07 · 공유 문서: https://claude.ai/code/artifact/f8497896-e61f-4266-a194-7db47a969bdb
기준: 기획서 v0.2 · Unity 6000.3 · URP 17.3 · uGUI 2.0(TMP) · Input System 1.20 · DOTween 무료판

UI 화면 14종 구현, 자동 점검 232/232 통과. 브랜치 `parkhansol_ui_261006` (커밋 `d84410d`).

## 1. 범위
| 화면 | 내용 | 상태 |
|---|---|---|
| 전투 HUD | 초상화 칸 + HP 바(Sci-Fi Hud_Lifebar B), Lv·EXP, 헌터 랭크(초상화 아래), 게이트 n/목표, Q/E/R 쿨타임, 1~4 아이템, 버프, 미니맵, 토스트, 안내 문구 | 구현 |
| 게이트 파괴 띠 | 게이트 봉쇄 시 "게이트 파괴 n/목표" 2초 표시 후 사라짐. 스테이지 클리어 창 대체 | v0.3 신규 |
| 피해 숫자·타격 이펙트·적 체력바 | 일반·치명타·약점·처치·지속 피해·플레이어 피격·회복·빗나감 | v0.3 신규 |
| 설정 창 | 시작 화면 '설정', 일시정지 '옵션'. 사운드·화면·게임 탭 | v0.3 신규 |
| 스킬 창 (K) | 포인트, 단계 1~3, 현재/다음 효과 | 구현 |
| 인벤토리 (I) | 칸 36px(아이콘 2배), 집기/놓기, 우클릭 사용 | v0.3 확대 |
| 지도 (Tab·M) | 방 단위 미니맵·전체 지도 | 구현 |
| 대화창 | 좌우 초상화, 화자 강조, 타이핑 | 밀림 버그 수정 |
| 보스 HUD | 등장 연출, 페이즈 눈금, 격파 | 구현 |
| 시작·계열 선택·일시정지·사망·데모 종료 | 버튼 → `UIRequests` | 구현 |

`ShowStageClear` → "게이트 파괴" 띠 → 사라질 때 `UIRequests.NextStage`. 장비 창은 범위 밖(기획서에 장비 없음).

## 2. 구조
- `OZ.UI.Contracts` — 팀원이 참조하는 유일한 어셈블리. `GameUI` 창구(HUD·Damage·Boss·Screens·Map·Dialogue·Notify), Source 인터페이스 8종(+`IEnemyHealthSource`), `UIRequests`, `UIState`, `UISettings`, `HitFlash`, 데이터 SO
- `OZ.UI` — 구현: `UIManager`, HUD(`HudBannerView` 포함), `DamageFxController`, 설정 창(`OptionsWindow`), 대화·지도·보스
- `OZ.UI.Editor` — `OZ > UI > Setup`(에셋·폰트·데이터·UIRoot·샌드박스), Self Test
- `OZ.UI.Samples` — 가상 플레이어·적·보스, `SandboxCombat`, `SandboxFeel`(히트스톱·흔들림 참고 구현)

## 3. v0.3 변경
| 영역 | 바뀐 점 | 팀원 코드 |
|---|---|---|
| HUD 체력 | Flask → Sci-Fi Hud_Lifebar B (Sci-Fi 에셋은 체력바만) | `IHealthSource` |
| 초상화 칸 | 34×34 팔각, 피격 흔들림·저체력 맥박 | `GameUI.HUD.SetPortrait(sprite)` |
| 랭크·SP | 랭크 배지 초상화 아래, HUD SP 표시 삭제 | — |
| 게이트 파괴 띠 | `GateSealed` 시 자동 | `GameUI.HUD.ShowBanner(제목, 부제, 초)` |
| 인벤토리 | 칸 22 → 36px, 창 480×236 | — |
| 설정 창 | 볼륨 3종, 전체 화면·해상도·수직 동기화, 흔들림·피해 숫자·크기·줄여 쓰기 | `UISettings.*`, `UISettings.Changed` |
| 버그 | 대화 반복 시 초상화 밀림, 안내 문구 밀림, 연타 시 체력바 붉게 남음 | — |

## 4. 피해 숫자·타격감 기준
| 종류 | 피해 숫자 | 히트스톱 | 흔들림(UI px) | 흰 번쩍임 | 이펙트 |
|---|---|---|---|---|---|
| Normal | 흰 12px | 0.03초 | 1px | 0.05초 | 작은 하늘색 |
| Critical | 노랑 15px + "치명타" | 0.07초 | 3px | 0.07초 | 큰 금색 |
| Weakness | 주황 굵은 24px, 그림자 2px | 0.08초 | 3px | 0.08초 | 큰 주황 |
| Finisher | 빨강 30px, 그림자 2px (마지막 일격 자동) | 0.12초 | 4px | 0.10초 | 큰 빨강 |
| DamageOverTime | 보라 10px, 아래, 0.4초 합산 | — | — | 0.03초 | — |
| PlayerHurt / Heal / Miss | 빨강 / 초록 + / 회색 | 0.06초 / — / — | 3px / — / — | — | — |

상세: `Docs/CombatFeedback.md`

## 5. 입력·화면
K 스킬 · I 인벤토리 · Tab/M 지도 · ESC 닫기/일시정지 · Q/E 설정 탭 · Space 대화. 640×360 × 정수배. 레이어 HUD 0 · Screen 10 · Popup 20 · Cinematic 30 · Fullscreen 40 · Modal 45 · Toast 50 · Overlay 100. 글꼴 Galmuri 11·9·14·11 Bold.

## 6. 남은 결정
- 장비 화면 포함 여부 · 노드형 스킬 트리 전환 · 히트스톱·흔들림 수치 채택(전투·카메라) · 볼륨 연결(사운드) · 인벤토리·지도 시간 정지 · 지도 세이브 · 아트 교체 담당
