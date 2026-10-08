using System;
using System.Collections.Generic;

namespace OZ.UI.Contracts
{
    /// <summary>스킬 트리 창이 노드를 그릴 때 쓰는 상태</summary>
    public enum SkillNodeState
    {
        /// <summary>선행 노드·레벨 부족 등으로 아직 못 찍음</summary>
        Locked = 0,
        /// <summary>지금 찍을 수 있음</summary>
        Available = 1,
        /// <summary>찍음</summary>
        Unlocked = 2,
        /// <summary>택1 그룹에서 다른 쪽을 골라서 막힘 (초기화해야 바꿀 수 있음)</summary>
        Blocked = 3,
    }

    /// <summary>
    /// 스킬 트리 규칙 계산기 (UI 없음, MonoBehaviour 아님).
    /// 플레이어 담당은 이걸 하나 들고 ISkillTreeSource를 얇게 감싸면 된다 — 예시: Samples/DummyPlayer.
    ///
    ///   var tree = new SkillTreeState(skillTreeData);
    ///   tree.Unlock("q_sword", free: true);           // 시작 스킬 (초기화해도 남음)
    ///   if (tree.CanUnlock(id, level, points, out var why)) { points -= tree.Tree.Find(id).cost; tree.Unlock(id); }
    ///   points += tree.Reset();                       // 초기화 → 쓴 포인트 돌려받기
    ///   SkillData q = tree.GetSkill(SkillSlot.Q);  int rank = tree.GetRank(SkillSlot.Q);
    ///   float hpBonus = tree.GetStat("max_hp_pct");
    /// </summary>
    public sealed class SkillTreeState
    {
        readonly HashSet<string> _unlocked = new HashSet<string>();
        readonly HashSet<string> _free = new HashSet<string>();

        public SkillTreeData Tree { get; }
        public IReadOnlyCollection<string> Unlocked => _unlocked;

        /// <summary>해금·초기화로 상태가 바뀔 때</summary>
        public event Action Changed;

        public SkillTreeState(SkillTreeData tree) { Tree = tree; }

        public bool IsUnlocked(string id) => !string.IsNullOrEmpty(id) && _unlocked.Contains(id);
        public bool IsFree(string id) => !string.IsNullOrEmpty(id) && _free.Contains(id);

        /// <summary>택1 그룹의 다른 노드가 이미 찍혀 있는지</summary>
        public bool IsBlockedByChoice(SkillTreeNode node)
        {
            if (node == null || string.IsNullOrEmpty(node.exclusiveGroup) || Tree == null) return false;
            foreach (var other in Tree.nodes)
                if (other != null && other != node && other.exclusiveGroup == node.exclusiveGroup && IsUnlocked(other.id))
                    return true;
            return false;
        }

        /// <summary>찍을 수 있는지. 못 찍으면 reason에 창에 보일 짧은 문구.</summary>
        public bool CanUnlock(string id, int level, int points, out string reason)
        {
            var n = Tree != null ? Tree.Find(id) : null;
            if (n == null) { reason = "없는 노드"; return false; }
            if (IsUnlocked(id)) { reason = "해금됨"; return false; }
            if (IsBlockedByChoice(n)) { reason = "다른 쪽 선택됨"; return false; }
            if (n.requires != null)
                foreach (var req in n.requires)
                    if (!IsUnlocked(req))
                    {
                        var r = Tree.Find(req);
                        reason = (r != null ? r.DisplayName : req) + " 먼저";
                        return false;
                    }
            if (level < n.requiredLevel) { reason = $"Lv.{n.requiredLevel} 필요"; return false; }
            if (points < n.cost) { reason = "포인트 부족"; return false; }
            reason = null;
            return true;
        }

        public SkillNodeState GetState(string id, int level, int points)
        {
            if (IsUnlocked(id)) return SkillNodeState.Unlocked;
            var n = Tree != null ? Tree.Find(id) : null;
            if (n != null && IsBlockedByChoice(n)) return SkillNodeState.Blocked;
            return CanUnlock(id, level, points, out _) ? SkillNodeState.Available : SkillNodeState.Locked;
        }

        /// <summary>규칙 검사 없이 찍는다 (검사·포인트 차감은 호출하는 쪽). free = 시작 스킬처럼 초기화해도 남는 노드.</summary>
        public void Unlock(string id, bool free = false)
        {
            if (Tree == null || Tree.Find(id) == null) return;
            bool added = _unlocked.Add(id);
            if (free) _free.Add(id);
            if (added) Changed?.Invoke();
        }

        /// <summary>찍은 노드에 쓴 포인트 합 (free 노드 제외)</summary>
        public int SpentPoints
        {
            get
            {
                int sum = 0;
                foreach (var id in _unlocked)
                {
                    if (_free.Contains(id)) continue;
                    var n = Tree.Find(id);
                    if (n != null) sum += n.cost;
                }
                return sum;
            }
        }

        /// <summary>free 노드만 남기고 전부 되돌린다. 돌려줄 포인트를 반환.</summary>
        public int Reset()
        {
            int refund = SpentPoints;
            int removed = _unlocked.RemoveWhere(id => !_free.Contains(id));
            if (removed > 0) Changed?.Invoke();
            return refund;
        }

        /// <summary>free 포함 전부 비운다 (새 게임·계열 변경)</summary>
        public void Clear()
        {
            if (_unlocked.Count == 0 && _free.Count == 0) return;
            _unlocked.Clear();
            _free.Clear();
            Changed?.Invoke();
        }

        /// <summary>이 버튼에 배정된 스킬 (Skill 노드를 안 찍었으면 null)</summary>
        public SkillData GetSkill(SkillSlot slot)
        {
            if (Tree == null) return null;
            foreach (var n in Tree.nodes)
                if (n != null && n.kind == SkillNodeKind.Skill && n.slot == slot && n.skill != null && IsUnlocked(n.id))
                    return n.skill;
            return null;
        }

        /// <summary>배정된 스킬의 단계 (0 = 미배정)</summary>
        public int GetRank(SkillSlot slot)
        {
            var skill = GetSkill(slot);
            if (skill == null) return 0;
            int rank = 0;
            foreach (var n in Tree.nodes)
                if (n != null && n.kind != SkillNodeKind.Passive && n.skill == skill && IsUnlocked(n.id) && n.grantsRank > rank)
                    rank = n.grantsRank;
            return rank;
        }

        /// <summary>
        /// 이 버튼에서 다음에 찍을 노드 (ISkillSource.TryInvest 호환용).
        /// 배정된 스킬이 있으면 그 스킬의 다음 단계, 없으면 fallback 스킬의 Skill 노드.
        /// </summary>
        public SkillTreeNode NextNodeFor(SkillSlot slot, SkillData fallback)
        {
            if (Tree == null) return null;
            var skill = GetSkill(slot);
            if (skill == null) return Tree.FindSkillNode(fallback);

            SkillTreeNode best = null;
            foreach (var n in Tree.nodes)
            {
                if (n == null || n.kind != SkillNodeKind.Upgrade || n.skill != skill || IsUnlocked(n.id)) continue;
                if (best == null || n.grantsRank < best.grantsRank) best = n;
            }
            return best;
        }

        /// <summary>찍은 패시브의 statValue 합</summary>
        public float GetStat(string statKey)
        {
            if (Tree == null || string.IsNullOrEmpty(statKey)) return 0f;
            float sum = 0f;
            foreach (var n in Tree.nodes)
                if (n != null && n.kind == SkillNodeKind.Passive && n.statKey == statKey && IsUnlocked(n.id))
                    sum += n.statValue;
            return sum;
        }
    }
}
