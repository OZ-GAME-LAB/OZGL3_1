using System;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [플레이어 담당 구현] 노드식 스킬 트리 (K 창).
    /// 규칙은 SkillTreeState에 다 들어 있어서, 보통은 그걸 감싸기만 하면 된다 (Samples/DummyPlayer 참고).
    /// 기존 ISkillSource(Q/E/R 아이콘·쿨타임·단계)는 그대로 쓰고, 단계 값은 트리에서 계산한다.
    /// </summary>
    public interface ISkillTreeSource
    {
        SkillTreeData Tree { get; }

        bool IsUnlocked(string nodeId);
        /// <summary>찍을 수 있는지. 못 찍으면 reason에 창에 보일 문구 (예: "Lv.4 필요", "포인트 부족")</summary>
        bool CanUnlock(string nodeId, out string reason);
        /// <summary>포인트를 쓰고 노드를 찍는다. 성공 시 TreeChanged + (스킬 노드면) ISkillSource.SkillChanged</summary>
        bool TryUnlock(string nodeId);

        /// <summary>초기화 가능 여부 (예: 쓴 포인트가 없으면 불가)</summary>
        bool CanResetTree(out string reason);
        /// <summary>시작 스킬만 남기고 되돌리고 포인트를 돌려준다</summary>
        bool TryResetTree();

        /// <summary>해금·초기화 등으로 트리 표시가 바뀔 때</summary>
        event Action TreeChanged;
    }
}
