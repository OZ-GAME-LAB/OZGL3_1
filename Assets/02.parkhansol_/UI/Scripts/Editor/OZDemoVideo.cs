using System;
using System.IO;
using System.Threading.Tasks;
using OZ.UI.Contracts;
using OZ.UI.Samples;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OZ.UI.EditorTools
{
    /// <summary>
    /// [v0.7] OZ > UI > Record Demo Videos : UI_Sandbox를 플레이하며 기능별 시연 장면을 자동으로 연출·녹화.
    /// [v0.8] OZ > UI > Record Demo Videos (v0.8 플레이어 옆 게이지) : 06 배터리 체력 · 07 대시 동그라미만 녹화.
    /// [v0.9] OZ > UI > Record Demo Videos (v0.9 맵 전환·캐릭터 옆 게이지) : 08 맵 전환 화면 어둡게 · 09 임시 캐릭터 옆 게이지 + Shift 대시.
    ///   UI_Sandbox Play 시작 때 플레이어 옆 게이지 위치·간격을 임시 캐릭터(Player (Avatar)) 그림 크기에 자동으로 맞춤.
    ///   화면 + 유니티 소리만 (DemoCapture). 결과: 프로젝트/Temp/OZDemo/{번호_이름}.frames + .wav
    ///   (mp4 변환은 별도 — ffmpeg로 프레임 시각에 맞춰 합침)
    /// </summary>
    [InitializeOnLoad]
    internal static class OZDemoVideo
    {
        const string Flag = "OZ_UI_DEMO_PENDING";
        const string SetKey = "OZ_UI_DEMO_SET"; // "" = v0.7 전체, "v08" = 플레이어 옆 게이지
        public static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "OZDemo"));

        static OZDemoVideo() => EditorApplication.playModeStateChanged += OnPlayMode;

        [MenuItem("OZ/UI/Record Demo Videos", priority = 22)]
        static void Start() => Begin("");

        [MenuItem("OZ/UI/Record Demo Videos (v0.8 플레이어 옆 게이지)", priority = 23)]
        static void StartV08() => Begin("v08");

        [MenuItem("OZ/UI/Record Demo Videos (v0.9 맵 전환·캐릭터 옆 게이지)", priority = 24)]
        static void StartV09() => Begin("v09");

        static void Begin(string set)
        {
            if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; return; }
            if (!OZSandboxBuilder.IsDone) { Debug.LogError("[OZ Demo] UI_Sandbox 씬이 없습니다."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(OZPaths.SandboxScene);
            SessionState.SetString(SetKey, set);
            SessionState.SetBool(Flag, true);
            EditorApplication.isPlaying = true;
        }

        static void OnPlayMode(PlayModeStateChange s)
        {
            // v0.9: 샌드박스에서는 게이지를 임시 캐릭터 그림에 맞춤 (녹화가 아닐 때도)
            if (s == PlayModeStateChange.EnteredPlayMode && EditorSceneManager.GetActiveScene().path == OZPaths.SandboxScene)
                EditorApplication.delayCall += FitGaugeToAvatar;
            if (s != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Flag, false)) return;
            SessionState.SetBool(Flag, false);
            Run();
        }

        static Task Wait(float sec) => Task.Delay(Mathf.RoundToInt(sec * 1000));
        static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
        static DummyPlayer P => Find<DummyPlayer>();

        static async void Run()
        {
            bool bg = Application.runInBackground;
            Application.runInBackground = true;
            try
            {
                if (Directory.Exists(OutDir)) Directory.Delete(OutDir, true);
                Directory.CreateDirectory(OutDir);
                File.Delete(Path.Combine(OutDir, "done.txt"));
                await Wait(2.0f);
                string set = SessionState.GetString(SetKey, "");
                if (set == "v09")
                {
                    await Clip("08_맵전환_화면어둡게", MapTransition);
                    await Clip("09_임시캐릭터옆게이지_대시", SideGaugeOnAvatar);
                }
                else if (set == "v08")
                {
                    await Clip("06_플레이어옆_배터리체력", SideBattery);
                    await Clip("07_대시동그라미_사용_충전점멸", SideDash);
                }
                else
                {
                    await Clip("01_체력바색_저체력경고_회복", Health);
                    await Clip("02_쿨타임_사용불가_자리변경", Cooldowns);
                    await Clip("03_버프테두리_번갈아점멸", Buffs);
                    await Clip("04_랭크승급_팡파레", RankUp);
                    await Clip("05_지역이름_상호작용F_화면밖화살표", World);
                }
                File.WriteAllText(Path.Combine(OutDir, "done.txt"), DateTime.Now.ToString("s"));
                Debug.Log("[OZ Demo] 녹화 완료 → " + OutDir);
            }
            catch (Exception e)
            {
                Debug.LogError("[OZ Demo] 녹화 실패: " + e);
            }
            finally
            {
                Application.runInBackground = bg;
                Time.timeScale = 1f;
                EditorApplication.isPlaying = false;
            }
        }

        static async Task Clip(string name, Func<Task> body)
        {
            await Prepare();
            var cap = DemoCapture.Begin(Path.Combine(OutDir, name));
            try { await body(); }
            finally { cap.End(); }
            Debug.Log($"[OZ Demo] {name}: {cap.FrameCount}프레임");
            await Wait(0.3f);
        }

        static async Task Prepare()
        {
            UIManager.Instance.CloseAll();
            GameUI.Dialogue.Stop();
            GameUI.Indicators.Clear();
            GameUI.Prompt.Hide();
            GameUI.Area.Hide();
            var dir = Find<SandboxDirector>();
            ClassData sword = null;
            foreach (var c in dir.classes) if (c != null && c.playerClass == PlayerClass.Sword) sword = c;
            P.ResetAll();
            P.SetClass(sword);
            GameUI.HUD.Visible = true;
            GameUI.Boss.Hide();
            var help = GameObject.Find("SandboxHelp");
            if (help != null) help.SetActive(false);
            UIManager.Instance.CloseAll();
            Time.timeScale = 1f;
            await Wait(1.2f);
        }

        // 1) 체력바 색 하늘→노랑→빨강, 저체력 붉은 비네트+심장 박동, 회복 초록 번쩍
        static async Task Health()
        {
            await Wait(1.0f);
            P.ChangeHP(-20); await Wait(1.5f);
            P.ChangeHP(-25); await Wait(2.0f);
            P.ChangeHP(-30); await Wait(4.0f);
            P.UseItem(0); await Wait(2.2f);
            P.ChangeHP(60); await Wait(2.2f);
        }

        // 2) 스킬(왼쪽)·아이템(오른쪽) 자리, 쿨타임 어둡게+도는 바늘+완료 점멸, 미습득·수량 0 어둡게
        static async Task Cooldowns()
        {
            await Wait(1.2f);
            P.UseSkill(SkillSlot.Q); await Wait(0.8f);
            P.UseSkill(SkillSlot.E); await Wait(0.8f);   // 미습득 → 흔들림 (어둡게 표시 중)
            P.UseItem(1); await Wait(0.8f);              // 마지막 1개 → 쿨타임 후 수량 0 어둡게
            P.UseItem(0); await Wait(2.5f);
            P.UseItem(1); await Wait(2.5f);              // 수량 0 → 실패
            await Wait(1.5f);
        }

        // 3) 버프 테두리: 공격·방어·신속을 먹으면 같은 자리에서 색이 번갈아 점멸
        static async Task Buffs()
        {
            await Wait(0.8f);
            P.UseItem(1); await Wait(2.4f);
            P.UseItem(2); await Wait(2.6f);
            P.UseItem(3); await Wait(6.5f);
        }

        // 4) 랭크 승급 팡파레
        static async Task RankUp()
        {
            await Wait(1.0f);
            P.PromoteRank(); await Wait(4.2f);
            P.PromoteRank(); await Wait(4.2f);
        }

        // 5) 지역 이름, 상호작용 [F] + 누름 반응, 화면 밖 화살표
        static async Task World()
        {
            var dir = Find<SandboxDirector>();
            await Wait(0.6f);
            GameUI.Area.Show("서울 거리", "STAGE 1"); await Wait(3.6f);
            // 상호작용 대상: 적 A 머리 위
            var target = GameObject.Find("Enemy_A");
            Transform tt = target != null ? target.transform : P.transform;
            var rend = tt.GetComponentInChildren<Renderer>();
            Vector3 offset = rend != null ? new Vector3(0f, rend.bounds.max.y - tt.position.y + 0.25f, 0f) : new Vector3(0f, 2.2f, 0f);
            GameUI.Prompt.Show(tt, "조사", "F", offset); await Wait(1.4f);
            var pv = Find<InteractPromptView>();
            pv.Press(); await Wait(1.2f);
            pv.Press(); await Wait(1.2f);
            GameUI.Prompt.Hide(); await Wait(0.6f);
            var gate = new GameObject("Demo_FarGate").transform;
            var bossFar = new GameObject("Demo_FarBoss").transform;
            gate.position = P.transform.position + new Vector3(-40f, 3f, 0f);
            bossFar.position = P.transform.position + new Vector3(55f, -2f, 0f);
            try
            {
                GameUI.Indicators.Track(gate, IndicatorKind.Gate);
                GameUI.Indicators.Track(bossFar, IndicatorKind.Boss, "보스");
                await Wait(3.0f);
                GameUI.Area.Show("지하철역 승강장", "STAGE 2"); await Wait(3.8f);
            }
            finally
            {
                GameUI.Indicators.Clear();
                UnityEngine.Object.Destroy(gate.gameObject);
                UnityEngine.Object.Destroy(bossFar.gameObject);
            }
        }

        // ── v0.8 플레이어 옆 게이지 ──

        /// <summary>시연용 대시: 대기 0.6초, 공중 대시는 착지 전까지 1번 (dev_Player_2와 같은 규칙)</summary>
        sealed class DemoDash : IDashSource
        {
            public float Cooldown = 0.6f;
            float _readyAt;
            public bool AirLocked;
            public int MaxDashCharges => 1;
            public int DashCharges => CanDash ? 1 : 0;
            public float DashCooldownRemaining => Mathf.Max(0f, _readyAt - Time.time);
            public float DashCooldownDuration => Cooldown;
            public bool CanDash => Time.time >= _readyAt && !AirLocked;
            public void Use(bool inAir = false) { if (!CanDash) return; _readyAt = Time.time + Cooldown; if (inAir) AirLocked = true; }
        }

        /// <summary>
        /// 샌드박스 더미는 회전하지 않으므로, 바라보는 방향을 대신 표현하는 보이지 않는 대상(UIPlayerTag)을 더미 위치에 둔다.
        /// 게이지 위치·크기는 더미 그림 크기에 맞춤.
        /// </summary>
        static (Transform facing, PlayerSideGaugeView gauge) SetupSideGauge()
        {
            // 샌드박스에서 보이는 플레이어 그림은 'Player (Avatar)' (DummyPlayer는 데이터만 가진 빈 오브젝트)
            var avatar = GameObject.Find("Player (Avatar)");
            var p = avatar != null ? avatar.transform : P.transform;
            var facing = new GameObject("Demo_PlayerFacing").transform;
            var rend = p.GetComponentInChildren<Renderer>();
            Vector3 center = rend != null ? rend.bounds.center : p.position + Vector3.up;
            facing.position = new Vector3(center.x, rend != null ? rend.bounds.min.y : p.position.y, p.position.z);
            facing.rotation = Quaternion.LookRotation(Vector3.right);
            facing.gameObject.AddComponent<UIPlayerTag>();

            var gauge = Find<PlayerSideGaugeView>();
            if (gauge != null)
            {
                gauge.mode = PlayerSideGaugeView.ShowMode.Auto;
                gauge.worldOffset = new Vector3(0f, center.y - facing.position.y, 0f);
                var cam = Camera.main;
                var canvas = UIManager.Instance.GetLayer(UILayer.HUD);
                if (cam != null && rend != null && canvas != null)
                {
                    float halfPx = Mathf.Abs(cam.WorldToScreenPoint(new Vector3(rend.bounds.max.x, center.y, center.z)).x
                                             - cam.WorldToScreenPoint(center).x) / Mathf.Max(1f, canvas.scaleFactor);
                    gauge.sideDistance = Mathf.Round(halfPx) + 4f;
                }
            }
            return (facing, gauge);
        }

        /// <summary>
        /// v0.9: 게이지 대상(UIPlayerTag)은 SandboxDirector가 임시 캐릭터 'Player (Avatar)'에 붙임.
        /// 여기서는 게이지 높이·간격만 캐릭터 그림 크기에 맞춘다 (캐릭터 가운데 높이, 그림 옆으로 4px).
        /// </summary>
        static void FitGaugeToAvatar()
        {
            if (!EditorApplication.isPlaying) return;
            var avatar = GameObject.Find("Player (Avatar)");
            var gauge = Find<PlayerSideGaugeView>();
            if (avatar == null || gauge == null) return;
            var rend = avatar.GetComponentInChildren<Renderer>();
            if (rend == null) return;
            Vector3 center = rend.bounds.center;
            gauge.worldOffset = new Vector3(0f, center.y - avatar.transform.position.y, 0f);
            var cam = Camera.main;
            var canvas = UIManager.Instance != null ? UIManager.Instance.GetLayer(UILayer.HUD) : null;
            if (cam != null && canvas != null)
            {
                float halfPx = Mathf.Abs(cam.WorldToScreenPoint(new Vector3(rend.bounds.max.x, center.y, center.z)).x
                                         - cam.WorldToScreenPoint(center).x) / Mathf.Max(1f, canvas.scaleFactor);
                gauge.sideDistance = Mathf.Round(halfPx) + 4f;
            }
        }

        // 8) 맵 전환: 다음 방(P)·재도전 때 화면이 0.35초 어두워졌다 밝아짐, 그동안 지도 위치·체력이 바뀜
        static async Task MapTransition()
        {
            var dir = Find<SandboxDirector>();
            FitGaugeToAvatar();
            await Wait(1.0f);
            dir.GoNextRoom(); await Wait(1.8f);
            dir.GoNextRoom(); await Wait(1.8f);
            P.ChangeHP(-40); await Wait(1.2f);
            GameUI.Screens.ShowDeath(); await Wait(2.2f);
            var death = Find<DeathScreen>();
            if (death != null && death.retryButton != null) death.retryButton.onClick.Invoke(); // 재도전 버튼과 같음
            else { UIManager.Instance.CloseAll(); UIRequests.RaiseRetry(); }
            await Wait(2.6f);
        }

        // 9) 임시 캐릭터 옆 게이지: 피격 → 배터리 칸 줄어듦, Shift 대시 2칸 사용 → 하나씩 다시 참, 가득 차면 사라짐
        static async Task SideGaugeOnAvatar()
        {
            FitGaugeToAvatar();
            await Wait(1.0f);
            P.ChangeHP(-20); await Wait(1.4f);
            P.Dash(); await Wait(0.5f);
            P.Dash(); await Wait(0.6f);
            P.Dash(); await Wait(2.0f);           // 다 써서 실패 → 0.8초마다 하나씩 깜빡이며 참
            P.ChangeHP(-35); await Wait(1.8f);
            P.ChangeHP(-25); await Wait(2.0f);    // 저체력 빨강 깜빡임
            P.ChangeHP(80); await Wait(3.6f);     // 가득 + 대시 준비 → 2.5초 뒤 사라짐
        }

        static void Face(Transform facing, float dirX) => facing.rotation = Quaternion.LookRotation(dirX > 0f ? Vector3.right : Vector3.left);

        // 6) 플레이어 옆 세로 배터리 체력: 피격(하양→빨강→빈칸), 노랑·빨강 깜빡임, 회복 초록, 방향 바꾸면 반대쪽으로 바로
        static async Task SideBattery()
        {
            var (facing, _) = SetupSideGauge();
            try
            {
                await Wait(1.2f);
                P.ChangeHP(-20); await Wait(1.6f);
                Face(facing, -1f); await Wait(1.0f);
                Face(facing, 1f); await Wait(1.0f);
                P.ChangeHP(-25); await Wait(1.8f);
                Face(facing, -1f); await Wait(0.8f);
                P.ChangeHP(-30); await Wait(2.6f);
                P.ChangeHP(35); await Wait(1.6f);
                P.ChangeHP(40); await Wait(3.4f);   // 가득 + 대시 준비 → 2.5초 뒤 사라짐
            }
            finally { UnityEngine.Object.Destroy(facing.gameObject); }
        }

        // 7) 대시 동그라미: 쓰면 사라짐, 0.6초 뒤 0.5초 깜빡이며 다시 참, 공중 대시는 착지 전까지 없음
        static async Task SideDash()
        {
            var (facing, _) = SetupSideGauge();
            var dash = new DemoDash();
            GameUI.Bind(dash);
            try
            {
                P.ChangeHP(-10); await Wait(1.4f);
                dash.Use(); await Wait(1.6f);
                Face(facing, -1f); dash.Use(); await Wait(1.6f);
                Face(facing, 1f); dash.Use(); await Wait(0.7f);
                dash.Use(); await Wait(1.6f);           // 다 차자마자 다시 사용
                dash.Use(inAir: true); await Wait(1.8f); // 공중 대시 → 착지 전까지 없음
                dash.AirLocked = false; await Wait(1.6f); // 착지 → 깜빡이며 다시 참
                P.ChangeHP(10); await Wait(3.4f);
            }
            finally
            {
                GameUI.Unbind(dash);
                UnityEngine.Object.Destroy(facing.gameObject);
            }
        }
    }
}
