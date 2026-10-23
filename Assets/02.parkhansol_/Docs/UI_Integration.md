# UI 연결 가이드 (팀원용) — v1.3

작성: 박한솔 (UI/UX) · 2026-10-12
대상: 팀 설계 `GameManager → StageManager / SpawnManager / PoolManager`, `StageManager → GateManager → Gate → GateSpawner → SpawnManager`

UI는 `OZ.UI.Contracts` 어셈블리 하나만 참조하면 됩니다. asmdef를 쓰면 references에 `OZ.UI.Contracts` 추가, 안 쓰면 그냥 `using OZ.UI.Contracts;`.
UI가 씬에 없어도 모든 호출은 에러 없이 무시됩니다 (테스트 씬에서 그대로 써도 됨).

## 0. 씬 준비 (이미 해 둠)

`Assets/Scenes/MainScene`에 들어 있는 것:

| 오브젝트 | 내용 |
|---|---|
| `UIRoot` | 모든 UI (프리팹. UI 쪽 수정은 프리팹 갱신으로 자동 반영) |
| `EventSystem` | UI 클릭용 |
| `UI Links` | `GateUILink` · `PlayerUILink` · `BossUILink` · `UIRequestEvents` + 안내 메모(`UINote`) |

다른 씬에 넣을 때: 메뉴 `OZ > UI > Install UI → MainScene`을 참고하거나 `UIRoot` 프리팹(`Assets/02.parkhansol_/UI/Prefabs/UIRoot.prefab`)을 끌어다 놓기.

연결 방식은 두 가지 중 편한 쪽:
- **A. Link 컴포넌트** — 인터페이스 구현 없이 메서드만 부름 (아래 예시). 처음엔 이쪽 추천.
- **B. 인터페이스 직접 구현** — `IGateSource` 등을 구현하고 `GameUI.Bind(this)`. 이 경우 해당 Link 컴포넌트는 지우기.

## 1. 매니저별 연결

### GameManager
```csharp
[SerializeField] UIRequestEvents uiRequests; // 인스펙터 연결만 쓸 거면 코드 필요 없음

public async Task InitializeAsync()
{
    GameUI.Flow.ShowLoading("데이터 불러오는 중");
    // ... 로드 ...
    GameUI.Flow.SetLoadingProgress(0.5f, "오브젝트 풀 준비");   // 0~1, 뒤로 안 감
    // ... PoolManager 예열 ...
    GameUI.Flow.HideLoading();                                 // 100% 보여 주고 사라짐
}

public void RestartStage()
{
    GameUI.Flow.Transition(() => {           // 화면이 완전히 검을 때 실행
        GameUI.Screens.CloseAll();
        stageManager.StartStage(stageManager.CurrentStage);
    });
}

public void ChangeStage(int next)
{
    GameUI.Flow.Transition(() => stageManager.StartStage(next));
    // async면: await GameUI.Flow.TransitionAsync(async () => await LoadStageAsync(next));
}
```
UI 버튼 → 게임: `UI Links` 오브젝트의 **UIRequestEvents** 인스펙터에서
`On Retry → GameManager.RestartStage`, `On Next Stage → GameManager.(다음 스테이지 함수)`, `On New Game → GameManager.StartGame` 연결.
코드로 하려면 `UIRequests.Retry += RestartStage;`

### StageManager
```csharp
[SerializeField] BossUILink bossUI;

public void StartStage(int stage)
{
    GameUI.Flow.StageIntro(stage, "지하철역 승강장", "게이트 3곳을 봉쇄하라"); // 페이드 중이면 걷힌 뒤 자동 재생
    gateManager.StartGatePhase();
}

public void StartBossphase()
{
    GameUI.HUD.ShowGuide("보스 구역이 열렸다", 2.5f);   // 선택
    bossUI.Begin(boss.MaxHP, () => boss.StartAI());     // 등장 연출이 끝나면 AI 시작
}
// 보스가 맞을 때: bossUI.SetHP(boss.HP);  (페이즈 눈금·격파 연출 자동, 0이 되면 Defeat 자동)

public void ClearStage()
{
    if (isLastStage) GameUI.Screens.ShowDemoEnd();
    else GameUI.Screens.ShowStageClear(CurrentStage, rank); // "게이트 파괴" 띠 → 끝나면 UIRequests.NextStage
}
```

### GateManager · Gate
```csharp
[SerializeField] GateUILink gateUI;

public void StartGatePhase()   { gateUI.BeginStage(stage, targetCount); ActivateNextGate(); }
public void ActivateNextGate() { gates[i].Open(); gateUI.GateOpened(); }
void OnGateClosed()            { gateUI.GateSealed(); }   // HUD 숫자 + "게이트 파괴" 띠 + 목표 달성 시 "보스 구역 개방" 자동
```
Gate · GateSpawner는 UI 연결 필요 없음. (지도 아이콘을 쓰면 `GameUI.Map.SetRoomIcon(roomId, MapRoomIcon.Gate / None)`)

