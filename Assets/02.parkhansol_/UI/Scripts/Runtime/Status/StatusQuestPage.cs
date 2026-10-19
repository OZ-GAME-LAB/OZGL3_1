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
    /// <summary>퀘스트 탭: 지금 메인·서브 목표와 진행·보상 (GameUI.Quest 목록 그대로)</summary>
    [AddComponentMenu("OZ/UI/Status/Status Quest Page")]
    public class StatusQuestPage : StatusPage
    {
        [SerializeField] internal RectTransform list;
        [SerializeField] internal QuestEntryView rowTemplate;
        [SerializeField] internal TMP_Text emptyText;

        readonly List<QuestEntryView> _rows = new List<QuestEntryView>();
        internal override string Hint => "보상은 처음 완료할 때 자동 지급";

        void Awake() { if (rowTemplate != null) rowTemplate.gameObject.SetActive(false); }

        internal override void Refresh()
        {
            var q = Owner != null ? Owner.Quests : null;
            int n = q != null ? q.Count : 0;
            while (_rows.Count < n && rowTemplate != null && list != null)
            {
                var r = Instantiate(rowTemplate, list);
                _rows.Add(r);
            }
            // 메인 먼저, 그다음 서브 (같은 구분 안에서는 HUD 순서)
            var order = new List<int>();
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < n; i++)
                {
                    var info = q.GetAt(i);
                    bool has = q.TryGetReward(info.Id, out _, out bool main);
                    if ((pass == 0) == (main || !has)) order.Add(i);
                }
            for (int i = 0; i < _rows.Count; i++)
            {
                bool on = i < order.Count;
                _rows[i].gameObject.SetActive(on);
                if (!on) continue;
                var info = q.GetAt(order[i]);
                bool has = q.TryGetReward(info.Id, out int reward, out bool main);
                _rows[i].Show(info, has, reward, main);
            }
            if (emptyText != null) emptyText.gameObject.SetActive(n == 0);
        }
    }
}
