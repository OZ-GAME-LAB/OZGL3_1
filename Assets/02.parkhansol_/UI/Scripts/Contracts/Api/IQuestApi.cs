using System;

namespace OZ.UI.Contracts
{
    public enum QuestState
    {
        /// <summary>진행 중 — 흰 글자 + 진행 막대</summary>
        Active = 0,
        /// <summary>아직 못 함 — 흐리게 + 오른쪽에 조건 문구 (예: "게이트 후")</summary>
        Locked = 1,
        /// <summary>완료 — 초록 체크 + 취소선, 잠시 뒤 목록에서 빠짐</summary>
        Done = 2,
    }

    public readonly struct QuestInfo
    {
        public readonly string Id, Title, Hint;
        public readonly int Progress, Target, Order;
        public readonly QuestState State;
        public QuestInfo(string id, string title, int progress, int target, QuestState state, string hint, int order)
        { Id = id; Title = title; Progress = progress; Target = target; State = state; Hint = hint; Order = order; }
    }

    /// <summary>
    /// 좌상단 퀘스트 목록 (체력 블록 바로 아래, 최대 4줄).
    /// 게이트 봉쇄·보스 처치 줄은 IGateSource(GateUILink)가 연결돼 있으면 UI가 자동으로 채운다 (id "gate", "boss").
    ///
    ///   GameUI.Quest.Set("npc_talk", "역무원과 대화");                  // 진행 막대 없는 한 줄
    ///   GameUI.Quest.Set("kill", "감염체 처치", 0, 20, order: 5);         // 0 / 20 + 막대
    ///   GameUI.Quest.SetProgress("kill", 7);
    ///   GameUI.Quest.Complete("kill");                                    // 체크 → 잠시 뒤 사라짐
    ///   GameUI.Quest.Set("exit", "출구로 이동", state: QuestState.Locked, hint: "보스 후");
    /// </summary>
    public interface IQuestApi
    {
        /// <summary>추가 또는 덮어쓰기. target 0이면 진행 숫자·막대 없음. order 작을수록 위</summary>
        void Set(string id, string title, int progress = 0, int target = 0, QuestState state = QuestState.Active, string hint = null, int order = 0);
        void SetProgress(string id, int progress, int target = -1);
        void SetState(string id, QuestState state, string hint = null);
        void Complete(string id);
        void Remove(string id);
        void Clear();
        bool TryGet(string id, out QuestInfo info);

        // ── v1.0 상태창 퀘스트 탭 (기획서 4장: 메인 목표와 서브 목표의 진행·보상) ──
        /// <summary>보상 포인트와 메인/서브 구분. HUD 목록에는 안 보이고 상태창 퀘스트 탭에만 나온다</summary>
        void SetReward(string id, int rewardPoints, bool isMain = true);
        bool TryGetReward(string id, out int rewardPoints, out bool isMain);
        /// <summary>지금 목록에 있는 퀘스트 수 / 순서대로 읽기</summary>
        int Count { get; }
        QuestInfo GetAt(int index);
        /// <summary>추가·진행·완료·삭제·보상 변경</summary>
        event Action Changed;
    }
}
