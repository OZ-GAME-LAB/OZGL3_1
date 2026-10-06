# 02.parkhansol_ — UI/UX 모듈 (박한솔)

OZGL3_1 데모의 UI 전부가 이 폴더 안에 있습니다. **팀원 폴더·공용 설정은 건드리지 않습니다.**

## 처음 한 번 (UI 담당만)

1. Unity 메뉴 **OZ > UI > Setup** → **전체 실행**
   1. Pixel UI 에셋 이식 (`_Import~/PixelUIHUD_UnityDemo.zip` → `ThirdParty/`)
   2. 한글 폰트 생성 (Galmuri → TMP, 영문 폰트 Fallback 연결)
   3. 샘플 데이터 (기획서 v0.2 검증값)
   4. `UI/Prefabs/UIRoot.prefab` 생성
   5. `UI/Scenes/UI_Sandbox.unity` 생성
2. **OZ > UI > Open UI Sandbox** → Play → 화면 오른쪽 위 도움말의 키로 전체 UI 확인
3. **OZ > UI > Run Self Test** → 수치 변화 150개 항목 자동 점검 (결과: `Logs/OZ_UI_SelfTest.txt`)

> 팀원은 1번을 할 필요가 없습니다. 생성된 결과물이 깃에 같이 올라갑니다.

## 팀원 연동 — 이것만 알면 됩니다

팀원 코드는 `OZ.UI.Contracts`만 씁니다 (`using OZ.UI.Contracts;`). 자동 참조라 asmdef 설정이 필요 없습니다.
UI가 없는 씬에서 호출해도 **에러가 나지 않습니다** (로그 한 줄만 남김).

### 1) 플레이어/스테이지 → HUD 연결 (정명진 · 이광호)

필요한 인터페이스를 구현하고 **등록 한 줄**:

```csharp
public class Player : MonoBehaviour, IHealthSource, ISkillSource, IItemSource
{
    void OnEnable()  => GameUI.Bind(this);   // 또는 오브젝트에 UISourceBinder 컴포넌트 추가
    void OnDisable() => GameUI.Unbind(this);

    public event Action<HealthChange> HealthChanged;
    void TakeDamage(float dmg)
    {
        float prev = hp; hp -= dmg;
        HealthChanged?.Invoke(new HealthChange(prev, hp, maxHp));   // HUD가 알아서 연출
    }
    ...
}
```

| 인터페이스 | 담당(예상) | HUD에 보이는 것 |
|---|---|---|
| `IHealthSource` | 플레이어 | 초상화 칸 + HP 바 + 피격/회복 연출 |
| `IProgressionSource` | 플레이어/코어 | 레벨 · 경험치 · 헌터 랭크 · 남은 스킬 포인트 |
| `ISkillSource` | 플레이어 | Q/E/R 아이콘 · 쿨타임 · 완료 연출 · 잠금 / 스킬 창 투자 |
| `IItemSource` | 플레이어/코어 | 1~4 아이템 수량 · 사용 연출 · 버프 타이머 |
| `IInventorySource` | 코어 | 인벤토리 창 (1×1 슬롯, 이동/사용) |
| `IGateSource` | 코어 | 게이트 봉쇄 n/목표 · 보스 구역 개방 표시 |
| `IBossSource` | 보스 담당 | 보스 체력바 · 페이즈 · 처치 연출 |

전체 구현 예시: `UI/Scripts/Samples/DummyPlayer.cs`, `DummyBoss.cs`

### 2) 한 줄 호출 (GameUI)

```csharp
GameUI.Boss.Show(this, () => StartPattern());   // 보스 등장 연출 후 콜백
GameUI.Screens.ShowDeath();                      // 사망 화면
GameUI.Screens.ShowStageClear(1, HunterRank.E);  // "게이트 파괴" 띠 → 끝나면 UIRequests.NextStage
GameUI.HUD.ShowBanner("게이트 파괴", "1 / 3");     // 가운데 큰 띠 (게이트 봉쇄 시엔 자동)
GameUI.HUD.SetPortrait(playerPortraitSprite);    // HP 바 옆 초상화 칸
GameUI.Map.SetPlayerRoom("S1_03");               // 방 트리거
GameUI.Dialogue.Play(dialogueData, OnTalkEnd);   // 대화
GameUI.Notify.Toast("게이트를 봉쇄했다", ToastType.Success);
GameUI.HUD.ShowGuide("보스 구역이 열렸다");
```

### 2-1) 피해 숫자 · 타격 이펙트 · 적 체력바 (전투 · 적 담당)

```csharp
// 적이 맞았을 때 (위치는 월드 좌표 — UI가 따라감)
GameUI.Damage.Show(hitPoint, damage, isCrit ? DamageKind.Critical : DamageKind.Normal);
GameUI.Damage.ShowText(hitPoint, "MISS");                 // 빗나감·면역 등 글자
GameUI.Damage.Show(playerHead, 12, DamageKind.PlayerHurt); // 플레이어 피격 (빨강), Heal = 초록 +숫자

// 적 머리 위 체력바: IEnemyHealthSource 구현 후 등록 (맞을 때만 뜨고 2.5초 뒤 숨김, 엘리트는 항상 표시)
void Start()     => GameUI.Damage.TrackEnemy(this);
void OnDestroy() => GameUI.Damage.UntrackEnemy(this);
```