### SpawnManager · PoolManager · 적 프리팹
적 프리팹에 **EnemyUILink** 컴포넌트 추가 (`Add Component > OZ > UI > Links > Enemy UI Link`).
```csharp
var link = enemy.GetComponent<EnemyUILink>();
link.Init(maxHP);                    // Spawn() 직후 (풀에서 꺼낼 때)
bool dead = link.Hit(damage, isCrit); // 맞을 때 — 피해 숫자 + 타격 이펙트 + 머리 위 체력바, 마지막 일격은 자동 강조
```
- 풀에서 꺼낼 때(OnEnable) 체력바 등록, 넣을 때(OnDisable) 해제 → **PoolManager는 고칠 것 없음**
- 체력바 위치: `Bar Anchor`에 머리 위 빈 오브젝트 지정 (비우면 위로 2m)
- 엘리트: `Elite` 체크 → 체력바 항상 표시
- 이미 `IEnemyHealthSource`를 구현한 적이면 `EnemyHealthBarTracker` 컴포넌트만 붙여도 됨

### 플레이어
```csharp
[SerializeField] PlayerUILink playerUI;
playerUI.SetHP(hp, maxHP);          // 피격·회복 연출 자동
playerUI.SetExp(exp, expToNext);
playerUI.SetLevel(level);           // 오르면 레벨업 연출
if (UIState.IsGameplayInputBlocked) return;   // 입력 처리 맨 앞 (창·로딩·페이드 중엔 true)
```
**체력은 이미 연결됨 (v0.7)** — `PlayerHealthBridge`가 Play 시 자동으로 붙어 HUD 체력 · 피해 숫자 · 피격 모션 · 사망 화면까지 처리. PlayerController 수정 없음.
```csharp
PlayerHealthBridge.Instance.TakeDamage(10f);          // 몬스터 공격·함정 등에서 호출 (무적 0.8초 자동)
PlayerHealthBridge.Instance.TakeDamage(10f, hitPos);  // 맞은 위치 지정
PlayerHealthBridge.Instance.Heal(20f);
PlayerHealthBridge.Instance.Died += () => { ... };    // 사망 시 게임 쪽 처리 (사망 화면은 자동)
```
- 지금은 **임시로 몬스터(`Monster`)에 닿으면 10 피해**. 몬스터 공격을 직접 만들면 `UI Links`에 `Player Health Bridge`를 Add Component 해서 `Monster Contact Damage` 끄기.
- 재도전 → `SceneGameLink.RestartStage`가 체력 가득으로 복구.
- 코드: `Assets/02.parkhansol_/UI/Scripts/GameBridge/PlayerHealthBridge.cs`

스킬(Q/E/R) · 아이템(1~4) · 스킬 트리는 `ISkillSource` · `IItemSource` · `ISkillTreeSource` 구현 → `GameUI.Bind(this)`
(예시: `Assets/02.parkhansol_/UI/Scripts/Samples/DummyPlayer.cs`)

## 1-1. v0.6 추가
```csharp
GameUI.Quest.Set("kill", "감염체 처치", 0, 20);      // 좌상단 퀘스트 목록에 한 줄 (게이트 봉쇄·보스 처치 줄은 GateUILink가 자동)
GameUI.Quest.SetProgress("kill", 7);                  // 목표 도달 시 자동 완료(체크)
GameUI.Notify.System("획득", "게이트 키");             // 지도 아래 SYSTEM 알림 (레벨업·랭크 승급은 자동)
GameUI.Sound.Play(UISound.Confirm);                   // UI 효과음 — 버튼·창·알림 소리는 UI가 자동으로 냄
```

## 1-2. v0.7 추가 — 지역 이름 · 저체력 경고 · 화면 밖 화살표 · 상호작용 안내

