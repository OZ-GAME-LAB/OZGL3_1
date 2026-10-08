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
            Application.logMessageReceived += OnLog;
            Log($"OZ UI Self Test — {DateTime.Now:yyyy-MM-dd HH:mm:ss}  (Unity {Application.unityVersion})");
            try
            {
                await Wait(1.0f);
                await Suite("해상도 배율", Scale);
                await Suite("체력", Health);
                await Suite("레벨·경험치·랭크", Progression);
                await Suite("스킬 슬롯·스킬 창", Skills);
                await Suite("아이템·버프", Items);
                await Suite("인벤토리", Inventory);
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
                await Suite("팀원 연결용 Link 컴포넌트 (v0.5)", Links);
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
            P.SetClass(data);
            GameUI.HUD.Visible = true;
            GameUI.Dialogue.Stop();
            UIManager.Instance.CloseAll();
            Time.timeScale = 1f;
            await Wait(0.6f);
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

                player.SetHP(40f, 80f); await Wait(1.0f);
                Eq("PlayerUILink.SetHP → 40/80", Hud.health.valueText.text, "40/80");
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
            Eq("시작 Lv.1", v.levelText.text, "Lv.1");
            Check("HUD에 SP 표시 없음 (삭제됨)", v.pointsHint == null && v.pointsText == null);

            P.AddExp(40); await Wait(1.0f);
            Near("경험치 40/100 → 바 0.4", v.expBar.fill.fillAmount, 0.4f);

            P.AddExp(70); await Wait(1.0f);
            Eq("110 누적 → Lv.2", v.levelText.text, "Lv.2");
            Near("남은 경험치 10 → 바 0.1", v.expBar.Value, 0.1f);

            P.AddExp(10000); await Wait(1.0f);
            Eq("상한 Lv.15에서 멈춤", v.levelText.text, "Lv.15");
            Near("최대 레벨 → 바 가득", v.expBar.Value, 1f);
            Check("Lv.15까지 스킬 포인트 14", P.SkillPoints == 14, "SP " + P.SkillPoints);
            P.AddExp(100); await Wait(0.3f);
            Eq("Lv.15 이후 경험치 무시", v.levelText.text, "Lv.15");

            Eq("랭크 F급", v.rankText.text, "F급");
            P.PromoteRank(); await Wait(0.3f);
            Eq("승급 → E급", v.rankText.text, "E급");
            P.PromoteRank(); await Wait(0.3f);
            Eq("승급 → D급", v.rankText.text, "D급");
        }

        static async Task Skills()
        {
            await ToCombat();
            var bar = Hud.skills;
            SkillSlotView q = bar.slots[0], e = bar.slots[1], r = bar.slots[2];
            Check("Q 시작 습득(잠금 해제)", !q.lockOverlay.activeSelf);
            Check("E 미습득 잠금", e.lockOverlay.activeSelf);
            Check("R 미습득 잠금", r.lockOverlay.activeSelf);
            Check("아이콘 표시(검술 Q)", q.icon.enabled && q.icon.sprite != null);

            int fails = 0; SkillUseFailReason last = default;
            Action<SkillSlot, SkillUseFailReason> onFail = (s, why) => { fails++; last = why; };
            P.SkillUseFailed += onFail;

            P.UseSkill(SkillSlot.Q); await Wait(0.1f);
            Check("Q 사용 → 쿨타임 오버레이 표시", q.cooldown.overlay.enabled);
            Near("오버레이 거의 가득", q.cooldown.overlay.fillAmount, 1f, 0.05f);
            Eq("남은 초 5", q.cooldown.label.text, "5");
            await Wait(1.1f);
            Eq("1초 뒤 4", q.cooldown.label.text, "4");
            Near("오버레이 0.78 근처", q.cooldown.overlay.fillAmount, 0.78f, 0.05f);

            P.UseSkill(SkillSlot.Q); await Wait(0.05f);
            Check("쿨타임 중 재사용 → 실패(Cooldown)", fails == 1 && last == SkillUseFailReason.Cooldown);
            P.UseSkill(SkillSlot.E); await Wait(0.05f);
            Check("미습득 E 사용 → 실패(NotLearned)", fails == 2 && last == SkillUseFailReason.NotLearned);

            await Wait(4.2f);
            Check("쿨타임 끝 → 오버레이 꺼짐", !q.cooldown.overlay.enabled);
            Eq("쿨타임 끝 → 숫자 지움", q.cooldown.label.text, "");

            // 스킬 창 / 투자 규칙
            string reason;
            Check("Lv.1에서 E 투자 불가(포인트 없음 또는 레벨)", !P.CanInvest(SkillSlot.E, out reason));
            P.AddExp(150); await Wait(0.2f); // Lv.2, SP 1 (ISkillSource 호환 규칙 = 트리의 다음 노드)
            Check("Lv.2·SP1 → E 습득 가능", P.CanInvest(SkillSlot.E, out _));
            Check("Lv.2 → R은 레벨 부족", !P.CanInvest(SkillSlot.R, out reason) && reason == "Lv.4 필요", "사유: " + reason);

            // 스킬 트리 창 (K) — 노드 해금 · 택1 · 초기화
            GameUI.Screens.Open(ScreenId.SkillWindow); await Wait(0.4f);
            var win = Find<SkillTreeWindow>();
            var tree = P.Tree;
            SkillNodeView V(string id) { foreach (var nv in win.Views) if (nv.Node.id == id) return nv; return null; }
            Eq("트리 창 포인트 1", win.pointsText.text, "스킬 포인트  1");
            Check("노드 전부 생성", win.Views.Count == tree.nodes.Count, $"{win.Views.Count} / {tree.nodes.Count}");
            Check("데이터 오류 없음", tree.Validate().Count == 0, string.Join(", ", tree.Validate()));
            Check("시작 Q(검술) 해금 상태", V("sword_q").State == SkillNodeState.Unlocked);
            Check("Q 마법은 택1로 막힘", V("magic_q").State == SkillNodeState.Blocked);
            Check("E 검술/마법 둘 다 해금 가능", V("sword_e").State == SkillNodeState.Available && V("magic_e").State == SkillNodeState.Available);
            Check("R은 Lv.4 전까지 잠김", V("sword_r").State == SkillNodeState.Locked);

            V("magic_e").button.onClick.Invoke(); await Wait(0.3f);
            Check("E 마법 선택 → 해금", P.IsUnlocked("magic_e"));
            Eq("포인트 0", win.pointsText.text, "스킬 포인트  0");
            Check("E 검술은 막힘", V("sword_e").State == SkillNodeState.Blocked);
            Check("HUD E = 마법 스킬 아이콘", e.icon.sprite == P.GetSkill(SkillSlot.E).icon && P.GetSkill(SkillSlot.E).playerClass == PlayerClass.Magic);
            Check("HUD E 잠금 해제", !e.lockOverlay.activeSelf);
            Eq("HUD E 단계 점 1개", e.rankText.text, "·");

            V("magic_e_2").button.onClick.Invoke(); await Wait(0.3f);
            Check("레벨 부족 노드 → 거부", !P.IsUnlocked("magic_e_2"));
            Eq("거부 사유 표시", win.detailStatus.text, "Lv.3 필요");

            P.AddExp(10000); await Wait(0.3f); // Lv.15, SP 13
            Eq("Lv.15 (최대) 표시", win.levelText.text, "Lv.15  (최대)");
            Eq("포인트 13", win.pointsText.text, "스킬 포인트  13");
            V("magic_e_2").button.onClick.Invoke(); await Wait(0.1f);
            V("magic_e_3").button.onClick.Invoke(); await Wait(0.3f);
            Check("E 3단계", P.GetRank(SkillSlot.E) == 3);
            Eq("HUD E 단계 점 3개", e.rankText.text, "···");
            Check("ISkillSource 호환: 최대 단계 투자 거부", !P.TryInvest(SkillSlot.E));
            V("p_cdr").button.onClick.Invoke(); await Wait(0.1f);
            Check("선행 패시브 없이 거부", !P.IsUnlocked("p_cdr"));

            win.resetButton.onClick.Invoke(); await Wait(0.1f);
            Check("초기화 첫 클릭은 확인 대기 (아무것도 안 지움)", P.IsUnlocked("magic_e"));
            Eq("초기화 버튼: 한 번 더", win.resetLabel.text, "한 번 더: 초기화");
            win.resetButton.onClick.Invoke(); await Wait(0.3f);
            Check("초기화 → E 해제", !P.IsUnlocked("magic_e") && !P.IsUnlocked("magic_e_3"));
            Check("초기화해도 시작 Q 유지", P.IsUnlocked("sword_q"));
            Eq("포인트 환급 → 14", win.pointsText.text, "스킬 포인트  14");
            Check("초기화 후 E 검술 다시 선택 가능", V("sword_e").State == SkillNodeState.Available);
            Check("HUD E 다시 잠금", e.lockOverlay.activeSelf);
            Check("찍은 게 없으면 초기화 버튼 비활성", !win.resetButton.interactable);
            GameUI.Screens.Close(ScreenId.SkillWindow);

            // 데이터 에셋 수치 변경 → UI 반영
            var qData = P.GetSkill(SkillSlot.Q);
            var backup = qData.ranks[0];
            try
            {
                var changed = backup; changed.cooldown = 2f;
                qData.ranks[0] = changed;
                P.UseSkill(SkillSlot.Q); await Wait(0.1f);
                Eq("SkillData 쿨타임 5→2 수정 → HUD 2초", q.cooldown.label.text, "2");
            }
            finally
            {
                qData.ranks[0] = backup; // 에셋 원복
                EditorUtility.SetDirty(qData);
            }
            P.SkillUseFailed -= onFail;
            await Wait(2.2f);

            // 계열 변경 → 아이콘 교체
            await ToCombat(PlayerClass.Magic);
            Check("마법 계열 → Q 아이콘 교체", q.icon.sprite == P.GetSkill(SkillSlot.Q).icon && P.GetSkill(SkillSlot.Q).playerClass == PlayerClass.Magic);
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
            P.UseItem(0); await Wait(1.0f);
            Eq("회복은 최대 체력 초과 안 함", Hud.health.valueText.text, "100/100");
            P.ItemUseFailed -= onFail;
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

        static async Task Inventory()
        {
            await ToCombat();
            GameUI.Screens.Open(ScreenId.Inventory); await Wait(0.4f);
            var w = Find<InventoryWindow>();
            var slots = Get<List<InventorySlotView>>(w, "_slots");
            Check("슬롯 24칸 생성", slots != null && slots.Count == 24, "칸 " + slots?.Count);
            Eq("0번 칸 회복제 수량 3", slots[0].countText.text, "3");
            Check("열쇠(1개)는 수량 숫자 숨김", slots[4].countText.text == "" && slots[4].icon.enabled);

            P.TryMove(0, 10); await Wait(0.2f);
            Check("이동 0→10: 10번에 아이콘", slots[10].icon.enabled && !slots[0].icon.enabled);
            Eq("퀵슬롯 수량은 그대로 3", Hud.items.slots[0].countText.text, "3");

            P.TryMove(10, 1); await Wait(0.2f);
            Check("교환 10↔1", slots[1].icon.sprite == P.GetItem(0).icon && slots[10].icon.sprite == P.GetItem(1).icon);

            Check("열쇠 아이템 사용 불가", !P.TryUse(4));
            P.TryUse(1); await Wait(0.2f);
            Eq("인벤토리에서 회복제 사용 → 퀵슬롯 2", Hud.items.slots[0].countText.text, "2");

            int toasts0 = ToastCount();
            for (int i = 0; i < 400; i++) P.AddItem(0);
            await Wait(0.3f);
            Check("가득 참 → 경고 알림", ToastCount() > toasts0 || ToastTexts().Contains("인벤토리가 가득 찼다"));
            int filled = 0; for (int i = 0; i < P.Capacity; i++) if (!P.GetSlot(i).IsEmpty) filled++;
            Check("모든 칸 사용 중", filled == P.Capacity, filled + "/" + P.Capacity);
            Check("스택 최대 9 지킴", P.GetSlot(23).Count <= 9);
            GameUI.Screens.Close(ScreenId.Inventory);
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
            P.OpenGate(); P.SealGate(); P.OpenGate(); P.SealGate(); await Wait(0.3f);
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
            Check("Remove → 게이트·보스 2줄만 남음", Hud.quests.Rows.Count == 2);
        }

        static async Task HudLayout()
        {
            await ToCombat();
            var canvas = Hud.GetComponentInParent<Canvas>().rootCanvas;
            var crt = (RectTransform)canvas.transform;
            float W = crt.rect.width;
            float X(RectTransform rt) => crt.InverseTransformPoint(rt.TransformPoint(rt.rect.center)).x + W * 0.5f;
            Check("스킬 Q/E/R은 화면 오른쪽 (양손 분리)", X((RectTransform)Hud.skills.slots[0].transform) > W * 0.7f, "Q x " + X((RectTransform)Hud.skills.slots[0].transform));
            Check("아이템 1~4는 화면 왼쪽", X((RectTransform)Hud.items.slots[3].transform) < W * 0.35f);
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
            await Expect("인벤토리 열기 → Open", UISound.Open, () => GameUI.Screens.Open(ScreenId.Inventory));
            await Expect("닫기 → Close", UISound.Close, () => GameUI.Screens.Close(ScreenId.Inventory));
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
            P.AddExp(600); await Wait(0.5f);
            Check("연속 레벨업 → 알림 1장", sa.VisibleCount == 1, "장 수 " + sa.VisibleCount);
            var card = sa.Cards[0];
            Check("'레벨 업' + 'Lv.a → Lv.b' 형식", card.kind.text == "레벨 업" && card.main.text.Contains("→"), card.main.text);
            Check("스킬 포인트 합산 표시", card.sub.text.StartsWith("스킬 포인트 +"));
            Near("카드가 마스크 안 첫 자리(y 0)", card.rt.anchoredPosition.y, 0f, 1f);
            GameUI.Notify.System("랭크 승급", "F급 → E급", "새 스킬 해금"); await Wait(0.4f);
            GameUI.Notify.System("획득", "게이트 키", null); await Wait(0.4f);
            Check("다른 알림은 아래로 쌓임 (3장)", sa.VisibleCount == 3);
            Near("두 번째 카드 위치", sa.Cards[1].rt.anchoredPosition.y, -(sa.cardHeight + sa.gap), 1f);
            GameUI.Notify.System("테스트", "4번째", null); await Wait(0.6f);
            Check("최대 3장 유지", sa.VisibleCount <= 3);
            await Wait(sa.holdSeconds * 3 + 2f);
            Check("시간이 지나면 모두 지도 뒤로 사라짐", sa.VisibleCount == 0);
            Check("토스트(우하단)엔 레벨업이 안 쌓임", !ToastTexts().Contains("레벨 업"));
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

            normal.TakeHit(23, false); await Wait(0.05f);
            var nums = Get<List<DamageNumberView>>(fx, "_numbers");
            DamageNumberView last = Newest(nums);
            Check("일반 피해 → 숫자 1개", fx.ActiveNumberCount == 1 && last != null);
            Eq("일반 숫자 = 23", last.text.text, "23");
            Check("일반 숫자: 흰색, 12px, 라벨 없음", last.text.color == Color.white && Mathf.Approximately(last.text.fontSize, 12f) && !last.label.gameObject.activeSelf);
            Check("타격 이펙트 재생", Get<List<HitSparkView>>(fx, "_sparks").Exists(s => s.active));
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
