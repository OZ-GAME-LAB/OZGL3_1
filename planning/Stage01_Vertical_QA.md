# Stage 01 수직 레벨 실제 플레이 QA

검증일: 2026-11-18. 대상 프로젝트는 `C:/Users/me/Desktop/OZGameLab3/OZGL3_1`, 씬은 `Assets/Scenes/MainScene.unity`, 스테이지는 `Assets/2.Prefab/CoreSystem/stage.prefab`이다. 제작 기준은 `Stage01_LevelDesign.md`, 실제 입력과 정적 검수의 결과는 이 문서에 정리한다.

실제 타이틀·검사 클래스 선택·상점 구매부터 시작한 최종 플레이에서 일반 전투 3개와 보스전을 완료하고 2스테이지 전환까지 확인했다. 상승·회복 동선, 사다리, 발판 통과, 하강도 실제 입력으로 통과했다. 레벨 진행은 완주 가능하며, 전투 손맛과 보스의 반복 피격 대응은 아래에 별도 시스템 개선 사항으로 남긴다.

## 검증 방식과 증거의 범위

Unity Editor Play Mode에서 실제 타이틀과 클래스 선택 UI를 클릭해 시작했다. `Stage01VerticalQA`가 외부 `command.json`/`batch.json`을 읽고 `InputSystem.QueueStateEvent`로 키보드와 마우스 장치 상태를 전달했다. 이동·점프·사다리·S+Space·공격·아이템·상점은 기존 런타임 입력과 상태 전이를 그대로 사용했다. 공격은 0.07초 클릭과 0.3초 해제 등을 반복하는 실제 버튼 입력이다. 커서가 공격 방향을 결정하므로 전투 대상 쪽으로 조준했다.

플레이어 위치·속도·체력을 시험 코드로 설정하거나, 무적을 주거나, 적을 직접 제거하거나, `TakeDamage`를 시험 코드에서 호출하지 않았다. 최종 검증은 실제 타이틀에서 새 게임과 검사 클래스를 선택한 뒤, 정상 상점에서 칼 250P·응급 회복제 구매 4회 400P·공격 강화제 150P·방어 강화제 150P, 합계 950P를 구매했다. [94_verified_shop.png](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/play01/94_verified_shop.png)에 칼 보유와 회복·강화제 재고가 표시된다. `release-new-up`부터 최종 보스 결과까지 기록된 입력 시작 1,091개에서 T 키 입력은 0개다. 기존 T 키 무기 프리뷰를 사용한 `51`·`58`·`70`번 예비 검증은 최종 완주 증거와 구분한다.

[108_runtime-code-preservation.json](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/play01/108_runtime-code-preservation.json)은 요청 시작 시 SHA256 기준과 `Assets/1.Script/**/*.cs` 중 Editor를 제외한 런타임 C# 71개를 비교했고 `changed=[]`다. 레벨·환경 에셋과 Editor 전용 빌드·QA 도구가 변경 대상이며, 게임플레이 시스템 코드는 보존했다.

관찰 사이에는 Escape로 게임의 정상 일시정지 UI를 열었다. 결과 JSON의 `uiStack=["Pause:Pause"]`, 액션 `Disabled`는 이 상태에서 저장된 것이다. 해당 값만으로 입력 고장을 판정하지 않는다. 일시정지와 도구 대기 시간을 포함한 벽시계 시간은 전투 템포 측정으로 사용하지 않는다.

예전 위치 이동·직접 피해 기반 테스트는 제거되었다. 현재 `Stage01Validation.ValidateAndCapture()`는 `Stage01VerticalAssetQA.Run()`으로, `Stage01PlayValidation.Start()`는 `Stage01VerticalQA.Start()`로 연결된다. 정적 프리뷰 `static_*.png`와 실제 HUD가 포함된 `play01/*.png`는 서로 다른 증거다.

증거 폴더: [vertical](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical). 실제 입력의 ID·키·클릭·커서·시각·게임 시간·프레임은 [history.jsonl](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/play01/history.jsonl)에 남아 있다.

## 실제 전투 진행 확인

