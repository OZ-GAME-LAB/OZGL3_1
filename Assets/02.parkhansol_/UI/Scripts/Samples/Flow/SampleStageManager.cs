using System;
using System.Collections;
using OZ.UI.Contracts;
using UnityEngine;

namespace OZ.UI.Samples
{
    /// <summary>
    /// [팀 설계 StageManager 모양 샘플] CurrentStage / StartStage() / ClearStage() / StartBossPhase().
    /// UI 연결 (각 메서드 안 [UI] 줄):
    ///   StartStage     → GameUI.Flow.StageIntro(n, 제목)       "STAGE 1 · 지하철역 승강장" 띠
    ///   StartBossPhase → GameUI.HUD.ShowGuide + GameUI.Boss.Show (보스 담당 쪽)
    ///   ClearStage     → GameUI.Screens.ShowStageClear(n, 랭크) → 띠가 끝나면 UIRequests.NextStage
    ///                    마지막 스테이지면 GameUI.Screens.ShowDemoEnd()
    /// </summary>
    [AddComponentMenu("OZ/UI/Samples/Flow/Sample Stage Manager")]
    public class SampleStageManager : MonoBehaviour
    {
        [Serializable]
        public class StageDef
        {
            public string title = "지하철역";
            public int gateTarget = 2;
            public int enemiesPerGate = 3;
        }

        [SerializeField] internal SampleGateManager gateManager;
        [SerializeField] internal SampleBoss boss;
        [SerializeField] internal Vector3 bossSpawn = new Vector3(0f, 0f, 0f);
        [SerializeField] internal DummyPlayer player;
        [SerializeField] internal StageDef[] stages =
        {
            new StageDef { title = "지하철역 승강장", gateTarget = 2, enemiesPerGate = 3 },
            new StageDef { title = "지하철역 환승통로", gateTarget = 3, enemiesPerGate = 4 },
        };

        public int CurrentStage { get; private set; }
        public int StageCount => stages.Length;
        public bool IsBossPhase { get; private set; }
        public event Action<int> StageCleared;

        Coroutine _bossDelay;

        void OnEnable() { if (gateManager != null) gateManager.AllGatesSealed += OnAllGatesSealed; }
        void OnDisable() { if (gateManager != null) gateManager.AllGatesSealed -= OnAllGatesSealed; }

        StageDef Def(int stage) => stages[Mathf.Clamp(stage - 1, 0, stages.Length - 1)];

        public void StartStage(int stage)
        {
            StopStage();
            CurrentStage = Mathf.Clamp(stage, 1, stages.Length);
            var def = Def(CurrentStage);
            GameUI.Flow.StageIntro(CurrentStage, def.title, $"게이트 {def.gateTarget}곳을 봉쇄하라"); // [UI] 페이드가 걷힌 뒤 자동 재생
            if (gateManager != null) gateManager.StartGatePhase(CurrentStage, def.gateTarget, def.enemiesPerGate);
        }

        /// <summary>진행 중인 게이트·보스 정리 (재시작·교체 전)</summary>
        public void StopStage()
        {
            if (_bossDelay != null) StopCoroutine(_bossDelay);
            _bossDelay = null;
            IsBossPhase = false;
            if (gateManager != null) gateManager.StopAll();
            if (boss != null) boss.Despawn();
        }

        void OnAllGatesSealed()
        {
            GameUI.HUD.ShowGuide("보스 구역이 열렸다", 2.5f); // [UI]
            _bossDelay = StartCoroutine(BossAfter(3f));
        }

        IEnumerator BossAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            _bossDelay = null;
            StartBossPhase();
        }

        public void StartBossPhase()
        {
            if (IsBossPhase) return;
            IsBossPhase = true;
            if (boss == null) { ClearStage(); return; }
            boss.Defeated -= OnBossDefeated;
            boss.Defeated += OnBossDefeated;
            boss.Spawn(bossSpawn, null); // 안에서 GameUI.Boss.Show(this, …)
        }

        void OnBossDefeated()
        {
            boss.Defeated -= OnBossDefeated;
            StartCoroutine(ClearAfter(2f)); // 격파 연출을 보고 나서
        }

        IEnumerator ClearAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            ClearStage();
        }

        public void ClearStage()
        {
            IsBossPhase = false;
            StageCleared?.Invoke(CurrentStage);
            if (player != null) player.PromoteRank();
            var rank = player != null ? player.Rank : HunterRank.F;
            if (CurrentStage >= stages.Length) GameUI.Screens.ShowDemoEnd();           // [UI] 마지막
            else GameUI.Screens.ShowStageClear(CurrentStage, rank);                    // [UI] 띠 → UIRequests.NextStage
        }
    }
}
