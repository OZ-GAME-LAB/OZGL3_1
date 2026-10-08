# UI 연결 가이드 (팀원용) — v0.6

작성: 박한솔 (UI/UX) · 2026-10-08
대상: 팀 설계 `GameManager → StageManager / SpawnManager / PoolManager`, `StageManager → GateManager → Gate → GateSpawner → SpawnManager`

UI는 `OZ.UI.Contracts` 어셈블리 하나만 참조하면 됩니다. asmdef를 쓰면 references에 `OZ.UI.Contracts` 추가, 안 쓰면 그냥 `using OZ.UI.Contracts;`.
UI가 씬에 없어도 모든 호출은 에러 없이 무시됩니다 (테스트 씬에서 그대로 써도 됨).

## 0. 씬 준비 (이미 해 둠)

`Assets/Scenes/SampleScene`에 들어 있는 것:

| 오브젝트 | 내용 |
|---|---|
| `UIRoot` | 모든 UI (프리팹. UI 쪽 수정은 프리팹 갱신으로 자동 반영) |
| `EventSystem` | UI 클릭용 |
| `UI Links (팀원 연결용)` | `GateUILink` · `PlayerUILink` · `BossUILink` · `UIRequestEvents` + 안내 메모(`UINote`) |

다른 씬에 넣을 때: 메뉴 `OZ > UI > Install UI → SampleScene`을 참고하거나 `UIRoot` 프리팹(`Assets/02.parkhansol_/UI/Prefabs/UIRoot.prefab`)을 끌어다 놓기.

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
스킬(Q/E/R) · 아이템 · 인벤토리 · 스킬 트리는 `ISkillSource` · `IItemSource` · `IInventorySource` · `ISkillTreeSource` 구현 → `GameUI.Bind(this)`
(예시: `Assets/02.parkhansol_/UI/Scripts/Samples/DummyPlayer.cs`)

## 1-1. v0.6 추가
```csharp
GameUI.Quest.Set("kill", "감염체 처치", 0, 20);      // 좌상단 퀘스트 목록에 한 줄 (게이트 봉쇄·보스 처치 줄은 GateUILink가 자동)
GameUI.Quest.SetProgress("kill", 7);                  // 목표 도달 시 자동 완료(체크)
GameUI.Notify.System("획득", "게이트 키");             // 지도 아래 SYSTEM 알림 (레벨업·랭크 승급은 자동)
GameUI.Sound.Play(UISound.Confirm);                   // UI 효과음 — 버튼·창·알림 소리는 UI가 자동으로 냄
```

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
| PoolManager.Get / Release | 꺼냄 / 반납 | 없음 (자동) |

## 3. 동작하는 예시

`OZ > UI > Open Subway Showcase` → Play.
`Samples/Flow/`의 `SampleGameManager` · `SampleStageManager` · `SampleGateManager` · `SampleGate` · `SampleGateSpawner` · `SampleSpawnManager` · `SamplePoolManager`가
위 다이어그램 그대로 돌아가며 로딩 → STAGE 1 띠 → 게이트 열림·적 웨이브 → 게이트 파괴 → 보스 → 클리어 → 페이드 → STAGE 2를 보여 줍니다. UI 호출 줄에는 `// [UI]` 표시.
디버그 키: F6 재시작 · F7 다음 스테이지 · F8 로딩 화면.

문의: 박한솔 (UI/UX)
