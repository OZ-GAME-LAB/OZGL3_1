using System;
using System.Collections.Generic;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>스탯 탭: 4스탯 지금 효과와 구매 단계 (구매는 상점 탭) + 전투 수치 요약</summary>
    [AddComponentMenu("OZ/UI/Status/Status Stats Page")]
    public class StatusStatsPage : StatusPage
    {
        [SerializeField] internal RectTransform grid;
        [SerializeField] internal GrowthCardView cardTemplate;
        [SerializeField] internal TMP_Text summaryText;
        [SerializeField] internal TMP_Text emptyText;

        CardPool _pool;
        internal override string Hint => "스탯 구매는 [3] 상점";

        CardPool Pool => _pool ?? (_pool = new CardPool(cardTemplate, grid) { Columns = 2 });

        internal override void Refresh()
        {
            var stats = Owner != null ? Owner.Stats : null;
            Pool.Ensure(stats != null ? 4 : 0);
            if (emptyText != null) emptyText.gameObject.SetActive(stats == null);
            if (stats == null) { if (summaryText != null) summaryText.text = ""; return; }

            for (int i = 0; i < 4; i++)
            {
                var s = (StatKind)i;
                int lv = stats.GetStatLevel(s), max = stats.GetStatMaxLevel(s);
                float v = stats.GetStatValue(s);
                Pool.Cards[i].Set(null, StatusText.StatName(s), StatusColors.Stat(s),
                    StatusText.StatName(s), StatusText.StatDesc(s), StatusText.StatValue(s, v) + (lv > 0 ? $"   (구매 {lv}회)" : ""),
                    lv, max, null, false, false);
            }

            if (summaryText != null)
            {
                string rank = UISources.Progression != null ? $"헌터 랭크 {UISources.Progression.Rank.ToDisplay()}   " : "";
                float hp = UISources.Health != null ? UISources.Health.MaxHP : 0f;
                summaryText.text = rank
                    + $"물리 공격력 {stats.GetStatValue(StatKind.Strength):0}   마법 공격력 {stats.GetStatValue(StatKind.Magic):0}"
                    + (hp > 0f ? $"   최대 HP {hp:0}" : "")
                    + $"   속도 +{stats.GetStatValue(StatKind.Agility) * 100f:0}%";
            }
        }
    }
}