| 기능 | 팀원이 할 일 | 코드로 부를 때 |
|---|---|---|
| **지역 이름** (화면 위쪽 가운데, 부드럽게 떴다 사라짐) | 레벨: 지역 입구에 빈 오브젝트 + `Area Name Trigger` (`Add Component > OZ > UI > Links`). BoxCollider 자동 추가·Is Trigger. 이름/부제 입력, `Map Room Id`를 넣으면 지도 방도 같이 바뀜 | `GameUI.Area.Show("서울 거리", "1스테이지");` |
| **저체력 경고** (체력 30% 이하 → 화면 가장자리 붉은 비네트가 심장 박동에 맞춰 뜀 + 심장 소리) | 없음 (체력 연결만 되어 있으면 자동) | — |
| **회복 번쩍임** (회복 순간 가장자리 초록 비네트가 짧게) | 없음 (자동) | — |
| **버프 테두리** (포션 버프 동안 그 포션 색의 얇은 빛 테두리가 점멸, 여러 개면 같은 자리에서 주황→파랑→초록 번갈아 점멸, 새로 먹은 버프 색부터, 점멸 주기는 끝날 때까지 일정) | 없음 — `IItemSource`의 `BuffApplied`/`BuffEnded`만 보내면 됨. 색은 `ItemData.fxColor` | — |
| **체력바 상태 색** (60% 초과 하늘색 → 60% 이하 노랑 → 30% 이하 빨강, 바뀌는 순간 번쩍) | 없음 (자동) | — |
| **랭크 승급 연출** (팡파레 + 금빛 번쩍·빛줄기·충격파, 이전 랭크가 빠지고 새 랭크가 솟아오름, 반짝이 터짐, "RANK UP!") | 없음 — `PlayerUILink.SetRank`(또는 `IProgressionSource.RankUp`)로 랭크가 오르면 자동 | — |
| **화면 밖 목표 화살표** (화면 가장자리 화살표 + 아이콘 + 거리) | MainScene 게이트는 자동 (`GateIndicatorBridge`). 다른 목표는 프리팹에 `Objective Marker Link` (종류: Objective / Gate / Boss) | `GameUI.Indicators.Track(t, IndicatorKind.Gate);` / `Untrack(t)` |
| **상호작용 안내** "[F] 조사" (대상 머리 위) | 문·레버·NPC·세이브 지점에 `Interact Prompt Link` (행동 글자·키·반경). **표시만** 담당 — 키 입력과 실제 동작은 게임 쪽 | `GameUI.Prompt.Show(door, "열기");` / `Hide(door)` |

- 플레이어 판별: `UIPlayerTag` (MainScene은 Play 시 플레이어에 자동으로 붙음). 다른 씬은 플레이어에 직접 추가하거나 트리거의 `Player Tag`를 맞추면 됨.
- 같은 지역 이름은 4초 안에 다시 들어와도 안 뜸 (경계에서 왔다 갔다 할 때 깜빡임 방지). 처음 한 번만 띄우려면 `Only First Time`.
- 안내가 보이는 동안 **F(패드 X)를 누르면** 키 칸이 눌리는 연출 + 확인음 → `InteractPromptLink`의 **On Interact** 이벤트 (인스펙터에서 문 열기 등 연결) / 코드는 `GameUI.Prompt.Pressed += target => ...`. 1회용이면 `Disable After Use`.
- 상호작용이 끝나 더 못 쓰게 되면 `link.SetAvailable(false)`.
- 확인: UI_Sandbox에서 **F6 지역 이름 · F7 상호작용 안내 · F8 화면 밖 화살표 · H 연타(저체력)**

## 1-3. v1.3 추가 — 튜토리얼 연출 (기획서 v0.5 3장)

튜토리얼 흐름을 만드는 쪽(코어)이 순서대로 부르기만 하면 됩니다. 씬·프리팹에 따로 놓을 것 없음 (UIRoot가 알아서 붙임).

| 기획서 3장 | 호출 | 하는 일 |
|---|---|---|
| 1 일상과 경보 (UI 없는 화면) | `GameUI.HUD.SetParts(HudPart.All, false, animate: false);` | 전투 HUD 전부 숨김. 미니맵 아래 알림은 남음 |
| 1 대피 방송 | `GameUI.Notify.System("방송", "현재 12번 지구에서 던전 브레이크가 …");` | HUD를 숨겨도 뜸 |
| 2 각성 + 상태창 첫 등장 | `GameUI.Tutorial.ShowAwakening(1000, HunterRank.F, () => 다음());` | 번쩍 → "각성 · F급 · 상태창 · +1,000P" 카드 → HUD가 하나씩 등장 → 콜백. 게임은 안 멈춤. **포인트 지급은 코어가** |
| 3 첫 게이트·전투 설명 | `GameUI.Tutorial.Play(new[]{ TutorialStep.World(monster.transform, "기본 공격", "좌클릭으로 주먹 공격"), TutorialStep.Hud(HudPart.Quests, "현재 목표", "…") }, () => 전투시작());` | 화면 정지 + 그곳만 밝히고 설명. [Space]/[Enter]/패드 A로 다음 |
| 5 봉쇄 안내 | `GameUI.Tutorial.Show(TutorialStep.World(gate.transform, "게이트 봉쇄", "가까이 가서 [F]로 닫습니다", 56f));` | 한 단계만 |
| 6 상태창 설명·첫 구매 | `GameUI.Tutorial.PlayStatusTour(() => Stage1노드해금());` | 상태창 열기 → 탭 순서대로 강조 → 포인트 → 끝나면 **상태창은 열린 채** 상점 탭 |

