# OZGL3_1 (Project M) — UI/UX 설계 v0.2

작성: 박한솔 (개발 · UI) · 2026-10-06
기준: 기획서 v0.2 · Unity 6000.3 · URP 17.3 · uGUI 2.0(TMP) · Input System 1.20 · DOTween 무료판

## 0. 범위
| 화면 | 내용 | 상태 |
|---|---|---|
| 전투 HUD | 체력 통(Flask) · Lv/EXP · 헌터 랭크 · 게이트 n/목표 · 보스 구역 개방 · Q/E/R 쿨타임 · 1~4 아이템 · 버프 · 미니맵 · 토스트 · 안내 문구 | 구현 |
| 스킬 창 (K) | 포인트 · 단계 1~3 · 현재/다음 효과 · 비용 · 습득 레벨 | 구현 |
| 인벤토리 (I) | 1×1 슬롯, 집기/놓기, 우클릭 사용, 정보창 | 구현 |
| 지도 (Tab · M) | 방 단위 미니맵/전체 지도, 발견·방문·현재 | 구현 |
| 대화창 | 좌/우 초상화, 화자 강조, 타이핑 | 구현 |
| 보스 HUD | 등장 연출, 페이즈 눈금, 큰 피격 플래시, 격파 | 구현 |
| 시작·계열 선택·일시정지·사망·클리어·데모 종료 | 버튼 → `UIRequests` | 구현 |

기획서 v0.2에서 빠졌던 인벤토리·지도·대화창은 2026-10-06 요청으로 이번 작업에 편입. 장비(장착) 창은 범위 밖.

## 1. 구조
- `OZ.UI.Contracts` — 팀원이 참조하는 유일한 어셈블리 (자동 참조)
  - Source 인터페이스: `IHealthSource` `IProgressionSource` `ISkillSource` `IItemSource` `IGateSource` `IInventorySource` `IBossSource`
  - `GameUI` 정적 창구: HUD · Boss · Screens · Map · Dialogue · Notify (UI 없으면 Null 구현)
  - `UISources` (Bind/Unbind), `UISourceBinder` 컴포넌트, `UIRequests`(UI→게임), `UIState`(입력 차단)
  - 데이터 SO: Skill · Class · Item · Boss · Map · Dialogue · Speaker
- `OZ.UI` — 구현: `UIManager`(레이어 캔버스·창 스택·일시정지), `UIWindow` 상속 창, HUD 뷰, `BossHudView`, `MapController`/`MapRenderer`, `DialogueView`, `ToastView`, 위젯(`TweenFillBar`, `CooldownRadial`), `UITween`(DOTween 프리셋), `PixelCanvasScaler`
- `OZ.UI.Editor` — `OZ > UI > Setup` (에셋 이식 → 한글 폰트 → 샘플 데이터 → UIRoot 프리팹 → UI_Sandbox 씬), `Validate Data`
- `OZ.UI.Samples` — `DummyPlayer`/`DummyBoss`/`SandboxDirector` (참고 구현)

다이어그램: `Docs/Diagrams/*.png` (원본 `.mmd`), 회의 자료 `Docs/UI_Meeting.html`

## 2. 화면 기준
- 1920×1080 = 논리 640×360 × 정수 3배 (`PixelCanvasScaler`). 2560×1440 4배, 1280×720 2배.
- 에셋 스프라이트 PPU 100, Point 필터. 한글 Galmuri(OFL) 11/9/14 → TMP 래스터 폰트, 영문 DeadRevolver에 Fallback.
- 레이어 sortingOrder: HUD 0 · Screen 10 · Popup 20 · Cinematic 30 · Fullscreen 40 · Toast 50 · Overlay 100

## 3. 입력
| 키 | 기능 | 처리 |
|---|---|---|
| K / 패드 Select | 스킬 창 | UI |
| I / 패드 Y | 인벤토리 | UI |
| Tab · M / 패드 ↑ | 지도 | UI |
| ESC / 패드 Start | 닫기 → 없으면 일시정지 | UI |
| Space·Enter·클릭 / 패드 A | 대화 진행 | UI |
| Q/E/R · 1~4 | 스킬 · 아이템 | 플레이어 담당 (UI는 이벤트만) |

공용 `InputSystem_Actions`는 수정하지 않음 (UI 액션은 코드로 생성).

## 4. 검증 (2026-10-06, UI_Sandbox)
컴파일 에러 0. 타이틀 → 계열 선택 → 대화 → HUD(피격·회복·경험치·레벨업·스킬 쿨타임·아이템·버프·게이트) → 스킬 투자 → 인벤토리 집기/놓기 → 지도 → 보스 등장·피격 → 사망/재도전 → 클리어 승급 → 일시정지 확인.
참고: 원격 조작 시 에디터 Game 뷰에서 ESC 입력이 전달되지 않음 → 직접 키보드로 재확인 필요.

## 5. 남은 결정
1. 인벤토리·지도 열 때도 시간 정지할지 (현재 정지)
2. 지도 발견 상태 세이브 포함 여부
3. 아이콘·초상화 아트 교체 담당
4. TMP Essentials 재설치 금지 공지