| 구간 | 실제 포탈 위치 | 적 수 / 소환 간격 | 결과 스냅샷의 확인 값 |
| --- | --- | --- | --- |
| 시장 | X14, Y0 | 근접 3 / 0.65초 | 시장 `Closed`, 적 배열 비어 있음, 플레이어 Y0·지상·HP90 |
| 카페 옥상 | X45, Y4.5 | 근접·원거리 4 / 0.55초 | 시장·카페 `Closed`, 적 배열 비어 있음, 플레이어 Y4.5·지상·HP50 |
| 정원 옥상 | X76, Y8.5 | 근접·원거리·돌진 6 / 0.5초 | 일반 포탈 셋 모두 `Closed`, `BossGateUnlocked`, 적 배열 비어 있음, 플레이어 Y8.5·지상·HP44 |
| 보스 개방·소환 | X104, Y0 | HP1800 보스 1체 | `BossBattle`, 보스 포탈 `Active`, 실제 보스 X109.129·Y1.125, 플레이어 HP100 |
| 보스 처치 | 보스 광장 | 정상 검 공격 반복 | `Cleared`, 포탈 넷 모두 `Closed`, 적 배열 비어 있음, 플레이어 HP80 |
| 다음 스테이지 | 기존 2스테이지 | 기존 전환 흐름 | `stageNumber=2`, `NormalGates`, 입력 액션 활성, 기존 2스테이지 적 소환 |

근거 JSON:

- [시장 결과](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/play01/95_verified_market-state.json), 저장 시각 01:37:19 UTC, 누적 게임 시간 620.597초.
- [카페 결과](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/play01/98_verified_cafe-state.json), 저장 시각 01:38:00 UTC, 누적 게임 시간 645.522초.
- [정원 결과](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/play01/101_verified_garden-state.json), 저장 시각 01:38:54 UTC, 누적 게임 시간 674.326초.
- [보스 개방·소환](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/play01/104_verified_boss_open-state.json), 저장 시각 01:44:09 UTC, 누적 게임 시간 690.015초.
- [보스 처치·스테이지 클리어](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/play01/106_verified_boss_fight-state.json), 저장 시각 01:44:58 UTC, 누적 게임 시간 738.967초.
- [2스테이지 전환](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/play01/107_stage02_transition-proof.json), 저장 시각 01:47:15 UTC, 누적 게임 시간 872.813초.

위 여섯 결과의 `errors`는 모두 빈 배열이다. 일반 전투의 입력 제어 시험에서는 약 15~20초 단위의 공격 묶음으로 정리되는 흐름을 확인했으나, 위치·아이템·조준·묶음 사이 간격이 통제되어 일반 사용자의 평균 처치 시간이나 난이도 지표로 제시하지 않는다. 누적 게임 시간은 같은 에디터 세션의 재시도도 포함하므로 위 숫자 전체를 1스테이지 완주 시간으로 빼서 계산하지 않는다. `87`·`91`번 실패 시도는 스크립트의 공격 거리·겹침·입력 타이밍 문제를 포함하며, 그 실패만으로 일반 플레이 난이도를 판정하지 않았다.

보스전에서는 실제 첫 피해 확인 약 게임시간 695.6초(보스 HP1697)부터 약 738.53초의 포탈 종료까지 약 43초 동안 검 공격을 반복했다. 플레이어 X107.743 부근에서 이동하지 않은 채 지속 공격이 가능했고, 처음 20 피해 이후 HP80이 유지됐다. HP 절반 이하에서도 공격을 중단하고 회피해야 하는 뚜렷한 반격을 관측하지 못했다. 보스 처치와 진행은 통과했지만, 이 상황의 단조로움은 지형만으로 해결된 것으로 보고하지 않는다. 정확한 원인은 보스의 공격 거리·피격 반응·패턴 전이 검토가 필요하다.

![최종 정상 구매 후 시장 실제 공격](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/play01/95_verified_market-2action.png)

![최종 카페 옥상 실제 전투](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/play01/98_verified_cafe-2action.png)

![최종 정원 옥상 실제 전투](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/play01/101_verified_garden-3action.png)

![실제 보스전](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/play01/106_verified_boss_fight-10action.png)

## 실제 이동·점프·낙하 확인