- 설명이 떠 있는 동안: `Time.timeScale = 0`, `UIState.IsGameplayInputBlocked = true`, ESC·Tab 단축키 무시. 상태창 버튼도 잠김 (Space가 구매로 새지 않게).
- 끝 콜백은 창이 닫힌 **다음 프레임**에 옴 → 마지막 [Space]가 점프로 안 읽힘.
- 강조 대상: `TutorialStep.Message`(대상 없음) · `Hud(HudPart)` · `Tab(StatusTab)` · `World(Transform)` · `Ui(RectTransform)`. 인스펙터에서 `TutorialStep[]`로 채워도 됨.
- 부위 이름: `Health` 체력 · `Minimap` · `Items` 1~4 · `Quests` · `Skills` Q/E/R · `Buffs` · (`Alarm` 알림, All에 없음). 일부만: `SetParts(HudPart.Health | HudPart.Quests, true)`.
- UI가 없는 씬: 설명은 건너뛰고 콜백만 바로 불림 (튜토리얼 진행이 막히지 않음).

## 2. 한눈에 보기

| 팀 설계 | 언제 | UI 호출 |
|---|---|---|
| GameManager.InitializeAsync | 시작 로딩 | `GameUI.Flow.ShowLoading` → `SetLoadingProgress` → `HideLoading` |
| GameManager.StartGame | 타이틀 '새 게임' | `UIRequestEvents.onNewGame` 또는 `UIRequests.NewGame` |
| GameManager.RestartStage | 사망 '재도전' | `GameUI.Flow.Transition(...)` / 요청: `onRetry` |
| GameManager.ChangeStage | 클리어 띠 끝 | `GameUI.Flow.Transition(...)` / 요청: `onNextStage` |
| StageManager.StartStage | 스테이지 시작 | `GameUI.Flow.StageIntro(n, 이름)` |
| StageManager.StartBossphase | 보스 등장 | `BossUILink.Begin(maxHP, 콜백)` → `SetHP` |
| StageManager.ClearStage | 보스 처치 후 | `GameUI.Screens.ShowStageClear(n, 랭크)` / 마지막 `ShowDemoEnd()` |
| GateManager.StartGatePhase | 게이트 단계 시작 | `GateUILink.BeginStage(stage, 목표)` |
| GateManager.ActivateNextGate | 게이트 열림 | `GateUILink.GateOpened()` |
| Gate.Close (봉쇄) | 웨이브 정리 | `GateUILink.GateSealed()` |
| SpawnManager.Spawn | 적 생성 | `EnemyUILink.Init(maxHP)` |
| (적 피격) | 맞을 때 | `EnemyUILink.Hit(피해, 치명타)` |
| (플레이어 피격) | 맞을 때 | `PlayerHealthBridge.Instance.TakeDamage(피해)` |
| 레벨 (지역 입구) | 들어올 때 | `AreaNameTrigger` 또는 `GameUI.Area.Show(이름)` |
| 게이트·보스 프리팹 | 켜져 있는 동안 | `ObjectiveMarkerLink` 또는 `GameUI.Indicators.Track` |
| 보스 구역 입구 (레벨) | 게이트 목표 달성 후 | `ObjectiveMarkerLink` (Kind = Boss, **Only After Boss Unlock** 체크) — v0.9 |
| 문·NPC·세이브 지점 | 가까이 왔을 때 | `InteractPromptLink` 또는 `GameUI.Prompt.Show` |
| PoolManager.Get / Release | 꺼냄 / 반납 | 없음 (자동) |

## 3. 동작하는 예시

`OZ > UI > Open Subway Showcase` → Play.
`Samples/Flow/`의 `SampleGameManager` · `SampleStageManager` · `SampleGateManager` · `SampleGate` · `SampleGateSpawner` · `SampleSpawnManager` · `SamplePoolManager`가
위 다이어그램 그대로 돌아가며 로딩 → STAGE 1 띠 → 게이트 열림·적 웨이브 → 게이트 파괴 → 보스 → 클리어 → 페이드 → STAGE 2를 보여 줍니다. UI 호출 줄에는 `// [UI]` 표시.
디버그 키: F6 재시작 · F7 다음 스테이지 · F8 로딩 화면.

문의: 박한솔 (UI/UX)
