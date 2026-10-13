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
    ///   화면 + 유니티 소리만 (DemoCapture). 결과: 프로젝트/Temp/OZDemo/{번호_이름}.frames + .wav
    ///   (mp4 변환은 별도 — ffmpeg로 프레임 시각에 맞춰 합침)
    /// </summary>
    [InitializeOnLoad]
    internal static class OZDemoVideo
    {
        const string Flag = "OZ_UI_DEMO_PENDING";
        public static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "OZDemo"));

        static OZDemoVideo() => EditorApplication.playModeStateChanged += OnPlayMode;

        [MenuItem("OZ/UI/Record Demo Videos", priority = 22)]
        static void Start()
        {
            if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; return; }
            if (!OZSandboxBuilder.IsDone) { Debug.LogError("[OZ Demo] UI_Sandbox 씬이 없습니다."); return; }
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
                await Clip("01_체력바색_저체력경고_회복", Health);
                await Clip("02_쿨타임_사용불가_자리변경", Cooldowns);
                await Clip("03_버프테두리_번갈아점멸", Buffs);
                await Clip("04_랭크승급_팡파레", RankUp);
                await Clip("05_지역이름_상호작용F_화면밖화살표", World);
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
    }
}
