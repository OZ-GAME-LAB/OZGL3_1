using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>노드 종류. 모양도 이걸로 정해진다 (스킬 = 큰 칸, 강화 = 각진 칸, 패시브 = 둥근 칸).</summary>
    public enum SkillNodeKind
    {
        /// <summary>Q/E/R 버튼에 스킬을 배정하는 노드 (= 1단계). 같은 버튼끼리 exclusiveGroup으로 택1.</summary>
        Skill = 0,
        /// <summary>배정된 스킬의 단계 올리기 (grantsRank = 2, 3 …)</summary>
        Upgrade = 1,
        /// <summary>능력치 패시브 (statKey / statValue)</summary>
        Passive = 2,
    }

    [Serializable]
    public class SkillTreeNode
    {
        [Tooltip("고유 ID (예: q_sword, q_sword_2, p_hp)")]
        public string id;
        public SkillNodeKind kind;

        [Header("스킬 / 강화 노드")]
        public SkillSlot slot;
        public SkillData skill;
        [Tooltip("이 노드를 찍으면 되는 단계. Skill 노드 = 1, Upgrade = 2·3 …")]
        [Min(1)] public int grantsRank = 1;

        [Header("패시브 노드")]
        [Tooltip("플레이어 담당이 읽을 능력치 키 (예: max_hp_pct, crit_pct, cooldown_pct, move_pct)")]
        public string statKey;
        public float statValue;

        [Header("표시 (비우면 SkillData에서 가져옴)")]
        public string displayName;
        [TextArea(2, 4)] public string description;
        public Sprite icon;

        [Header("배치 / 규칙")]
        [Tooltip("격자 좌표 (0,0 = 왼쪽 위). 칸 크기는 스킬 트리 창에서 정한다")]
        public Vector2Int gridPos;
        [Tooltip("먼저 찍어야 하는 노드 id (모두 필요)")]
        public List<string> requires = new List<string>();
        [Tooltip("같은 그룹 안에서는 하나만 찍을 수 있다 (예: Q 버튼의 검술/마법 택1). 비우면 제한 없음")]
        public string exclusiveGroup;
        [Min(0)] public int cost = 1;
        [Min(1)] public int requiredLevel = 1;

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(displayName)) return displayName;
                if (skill != null) return kind == SkillNodeKind.Upgrade ? $"{skill.displayName} {grantsRank}단계" : skill.displayName;
                return id;
            }
        }

        public Sprite Icon => icon != null ? icon : skill != null ? skill.icon : null;

        /// <summary>창 설명 칸에 보일 문장. description이 있으면 그대로, 없으면 SkillData 단계 요약.</summary>
        public string Describe()
        {
            if (!string.IsNullOrEmpty(description)) return description;
            if (skill == null) return "";
            var r = skill.GetRank(grantsRank);
            string line = $"{grantsRank}단계: {r.summary}  (대기 {r.cooldown:0.#}초)";
            return kind == SkillNodeKind.Skill ? skill.description + "\n" + line : line;
        }
    }

    /// <summary>창에만 보이는 글자 (버튼 이름, '택1' 같은 안내). 격자 좌표라 소수도 된다.</summary>
    [Serializable]
    public struct SkillTreeLabel
    {
        public string text;
        public Vector2 gridPos;
        public bool small;
    }

    /// <summary>
    /// 노드식 스킬 트리 데이터. 이 에셋만 채우면 스킬 트리 창이 알아서 그린다.
    /// 규칙 계산은 SkillTreeState (플레이어 담당이 그대로 가져다 써도 된다).
    /// </summary>
    [CreateAssetMenu(menuName = "OZ/UI/Skill Tree", fileName = "SkillTree_")]
    public class SkillTreeData : ScriptableObject
    {
        public string treeId;
        public string displayName = "스킬 트리";
        public List<SkillTreeNode> nodes = new List<SkillTreeNode>();
        public List<SkillTreeLabel> labels = new List<SkillTreeLabel>();

        public SkillTreeNode Find(string id)
        {
            if (string.IsNullOrEmpty(id) || nodes == null) return null;
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i] != null && nodes[i].id == id) return nodes[i];
            return null;
        }

        /// <summary>이 스킬을 버튼에 배정하는 Skill 노드</summary>
        public SkillTreeNode FindSkillNode(SkillData skill)
        {
            if (skill == null || nodes == null) return null;
            foreach (var n in nodes)
                if (n != null && n.kind == SkillNodeKind.Skill && n.skill == skill) return n;
            return null;
        }

        /// <summary>모든 노드를 다 찍는 데 드는 포인트 (택1 그룹은 가장 비싼 쪽 하나만 계산)</summary>
        public int MaxSpendablePoints()
        {
            int total = 0;
            var groupBest = new Dictionary<string, int>();
            foreach (var n in nodes)
            {
                if (n == null) continue;
                if (string.IsNullOrEmpty(n.exclusiveGroup)) { if (n.kind == SkillNodeKind.Passive) total += n.cost; continue; }
                int branch = BranchCost(n);
                groupBest.TryGetValue(n.exclusiveGroup, out var best);
                if (branch > best) groupBest[n.exclusiveGroup] = branch;
            }
            foreach (var v in groupBest.Values) total += v;
            return total;
        }

        int BranchCost(SkillTreeNode root)
        {
            int sum = root.cost;
            foreach (var n in nodes)
                if (n != null && n.kind == SkillNodeKind.Upgrade && n.skill == root.skill && n.skill != null) sum += n.cost;
            return sum;
        }

        /// <summary>데이터 오류 목록 (비어 있으면 정상). OZ > UI > Validate Data 와 런타임 경고에서 같이 쓴다.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            var ids = new HashSet<string>();
            var cells = new HashSet<Vector2Int>();
            if (nodes == null) return errors;

            foreach (var n in nodes)
            {
                if (n == null) { errors.Add("빈 노드 칸이 있음"); continue; }
                if (string.IsNullOrEmpty(n.id)) errors.Add("id가 비어 있는 노드가 있음");
                else if (!ids.Add(n.id)) errors.Add($"중복 id: {n.id}");
                if (!cells.Add(n.gridPos)) errors.Add($"{n.id}: 같은 칸 {n.gridPos} 에 노드가 2개 이상");

                switch (n.kind)
                {
                    case SkillNodeKind.Skill:
                        if (n.skill == null) errors.Add($"{n.id}: Skill 노드인데 SkillData가 없음");
                        else if (n.skill.slot != n.slot) errors.Add($"{n.id}: 슬롯 {n.slot} ≠ SkillData 슬롯 {n.skill.slot}");
                        break;
                    case SkillNodeKind.Upgrade:
                        if (n.skill == null) errors.Add($"{n.id}: Upgrade 노드인데 SkillData가 없음");
                        else if (n.grantsRank > n.skill.maxRank) errors.Add($"{n.id}: {n.grantsRank}단계 > 최대 {n.skill.maxRank}단계");
                        if (n.requires == null || n.requires.Count == 0) errors.Add($"{n.id}: Upgrade 노드는 선행 노드가 필요");
                        break;
                    case SkillNodeKind.Passive:
                        if (string.IsNullOrEmpty(n.statKey)) errors.Add($"{n.id}: Passive 노드인데 statKey가 없음");
                        break;
                }

                if (n.requires != null)
                    foreach (var req in n.requires)
                        if (Find(req) == null) errors.Add($"{n.id}: 없는 선행 노드 '{req}'");
            }

            if (HasCycle()) errors.Add("선행 조건이 순환함");
            return errors;
        }

        bool HasCycle()
        {
            var state = new Dictionary<string, int>(); // 1 = 방문 중, 2 = 끝
            bool Visit(SkillTreeNode n)
            {
                if (n == null || string.IsNullOrEmpty(n.id)) return false;
                state.TryGetValue(n.id, out var s);
                if (s == 1) return true;
                if (s == 2) return false;
                state[n.id] = 1;
                if (n.requires != null)
                    foreach (var req in n.requires)
                        if (Visit(Find(req))) return true;
                state[n.id] = 2;
                return false;
            }
            foreach (var n in nodes)
                if (Visit(n)) return true;
            return false;
        }
    }
}