| 동선 | 실제 입력으로 확인한 결과 | 증거 |
| --- | --- | --- |
| 진입·첫 상승 | D 이동과 Space 점프 반응, 접근 발판과 Y4.5 옥상에 착지 | `50`, `55`, `56`, `57` 캡처 |
| 낮은 사다리 | X33.5에서 지면으로 복귀한 뒤 W로 올라 X34.45, Y4.5 출구에 도착 | [거리 복귀](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/play01/59_fallback_street.png), [사다리 출구](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/play01/60_low_ladder_exit.png) |
| 통과 발판 | Y6.9 발판에 착지 후 S+Space로 하강. 관측 Y6.28을 거쳐 Y4.5 옥상으로 복귀 | [착지](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/play01/61_perch_safe_landing.png), [하강 중](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/play01/62_drop_through_perch.png), [복귀](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/play01/63_drop_return.png) |
| 두 번째 상승 | 정비 연결 발판 Y6.5에서 정원 옥상 Y8.5로 점프·착지 | `64`, `65`, `66`, `67` 캡처 |
| 높은 사다리 | X65.5에서 지면까지 내려간 뒤 W로 올라 X66.45, Y8.5 출구에 도착 | [거리 복귀](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/play01/71_highroof_fallback.png), [사다리 출구](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/play01/72_high_ladder_exit.png) |
| 최고 발판·시야 | Y10.9 발판에 착지하고 점프. 실시간 관측 최고 발 높이 약 Y18.108, 캡처의 머리 위 약 35px 여유로 화면 내 유지 | [최고 발판](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/play01/73_high_perch.png), [점프 정점](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/play01/74_highest_jump_apex.png) |
| 최종 하강 | Y4.5 하강 테라스를 거쳐 지면 보스 광장으로 도착 | [테라스](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/play01/75_descent_terrace.png), [보스 광장](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/play01/76_boss_plaza_actual.png) |

이 경로에서는 사다리 출구 막힘, 발판 내부에 남는 현상, 하강 후 상향 튕김, 필수 점프 실패로 진행이 막히는 현상을 관측하지 않았다. 발판 밖 지면 복귀 후에도 두 옥상으로 다시 올라갈 수 있었다. 카메라 최상단 여유는 큰 편이 아니므로 최고 발판 높이나 점프 수치를 바꾸면 재검증이 필요하다.

## 정적 에셋·충돌 QA

[vertical_asset_report.json](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/vertical_asset_report.json)의 최종 01:50:25.453 UTC 빌드 기준 결과는 지면·발판 지지 745개, 플레이어 몸통 여유 762개, 스폰 앵커 14개 검사 통과다. 렌더러 817개와 콜라이더 79개를 확인했으며 오류·경고 배열은 비어 있다. 연속 지면과 옥상, 두 사다리 출구, 포탈 위치·고도, 적 발바닥 스폰, 누락 스크립트·메시·머티리얼·참조를 검사했다. 옥상 후면 지지 면 4개를 추가한 최종 조형 보정까지 포함하며, 이 면에는 콜라이더가 없어 플레이 경로·충돌은 변하지 않는다.

실제 씬 조명과 카메라로 7개 정적 프리뷰를 생성했다. [static_06_roof_depth.png](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/static_06_roof_depth.png)의 높은 시점에서 옥상 후면 소품·공조기 아래로 지붕 면이 이어지는 것을 확인했고, 실제 카페 `98_verified_cafe-3action.png`·정원 `101_verified_garden-10action.png`에서도 교차·부유로 보이는 요소가 없는지 다시 확인했다. 최초 예열 전 캡처의 색상 읽기 이상은 렌더 타깃 초기화와 예열 2회 후 동기 읽기로 교정했다. 최종 보고서의 모든 프리뷰는 RGB 채널과 밝기 검사를 통과한다. 이 정적 검사는 실행 중 적 AI·공격·난이도 검증을 대신하지 않는다.

## 별도 검토가 필요한 시스템·데이터

