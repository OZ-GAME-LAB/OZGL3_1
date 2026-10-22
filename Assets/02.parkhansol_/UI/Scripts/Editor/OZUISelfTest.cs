using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using OZ.UI.Contracts;
using OZ.UI.Samples;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OZ.UI.EditorTools
{
    /// <summary>
    /// OZ > UI > Run Self Test : UI_Sandbox를 플레이하며 수치 변화 → UI 표시가 맞는지 자동 점검.
    /// 결과: 콘솔 [OZ SelfTest] + 프로젝트/Logs/OZ_UI_SelfTest.txt
    /// 데이터 에셋을 잠깐 바꾸는 테스트는 끝나면 원래 값으로 되돌린다.
    /// </summary>
    [InitializeOnLoad]
    internal static class OZUISelfTest
    {
        /// <summary>PlayerHealth와 같은 방식(IHealthSource + HealthChanged)의 테스트용 체력</summary>
        sealed class SelfTestHealth : IHealthSource
        {
            public float HP { get; private set; } = 100f;
            public float MaxHP { get; private set; } = 100f;
            public event Action<HealthChange> HealthChanged;
            public void Set(float hp, float max)
            {
                float prev = HP; MaxHP = max; HP = Mathf.Clamp(hp, 0f, max);
                HealthChanged?.Invoke(new HealthChange(prev, HP, MaxHP));
            }
        }

        const string Flag = "OZ_UI_SELFTEST_PENDING";
        static readonly List<string> Lines = new List<string>();
        static int _pass, _fail, _logErrors;

        static OZUISelfTest()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        [MenuItem("OZ/UI/Run Self Test", priority = 21)]
        static void Start()
        {
            if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; return; }
            if (!OZSandboxBuilder.IsDone) { Debug.LogError("[OZ SelfTest] UI_Sandbox 씬이 없습니다. Setup을 먼저 실행하세요."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(OZPaths.SandboxScene);
            SessionState.SetBool(Flag, true);
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange s)
        {
            if (s != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
            SessionState.SetBool(Flag, false);
            Run();
        }

        // ───────────────────────────── 러너 ─────────────────────────────
        static async void Run()
        {
            Lines.Clear(); _pass = _fail = _logErrors = 0;
            bool bg = Application.runInBackground;
            Application.runInBackground = true; // Unity 창에서 포커스가 빠져도 플레이가 멈추지 않게 (시간 재는 테스트가 많음)
            Application.logMessageReceived += OnLog;
            Log($"OZ UI Self Test — {DateTime.Now:yyyy-MM-dd HH:mm:ss}  (Unity {Application.unityVersion})");
            try
            {
                await Wait(1.0f);
                await Suite("해상도 배율", Scale);
                await Suite("체력", Health);
                await Suite("랭크 (레벨·경험치 없음 — 기획서 v0.5 4·8장)", Progression);
                await Suite("기술 구매·Q/E/R 장착·스탯 구매 (기획데이터 4·5장)", Skills);
                await Suite("아이템·버프", Items);
                await Suite("게이트 → 퀘스트 목록 (v0.6)", Gate);
                await Suite("HUD 배치: 양손 분리·지도 아래 알림·보스 바 (v0.6)", HudLayout);
                await Suite("시스템 알림 (v0.6)", SystemAlarm);
                await Suite("UI 효과음 (v0.6)", Sounds);
                await Suite("보스", Boss);
                await Suite("지도", Map);
                await Suite("대화", Dialogue);
                await Suite("화면·입력 차단·일시정지", Screens);
                await Suite("옵션 창", Options);
                await Suite("피해 숫자·타격 이펙트·적 체력바", Damage);
                await Suite("대화 반복 시 초상화 위치 고정", DialogueDrift);
                await Suite("게임 흐름: 로딩·페이드·스테이지 띠 (v0.5)", Flow);
                await Suite("풀에서 꺼내는 적 체력바 (v0.5)", PooledEnemyBar);
                await Suite("UI Link 컴포넌트 (v0.5)", Links);
                await Suite("지역 이름·저체력·화면 밖 화살표·상호작용 (v0.7)", V07);
                await Suite("HUD 부위 숨김·각성 카드·튜토리얼 강조 (v1.3)", TutorialSuite);
            }
            catch (Exception e)
            {
                Fail("러너 예외", e.ToString());
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                Log("");
                Log($"결과: PASS {_pass} / FAIL {_fail} / 테스트 중 콘솔 에러 {_logErrors}");
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "OZ_UI_SelfTest.txt"));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, string.Join("\n", Lines), new UTF8Encoding(false));
                if (_fail == 0 && _logErrors == 0) Debug.Log($"[OZ SelfTest] 전체 통과 ({_pass}) → {path}");
                else Debug.LogError($"[OZ SelfTest] 실패 {_fail}, 콘솔 에러 {_logErrors} → {path}");
                Time.timeScale = 1f;
                Application.runInBackground = bg;
                EditorApplication.isPlaying = false;
            }
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            if (msg.StartsWith("[OZ SelfTest]")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _logErrors++;
                Lines.Add("  ! 콘솔 " + type + ": " + msg.Split('\n')[0]);
            }
        }

        static async Task Suite(string name, Func<Task> body)
        {
            Log("");
            Log("■ " + name);
            try { await body(); }
            catch (Exception e) { Fail(name + " 예외", e.GetType().Name + ": " + e.Message); }
        }

        static Task Wait(float sec) => Task.Delay(Mathf.RoundToInt(sec * 1000));

        static void Log(string s) { Lines.Add(s); }

        static void Check(string name, bool ok, string detail = "")
        {
            if (ok) { _pass++; Lines.Add($"  PASS  {name}"); }
            else Fail(name, detail);
        }

        static void Fail(string name, string detail)
        {
            _fail++;
            Lines.Add($"  FAIL  {name}  — {detail}");
        }

        static void Eq(string name, string actual, string expected) =>
            Check(name, actual == expected, $"표시 '{actual}' ≠ 기대 '{expected}'");

        static void Near(string name, float actual, float expected, float tol = 0.02f) =>
            Check(name, Mathf.Abs(actual - expected) <= tol, $"값 {actual:0.###} ≠ 기대 {expected:0.###}");

        static T Get<T>(object o, string field)
        {
            var f = o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return f != null ? (T)f.GetValue(o) : default;
        }

        static object Call(object o, string method, params object[] args)
        {
            var m = o.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return m?.Invoke(o, args);
        }

        static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);

        static DummyPlayer P => Find<DummyPlayer>();
        static HudView Hud => Find<HudView>();

        /// <summary>타이틀/대화 등을 닫고 전투 HUD 상태로</summary>
        static async Task ToCombat(PlayerClass cls = PlayerClass.Sword)
        {
            UIManager.Instance.CloseAll();
            var dir = Find<SandboxDirector>();
            ClassData data = null;
            foreach (var c in dir.classes) if (c != null && c.playerClass == cls) data = c;
            P.ResetGrowth(); // v1.3: 포인트 1000 · 스탯·기술·장착 처음 (각성 직후)
            P.SetClass(data);
            GameUI.HUD.Visible = true;
            GameUI.Dialogue.Stop();
            UIManager.Instance.CloseAll();
            Time.timeScale = 1f;
            await Wait(0.6f);
        }

        // ───────────────────────────── v1.3 HUD 부위 숨김·각성 카드·튜토리얼 강조 (기획서 v0.5 3장) ─────────────────────────────
        static bool Contains(Rect outer, Vector2 p) => p.x >= outer.xMin - 0.5f && p.x <= outer.xMax + 0.5f && p.y >= outer.yMin - 0.5f && p.y <= outer.yMax + 0.5f;

        static async Task TutorialSuite()
        {
            await ToCombat();
            var hud = Hud;
            var dir = Find<TutorialDirector>();
            Check("튜토리얼 연결 (GameUI.Tutorial)", dir != null && ReferenceEquals(GameUI.Tutorial, dir));
            if (dir == null || hud == null) return;

            // ── HUD 부위 숨김 ──
            var parts = new[] { HudPart.Health, HudPart.Minimap, HudPart.Items, HudPart.Quests, HudPart.Skills };
            var before = new Dictionary<HudPart, Vector3>();
            foreach (var p in parts) { var r = hud.PartRoot(p); Check($"부위 찾기: {p}", r != null); if (r != null) before[p] = r.position; }
            GameUI.HUD.SetParts(HudPart.All, false, animate: false);
            await Wait(0.1f);
            bool allHidden = true, posKept = true;
            foreach (var p in parts)
            {
                var g = hud.Gate(p);
                if (g == null || g.alpha > 0.01f) allHidden = false;
                var r = hud.PartRoot(p);
                if (r != null && before.TryGetValue(p, out var pos) && (r.position - pos).sqrMagnitude > 0.01f) posKept = false;
            }
            Check("SetParts(All, false) → 전투 HUD 전부 숨김", allHidden);
            Check("감싸도 HUD 배치 그대로", posKept);
            Check("알림은 남음 (VisibleParts = Alarm)", GameUI.HUD.VisibleParts == HudPart.Alarm, GameUI.HUD.VisibleParts.ToString());
            GameUI.Notify.System("방송", "현재 12번 지구에서 던전 브레이크가 터졌습니다.");
            await Wait(0.4f);
            Check("HUD 숨김 중에도 방송 알림은 뜸", hud.systemAlarm != null && hud.systemAlarm.VisibleCount > 0);

            GameUI.HUD.SetParts(HudPart.Health | HudPart.Quests, true);
            await Wait(1.2f);
            Check("일부만 등장 (체력·퀘스트)", hud.Gate(HudPart.Health).alpha > 0.99f && hud.Gate(HudPart.Quests).alpha > 0.99f && hud.Gate(HudPart.Items).alpha < 0.01f);
            GameUI.HUD.SetParts(HudPart.All, false, animate: false);

            // ── 각성 카드 ──
            float hold = dir.awakeningHold;
            dir.awakeningHold = 0.4f;
            bool awakeDone = false;
            GameUI.Tutorial.ShowAwakening(1000, HunterRank.F, () => awakeDone = true);
            await Wait(0.6f);
            Check("각성 카드 등장", GameUI.Tutorial.IsAwakening && dir.CardGroup != null && dir.CardGroup.alpha > 0.9f, dir.CardGroup != null ? $"alpha {dir.CardGroup.alpha:0.00}" : "카드 없음");
            await Wait(0.9f);
            Eq("포인트 올라가며 표시", dir.CardPoints.text, "+1,000P");
            await Wait(2.2f);
            Check("끝나면 콜백 + 카드 꺼짐", awakeDone && !GameUI.Tutorial.IsAwakening && !dir.Card.gameObject.activeSelf);
            bool shownAll = true;
            foreach (var p in parts) if (hud.Gate(p).alpha < 0.99f) shownAll = false;
            Check("각성 뒤 HUD 전부 등장", shownAll && (GameUI.HUD.VisibleParts & HudPart.All) == HudPart.All);
            dir.awakeningHold = hold;

            // ── 강조 설명 (화면 정지) ──
            bool done = false;
            int changes = 0;
            Action<int> onStep = _ => changes++;
            GameUI.Tutorial.StepChanged += onStep;
            GameUI.Tutorial.Play(new[]
            {
                TutorialStep.World(P.transform, "기본 공격", "좌클릭으로 주먹 공격. 공격은 커서 방향을 따릅니다."),
                TutorialStep.Hud(HudPart.Quests, "현재 목표", "게이트 소속 몬스터를 전부 잡아야 봉쇄할 수 있습니다."),
                TutorialStep.Message("게이트", "가까이 가서 [F]로 직접 닫습니다."),
            }, () => done = true);
            await Wait(0.2f);
            var win = dir.Window;
            Check("설명 창 열림 + 1단계", GameUI.Tutorial.IsShowing && GameUI.Tutorial.StepIndex == 0 && GameUI.Tutorial.StepCount == 3);
            Check("화면 정지 + 전투 입력 차단", Mathf.Approximately(Time.timeScale, 0f) && UIState.IsGameplayInputBlocked);
            if (Camera.main != null) Check("월드 대상 밝힘", win.HasHole);
            Eq("아래 글자", win.footerText.text, "[Space] 다음   1/3");
            win.RaiseNext();
            Check("바로 연타 → 무시 (입력 보호)", GameUI.Tutorial.StepIndex == 0);
            var router = Find<UIInputRouter>();
            Check("설명 중 ESC·Tab 단축키 막힘", router != null && (bool)Call(router, "HotkeysBlocked", UIManager.Instance));

            GameUI.Tutorial.Next();
            await Wait(0.2f);
            var qr = hud.PartRoot(HudPart.Quests);
            Check("2단계: 퀘스트 목록 밝힘", GameUI.Tutorial.StepIndex == 1 && win.HasHole && qr != null);
            if (qr != null)
            {
                var want = win.FromRect(qr, 0f);
                Check("밝힌 칸이 퀘스트 목록을 덮음", want.HasValue && Contains(win.Hole, want.Value.center));
            }
            var root = (RectTransform)win.transform;
            Rect cr = new Rect(win.callout.anchoredPosition, win.callout.sizeDelta);
            Check("설명 칸이 화면 안", cr.xMin >= root.rect.xMin && cr.xMax <= root.rect.xMax && cr.yMin >= root.rect.yMin && cr.yMax <= root.rect.yMax,
                $"{cr} / {root.rect}");
            GameUI.Tutorial.Next();
            await Wait(0.1f);
            Check("3단계: 대상 없음 → 화면 전체 어둡게", GameUI.Tutorial.StepIndex == 2 && !win.HasHole);
            Eq("마지막은 '확인'", win.footerText.text, "[Space] 확인   3/3");
            GameUI.Tutorial.Next();
            await Wait(0.3f);
            Check("끝 → 콜백 + 창 닫힘 + 게임 재개", done && !win.IsOpen && Time.timeScale > 0.99f && !UIState.IsGameplayInputBlocked);
            Check("단계 이벤트 3번", changes == 3, changes.ToString());
            GameUI.Tutorial.StepChanged -= onStep;

            // ── 상태창 설명 (3장 6단계) ──
            var sw = UIManager.Instance.GetWindow(ScreenId.Status) as StatusWindow;
            Check("상태창 있음", sw != null);
            if (sw == null) return;
            int pages = 0;
            foreach (var pg in sw.pages) if (pg != null) pages++;
            bool tourDone = false;
            GameUI.Tutorial.PlayStatusTour(() => tourDone = true);
            await Wait(0.4f);
            var swGroup = sw.GetComponent<CanvasGroup>();
            Check("상태창 열림 + 설명 중엔 버튼 잠금", sw.IsOpen && swGroup != null && !swGroup.interactable);
            Check("단계 수 = 소개 + 탭 수 + 포인트", GameUI.Tutorial.StepCount == pages + 2, $"{GameUI.Tutorial.StepCount} ≠ {pages + 2}");
            GameUI.Tutorial.Next(); // 첫 탭
            GameUI.Tutorial.Next(); // 둘째 탭 (퀘스트)
            await Wait(0.3f);
            Check("탭 단계 → 그 탭으로 바뀜", sw.CurrentTab == StatusTab.Quests, sw.CurrentTab.ToString());
            if (sw.tabButtons.Length > 1 && sw.tabButtons[1] != null)
            {
                var tb = win.FromRect((RectTransform)sw.tabButtons[1].transform, 0f);
                Check("탭 버튼을 밝힘", tb.HasValue && win.HasHole && Contains(win.Hole, tb.Value.center));
            }
            while (GameUI.Tutorial.IsShowing) { GameUI.Tutorial.Next(); await Wait(0.05f); }
            await Wait(0.3f);
            Check("끝 → 콜백 · 상태창은 열린 채 상점 탭 · 버튼 다시 켜짐", tourDone && sw.IsOpen && sw.CurrentTab == StatusTab.Shop && swGroup.interactable,
                $"done {tourDone}, open {sw.IsOpen}, tab {sw.CurrentTab}, interactable {swGroup.interactable}");
            UIManager.Instance.CloseAll();
            await Wait(0.3f);
            Check("상태창 닫으면 게임 재개", Time.timeScale > 0.99f);
        }

        // ───────────────────────────── v0.7 지역 이름·저체력·화면 밖 화살표·상호작용 ─────────────────────────────
        static async Task V07()
        {
            await ToCombat();
            P.ChangeHP(9999); await Wait(0.3f);

            // 지역 이름
            var area = Hud.areaName;
            Check("지역 이름 뷰 연결 (GameUI.Area)", area != null && ReferenceEquals(GameUI.Area, area));
            var art = (RectTransform)area.transform;
            Check("위쪽 가운데 배치", Mathf.Approximately(art.anchorMin.x, 0.5f) && Mathf.Approximately(art.anchorMin.y, 1f));
            GameUI.Area.Show("테스트 지역", "부제", 1.0f); await Wait(1.0f);
            Check("Show → 부드럽게 나타남", area.IsShowing && area.group.alpha > 0.9f, $"alpha {area.group.alpha:0.00}");
            Eq("이름 표시", area.nameText.text, "테스트 지역");
            Check("부제 표시", area.subtitleText.gameObject.activeSelf && area.subtitleText.text == "부제");
            await Wait(2.2f);
            Check("유지 후 사라짐", area.group.alpha < 0.05f, $"alpha {area.group.alpha:0.00}");
            GameUI.Area.Show("테스트 지역"); await Wait(0.4f);
            Check("같은 이름 바로 다시 → 무시 (경계 왕복 깜빡임 방지)", area.group.alpha < 0.05f);
            var trig = new GameObject("SelfTest_AreaTrigger").AddComponent<AreaNameTrigger>();
            try
            {
                trig.areaName = "트리거 지역";
                trig.Enter(); await Wait(0.9f);
                Eq("AreaNameTrigger.Enter → 이름 표시", area.nameText.text, "트리거 지역");
                area.Hide();
            }
            finally { UnityEngine.Object.Destroy(trig.gameObject); }

            // 저체력 경고
            var low = Hud.lowHealth;
            Check("저체력 뷰 연결", low != null && low.vignette != null);
            Check("체력 가득 → 비네트 없음", !low.IsActive && low.CurrentAlpha < 0.01f);
            P.ChangeHP(-80); await Wait(1.2f);
            Check("체력 20% → 붉은 비네트 ON", low.IsActive && low.vignette.enabled && low.CurrentAlpha > 0.05f, $"alpha {low.CurrentAlpha:0.00}");
            P.ChangeHP(-15); await Wait(0.6f);
            Check("더 낮을수록 진해짐", low.Intensity > 0.6f, $"intensity {low.Intensity:0.00}");
            P.ChangeHP(9999); await Wait(1.5f);
            Check("회복 → 부드럽게 꺼짐", !low.IsActive && low.CurrentAlpha < 0.02f, $"alpha {low.CurrentAlpha:0.00}");

            // 체력 상태 색
            var hv = Hud.health;
            P.ChangeHP(9999); await Wait(0.5f);
            Check("체력 가득 → 기본(하늘색) 바", hv.State == 0 && hv.stateFill.sprite == hv.normalSprite);
            P.ChangeHP(-50); await Wait(0.5f);
            Check("체력 50% → 노랑 바", hv.State == 1 && hv.stateFill.sprite == hv.cautionSprite && hv.cautionSprite != null);
            P.ChangeHP(-25); await Wait(0.5f);
            Check("체력 25% → 빨강 바", hv.State == 2 && hv.stateFill.sprite == hv.dangerSprite && hv.dangerSprite != null);
            P.ChangeHP(9999); await Wait(0.5f);
            Check("회복 → 다시 하늘색", hv.State == 0 && hv.stateFill.sprite == hv.normalSprite);

            // 랭크 승급 연출
            var ru = Find<RankUpView>();
            Check("랭크 승급 연출 뷰 있음", ru != null);
            P.PromoteRank(); await Wait(0.3f);
            Check("승급 → 연출 재생 + 팡파레", ru.IsPlaying && ru.group.alpha > 0.9f && Find<UISoundPlayer>().LastPlayed == UISound.RankUp);
            await Wait(1.2f);
            Eq("새 랭크가 솟아오름", ru.newRankText.text, P.Rank.ToString());
            Check("RANK UP 글자 표시", ru.titleText.alpha > 0.9f);
            await Wait(2.5f);
            Check("약 3초 뒤 사라짐", !ru.IsPlaying && ru.group.alpha < 0.05f);

            // 회복 초록 번쩍임
            var heal = Hud.healFlash;
            P.ChangeHP(-50); await Wait(0.8f);
            int flashes = heal.FlashCount;
            P.ChangeHP(30); await Wait(0.1f);
            Check("회복 → 초록 비네트 번쩍", heal.FlashCount == flashes + 1 && heal.CurrentAlpha > 0.1f, $"alpha {heal.CurrentAlpha:0.00}");
            await Wait(0.8f);
            Check("짧게 나오고 사라짐 (0.6초 안)", heal.CurrentAlpha < 0.02f, $"alpha {heal.CurrentAlpha:0.00}");
            P.ChangeHP(-999); await Wait(0.3f); P.ChangeHP(9999); await Wait(0.2f);
            Check("0 → 가득(재도전 복구)은 번쩍 없음", heal.CurrentAlpha < 0.02f);
            await Wait(1.0f);

            // 쿨타임: 어둡게 + 도는 바늘 + 완료 점멸 (스킬·아이템)
            var qv = Hud.skills.slots[0];
            ((IGrowthShopSource)P).TryBuySkill("sword_wave"); await Wait(0.2f); // v1.3: 기술은 사야 Q에 장착됨 (검기 4초)
            P.UseSkill(SkillSlot.Q); await Wait(0.3f);
            Check("스킬 대기 중 → 아이콘 어둡게", qv.cooldown.IsDimmed && qv.icon.color.r < 0.5f, $"r {qv.icon.color.r:0.00}");
            Check("스킬 대기 중 → 도는 바늘 표시", qv.cooldown.sweepHand != null && qv.cooldown.sweepHand.gameObject.activeSelf);
            float a1 = qv.cooldown.sweepHand.localEulerAngles.z; await Wait(0.5f);
            Check("바늘이 돈다", Mathf.Abs(Mathf.DeltaAngle(a1, qv.cooldown.sweepHand.localEulerAngles.z)) > 10f);
            var iv4 = Hud.items.slots[3];
            P.AddItem(3); await Wait(1.6f);
            P.UseItem(3); await Wait(0.3f);
            Check("아이템 대기 중 → 아이콘 어둡게 + 쿨타임", iv4.cooldown.IsDimmed && iv4.cooldown.overlay.enabled);
            await Wait(1.3f);
            Check("아이템 쿨타임 끝 → 밝게 점멸", !iv4.cooldown.IsDimmed && (iv4.cooldown.IsReadyFlashing || iv4.icon.color.r > 0.9f));
            await Wait(0.5f);
            Check("점멸 후 원래 밝기", iv4.icon.color.r > 0.9f && !iv4.cooldown.IsReadyFlashing);
            await Wait(4.5f); // Q 쿨타임 끝까지
            Check("스킬 쿨타임 끝 → 원래 밝기", !qv.cooldown.IsDimmed && qv.icon.color.r > 0.9f);

            // 사용 불가도 어둡게
            var ev = Hud.skills.slots[1];
            if (P.GetEquipped(SkillSlot.E) == null) Check("빈 스킬 칸 → 어둡게", ev.icon.color.r < 0.5f, $"r {ev.icon.color.r:0.00}");
            while (P.GetCount(0) > 0) { P.UseItem(0); await Wait(1.6f); }
            await Wait(1.0f);
            Check("수량 0 아이템 → 어둡게 (쿨타임 끝나도 유지)", Hud.items.slots[0].icon.color.r < 0.5f, $"r {Hud.items.slots[0].icon.color.r:0.00}");
            P.AddItem(0); await Wait(0.3f);
            Check("다시 생기면 밝게", Hud.items.slots[0].icon.color.r > 0.9f);

            // 버프 테두리
            var edges = Hud.buffEdges;
            var atk = P.GetItem(1); var def = P.GetItem(2); var spd = P.GetItem(3);
            P.AddItem(1); P.AddItem(2); P.AddItem(3);
            edges.Shown.Clear();
            P.UseItem(3); await Wait(0.3f);
            Check("버프 1개 → 그 색 테두리", edges.ActiveCount == 1 && edges.CurrentItem == spd && edges.CurrentAlpha > 0.1f);
            P.UseItem(1); await Wait(0.1f);
            Check("새 버프 → 그 색부터 바로", edges.CurrentItem == atk);
            P.UseItem(2); await Wait(0.1f);
            Check("버프 3개 활성", edges.ActiveCount == 3, $"{edges.ActiveCount}");
            edges.Shown.Clear();
            await Wait(2.5f);
            Check("같은 자리에서 주황·파랑·초록이 번갈아 점멸", edges.Shown.Contains(atk) && edges.Shown.Contains(def) && edges.Shown.Contains(spd), $"{edges.Shown.Count}색");

            // 화면 밖 화살표
            var ind = Hud.indicators;
            var cam = Camera.main;
            var far = new GameObject("SelfTest_FarGate").transform;
            var near = new GameObject("SelfTest_NearBoss").transform;
            far.position = P.transform.position + new Vector3(-80f, 0f, 0f);
            near.position = cam.transform.position + cam.transform.forward * 10f + Vector3.down * 1f;
            try
            {
                GameUI.Indicators.Track(far, IndicatorKind.Gate);
                GameUI.Indicators.Track(near, IndicatorKind.Boss, "보스");
                await Wait(0.6f);
                Check("추적 2개", ind.Count == 2, $"Count {ind.Count}");
                Check("화면 밖 대상만 화살표", ind.VisibleCount == 1, $"Visible {ind.VisibleCount}");
                near.position = P.transform.position + new Vector3(90f, 0f, 0f); await Wait(0.6f);
                Check("화면 밖으로 나가면 화살표 생김", ind.VisibleCount == 2, $"Visible {ind.VisibleCount}");
                UnityEngine.Object.Destroy(far.gameObject); await Wait(0.3f);
                Check("대상 파괴 → 자동 해제", ind.Count == 1, $"Count {ind.Count}");
                GameUI.Indicators.Untrack(near);
                Check("Untrack → 0개", ind.Count == 0);
            }
            finally
            {
                GameUI.Indicators.Clear();
                if (far != null) UnityEngine.Object.Destroy(far.gameObject);
                if (near != null) UnityEngine.Object.Destroy(near.gameObject);
            }

            // 상호작용 안내
            var pr = Hud.prompt;
            var target = new GameObject("SelfTest_PromptTarget").transform;
            target.position = cam.transform.position + cam.transform.forward * 10f + Vector3.down * 2f;
            var link = new GameObject("SelfTest_PromptLink").AddComponent<InteractPromptLink>();
            link.transform.position = new Vector3(9999f, 9999f, 0f); // 처음엔 멀리
            try
            {
                GameUI.Prompt.Show(target, "조사"); await Wait(0.5f);
                Check("Show → 표시", pr.IsVisible);
                Eq("키 글자", pr.keyText.text, "F");
                Eq("행동 글자", pr.actionText.text, "조사");
                GameUI.Screens.Open(ScreenId.Map); await Wait(0.5f);
                Check("창이 열리면 숨김", !pr.IsVisible);
                UIManager.Instance.CloseAll(); await Wait(0.5f);
                Check("창이 닫히면 다시 표시", pr.IsVisible);
                GameUI.Prompt.Hide(link.transform); await Wait(0.3f);
                Check("다른 대상 Hide는 무시", pr.IsVisible);
                GameUI.Prompt.Hide(target); await Wait(0.5f);
                Check("Hide → 사라짐", !pr.IsVisible);

                Check("플레이어 표시(UIPlayerTag) 있음", UIPlayerTag.Current != null);
                // 플레이어 표시(UIPlayerTag)는 아바타에 있음 — 게임 로직 오브젝트(P) 자리가 아님
                Vector3 me = UIPlayerTag.Current != null ? UIPlayerTag.Current.position : P.transform.position;
                link.transform.position = me; await Wait(0.5f);
                Check("InteractPromptLink: 플레이어가 가까우면 표시", GameUI.Prompt.Current == link.transform);
                link.transform.position = me + new Vector3(50f, 0f, 0f); await Wait(0.5f);
                Check("InteractPromptLink: 멀어지면 숨김", GameUI.Prompt.Current == null);

                // F 누름
                bool fired = false;
                System.Action<Transform> onPress = t => fired = t == target;
                GameUI.Prompt.Pressed += onPress;
                GameUI.Prompt.Show(target, "조사"); await Wait(0.5f);
                int before = pr.PressCount;
                Check("보일 때 F → 눌림 처리", pr.Press() && pr.PressCount == before + 1);
                Check("F → Pressed(대상) 이벤트", fired);
                Check("연타 방지 (바로 다시 누름 무시)", !pr.Press());
                GameUI.Prompt.Pressed -= onPress;
                GameUI.Prompt.Hide(target); await Wait(0.5f);
                Check("안 보일 때 F → 무시", !pr.Press());

                int used = 0;
                link.onInteract.AddListener(() => used++);
                link.transform.position = me; await Wait(0.6f);
                pr.Press(); await Wait(0.1f);
                Check("InteractPromptLink.onInteract 호출", used == 1);
            }
            finally
            {
                GameUI.Prompt.Hide();
                UnityEngine.Object.Destroy(target.gameObject);
                UnityEngine.Object.Destroy(link.gameObject);
            }
        }

        // ───────────────────────────── v0.5 게임 흐름 ─────────────────────────────
        static async Task Flow()
        {
            await ToCombat();
            var flow = Find<FlowOverlayView>();
            Check("FlowOverlayView 있음 + GameUI.Flow 등록", flow != null && ReferenceEquals(GameUI.Flow, flow));
            if (flow == null) return;
            flow.ResetAll();

            // 로딩
            GameUI.Flow.ShowLoading("데이터 불러오는 중"); await Wait(0.35f);
            Check("로딩: 표시됨", flow.IsLoading && flow.loadingGroup.alpha > 0.99f);
            Check("로딩: 입력 차단", UIState.IsGameplayInputBlocked);
            Eq("로딩: 문구", flow.loadingMessage.text, "데이터 불러오는 중");
            Eq("로딩: 0%", flow.loadingPercent.text, "0%");
            Check("로딩: TIP 표시", flow.loadingTip.text.StartsWith("TIP"));
            GameUI.Flow.SetLoadingProgress(0.5f, "풀 준비"); await Wait(0.6f);
            Near("로딩: 진행 막대 50%", flow.loadingFill.fillAmount, 0.5f);
            Eq("로딩: 50%", flow.loadingPercent.text, "50%");
            Eq("로딩: 문구 바뀜", flow.loadingMessage.text, "풀 준비");
            GameUI.Flow.SetLoadingProgress(0.2f); await Wait(0.2f);
            Near("로딩: 진행률은 뒤로 가지 않음", flow.loadingFill.fillAmount, 0.5f);
            bool hidden = false;
            GameUI.Flow.HideLoading(() => hidden = true); await Wait(0.2f);
            Check("로딩: 숨기기 전에 100%까지 채움", flow.ShownProgress > 0.5f);
            await Wait(1.0f);
            Check("로딩: 사라짐 + 콜백", hidden && !flow.IsLoading && flow.loadingGroup.alpha < 0.01f);
            Check("로딩 끝: 입력 차단 풀림", !UIState.IsGameplayInputBlocked);

            // 페이드 + 스테이지 띠 대기열
            bool black = false;
            GameUI.Flow.FadeOut(0.2f, () => black = true); await Wait(0.08f);
            Check("페이드 중: 입력 차단", UIState.IsGameplayInputBlocked);
            await Wait(0.3f);
            Check("페이드 아웃: 검은 화면 + 콜백", black && flow.IsFaded && flow.fadeGroup.alpha > 0.99f);
            bool introDone = false;
            GameUI.Flow.StageIntro(2, "지하철역 환승통로", "게이트 3곳을 봉쇄하라", () => introDone = true); await Wait(0.3f);
            Check("검은 화면 동안 스테이지 띠는 대기", flow.introGroup.alpha < 0.01f && !flow.IsIntroPlaying);
            bool clear = false;
            GameUI.Flow.FadeIn(0.2f, () => clear = true); await Wait(0.6f);
            Check("페이드 인: 걷힘 + 콜백", clear && !flow.IsFaded);
            Check("페이드 인 끝: 입력 차단 풀림", !UIState.IsGameplayInputBlocked);
            Check("걷힌 뒤 스테이지 띠 재생", flow.IsIntroPlaying && flow.introGroup.alpha > 0.9f);
            Eq("스테이지 띠: STAGE 2", flow.introStage.text, "STAGE 2");
            Eq("스테이지 띠: 제목", flow.introTitle.text, "지하철역 환승통로");
            Check("스테이지 띠: 부제 표시", flow.introSubtitle.gameObject.activeSelf && flow.introSubtitle.text.Contains("3곳"));
            Check("스테이지 띠: 게임 입력 막지 않음", !UIState.IsGameplayInputBlocked);
            await Wait(flow.introHold + 0.8f);
            Check("스테이지 띠: 끝 + 콜백", introDone && flow.introGroup.alpha < 0.01f);

            // Transition
            var order = new List<string>();
            float alphaAtSwap = -1f;
            GameUI.Flow.Transition(() => { alphaAtSwap = flow.fadeGroup.alpha; order.Add("swap"); }, 0.2f, () => order.Add("done"));
            await Wait(0.8f);
            Check("Transition: 검은 화면일 때 교체 실행", alphaAtSwap > 0.99f, $"교체 시 알파 {alphaAtSwap:0.##}");
            Eq("Transition: 순서", string.Join(",", order), "swap,done");
            Check("Transition 끝: 화면 걷힘 + 입력 풀림", !flow.IsFaded && !UIState.IsGameplayInputBlocked);

            // 페이드 도중 다른 페이드: 앞 콜백도 빠짐없이 실행
            int calls = 0;
            GameUI.Flow.FadeOut(0.3f, () => calls++); await Wait(0.1f);
            GameUI.Flow.FadeIn(0.2f, () => calls++); await Wait(0.5f);
            Check("끊긴 페이드 콜백도 실행 (교체 작업 누락 없음)", calls == 2, $"콜백 {calls}회");

            // async
            float alphaInside = -1f;
            await GameUI.Flow.TransitionAsync(async () => { alphaInside = flow.fadeGroup.alpha; await Task.Yield(); }, 0.15f);
            Check("TransitionAsync: 안쪽은 검은 화면", alphaInside > 0.99f);
            Check("TransitionAsync: 끝나면 걷힘", !flow.IsFaded);
            flow.ResetAll();
        }

        static async Task PooledEnemyBar()
        {
            await ToCombat();
            var fx = Find<DamageFxController>();
            int baseCount = fx.TrackedEnemyCount;
            var go = new GameObject("SelfTest_PooledEnemy");
            go.SetActive(false);
            var e = go.AddComponent<ShowcaseEnemy>();
            e.trackHealthBar = false;
            e.respawn = false;
            go.AddComponent<EnemyHealthBarTracker>();
            go.transform.position = new Vector3(0f, -100f, 0f);
            try
            {
                go.SetActive(true); await Wait(0.1f);
                Check("풀에서 꺼냄 → 체력바 등록", fx.TrackedEnemyCount == baseCount + 1);
                go.SetActive(false); await Wait(0.1f);
                Check("풀에 넣음 → 체력바 해제", fx.TrackedEnemyCount == baseCount);
                go.SetActive(true); await Wait(0.1f);
                Check("다시 꺼냄 → 다시 등록 (중복 없음)", fx.TrackedEnemyCount == baseCount + 1);
                e.ResetForSpawn(new Vector3(0f, -100f, 0f));
                Near("꺼낼 때 체력 초기화", e.HP, e.MaxHP, 0.01f);

                // 트래커 없이 직접 TrackEnemy 한 적이 풀에 들어가도 체력바가 화면에 남지 않음
                go.GetComponent<EnemyHealthBarTracker>().enabled = false;
                GameUI.Damage.TrackEnemy(e);
                go.SetActive(false); await Wait(0.1f);
                var bar = Get<Dictionary<IEnemyHealthSource, EnemyHealthBarView>>(fx, "_bars");
                Check("비활성 적 체력바는 화면 밖으로", bar.TryGetValue(e, out var v) && v.Rect.anchoredPosition.x < -5000f);
                GameUI.Damage.UntrackEnemy(e);
            }
            finally
            {
                UnityEngine.Object.Destroy(go);
            }
            await Wait(0.1f);
            Check("정리 후 추적 수 원래대로", fx.TrackedEnemyCount == baseCount);
        }

        static async Task Links()
        {
            await ToCombat();
            var go = new GameObject("SelfTest_Links");
            go.SetActive(false);
            var gate = go.AddComponent<GateUILink>();
            var player = go.AddComponent<PlayerUILink>();
            var boss = go.AddComponent<BossUILink>();
            boss.SetData(Find<DummyBoss>().Data);
            var enemyGo = new GameObject("SelfTest_EnemyLink");
            enemyGo.SetActive(false);
            var enemy = enemyGo.AddComponent<EnemyUILink>();
            var fx = Find<DamageFxController>();
            int baseBars = fx.TrackedEnemyCount;
            try
            {
                go.SetActive(true); await Wait(0.2f);
                gate.BeginStage(1, 2); await Wait(0.2f);
                Eq("GateUILink.BeginStage → 퀘스트 0 / 2", QRight("gate"), "0 / 2");
                gate.GateOpened(); await Wait(0.1f);
                Check("GateUILink.GateOpened → 게이트 줄 진행 중", QRow("gate").Info.State == QuestState.Active);
                gate.GateSealed(); await Wait(0.2f);
                Eq("GateUILink.GateSealed → 1 / 2", QRight("gate"), "1 / 2");
                Check("GateUILink.GateSealed → '게이트 파괴' 띠", Hud.banner.IsShowing);
                gate.GateOpened(); gate.GateSealed(); await Wait(0.2f);
                Check("GateUILink 목표 달성 → 보스 처치 활성", QRow("boss").Info.State == QuestState.Active && gate.IsBossAreaUnlocked);
                Hud.banner.Hide();

                // v0.8: 체력은 PlayerHealth(IHealthSource)가 관리 → 같은 방식의 테스트용 소스로 확인
                var hpSource = new SelfTestHealth(); GameUI.Bind(hpSource);
                hpSource.Set(40f, 80f); await Wait(1.0f);
                Eq("IHealthSource(PlayerHealth 방식) → 40/80", Hud.health.valueText.text, "40/80");
                Check("PlayerUILink는 체력 소스가 아님 (v0.8)", !(player is IHealthSource));
                GameUI.Unbind(hpSource);
                player.SetLevel(3); player.SetExp(20f, 100f); await Wait(0.3f);
                Check("PlayerUILink.SetLevel → Lv.3", UISources.Progression == (IProgressionSource)player && player.Level == 3);

                bool intro = false;
                boss.Begin(500f, () => intro = true); await Wait(3.6f);
                Check("BossUILink.Begin → 연출 후 콜백", intro && GameUI.Boss.IsShowing);
                var bv = Find<BossHudView>();
                boss.SetHP(250f); await Wait(1.2f);
                Near("BossUILink.SetHP 250/500 → 바 0.5", bv.bar.fill.fillAmount, 0.5f);
                Check("BossUILink 페이즈 자동 (구간 지남)", boss.Phase >= 1);
                boss.Damage(9999f); await Wait(0.3f);
                Check("BossUILink 체력 0 → 격파", boss.HP <= 0f);
                await Wait(2.0f);
                boss.Hide();

                enemyGo.transform.position = new Vector3(0f, 0f, 0f);
                enemyGo.SetActive(true); await Wait(0.1f);
                Check("EnemyUILink 활성 → 체력바 등록", fx.TrackedEnemyCount == baseBars + 1);
                enemy.Init(100f);
                bool dead = enemy.Hit(30f, true);
                Check("EnemyUILink.Hit → 체력 70, 안 죽음", !dead && Mathf.Approximately(enemy.HP, 70f));
                dead = enemy.Hit(999f);
                Check("EnemyUILink.Hit 마지막 일격 → 죽음", dead && enemy.HP <= 0f);
                enemyGo.SetActive(false); await Wait(0.1f);
                Check("EnemyUILink 비활성(풀 반납) → 체력바 해제", fx.TrackedEnemyCount == baseBars);
                enemyGo.SetActive(true); enemy.Init(); await Wait(0.1f);
                Check("EnemyUILink 재사용 → 다시 등록 + 체력 가득", fx.TrackedEnemyCount == baseBars + 1 && Mathf.Approximately(enemy.HP, enemy.MaxHP));
            }
            finally
            {
                UnityEngine.Object.Destroy(enemyGo);
                UnityEngine.Object.Destroy(go);
            }
            await Wait(0.2f);
            GameUI.Bind(P); // 샌드박스 더미 플레이어로 되돌림
            P.ResetAll();
            await Wait(0.3f);
            Check("정리 후 HUD 소스 원래대로", ReferenceEquals(UISources.Health, P) && ReferenceEquals(UISources.Gate, P));
        }

        // ───────────────────────────── 스위트 ─────────────────────────────
        static Task Scale()
        {
            var L = new Vector2Int(640, 360);
            Check("1920×1080 → 3배", PixelCanvasScaler.ComputeScale(1920, 1080, L) == 3);
            Check("2560×1440 → 4배", PixelCanvasScaler.ComputeScale(2560, 1440, L) == 4);
            Check("1280×720 → 2배", PixelCanvasScaler.ComputeScale(1280, 720, L) == 2);
            Check("1366×768 → 2배", PixelCanvasScaler.ComputeScale(1366, 768, L) == 2);
            Check("3840×2160 → 6배", PixelCanvasScaler.ComputeScale(3840, 2160, L) == 6);
            Check("2560×1080(울트라와이드) → 3배", PixelCanvasScaler.ComputeScale(2560, 1080, L) == 3);
            Check("800×600 → 최소 1배", PixelCanvasScaler.ComputeScale(800, 600, L) == 1);
            return Task.CompletedTask;
        }

        static async Task Health()
        {
            await ToCombat();
            var h = Hud.health;
            Eq("시작 100/100", h.valueText.text, "100/100");
            Near("시작 체력 바 1.0", h.bar.fill.fillAmount, 1f);
            Check("초상화 칸에 기본 초상화", Hud.portrait != null && Hud.portrait.sprite != null);

            P.ChangeHP(-30); await Wait(1.0f);
            Eq("피격 -30 → 70/100", h.valueText.text, "70/100");
            Near("체력 바 0.7", h.bar.fill.fillAmount, 0.7f);
            Near("잔상(trail)도 0.7까지 내려옴", h.bar.trail.fillAmount, 0.7f);
            Check("25% 초과에선 맥박 없음", !Get<bool>(h, "_low"));

            P.ChangeHP(-50); await Wait(1.0f);
            Eq("20/100", h.valueText.text, "20/100");
            Check("25% 이하 → 맥박 ON", Get<bool>(h, "_low"));

            P.ChangeHP(-999); await Wait(1.0f);
            Eq("0 아래로 안 내려감 → 0/100", h.valueText.text, "0/100");
            Near("체력 바 0", h.bar.fill.fillAmount, 0f);
            Check("0일 때 맥박 OFF", !Get<bool>(h, "_low"));

            P.ChangeHP(40); await Wait(0.15f);
            Near("회복 시 잔상이 먼저 올라감(0.4)", h.bar.trail.fillAmount, 0.4f, 0.05f);
            await Wait(1.0f);
            Eq("회복 +40 → 40/100", h.valueText.text, "40/100");
            Near("회복 후 체력 바 0.4", h.bar.fill.fillAmount, 0.4f);

            P.ChangeHP(9999); await Wait(1.0f);
            Eq("최대 초과 회복 → 100/100", h.valueText.text, "100/100");
            Near("체력 바 1.0 복귀", h.bar.fill.fillAmount, 1f);
            GameUI.HUD.SetPortrait(null);
            Check("SetPortrait(null) → 기본 초상화", Hud.portrait.sprite == Hud.defaultPortrait);
        }

        static async Task Progression()
        {
            await ToCombat();
            var v = Hud.progression;
            Check("HUD에 레벨·경험치 없음 (기획서 4장: 레벨·경험치 시스템 없음)", v.levelText == null && v.expBar == null);
            Check("HUD에 SP 표시 없음", v.pointsHint == null && v.pointsText == null);
            int points = ((IPointsSource)P).Points;
            Eq("랭크 F급", v.rankText.text, "F급");
            P.PromoteRank(); await Wait(0.3f);
            Eq("승급 → E급 (1스테이지 보스)", v.rankText.text, "E급");
            P.PromoteRank(); await Wait(0.3f);
            Eq("승급 → D급 (2스테이지 보스)", v.rankText.text, "D급");
            Check("랭크는 기록일 뿐 — 포인트·스탯 변화 없음 (8장)", ((IPointsSource)P).Points == points && P.GetStatLevel(StatKind.Strength) == 0);
        }

        static async Task Skills()
        {
            await ToCombat();
            var bar = Hud.skills;
            SkillSlotView q = bar.slots[0], e = bar.slots[1], r = bar.slots[2];
            var lo = (ISkillLoadoutSource)P;
            var shop = (IGrowthShopSource)P;
            int Pts() => ((IPointsSource)P).Points;

            // 기획데이터 1·5장: 각성 1000P, 시작 기술 없음, 기술 6종
            Check("각성 포인트 1000", Pts() == 1000, "P " + Pts());
            Check("시작 기술 없음 → Q/E/R 빈 칸 (잠금 표시)", q.lockOverlay.activeSelf && e.lockOverlay.activeSelf && r.lockOverlay.activeSelf);
            int phys = 0, magic = 0;
            for (int i = 0; i < lo.SkillCount; i++) { if (lo.GetSkill(i).damageKind == SkillDamageKind.Magic) magic++; else phys++; }
            Check("기술 6종 (물리 3 · 마법 3)", lo.SkillCount == 6 && phys == 3 && magic == 3, $"{lo.SkillCount} ({phys}/{magic})");
            SkillOffer O(string id) { for (int i = 0; i < lo.SkillCount; i++) if (lo.GetSkill(i).id == id) return lo.GetSkill(i); return default; }
            Check("가격 500 / 600 / 800", O("sword_wave").price == 500 && O("spin_slash").price == 600 && O("combo_slash").price == 800 && O("mana_bolt").price == 500 && O("chain_blast").price == 800);
            Check("기본 쿨다운 4·7·12 / 5·9·13초", Mathf.Approximately(O("sword_wave").cooldown, 4f) && Mathf.Approximately(O("combo_slash").cooldown, 12f) && Mathf.Approximately(O("chain_blast").cooldown, 13f));
            Check("강화 단계 없음 (1회 구매)", O("sword_wave").MaxRank == 1 && O("mana_field").MaxRank == 1);
            Check("총피해 = 공격력 20 × 계수: 검기 36 · 마력장 80 · 연속 참격 75", P.GetDamage("sword_wave") == 36 && P.GetDamage("mana_field") == 80 && P.GetDamage("combo_slash") == 75);

            int fails = 0; SkillUseFailReason last = default;
            Action<SkillSlot, SkillUseFailReason> onFail = (sl, why) => { fails++; last = why; };
            P.SkillUseFailed += onFail;
            try
            {
                P.UseSkill(SkillSlot.Q); await Wait(0.05f);
                Check("빈 Q 사용 → 실패(NotLearned)", fails == 1 && last == SkillUseFailReason.NotLearned);

                Check("검기 구매", shop.TryBuySkill("sword_wave") == ShopBuyResult.Success); await Wait(0.2f);
                Check("1000 → 500P", Pts() == 500, "P " + Pts());
                Check("처음 산 기술은 빈 칸(Q)에 바로 장착", lo.GetEquipped(SkillSlot.Q) == "sword_wave" && !q.lockOverlay.activeSelf);
                Check("HUD Q 아이콘", q.icon.enabled && q.icon.sprite != null);
                Eq("강화 단계 점 없음", q.rankText.text, "");
                Check("같은 기술 다시 구매 불가 (영구 보유)", shop.TryBuySkill("sword_wave") == ShopBuyResult.LimitReached && Pts() == 500);

                P.UseSkill(SkillSlot.Q); await Wait(0.1f);
                Check("Q 사용 → 쿨타임 오버레이", q.cooldown.overlay.enabled);
                Near("오버레이 거의 가득", q.cooldown.overlay.fillAmount, 1f, 0.05f);
                Eq("남은 초 4 (검기 4초)", q.cooldown.label.text, "4");
                await Wait(1.1f);
                Eq("1초 뒤 3", q.cooldown.label.text, "3");
                P.UseSkill(SkillSlot.Q); await Wait(0.05f);
                Check("쿨타임 중 재사용 → 실패(Cooldown)", fails == 2 && last == SkillUseFailReason.Cooldown);

                // 물리·마법 섞어 장착 (6장 기본안: 직업 선택 없음)
                Check("마력탄 구매 → 빈 칸(E)에 장착", shop.TryBuySkill("mana_bolt") == ShopBuyResult.Success && lo.GetEquipped(SkillSlot.E) == "mana_bolt"); await Wait(0.2f);
                Check("물리(Q) + 마법(E) 함께 장착", O(lo.GetEquipped(SkillSlot.Q)).damageKind == SkillDamageKind.Physical && O(lo.GetEquipped(SkillSlot.E)).damageKind == SkillDamageKind.Magic);
                Check("포인트 0 → 회전 베기(600) 구매 거부", shop.TryBuySkill("spin_slash") == ShopBuyResult.NotEnoughCurrency && !lo.IsOwned("spin_slash") && Pts() == 0);

                // 같은 기술은 한 칸만 · 장착을 바꿔도 남은 쿨다운 유지 (5·6장)
                lo.TryEquip(SkillSlot.R, "sword_wave"); await Wait(0.2f);
                Check("같은 기술은 한 칸만 (Q → R로 옮겨짐)", lo.GetEquipped(SkillSlot.Q) == null && lo.GetEquipped(SkillSlot.R) == "sword_wave");
                Check("장착을 옮겨도 남은 쿨다운 유지", lo.GetCooldownRemaining(SkillSlot.R) > 1.5f && r.cooldown.overlay.enabled, $"{lo.GetCooldownRemaining(SkillSlot.R):0.0}s");
                Check("빈 Q 다시 잠금 표시", q.lockOverlay.activeSelf);
            }
            finally { P.SkillUseFailed -= onFail; }

            // 스탯 (기획데이터 4장)
            P.AddPoints(2000); await Wait(0.1f);
            Check("힘 첫 가격 200", P.GetStatPrice(StatKind.Strength) == 200);
            Check("힘 구매", P.TryBuyStat(StatKind.Strength) == ShopBuyResult.Success);
            Check("물리 공격력 20 → 23, 다음 가격 250", Mathf.Approximately(P.GetStatValue(StatKind.Strength), 23f) && P.GetStatPrice(StatKind.Strength) == 250);
            Check("검기 피해 36 → 41 (23 × 1.8 반올림)", P.GetDamage("sword_wave") == 41, "피해 " + P.GetDamage("sword_wave"));
            Check("마법 피해는 그대로 (마력탄 33)", P.GetDamage("mana_bolt") == 33);
            P.ChangeHP(-30); await Wait(0.2f);
            Check("체력 구매", P.TryBuyStat(StatKind.Vitality) == ShopBuyResult.Success); await Wait(0.5f);
            Eq("최대 HP +12, 현재 HP는 늘어난 만큼만 (70/100 → 82/112)", Hud.health.valueText.text, "82/112");
            for (int i = 0; i < 3; i++) P.TryBuyStat(StatKind.Agility);
            Check("민첩 상한 3회 → 더 못 삼", P.TryBuyStat(StatKind.Agility) == ShopBuyResult.LimitReached && P.GetStatLevel(StatKind.Agility) == 3);
            Near("민첩 추가 효과 +9%", P.GetStatValue(StatKind.Agility), 0.09f, 0.001f);
            Check("남은 포인트 2000 − 200 − 200 − 750 = 850", Pts() == 850, "P " + Pts());

            // 사망·재도전에도 구매·장착 유지 (8장)
            P.ResetAll(); await Wait(0.3f);
            Check("재도전(ResetAll) 후에도 기술·장착·포인트·스탯 유지", lo.IsOwned("sword_wave") && lo.GetEquipped(SkillSlot.R) == "sword_wave" && Pts() == 850 && P.GetStatLevel(StatKind.Strength) == 1);

            // K → 상태창 캐릭터 탭 (스킬 트리 창 대신)
            var router = Find<UIInputRouter>();
            Call(router, "ToggleStatus", (StatusTab?)StatusTab.Character); await Wait(0.4f);
            var sw = UIManager.Instance.GetWindow(ScreenId.Status) as StatusWindow;
            Check("K → 상태창 캐릭터 탭 + 시간 정지", sw != null && sw.IsOpen && sw.CurrentTab == StatusTab.Character && Time.timeScale == 0f, sw != null ? $"{sw.IsOpen} {sw.CurrentTab} ts {Time.timeScale}" : "상태창 없음");
            UIManager.Instance.CloseAll(); await Wait(0.3f);
            Check("상태창 닫힘 → 시간 다시 흐름", Time.timeScale == 1f);
        }

        static async Task Items()
        {
            await ToCombat();
            var items = Hud.items;
            Eq("1번 회복제 3", items.slots[0].countText.text, "3");
            Eq("2번 공격 1", items.slots[1].countText.text, "1");

            int fails = 0; Action<int> onFail = i => fails++;
            P.ItemUseFailed += onFail;

            P.UseItem(1); await Wait(0.2f);
            Eq("2번 사용 → 0", items.slots[1].countText.text, "0");
            Check("버프 아이콘 1개", BuffCount() == 1, "개수 " + BuffCount());
            P.UseItem(1); await Wait(0.1f);
            Check("수량 0 사용 → 실패 이벤트", fails == 1);

            P.AddItem(1); P.AddItem(1); await Wait(0.1f);
            Eq("획득 +2 → 2", items.slots[1].countText.text, "2");
            await Wait(2f);
            P.UseItem(1); await Wait(0.2f);
            Check("같은 버프 재사용 → 아이콘 중첩 없이 1개", BuffCount() == 1, "개수 " + BuffCount());
            var view = FirstBuff();
            float remain = view != null ? Get<float>(view.timer, "_remaining") : -1f;
            Check("버프 시간 갱신(10초 근처로 리셋)", remain > 9.3f, "남은 " + remain.ToString("0.0"));

            P.UseItem(2); await Wait(0.2f);
            Check("다른 버프 추가 → 아이콘 2개", BuffCount() == 2, "개수 " + BuffCount());

            P.ChangeHP(-50); await Wait(0.2f);
            P.UseItem(0); await Wait(1.0f);
            Eq("회복제(30%) → 50→80", Hud.health.valueText.text, "80/100");
            Eq("회복제 3→2", items.slots[0].countText.text, "2");
            await Wait(0.7f); // v0.7 샘플 아이템 쿨타임 1.5초
            P.UseItem(0); await Wait(1.0f);
            Eq("회복은 최대 체력 초과 안 함", Hud.health.valueText.text, "100/100");
            P.ItemUseFailed -= onFail;
            for (int i = 0; i < 8; i++) P.AddItem(2);
            await Wait(0.2f);
            Eq("종류별 최대 5개 (기획데이터 9장)", items.slots[2].countText.text, "5");
            P.ResetAll(); await Wait(0.2f);
        }

        static int BuffCount()
        {
            var dict = Get<IDictionary>(Hud.buffs, "_active");
            return dict?.Count ?? -1;
        }

        static BuffIconView FirstBuff()
        {
            var dict = Get<IDictionary>(Hud.buffs, "_active");
            if (dict == null) return null;
            foreach (DictionaryEntry kv in dict) return kv.Value as BuffIconView;
            return null;
        }

        static bool HasAlarm(string kind, string main = null)
        {
            foreach (var c in Hud.systemAlarm.Cards)
                if (c != null && c.kind.text == kind && (main == null || c.main.text == main)) return true;
            return false;
        }

        static int ToastCount() => Get<IList>(Find<ToastView>(), "_live")?.Count ?? 0;

        static string ToastTexts()
        {
            var sb = new StringBuilder();
            var list = Get<IList>(Find<ToastView>(), "_live");
            if (list != null) foreach (RectTransform rt in list) if (rt != null) sb.Append(rt.GetComponentInChildren<TMPro.TMP_Text>().text).Append('|');
            return sb.ToString();
        }

        static QuestRowView QRow(string id)
        {
            foreach (var r in Hud.quests.Rows) if (r.Id == id) return r;
            return null;
        }

        static string QRight(string id) { var r = QRow(id); return r != null ? r.right.text : "(없음)"; }

        static async Task Gate()
        {
            await ToCombat();
            Check("게이트 패널 대신 퀘스트 목록 (v0.6)", Hud.quests != null && Hud.gate == null);
            var g = QRow(QuestListView.GateId);
            Check("퀘스트: '게이트 봉쇄' 줄", g != null && g.title.text == "게이트 봉쇄");
            Check("퀘스트 제목이 실제로 그려짐 (글자 수 > 0)", g != null && g.title.textInfo.characterCount > 0 && g.title.textInfo.lineInfo[0].visibleCharacterCount > 0);
            Eq("시작 0 / 3", QRight("gate"), "0 / 3");
            var b = QRow(QuestListView.BossId);
            Check("퀘스트: '보스 처치' 잠김 + '게이트 후'", b != null && b.Info.State == QuestState.Locked && b.right.text == "게이트 후");
            Check("목록 순서: 게이트 → 보스", Hud.quests.Rows.Count >= 2 && Hud.quests.Rows[0].Id == "gate" && Hud.quests.Rows[1].Id == "boss");
            var banner = Hud.banner;
            P.SealGate(); await Wait(0.4f);
            Eq("봉쇄 → 1 / 3", QRight("gate"), "1 / 3");
            Near("진행 막대 1/3", QRow("gate").barFill.fillAmount, 1f / 3f, 0.03f);
            Check("봉쇄 → '게이트 파괴' 띠 표시", banner.IsShowing && banner.group.alpha > 0.5f);
            Eq("띠 제목", banner.titleText.text, "게이트 파괴");
            Eq("띠 부제", banner.subtitleText.text, "1 / 3");
            await Wait(banner.fadeIn + banner.gateHold + banner.fadeOut + 0.3f);
            Check("일정 시간 뒤 띠 사라짐", !banner.IsShowing && banner.group.alpha < 0.01f);
            P.SealGate(); await Wait(0.1f);
            Eq("비활성 상태 봉쇄 무시(중복 반영 X)", QRight("gate"), "1 / 3");
            Hud.systemAlarm.ClearAll();
            P.OpenGate(); P.SealGate(); await Wait(0.3f);
            Check("2/3 에서는 보스 알림 없음", !HasAlarm("보스 구역"));
            P.OpenGate(); P.SealGate(); await Wait(0.3f);
            Check("3/3 → SYSTEM '보스 구역 개방' 알림 (v0.9)", HasAlarm("보스 구역", "보스 구역 개방"));
            Check("3/3 → 게이트 줄 완료(체크)", QRow("gate").Info.State == QuestState.Done && QRow("gate").check.gameObject.activeSelf);
            Check("보스 처치 줄 활성", QRow("boss").Info.State == QuestState.Active);
            Eq("마지막 게이트 → 띠 부제에 보스 구역 개방", banner.subtitleText.text, "3 / 3  ·  보스 구역 개방");
            P.OpenGate(); P.SealGate(); await Wait(0.1f);
            Eq("목표 달성 후 추가 봉쇄 없음", QRight("gate"), "3 / 3");
            await Wait(Hud.quests.doneLinger + 0.6f);
            Check("완료 줄은 잠시 뒤 목록에서 빠짐", QRow("gate") == null && QRow("boss") != null);
            P.ResetAll(); await Wait(0.3f);
            Eq("재도전 → 0 / 3 복구", QRight("gate"), "0 / 3");
            Check("보스 처치 다시 잠김", QRow("boss").Info.State == QuestState.Locked);

            // 팀원 퀘스트 API
            GameUI.Quest.Set("kill", "감염체 처치", 0, 5, order: 5); await Wait(0.3f);
            Eq("Quest.Set → 0 / 5", QRight("kill"), "0 / 5");
            GameUI.Quest.SetProgress("kill", 5); await Wait(0.2f);
            Check("목표 도달 → 자동 완료", QRow("kill").Info.State == QuestState.Done);
            GameUI.Quest.Set("talk", "역무원과 대화"); await Wait(0.2f);
            Eq("진행 숫자 없는 줄", QRight("talk"), "");
            GameUI.Quest.Set("a", "A"); GameUI.Quest.Set("b", "B"); GameUI.Quest.Set("c", "C"); await Wait(0.3f);
            int visible = 0; foreach (var r in Hud.quests.Rows) if (r.gameObject.activeSelf) visible++;
            Check("최대 4줄까지만 표시", visible == Hud.quests.maxRows, "보이는 줄 " + visible);
            foreach (var id in new[] { "kill", "talk", "a", "b", "c" }) GameUI.Quest.Remove(id);
            await Wait(0.3f);
            Check("Remove → 게이트·보스·서브(근접형 처치) 3줄만 남음", Hud.quests.Rows.Count == 3 && QRow("gate") != null && QRow("boss") != null && QRow("sub_melee") != null,
                "줄 " + Hud.quests.Rows.Count);
        }

        static async Task HudLayout()
        {
            await ToCombat();
            var canvas = Hud.GetComponentInParent<Canvas>().rootCanvas;
            var crt = (RectTransform)canvas.transform;
            float W = crt.rect.width;
            float X(RectTransform rt) => crt.InverseTransformPoint(rt.TransformPoint(rt.rect.center)).x + W * 0.5f;
            Check("스킬 Q/E/R은 화면 왼쪽 (v0.7 자리 바꿈)", X((RectTransform)Hud.skills.slots[2].transform) < W * 0.35f, "R x " + X((RectTransform)Hud.skills.slots[2].transform));
            Check("아이템 1~4는 화면 오른쪽", X((RectTransform)Hud.items.slots[0].transform) > W * 0.6f, "1 x " + X((RectTransform)Hud.items.slots[0].transform));
            Check("스킬·아이템 칸 36px 유지 (아이콘 2배 픽셀)", Mathf.Approximately(((RectTransform)Hud.items.slots[0].transform).rect.width, 36f));
            var mm = Hud.transform.Find("Minimap") as RectTransform;
            var sa = Hud.systemAlarm.GetComponent<RectTransform>();
            Check("시스템 알림 폭 = 지도 폭", mm != null && Mathf.Approximately(mm.rect.width, sa.rect.width));
            Check("시스템 알림은 지도 바로 아래", mm != null && Mathf.Abs((mm.anchoredPosition.y - mm.rect.height) - sa.anchoredPosition.y) <= 6f);
            var boss = Find<BossHudView>();
            Check("보스 바 맨 위 (y ≥ -12)", boss.barRoot.anchoredPosition.y >= -12f, "y " + boss.barRoot.anchoredPosition.y);
        }

        static async Task Sounds()
        {
            await ToCombat();
            var sp = Find<UISoundPlayer>();
            Check("UISoundPlayer 있음 + GameUI.Sound 등록", sp != null && ReferenceEquals(GameUI.Sound, sp));
            if (sp == null) return;
            int missing = 0;
            foreach (UISound u in Enum.GetValues(typeof(UISound)))
            {
                if (u == UISound.None) continue;
                var e = sp.soundSet != null ? sp.soundSet.Find(u) : null;
                if (e == null || e.clips == null || e.clips.Length == 0 || e.clips[0] == null) { missing++; Log("    소리 없음: " + u); }
            }
            Check("모든 UISound에 소리 배정", missing == 0, missing + "개 비어 있음");
            int sel = 0, withSound = 0;
            foreach (var s in UIManager.Instance.GetComponentsInChildren<UnityEngine.UI.Selectable>(true)) { sel++; if (s.GetComponent<UISelectableSound>() != null) withSound++; }
            Check("모든 버튼·토글·슬라이더에 소리 컴포넌트", sel > 0 && sel == withSound, $"{withSound}/{sel}");

            async Task Expect(string name, UISound want, Action act)
            {
                int n = sp.PlayCount;
                act();
                await Wait(0.3f);
                Check(name, sp.PlayCount > n && sp.LastPlayed == want, $"마지막 소리 {sp.LastPlayed}");
            }
            await Expect("지도 열기 → Open", UISound.Open, () => GameUI.Screens.Open(ScreenId.Map));
            await Expect("닫기 → Close", UISound.Close, () => GameUI.Screens.Close(ScreenId.Map));
            await Expect("토스트 → Toast", UISound.Toast, () => GameUI.Notify.Toast("테스트"));
            await Expect("경고 토스트 → ToastWarning", UISound.ToastWarning, () => GameUI.Notify.Toast("경고", ToastType.Warning));
            await Expect("시스템 알림 → SystemAlarm", UISound.SystemAlarm, () => GameUI.Notify.System("테스트", "알림"));
            GameUI.Quest.Set("snd", "소리 테스트", 0, 3); await Wait(0.2f);
            await Expect("퀘스트 진행 → QuestUpdate", UISound.QuestUpdate, () => GameUI.Quest.SetProgress("snd", 1));
            await Expect("퀘스트 완료 → QuestComplete", UISound.QuestComplete, () => GameUI.Quest.SetProgress("snd", 3));
            GameUI.Quest.Remove("snd");
            await Expect("게이트 파괴 띠 → GateSealed", UISound.GateSealed, () => GameUI.HUD.ShowBanner("게이트 파괴", "1 / 3", 0.2f));
            var btn = Find<PauseWindow>();
            GameUI.Screens.Open(ScreenId.Pause); await Wait(0.3f);
            var resume = btn != null ? btn.resumeButton : null;
            if (resume != null)
            {
                int n = sp.PlayCount;
                UnityEngine.EventSystems.ExecuteEvents.Execute(resume.gameObject, new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { button = UnityEngine.EventSystems.PointerEventData.InputButton.Left }, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
                await Wait(0.3f);
                Check("버튼 클릭 → 소리 (계속하기 = Confirm 후 창 닫힘)", sp.PlayCount > n);
            }
            UIManager.Instance.CloseAll();
            float vol = UISettings.SfxVolume;
            UISettings.SfxVolume = 0f;
            int c0 = sp.PlayCount; GameUI.Sound.Play(UISound.Confirm);
            Check("SFX 볼륨 0이어도 에러 없음", sp.PlayCount == c0 + 1);
            UISettings.SfxVolume = vol;
            await Wait(0.3f);
        }

        static async Task SystemAlarm()
        {
            await ToCombat();
            var sa = Hud.systemAlarm;
            sa.ClearAll();
            int Pts() => ((IPointsSource)P).Points;
            int before = Pts();
            // 기획서 5장: 포인트는 퀘스트 완료 보상으로만 · 첫 완료에만 지급
            GameUI.Quest.SetProgress("sub_melee", 6); await Wait(0.5f);
            Check("서브 퀘스트 완료 → 알림 1장", sa.VisibleCount == 1, "장 수 " + sa.VisibleCount);
            if (sa.Cards.Count > 0)
            {
                var card = sa.Cards[0];
                Check("'상태창' + '… 완료' 형식", card.kind.text == "상태창" && card.main.text.Contains("완료"), card.kind.text + " / " + card.main.text);
                Eq("보상 표시", card.sub.text, "+150 포인트");
                Near("카드가 마스크 안 첫 자리(y 0)", card.rt.anchoredPosition.y, 0f, 1f);
            }
            Check("보상 150P 지급", Pts() == before + 150, $"{before} → {Pts()}");
            GameUI.Quest.Set("sub_melee", "근접형 처치 (1회차)", 0, 6, QuestState.Active, null, 10); await Wait(0.2f);
            GameUI.Quest.SetProgress("sub_melee", 6); await Wait(0.3f);
            Check("같은 퀘스트 다시 완료 → 다시 지급 안 함", Pts() == before + 150, $"{before} → {Pts()}");

            GameUI.Notify.System("상태창", "합치기 1", null, "selftest_merge"); await Wait(0.2f);
            GameUI.Notify.System("상태창", "합치기 2", null, "selftest_merge"); await Wait(0.3f);
            Check("같은 mergeKey → 새로 안 쌓고 내용만 바뀜", sa.VisibleCount == 2 && HasAlarm("상태창", "합치기 2") && !HasAlarm("상태창", "합치기 1"), "장 수 " + sa.VisibleCount);
            GameUI.Notify.System("방송", "주변 시민은 가까운 대피소로 이동해 주십시오.", null); await Wait(0.4f);
            Check("다른 알림은 아래로 쌓임 (3장)", sa.VisibleCount == 3);
            Near("두 번째 카드 위치", sa.Cards[1].rt.anchoredPosition.y, -(sa.cardHeight + sa.gap), 1f);
            GameUI.Notify.System("테스트", "4번째", null); await Wait(0.6f);
            Check("최대 3장 유지", sa.VisibleCount <= 3);
            await Wait(sa.holdSeconds * 3 + 2f);
            Check("시간이 지나면 모두 지도 뒤로 사라짐", sa.VisibleCount == 0);
            Check("토스트(우하단)엔 상태창 알림이 안 쌓임", !ToastTexts().Contains("완료"));
            GameUI.Quest.Set("sub_melee", "근접형 처치 (1회차)", 0, 6, QuestState.Active, null, 10);
            P.ResetAll(); await Wait(0.3f);
        }

        static async Task Boss()
        {
            await ToCombat();
            var b = Find<BossHudView>();
            var boss = Find<DummyBoss>();
            bool introDone = false;
            boss.StartFight();
            // StartFight 안에서 Show 호출 — 콜백 확인용으로 한 번 더 감시
            await Wait(0.5f);
            Check("등장 연출 중 레터박스 펼침", b.letterboxTop.sizeDelta.y > 10f, "높이 " + b.letterboxTop.sizeDelta.y);
            b.Show(boss, () => introDone = true);
            await Wait(3.2f);
            Check("연출 종료 콜백 호출", introDone);
            Near("바 0→100% 채움", b.bar.fill.fillAmount, 1f);
            Near("레터박스 접힘", b.letterboxTop.sizeDelta.y, 0f, 0.5f);
            Check("이름 표시", b.barNameText.text == boss.Data.displayName);

            int phase = -1; Action<int> onPhase = p => phase = p;
            boss.PhaseChanged += onPhase;
            boss.Damage(boss.MaxHP * 0.3f); await Wait(1.2f);
            Near("30% 피해 → 0.7", b.bar.fill.fillAmount, 0.7f);
            Check("페이즈 유지(0)", boss.Phase == 0);
            boss.Damage(boss.MaxHP * 0.25f); await Wait(1.2f);
            Near("45% 남음 → 0.45", b.bar.fill.fillAmount, 0.45f);
            Check("50% 이하 → 페이즈 1 이벤트", phase == 1);
            boss.PhaseChanged -= onPhase;

            boss.Damage(boss.MaxHP); await Wait(0.3f);
            Eq("처치 → 격파 배너", b.bannerName.text, "격파");
            Check("처치 후 IsShowing=false", !b.IsShowing);
            await Wait(1.5f);
            Check("체력바 페이드아웃", b.barGroup.alpha < 0.05f, "alpha " + b.barGroup.alpha.ToString("0.00"));
        }

        static async Task Map()
        {
            await ToCombat();
            var mc = Find<MapController>();
            var map = Find<SandboxDirector>().map;
            var mini = mc.renderers[0];
            GameUI.Map.SetMap(map); await Wait(0.1f);
            Check("시작 시 시작 방만 보임", RoomCount(mini) == 1, "방 " + RoomCount(mini));
            GameUI.Map.SetPlayerRoom("S1_01");
            GameUI.Map.SetPlayerRoom("S1_02"); await Wait(0.3f);
            Check("방문 → 2개", RoomCount(mini) == 2);
            Check("현재 방 저장", mc.State.CurrentRoom == "S1_02" && mc.State.IsVisited("S1_01"));
            GameUI.Map.RevealRoom("S1_09"); await Wait(0.1f);
            Check("RevealRoom → 3개", RoomCount(mini) == 3);
            GameUI.Map.SetPlayerRoom("없는방"); await Wait(0.1f);
            Check("없는 방 → 무시(현재 방 유지)", mc.State.CurrentRoom == "S1_02");
            GameUI.Map.SetMap(map); await Wait(0.1f);
            Check("SetMap → 발견 상태 초기화", RoomCount(mini) == 1);
        }

        static int RoomCount(MapRenderer r)
        {
            var d = Get<IDictionary>(r, "_rooms");
            return d?.Count ?? -1;
        }

        static async Task Dialogue()
        {
            await ToCombat();
            var dv = Find<DialogueView>();
            var data = Find<SandboxDirector>().introDialogue;
            bool done = false;
            GameUI.Dialogue.Play(data, () => done = true); await Wait(0.15f);
            Check("재생 중", dv.IsPlaying);
            Check("입력 차단", UIState.IsGameplayInputBlocked);
            Eq("첫 화자", dv.nameText.text, data.lines[0].speaker.displayName);
            dv.bodyText.ForceMeshUpdate();
            Check("타이핑 진행 중(일부만 보임)", dv.bodyText.maxVisibleCharacters < dv.bodyText.textInfo.characterCount);
            Call(dv, "Advance"); await Wait(0.05f);
            Check("진행 키 → 즉시 완성", dv.bodyText.maxVisibleCharacters >= dv.bodyText.textInfo.characterCount);
            Check("오른쪽 초상화 표시", dv.rightPortrait.enabled);
            Call(dv, "Advance"); await Wait(0.3f);
            Eq("2번째 줄 화자", dv.nameText.text, data.lines[1].speaker.displayName);
            Check("왼쪽 초상화 등장", dv.leftPortrait.enabled);
            Check("듣는 쪽(오른쪽) 어둡게", dv.rightPortrait.color.r < 0.8f, "r " + dv.rightPortrait.color.r.ToString("0.00"));
            for (int i = 0; i < 8 && dv.IsPlaying; i++) { Call(dv, "Advance"); await Wait(0.1f); }
            Check("끝까지 → 종료", !dv.IsPlaying);
            Check("종료 콜백 호출", done);
            await Wait(0.3f);
            Check("입력 차단 해제", !UIState.IsGameplayInputBlocked);
        }

        static async Task Screens()
        {
            await ToCombat();
            var ui = UIManager.Instance;
            Check("전투 중 입력 차단 없음", !UIState.IsGameplayInputBlocked);

            ui.Open(ScreenId.SkillWindow); await Wait(0.1f);
            Check("스킬 창 → 입력 차단", UIState.IsGameplayInputBlocked);
            Check("스킬 창 → 시간 정지", Time.timeScale == 0f && UIState.IsPausedByUI);
            var router = Find<UIInputRouter>();
            Call(router, "ToggleMenu", ScreenId.Map); await Wait(0.3f);
            Check("메뉴 전환: 지도 열면 스킬 창 닫힘", ui.IsOpen(ScreenId.Map) && !ui.IsOpen(ScreenId.SkillWindow));
            Call(router, "ToggleMenu", ScreenId.Map); await Wait(0.3f);
            Check("같은 키 → 닫힘", !ui.IsOpen(ScreenId.Map));
            Check("시간 복구", Time.timeScale == 1f && !UIState.IsPausedByUI);

            Check("Back(): 열린 창 없으면 false", !ui.Back());
            ui.Open(ScreenId.Pause); await Wait(0.1f);
            Check("Back(): 일시정지 닫힘", ui.Back() && !ui.IsOpen(ScreenId.Pause));

            ui.Open(ScreenId.Death); await Wait(0.1f);
            Check("사망 화면 → Back()으로 안 닫힘", !ui.Back() && ui.IsOpen(ScreenId.Death));
            bool retried = false; Action onRetry = () => retried = true;
            UIRequests.Retry += onRetry;
            Find<DeathScreen>().retryButton.onClick.Invoke(); await Wait(0.3f);
            UIRequests.Retry -= onRetry;
            Check("재도전 버튼 → UIRequests.Retry", retried);
            Check("사망 화면 닫힘", !ui.IsOpen(ScreenId.Death));

            ui.Open(ScreenId.Title); await Wait(0.1f);
            Call(router, "ToggleMenu", ScreenId.SkillWindow); await Wait(0.1f);
            Check("타이틀 중엔 메뉴 단축키 무시", !ui.IsOpen(ScreenId.SkillWindow));
            ui.CloseAll(); await Wait(0.3f);
            Check("CloseAll → 입력 차단 해제", !UIState.IsGameplayInputBlocked && Time.timeScale == 1f);

            bool next = false; Action onNext = () => next = true;
            UIRequests.NextStage += onNext;
            GameUI.Screens.ShowStageClear(1, HunterRank.E); await Wait(0.2f);
            var bn = Hud.banner;
            Eq("클리어 → '게이트 파괴' 띠", bn.titleText.text, "게이트 파괴");
            Eq("클리어 띠 부제", bn.subtitleText.text, "STAGE 1 클리어  ·  헌터 랭크 E급");
            Check("클리어해도 게임 안 멈춤", Time.timeScale == 1f && !UIState.IsGameplayInputBlocked);
            Check("띠가 떠 있는 동안 NextStage 아직 안 보냄", !next);
            await Wait(bn.fadeIn + 2.5f + bn.fadeOut + 0.3f);
            UIRequests.NextStage -= onNext;
            Check("띠 사라진 뒤 UIRequests.NextStage", next);
            ui.CloseAll();
        }

        static async Task Options()
        {
            var ui = UIManager.Instance;
            var router = Find<UIInputRouter>();
            ui.CloseAll(); await Wait(0.2f);
            float m0 = UISettings.MasterVolume; bool s0 = UISettings.ScreenShake; var r0 = UISettings.Resolution;

            ui.Open(ScreenId.Title); await Wait(0.2f);
            Find<TitleScreen>().settingsButton.onClick.Invoke(); await Wait(0.3f);
            Check("타이틀 '설정' → 옵션 창", ui.IsOpen(ScreenId.Options));
            Check("타이틀은 뒤에 그대로", ui.IsOpen(ScreenId.Title));
            var o = Find<OptionsWindow>();
            Check("첫 탭 = 사운드", o.CurrentTab == 0 && o.pages[0].activeSelf && !o.pages[1].activeSelf);

            o.master.slider.value = 0.35f; await Wait(0.05f);
            Near("마스터 볼륨 → UISettings", UISettings.MasterVolume, 0.35f);
            Near("AudioListener 볼륨 바로 적용", AudioListener.volume, 0.35f);
            Eq("퍼센트 표시", o.master.valueText.text, "35");
            o.bgm.slider.value = 0.52f; await Wait(0.05f);
            Near("5% 단위로 맞춤 (0.52 → 0.5)", UISettings.BgmVolume, 0.5f);

            o.ShowTab(2); await Wait(0.05f);
            Check("탭 전환 → 게임", o.CurrentTab == 2 && o.pages[2].activeSelf && !o.pages[0].activeSelf);
            o.ShowTab(3); await Wait(0.05f);
            Check("마지막 탭 다음 → 첫 탭으로 순환", o.CurrentTab == 0);
            o.screenShake.toggle.isOn = !s0; await Wait(0.05f);
            Check("화면 흔들림 토글 → UISettings", UISettings.ScreenShake == !s0);
            Eq("켜짐/꺼짐 글자", o.screenShake.stateText.text, !s0 ? "켜짐" : "꺼짐");

            if (o.resolution.Count > 1)
            {
                int before = o.resolution.Index;
                o.resolution.Step(1); await Wait(0.05f);
                Check("해상도 ▶ → 다음 항목", o.resolution.Index != before);
                Check("해상도 값 저장 대상에 반영", UISettings.Resolution.x > 0);
            }

            Call(router, "ToggleMenu", ScreenId.SkillWindow); await Wait(0.1f);
            Check("옵션 열린 동안 메뉴 단축키 무시", !ui.IsOpen(ScreenId.SkillWindow));

            o.defaultsButton.onClick.Invoke(); await Wait(0.05f);
            Near("기본값 → 마스터 100%", UISettings.MasterVolume, 1f);
            Eq("기본값이 화면에도 반영", o.master.valueText.text, "100");
            Check("기본값 → 화면 흔들림 켜짐", UISettings.ScreenShake && o.screenShake.toggle.isOn);

            Check("ESC(Back) → 옵션만 닫힘", ui.Back() && !ui.IsOpen(ScreenId.Options) && ui.IsOpen(ScreenId.Title));
            ui.CloseAll(); await Wait(0.2f);

            await ToCombat();
            ui.Open(ScreenId.Pause); await Wait(0.2f);
            Find<PauseWindow>().optionsButton.onClick.Invoke(); await Wait(0.3f);
            Check("일시정지 '옵션' → 옵션 창", ui.IsOpen(ScreenId.Options) && ui.IsOpen(ScreenId.Pause));
            Check("옵션 중에도 게임 정지 유지", Time.timeScale == 0f);
            ui.Back(); await Wait(0.2f);
            Check("ESC → 일시정지로 돌아감", ui.IsOpen(ScreenId.Pause) && !ui.IsOpen(ScreenId.Options));
            ui.Back(); await Wait(0.3f);
            Check("한 번 더 ESC → 게임 재개", !ui.IsOpen(ScreenId.Pause) && Time.timeScale == 1f);

            UISettings.MasterVolume = m0; UISettings.ScreenShake = s0; UISettings.Resolution = r0; UISettings.Save();
            ui.CloseAll();
        }

        static async Task Damage()
        {
            await ToCombat();
            var fx = Find<DamageFxController>();
            Check("DamageFx 연결됨", fx != null && ReferenceEquals(GameUI.Damage, fx));
            var enemies = new List<DummyEnemy>(UnityEngine.Object.FindObjectsByType<DummyEnemy>(FindObjectsSortMode.None));
            Check("가상의 적 3마리 배치", enemies.Count == 3);
            DummyEnemy normal = null, elite = null;
            foreach (var e in enemies) { if (e.IsElite) elite = e; else if (normal == null) normal = e; }
            foreach (var e in enemies) e.ResetHP();
            await Wait(0.2f);
            Check("적 3마리 체력바 추적", fx.TrackedEnemyCount == 3);
            Check("일반 적 체력바: 안 맞았으면 숨김", Get<Dictionary<IEnemyHealthSource, EnemyHealthBarView>>(fx, "_bars")[normal].group.alpha < 0.01f);
            Check("엘리트 체력바: 항상 표시", Get<Dictionary<IEnemyHealthSource, EnemyHealthBarView>>(fx, "_bars")[elite].group.alpha > 0.99f);

            bool prevSetting = UISettings.ShowDamageNumbers;
            UISettings.ShowDamageNumbers = true;

            normal.TakeHit(23, false);
            bool sparked = Get<List<HitSparkView>>(fx, "_sparks").Exists(s => s.active);
            await Wait(0.05f);
            var nums = Get<List<DamageNumberView>>(fx, "_numbers");
            DamageNumberView last = Newest(nums);
            Check("일반 피해 → 숫자 1개", fx.ActiveNumberCount == 1 && last != null);
            Eq("일반 숫자 = 23", last.text.text, "23");
            Check("일반 숫자: 흰색, 12px, 라벨 없음", last.text.color == Color.white && Mathf.Approximately(last.text.fontSize, 12f) && !last.label.gameObject.activeSelf);
            Check("타격 이펙트 재생", sparked);
            var bar = Get<Dictionary<IEnemyHealthSource, EnemyHealthBarView>>(fx, "_bars")[normal];
            await Wait(0.15f);
            Check("맞으면 일반 적 체력바 나타남", bar.group.alpha > 0.9f);
            await Wait(1.0f);
            Near("체력바 120 중 23 → 0.81", bar.bar.fill.fillAmount, 97f / 120f);
            Check("숫자는 시간이 지나면 사라짐", fx.ActiveNumberCount == 0);

            normal.TakeHit(50, true);
            last = Newest(nums);
            Check("치명타: 시작 크기가 크게 튐 (2배 이상)", last.Rect.localScale.x >= 2f);
            await Wait(0.05f);
            Eq("치명타 숫자 = 50", last.text.text, "50");
            Check("치명타: 24px, 글자 없음 (효과로만 표시)", Mathf.Approximately(last.text.fontSize, 24f) && !last.label.gameObject.activeSelf);
            await Wait(0.3f);
            Check("치명타: 노란색으로 정착", last.text.color.b < 0.5f && last.text.color.r > 0.9f);

            await Wait(1.0f);
            normal.TakeHit(5, false); normal.TakeHit(6, false); normal.TakeHit(7, false); await Wait(0.05f);
            var ys = new List<float>();
            foreach (var n in nums) if (n.active) ys.Add(n.offset.y);
            ys.Sort();
            Check("연타 3번 → 숫자 3개가 위로 쌓임(겹침 없음)", ys.Count == 3 && ys[1] - ys[0] >= 8f && ys[2] - ys[1] >= 8f);

            UISettings.ShowDamageNumbers = false;
            int before = fx.ActiveNumberCount;
            normal.TakeHit(4, false); await Wait(0.05f);
            Check("옵션 '피해 숫자' 끄면 숫자 안 뜸", fx.ActiveNumberCount == before);
            Check("…타격 이펙트는 그대로", Get<List<HitSparkView>>(fx, "_sparks").Exists(s => s.active));
            UISettings.ShowDamageNumbers = true;

            await Wait(bar.hideDelay + 0.6f);
            Check("한동안 안 맞으면 체력바 다시 숨김", bar.group.alpha < 0.05f);

            normal.TakeHit(9999, true); await Wait(0.9f);
            Check("처치 → 적 사망 상태", normal.IsDead);
            Check("처치 → 체력바 사라짐", bar.group.alpha < 0.05f);
            normal.Miss(); await Wait(0.05f);
            await Wait(normal.respawnDelay + 0.6f);
            Check("부활 → 체력 가득", !normal.IsDead && Mathf.Approximately(normal.HP, normal.MaxHP));

            elite.TakeHit(10, false); normal.ResetHP(); await Wait(0.05f);
            GameUI.Damage.ShowText(elite.HitPoint(), "MISS");
            await Wait(0.05f);
            last = Newest(nums);
            Eq("ShowText → 글자 그대로", last.text.text, "MISS");

            P.ChangeHP(-15); await Wait(0.05f);
            last = Newest(nums);
            Eq("플레이어 피격 → 숫자 15", last.text.text, "15");
            Check("플레이어 피격 숫자는 빨강", last.text.color.r > 0.9f && last.text.color.g < 0.5f);
            P.ChangeHP(15); await Wait(0.05f);
            last = Newest(nums);
            Eq("회복 → +15", last.text.text, "+15");

            for (int i = 0; i < 80; i++) GameUI.Damage.Show(elite.HitPoint(), i, DamageKind.Normal);
            await Wait(0.05f);
            Check("80개 연속 → 최대 개수(40) 안에서 재사용", nums.Count <= fx.maxNumbers);

            await Wait(1.3f);

            // ── 자료 조사 반영분 ──
            int prevSize = UISettings.DamageNumberSize; bool prevCompact = UISettings.CompactNumbers;
            UISettings.DamageNumberSize = 1; UISettings.CompactNumbers = true;

            normal.TakeHit(5, false); await Wait(0.02f); normal.TakeHit(5, false); await Wait(0.02f);
            var act = new List<DamageNumberView>(); foreach (var n in nums) if (n.active) act.Add(n);
            Check("연타 숫자 좌우 번갈아 (한쪽으로 몰리지 않음)", act.Count == 2 && Mathf.Sign(act[0].offset.x) != Mathf.Sign(act[1].offset.x),
                act.Count == 2 ? $"{act[0].offset.x} / {act[1].offset.x}" : act.Count.ToString());
            normal.TakeHit(30, true); await Wait(0.02f);
            var critN = Newest(nums);
            float normalMaxY = float.MinValue; foreach (var n in act) normalMaxY = Mathf.Max(normalMaxY, n.offset.y);
            Check("치명타는 위쪽 별도 줄 (일반 숫자보다 높게 시작)", critN.critLane && critN.offset.y > normalMaxY - 1f);
            await Wait(1.3f);

            elite.TakeHit(4, DamageKind.DamageOverTime); await Wait(0.1f);
            elite.TakeHit(5, DamageKind.DamageOverTime); await Wait(0.1f);
            elite.TakeHit(6, DamageKind.DamageOverTime); await Wait(0.05f);
            int dotCount = 0; DamageNumberView dotN = null;
            foreach (var n in nums) if (n.active && n.kind == DamageKind.DamageOverTime) { dotCount++; dotN = n; }
            Check("지속 피해 3틱 → 숫자 1개로 합침", dotCount == 1);
            if (dotN != null)
            {
                Eq("합친 값 = 15", dotN.text.text, "15");
                Check("지속 피해는 작게(10px) + 맞은 지점 아래", Mathf.Approximately(dotN.text.fontSize, 10f) && dotN.offset.y < 0f);
            }
            Check("지속 피해는 타격 이펙트 없음", !Get<List<HitSparkView>>(fx, "_sparks").Exists(sp => sp.active));
            await Wait(1.0f);

            elite.TakeHit(40, DamageKind.Weakness); await Wait(0.12f); // 등장 순간 흰 번쩍(0.04초) 뒤 색 확인
            var wk = Newest(nums);
            Check("약점: 글자 없음, 주황", !wk.label.gameObject.activeSelf && wk.text.color.r > 0.9f && wk.text.color.g < 0.7f && wk.text.color.b < 0.3f);
            Check("약점: 굵은 글꼴 24px (2배)", Mathf.Approximately(wk.text.fontSize, 24f) && wk.text.font != null && wk.text.font.name.Contains("Bold"),
                wk.text.font != null ? wk.text.font.name : "null");
            await Wait(0.4f);
            normal.TakeHit(9999, DamageKind.Normal); await Wait(0.3f);
            var fin = Newest(nums);
            Check("마지막 일격 → 자동 처치 숫자 (빨강, 글자 없음)", fin.kind == DamageKind.Finisher && !fin.label.gameObject.activeSelf && fin.text.color.g < 0.4f);
            Check("처치: 가장 큰 30px + 그림자 2px", Mathf.Approximately(fin.text.fontSize, 30f) && fin.shadow.rectTransform.anchoredPosition.x >= 2f);
            await Wait(normal.respawnDelay + 0.8f);

            UISettings.DamageNumberSize = 2;
            elite.TakeHit(7, false); await Wait(0.02f);
            Check("옵션 '크게' → 숫자 크기 정확히 2배(24px)", Mathf.Approximately(Newest(nums).text.fontSize, 24f));
            UISettings.DamageNumberSize = 1;
            Eq("줄여 쓰기: 9,999는 그대로", DamageFxController.FormatAmount(9999), "9,999");
            Eq("줄여 쓰기: 12,345 → 12.3k", DamageFxController.FormatAmount(12345), "12.3k");
            Eq("줄여 쓰기: 2,500,000 → 2.5M", DamageFxController.FormatAmount(2500000), "2.5M");
            UISettings.CompactNumbers = false;
            Eq("줄여 쓰기 끄면 12,345", DamageFxController.FormatAmount(12345), "12,345");

            // 흰색 번쩍임 + 히트스톱 + 화면 흔들림
            var combat = Find<SandboxCombat>();
            await Wait(1.0f);
            Check("적에 HitFlash 연결", elite.flash != null);
            combat.Attack(elite, DamageKind.Critical);
            Check("치명타 → 적 흰색 번쩍임", elite.flash.CurrentAmount > 0.99f);
            Check("치명타 → 히트스톱(시간 정지)", Time.timeScale == 0f);
            await Wait(0.25f);
            Check("히트스톱 후 시간 복구", Time.timeScale == 1f);
            Check("번쩍임 끝나면 원래 색", elite.flash.CurrentAmount < 0.01f);
            var cam = Camera.main; Vector3 camPos = cam.transform.position;
            UISettings.ScreenShake = false;
            combat.Attack(elite, DamageKind.Critical); await Wait(0.02f);
            Check("옵션 '화면 흔들림' 끄면 카메라 그대로", cam.transform.position == camPos);
            UISettings.ScreenShake = true;
            await Wait(0.4f);
            Check("흔들림 후 카메라 제자리", cam.transform.position == camPos);

            UISettings.DamageNumberSize = prevSize; UISettings.CompactNumbers = prevCompact;
            UISettings.ShowDamageNumbers = prevSetting;
            foreach (var e in enemies) e.ResetHP();
            await Wait(1.2f);
        }


        static DamageNumberView Newest(List<DamageNumberView> list)
        {
            DamageNumberView best = null;
            foreach (var n in list) if (n.active && (best == null || n.spawnTime >= best.spawnTime)) best = n;
            return best;
        }

        static async Task DialogueDrift()
        {
            await ToCombat();
            var dv = Find<DialogueView>();
            var data = Find<SandboxDirector>().introDialogue;
            Vector2 l0 = dv.leftPortrait.rectTransform.anchoredPosition;
            Vector2 r0 = dv.rightPortrait.rectTransform.anchoredPosition;
            Vector2 b0 = dv.box.anchoredPosition;
            // 슬라이드 도중에 빠르게 넘기기·중단을 반복 (예전엔 매번 조금씩 밀렸음)
            for (int k = 0; k < 6; k++)
            {
                GameUI.Dialogue.Play(data);
                for (int i = 0; i < data.lines.Count * 2; i++) { Call(dv, "Advance"); await Wait(0.03f); }
                GameUI.Dialogue.Stop();
                await Wait(0.02f);
            }
            GameUI.Dialogue.Play(data); await Wait(0.6f);
            for (int i = 0; i < data.lines.Count * 2 && dv.IsPlaying; i++) { Call(dv, "Advance"); await Wait(0.4f); }
            await Wait(0.3f);
            Check("왼쪽 초상화 제자리", (dv.leftPortrait.rectTransform.anchoredPosition - l0).magnitude < 0.5f,
                $"{l0} → {dv.leftPortrait.rectTransform.anchoredPosition}");
            Check("오른쪽 초상화 제자리", (dv.rightPortrait.rectTransform.anchoredPosition - r0).magnitude < 0.5f,
                $"{r0} → {dv.rightPortrait.rectTransform.anchoredPosition}");
            Check("대화창 제자리", (dv.box.anchoredPosition - b0).magnitude < 0.5f, $"{b0} → {dv.box.anchoredPosition}");

            var guide = Hud.guideText.rectTransform;
            Vector2 g0 = guide.anchoredPosition;
            for (int k = 0; k < 8; k++) { GameUI.HUD.ShowGuide("안내 " + k, 0.2f); await Wait(0.04f); }
            await Wait(1.2f);
            Check("안내 문구 연타 후 제자리", (guide.anchoredPosition - g0).magnitude < 0.5f, $"{g0} → {guide.anchoredPosition}");
        }
    }
}