| 종류 | 모양 |
|---|---|
| Normal | 흰 숫자 12px, 살짝 튀며 위로 18px, 0.65초 + 작은 하늘색 타격 이펙트 |
| Critical | 노란 숫자 15px + "치명타", 크게 튀고 좌우 흔들림, 0.95초 + 큰 금색 폭발 이펙트 |
| Weakness / Finisher | 글자 없이 색·크기·두께로: 주황 굵은 24px / 빨강 30px (마지막 일격) |
| DamageOverTime | 보라 10px, 맞은 지점 아래, 0.4초 안의 틱은 합산 |
| PlayerHurt / Heal / Miss | 빨강 / 초록 + / 회색 글자 |

피격 흰 번쩍임은 `HitFlash` 컴포넌트 + `UI/Art/FX/OZ_SpriteFlash.mat`. 히트스톱·흔들림 수치표: `Docs/CombatFeedback.md`

같은 자리에 연타가 들어오면 숫자가 위로 쌓여 겹치지 않습니다. 옵션의 '피해 숫자 표시'를 끄면 숫자만 사라지고 이펙트는 남습니다.
참고 구현: `UI/Scripts/Samples/DummyEnemy.cs`

### 3) UI → 게임 요청 받기 (코어)

```csharp
UIRequests.NewGame       += () => ...;           // 타이틀 '새 게임'
UIRequests.ClassSelected += cls => StartGame(cls);
UIRequests.Retry         += RestartStage;         // 사망 화면 '재도전'
UIRequests.NextStage     += LoadNextStage;
UIRequests.ReturnToTitle += GoTitle;
```

### 3-1) 옵션 값 읽기 (사운드 · 카메라 · 전투)

시작 화면 **설정**, 일시정지 **옵션**에서 같은 옵션 창이 열립니다. 값은 `UISettings`(Contracts)에 있고 자동 저장됩니다.

```csharp
bgm.volume = UISettings.BgmVolume;            // 0~1 (마스터 볼륨은 UI가 AudioListener로 이미 적용)
sfx.volume = UISettings.SfxVolume;
UISettings.Changed += ApplyVolume;            // 옵션에서 바꾸면 즉시 호출
if (UISettings.ScreenShake) cameraShake.Play();
if (UISettings.ShowDamageNumbers) SpawnDamageText(dmg);
```

전체 화면 · 해상도 · 수직 동기화는 UI가 직접 적용합니다.

### 4) 입력 막기

```csharp
if (UIState.IsGameplayInputBlocked) return;   // 스킬 창/인벤토리/대화 등이 열려 있으면 true
```

### 5) 데이터 (ScriptableObject, 기획/팀원 입력)

`Create > OZ > UI > …` : Skill / Class / Item / Boss / Map / Dialogue / Speaker
샘플: `UI/Data/Samples` · 검사: **OZ > UI > Validate Data**

## UI 단축키 (UI가 직접 처리)

| 키 | 기능 |
|---|---|
| K / 패드 Select | 스킬 창 |
| I / 패드 Y | 인벤토리 |
| Tab · M / 패드 ↑ | 지도 |
| ESC / 패드 Start | 닫기 → 없으면 일시정지 |
| Q / E · 패드 LB/RB | 옵션 창 탭 전환 |
| Space·Enter·클릭 / 패드 A | 대화 진행 |

Q/E/R, 1~4 는 **플레이어 담당이 입력 처리** → UI는 이벤트만 받습니다. 공용 `InputSystem_Actions`는 수정하지 않았습니다.

## 화면 기준

- 1920×1080 = UI 논리 640×360 × **정수 3배** (`PixelCanvasScaler`). 2560×1440은 4배, 1280×720은 2배.
- 좌표 환산: 화면 px ÷ 3 = UI 좌표.

## 폴더

```text
02.parkhansol_/
├── Docs/                 설계 문서, 회의 자료
├── ThirdParty/           DOTween(무료) · Galmuri(OFL) · Pixel UI & HUD · TextMesh Pro 기본 리소스
├── UI/
│   ├── Scripts/Contracts  ★ 팀원이 쓰는 유일한 어셈블리 (OZ.UI.Contracts)
│   ├── Scripts/Runtime    UI 구현 (OZ.UI) — Core / HUD / Screens / Inventory / Map / Dialogue
│   ├── Scripts/Editor     Setup 마법사, 프리팹 빌더, 데이터 검사
│   ├── Scripts/Samples    더미 플레이어/보스, Sandbox 흐름 (참고 구현)
│   ├── Art/Icons          임시 픽셀 아이콘 (아트 교체 전제)
│   ├── Data/Samples       샘플 데이터
│   ├── Fonts · Prefabs · Scenes
└── _Import~/             원본 zip (깃 제외, Unity 무시)
```

## 공지

- **TextMesh Pro 기본 리소스는 `02.parkhansol_/ThirdParty/TextMesh Pro`에 있습니다.** TMP Essentials를 다시 Import 하지 마세요 (중복 생성됨).
- 일시정지: 스킬 창·인벤토리·지도·일시정지는 열리면 `Time.timeScale = 0` (기획서 기본안). UI 연출은 시간 정지와 무관하게 동작.