| 항목 | 관측 또는 코드로 확인한 범위 | 해석·미완료 범위 |
| --- | --- | --- |
| 보스 포탈 공격 판정 — 에셋 보정 완료 | 초기 실제 검 공격에서 포탈이 개방되지 않았다. 원본 BossGate 루트는 Gate 레이어 8이고 Punch/Sword의 `_enemyLayer`는 128, 즉 Enemy 레이어 7만 포함한다. | Stage01의 해당 포탈 인스턴스만 Enemy 레이어 7로 맞췄다. 최종 정상 검 공격에서 `BossBattle`·포탈 `Active`·HP1800 보스 생성, 이후 처치·`Cleared`·2스테이지 전환까지 확인했다. 런타임 판정 코드는 보존했다. |
| 초기 무기·Q/E/R | 클래스 선택 후 초기 플레이에서 맨손과 빈 Q/E/R HUD를 관측했다. `PlayerSkill.CanUse`는 빈 장착 슬롯에 `NotLearned`를 반환한다. | 정상 해금·구매·장착 과정이 아직 수행되지 않은 상태일 수 있다. 클래스 초기 지급 또는 스킬 시스템 버그로 확정하지 않는다. 초기 클래스 흐름을 따로 추적해야 한다. |
| 공격·피격 중 회피 입력 | 전투 중 이동·점프·대시가 즉시 이어지지 않는 구간을 관측했다. `PlayerState_Attack`은 공격 콤보 예약을, `PlayerState_Hit`은 경직 종료만 처리한다. `PlayerController.Update()`는 매 프레임 미사용 입력 요청을 비운다. | 공격·피격 상태 중 점프/대시 취소나 종료 후 입력 버퍼가 없는 현재 동작은 코드로 설명된다. 지형 걸림으로 분류하지 않으며, 전투 응답성 개선은 시스템 작업이다. |
| 커서 기반 공격 방향 | 클래스 선택 위치에 커서를 둔 채 오른쪽으로 이동하면 공격 순간 왼쪽을 향할 수 있었다. `PlayerState_Attack.StartStep()`이 매 타 커서 위치를 읽고 즉시 방향을 정한다. | 입력 드라이버의 조준 위치를 적 쪽으로 맞춘 뒤 검 공격·처치가 확인됐다. 이동 방향과 조준 방향이 다른 것은 현재 제어 규칙이다. |
| 적의 층간 이동 | 기존 `MonsterBehaviour.UpdateChase()`는 X축 목적지로 이동하며 벽·지면 끝을 검사한다. 점프·사다리로 다른 층을 찾아가는 경로 탐색은 없다. | 각 필수 전투의 적과 포탈을 같은 넓은 옥상에 배치했다. 플레이어가 발판으로 도주했을 때의 무한 회피 가능성은 별도 AI/전투 규칙 검토 대상이다. |
| 적 원거리 투사체와 엄폐 | `MonsterProjectile`의 겹침·SphereCast 마스크는 Player뿐이다. 환경 콜라이더를 검사하지 않는다. | 장식물·지형에 탄이 막힌다고 기대할 수 없다. 환경 관통을 버그로 확정하기보다 의도 확인이 필요하며, 이번 레벨 작업에서는 엄폐 기능을 전제로 설계하지 않았다. |
| 보스 수치·반격 압박 | `Assets/04.Data/BossMonsterData.json` 첫 보스는 HP1800, 이동속도0.2다. 최종 검 전투는 첫 피해부터 처치까지 약 43초였고, 플레이어가 같은 위치에서 공격을 반복하는 동안 최초 피해 이후 HP80이 유지됐다. | 처치·진행은 통과했으나, 거리 유지와 반복 피격 상황에서 반격·패턴 전환이 충분히 압박하지 못하는 사례다. 보스 공격 거리·피격 반응·패턴 우선순위를 별도 시스템 작업으로 확인해야 한다. 단일 원인이나 모든 무기의 밸런스를 확정하지 않는다. |
| 미니맵 가시성 | 실제 HUD에서 우상단 지도 패널의 검은 여백이 크고 경로 표시는 작게 보인다. | 맵 연결의 정적 유효성과 UI 축척·빈 공간은 별개다. UI 레이아웃/스타일 작업 대상으로 남겼다. |

근거 소스는 [PlayerStates.cs](C:/Users/me/Desktop/OZGameLab3/OZGL3_1/Assets/1.Script/Player/Core/PlayerStates.cs), [PlayerController.cs](C:/Users/me/Desktop/OZGameLab3/OZGL3_1/Assets/1.Script/Player/Core/PlayerController.cs), [PlayerSkill.cs](C:/Users/me/Desktop/OZGameLab3/OZGL3_1/Assets/1.Script/Player/Skill/PlayerSkill.cs), [MonsterBehaviour.cs](C:/Users/me/Desktop/OZGameLab3/OZGL3_1/Assets/1.Script/CoreSystem/MonsterBehaviour.cs), [MonsterProjectile.cs](C:/Users/me/Desktop/OZGameLab3/OZGL3_1/Assets/1.Script/CoreSystem/MonsterProjectile.cs), [BossMonsterData.json](C:/Users/me/Desktop/OZGameLab3/OZGL3_1/Assets/04.Data/BossMonsterData.json)이다. 위 시스템 코드는 이번 QA 문서 작성에서 수정하지 않았다.

