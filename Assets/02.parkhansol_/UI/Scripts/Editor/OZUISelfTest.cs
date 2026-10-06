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
                await Suite("게이트", Gate);
                await Suite("보스", Boss);
                await Suite("지도", Map);
                await Suite("대화", Dialogue);
                await Suite("화면·입력 차단·일시정지", Screens);
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
            Near("시작 Flask 1.0", h.flaskFill.fillAmount, 1f);

            P.ChangeHP(-30); await Wait(1.0f);
            Eq("피격 -30 → 70/100", h.valueText.text, "70/100");
            Near("Flask 0.7", h.flaskFill.fillAmount, 0.7f);
            Near("체력 바 0.7", h.bar.fill.fillAmount, 0.7f);
            Near("잔상(trail)도 0.7까지 내려옴", h.bar.trail.fillAmount, 0.7f);
            Check("25% 초과에선 맥박 없음", !Get<bool>(h, "_low"));

            P.ChangeHP(-50); await Wait(1.0f);
            Eq("20/100", h.valueText.text, "20/100");
            Check("25% 이하 → 맥박 ON", Get<bool>(h, "_low"));

            P.ChangeHP(-999); await Wait(1.0f);
            Eq("0 아래로 안 내려감 → 0/100", h.valueText.text, "0/100");
            Near("Flask 0", h.flaskFill.fillAmount, 0f);
            Check("0일 때 맥박 OFF", !Get<bool>(h, "_low"));

            P.ChangeHP(40); await Wait(0.15f);
            Near("회복 시 잔상이 먼저 올라감(0.4)", h.bar.trail.fillAmount, 0.4f, 0.05f);
            await Wait(1.0f);
            Eq("회복 +40 → 40/100", h.valueText.text, "40/100");
            Near("회복 후 체력 바 0.4", h.bar.fill.fillAmount, 0.4f);

            P.ChangeHP(9999); await Wait(1.0f);
            Eq("최대 초과 회복 → 100/100", h.valueText.text, "100/100");
            Near("Flask 1.0 복귀", h.flaskFill.fillAmount, 1f);
        }

        static async Task Progression()
        {
            await ToCombat();
            var v = Hud.progression;
            Eq("시작 Lv.1", v.levelText.text, "Lv.1");
            Check("시작 SP 표시 없음", !v.pointsHint.activeSelf);

            P.AddExp(40); await Wait(1.0f);
            Near("경험치 40/100 → 바 0.4", v.expBar.fill.fillAmount, 0.4f);

            P.AddExp(70); await Wait(1.0f);
            Eq("110 누적 → Lv.2", v.levelText.text, "Lv.2");
            Near("남은 경험치 10 → 바 0.1", v.expBar.Value, 0.1f);
            Check("SP 1 표시", v.pointsHint.activeSelf);
            Eq("SP 문구", v.pointsText.text, "SP 1  [K]");

            P.AddExp(10000); await Wait(1.0f);
            Eq("상한 Lv.7에서 멈춤", v.levelText.text, "Lv.7");
            Near("최대 레벨 → 바 가득", v.expBar.Value, 1f);
            Eq("SP 6", v.pointsText.text, "SP 6  [K]");
            P.AddExp(100); await Wait(0.3f);
            Eq("Lv.7 이후 경험치 무시", v.levelText.text, "Lv.7");

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
            P.AddExp(150); await Wait(0.2f); // Lv.2, SP 1
            Check("Lv.2·SP1 → E 습득 가능", P.CanInvest(SkillSlot.E, out _));
            Check("Lv.2 → R은 레벨 부족", !P.CanInvest(SkillSlot.R, out reason) && reason == "Lv.4 필요", "사유: " + reason);

            GameUI.Screens.Open(ScreenId.SkillWindow); await Wait(0.4f);
            var win = Find<SkillWindow>();
            SkillCardView ce = win.cards[1], cr = win.cards[2];
            Eq("스킬 창 포인트 1", win.pointsText.text, "스킬 포인트  1");
            Eq("E 카드: 미습득", ce.rankText.text, "미습득");
            Eq("E 버튼: 습득", ce.investLabel.text, "습득");
            Eq("R 버튼: Lv.4 필요", cr.investLabel.text, "Lv.4 필요");
            Check("R 버튼 비활성", !cr.investButton.interactable);

            ce.investButton.onClick.Invoke(); await Wait(0.3f);
            Eq("E 습득 → 1/3단계", ce.rankText.text, "1/3단계");
            Eq("포인트 0", win.pointsText.text, "스킬 포인트  0");
            Eq("포인트 없음 → 포인트 부족", ce.investLabel.text, "포인트 부족");
            Check("HUD E 잠금 해제", !e.lockOverlay.activeSelf);
            Eq("HUD E 단계 점 1개", e.rankText.text, "·");

            P.AddExp(10000); await Wait(0.3f); // Lv.7, SP 5
            ce.investButton.onClick.Invoke(); await Wait(0.1f);
            ce.investButton.onClick.Invoke(); await Wait(0.3f);
            Eq("E 3/3단계", ce.rankText.text, "3/3단계");
            Eq("다음 효과: 최대 단계", ce.nextText.text, "최대 단계");
            Check("최대 단계 → 버튼 비활성", !ce.investButton.interactable);
            Check("최대 단계 투자 시도 → 거부", !P.TryInvest(SkillSlot.E));
            Eq("HUD E 단계 점 3개", e.rankText.text, "···");
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

        static async Task Gate()
        {
            await ToCombat();
            var g = Hud.gate;
            Eq("시작 0/3", g.countText.text, "게이트 0/3");
            Eq("게이트 활성", g.stateText.text, "게이트 활성");
            P.SealGate(); await Wait(0.2f);
            Eq("봉쇄 → 1/3", g.countText.text, "게이트 1/3");
            Eq("다음 게이트 탐색", g.stateText.text, "다음 게이트 탐색");
            P.SealGate(); await Wait(0.1f);
            Eq("비활성 상태 봉쇄 무시(중복 반영 X)", g.countText.text, "게이트 1/3");
            P.OpenGate(); P.SealGate(); P.OpenGate(); P.SealGate(); await Wait(0.3f);
            Eq("3/3", g.countText.text, "게이트 3/3");
            Check("보스 구역 개방 표시", g.bossReady.activeSelf);
            Eq("상태 문구", g.stateText.text, "보스 구역 개방");
            P.OpenGate(); P.SealGate(); await Wait(0.1f);
            Eq("목표 달성 후 추가 봉쇄 없음", g.countText.text, "게이트 3/3");
            P.ResetAll(); await Wait(0.3f);
            Eq("재도전 → 0/3 복구", g.countText.text, "게이트 0/3");
            Check("보스 개방 표시 꺼짐", !g.bossReady.activeSelf);
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

            GameUI.Screens.ShowStageClear(1, HunterRank.E); await Wait(0.2f);
            Eq("클리어 화면 스테이지", Find<StageClearScreen>().stageText.text, "STAGE 1 CLEAR");
            Eq("클리어 화면 랭크", Find<StageClearScreen>().rankText.text, "헌터 랭크  E급");
            ui.CloseAll();
        }
    }
}
