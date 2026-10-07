using System.Collections.Generic;
using OZ.UI.Contracts;
using UnityEditor;
using UnityEngine;

namespace OZ.UI.EditorTools
{
    /// <summary>OZ > UI > Validate Data : 데이터 에셋 오류 검사 (중복 id, 아이콘 누락, 슬롯 구성 등)</summary>
    internal static class OZDataValidator
    {
        [MenuItem("OZ/UI/Validate Data", priority = 20)]
        public static void Run()
        {
            int errors = 0, warnings = 0;
            void Err(Object o, string msg) { errors++; Debug.LogError("[OZ Validate] " + msg, o); }
            void Warn(Object o, string msg) { warnings++; Debug.LogWarning("[OZ Validate] " + msg, o); }

            var ids = new Dictionary<string, Object>();
            void CheckId(Object o, string id)
            {
                if (string.IsNullOrWhiteSpace(id)) { Err(o, $"{o.name}: id가 비어 있음"); return; }
                if (ids.TryGetValue(id, out var other)) Err(o, $"{o.name}: id '{id}' 중복 ({other.name})");
                else ids[id] = o;
            }

            foreach (var s in All<SkillData>())
            {
                CheckId(s, s.id);
                if (s.icon == null) Warn(s, $"{s.name}: 아이콘 없음");
                if (s.ranks == null || s.ranks.Count < s.maxRank) Err(s, $"{s.name}: 단계 정보 {s.ranks?.Count ?? 0}개 < maxRank {s.maxRank}");
            }
            foreach (var c in All<ClassData>())
            {
                if (c.skills == null || c.skills.Length != 3) { Err(c, $"{c.name}: 스킬은 Q/E/R 3개여야 함"); continue; }
                for (int i = 0; i < 3; i++)
                {
                    var s = c.skills[i];
                    if (s == null) { Err(c, $"{c.name}: {(SkillSlot)i} 스킬 비어 있음"); continue; }
                    if (s.slot != (SkillSlot)i) Err(c, $"{c.name}: {(SkillSlot)i} 칸에 {s.slot} 스킬({s.name})");
                    if (s.playerClass != c.playerClass) Err(c, $"{c.name}: {s.name} 계열 불일치");
                }
            }
            var quick = new Dictionary<int, ItemData>();
            foreach (var it in All<ItemData>())
            {
                CheckId(it, it.id);
                if (it.icon == null) Warn(it, $"{it.name}: 아이콘 없음");
                if (it.slotIndex >= 0)
                {
                    if (quick.TryGetValue(it.slotIndex, out var other)) Warn(it, $"{it.name}: 퀵슬롯 {it.slotIndex + 1} 중복 ({other.name})");
                    else quick[it.slotIndex] = it;
                }
                if (it.IsBuff && it.effectType == ItemEffectType.Heal) Warn(it, $"{it.name}: 회복인데 지속시간이 있음");
            }
            foreach (var b in All<BossData>()) CheckId(b, b.id);
            foreach (var m in All<MapData>())
            {
                var roomIds = new HashSet<string>();
                foreach (var r in m.rooms)
                    if (!roomIds.Add(r.id)) Err(m, $"{m.name}: 방 id '{r.id}' 중복");
                foreach (var l in m.links)
                    if (!roomIds.Contains(l.fromRoom) || !roomIds.Contains(l.toRoom)) Err(m, $"{m.name}: 연결 {l.fromRoom}→{l.toRoom} 방 없음");
            }
            foreach (var t in All<SkillTreeData>())
            {
                foreach (var e in t.Validate()) Err(t, $"{t.name}: {e}");
                Debug.Log($"[OZ Validate] {t.name}: 노드 {t.nodes.Count}개, 전부 찍는 데 {t.MaxSpendablePoints()}P (시작 스킬 포함)", t);
            }
            foreach (var d in All<DialogueData>())
                for (int i = 0; i < d.lines.Count; i++)
                    if (d.lines[i].speaker == null) Warn(d, $"{d.name}: {i + 1}번째 줄 화자 없음");

            string summary = $"[OZ Validate] 완료 — 오류 {errors}, 경고 {warnings}";
            if (errors > 0) Debug.LogError(summary); else Debug.Log(summary);
        }

        static IEnumerable<T> All<T>() where T : Object
        {
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null) yield return a;
            }
        }
    }
}