## 최종 변경 후 재확인 완료

- Stage01 보스 포탈 인스턴스 레이어 보정 후 정상 상점 구매 검으로 개방·소환·처치·2스테이지 전환 확인. 근거는 `104`·`106`·`107`번 JSON이다.
- 새 하늘 그라데이션·구름을 반영한 최종 정적 프리뷰와 `95`~`106`번 실제 Game View에서 원경·옥상 실루엣·포탈·플레이어 대비 확인. 옛 `51`~`76`번 이동 캡처의 회색 하늘은 마지막 배경 변경 이전 상태다.
- 정비 연결 보급 `Service_Bridge_Cache`를 회복 보급으로 고정한 뒤 [99_verified_bridge-land.png](C:/Users/me/.codex/visualizations/2026/11/17/01a1ea95-4744-7e80-b884-b43fd7b8343e/stage01/vertical/play01/99_verified_bridge-land.png)에서 빨간 회복 보급·정상 접촉 획득 확인. 실제 회복 아이템 사용 전후 HP50→80을 관측했다.
- 최종 플레이 결과 JSON 6개의 오류 배열이 비었으며, 런타임 C# 보존 검사 71개에서 변경 0개 확인.
- 옥상 후면 지지 면 4개 조형 보정 후 마지막 정적 재검증 완료: 01:50:25.453 UTC, 렌더러 817·콜라이더 79·지지 745·몸통 여유 762·스폰 14, 오류·경고 0개, 정적 프리뷰 7개. 높은 시점의 옥상 깊이 확인과 실제 전투 캡처 재검토를 마쳤다.

완주·진행 검증과 전투 체감 완성도는 구분한다. 수직 레벨과 실제 진행은 확인됐으며, 보스의 반복 공격 대응·공격 및 피격 중 입력 수용·초기 클래스 스킬 지급 흐름 등은 시스템 검토가 필요하다.

## develop 통합 후 게시 검증 — 2026-11-18

위 실제 완주와 전투 관측은 수직 레벨 작업 커밋 `a77a5896`까지의 상태를 기준으로 한다. 게시 요청 후 최신 `origin/develop`의 `0513ad87`을 통합했다. 해당 develop에는 몬스터·보스 데이터, 보스 패턴, 투사체 풀링, 퀘스트 보상 및 UI 연결 변경이 포함된다. 따라서 위 전투 시간·HP·패턴 관측을 통합본의 재측정 결과로 해석하지 않는다. 아트 작업 중 런타임 C# 71개 보존 기록도 당시 작업 범위에 대한 검사이며, 이후 develop에서 반영된 시스템 변경을 제외한다.

충돌 해결 시 Stage01은 검증한 수직 맵을 유지했다. develop의 Stage01 변경은 이전 맵의 부모 묶음·위치·회전 변경이므로 새 구조에 덮어쓰지 않았다. Stage02는 develop의 정식 Gate/BossGate 프리팹 인스턴스 구조를 채택했다. 로컬의 옛 인라인 오브젝트에 추가된 기본 직렬화값은 새 구조에 별도로 이식할 고유 설정이 없었다. StageAreaBridge는 develop의 이벤트 기반 표시를 유지하면서 StageLayout 구역명 표시를 보호하는 기존 가드를 통합했다.

통합 후 Unity 6000.3.21f1 배치 실행은 종료 코드 0으로 완료했다. C# 컴파일 성공, Stage01 렌더러 817개·콜라이더 79개·지지면 745곳·몸통 여유 762곳·스폰 앵커 14개, 정적 프리뷰 7개 검사에서 오류·경고 0개다. 데이터·풀·스폰·씬 초기화 연결도 소스로 재검토했다. 이번 게시 검증에서는 최신 전투 시스템으로 전체 완주를 다시 실행하지 않았다.

로컬 검증 산출물: [Unity 로그](C:/Users/me/Desktop/OZGameLab3/OZGL3_1/.local-validation/publish/unity-merge.log), [정적 검사 보고서](C:/Users/me/Desktop/OZGameLab3/OZGL3_1/.local-validation/publish/static/vertical_asset_report.json). 이 임시 산출물은 저장소 게시 대상에서 제외한다.
