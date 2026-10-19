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
    /// <summary>지도 탭: 노드 4개 (1스테이지 → 1보스 → 2스테이지 → 2보스). 해금된 노드를 골라 이동, 잠긴 노드는 조건 표시</summary>
    [AddComponentMenu("OZ/UI/Status/Status Map Page")]
    public class StatusMapPage : StatusPage
    {
        [SerializeField] internal RectTransform row;
        [SerializeField] internal GrowthCardView nodeTemplate;
        [SerializeField] internal TMP_Text emptyText;
        [SerializeField] internal Color bossColor = new Color(1f, 0.45f, 0.45f);
        [SerializeField] internal Color fieldColor = new Color(0.55f, 0.85f, 1f);
        [SerializeField] internal Color clearedColor = new Color(0.55f, 1f, 0.65f);

        CardPool _pool;
        int _selected;
        internal override string Hint => "[←→] 고르기   [Enter] 이동";

        CardPool Pool
        {
            get
            {
                if (_pool != null) return _pool;
                _pool = new CardPool(nodeTemplate, row) { Columns = 4 };
                _pool.OnPicked = v => { if (v.Index != _selected) { _selected = v.Index; Refresh(); } };
                _pool.OnAction = v => { _selected = v.Index; Travel(); };
                return _pool;
            }
        }

        internal override void Focus() => Pool.Focus(_selected);

        internal override void OnShow()
        {
            var map = Owner != null ? Owner.StageMap : null;
            if (map == null) return;
            for (int i = 0; i < map.NodeCount; i++) if (map.GetNode(i).current) { _selected = i; break; }
        }

        internal override void Refresh()
        {
            var map = Owner != null ? Owner.StageMap : null;
            int n = map != null ? map.NodeCount : 0;
            Pool.Columns = Mathf.Max(1, n);
            Pool.Ensure(n);
            if (emptyText != null) emptyText.gameObject.SetActive(n == 0);
            for (int i = 0; i < n; i++)
            {
                var node = map.GetNode(i);
                Color c = !node.unlocked ? StatusColors.Dim : node.cleared ? clearedColor : node.isBoss ? bossColor : fieldColor;
                string state = node.current ? "현재 위치" : !node.unlocked ? "잠김" : node.cleared ? "완료" : "해금";
                string detail = node.unlocked ? node.mainGoal : "조건: " + node.lockCondition;
                string action = node.current ? "현재 위치" : node.unlocked ? "이동" : "잠김";
                Pool.Cards[i].Set(null, node.isBoss ? "B" : (i + 1).ToString(), c, node.displayName,
                    (node.isBoss ? "보스 구역" : "일반 필드") + " · " + state, detail, 0, 0, action, node.unlocked && !node.current, i == _selected);
            }
        }

        void Travel()
        {
            var map = Owner != null ? Owner.StageMap : null;
            if (map == null || _selected >= map.NodeCount) return;
            var node = map.GetNode(_selected);
            if (node.current) { UISfx.Play(UISound.Error); Owner.ShowMessage("이미 이곳에 있다", false); return; }
            if (!node.unlocked) { UISfx.Play(UISound.Error); Owner.ShowMessage("잠김 — " + node.lockCondition, false); Pool.Shake(_selected); return; }
            if (!map.TryTravel(node.id)) { UISfx.Play(UISound.Error); Owner.ShowMessage("지금은 이동할 수 없다", false); return; }
            UISfx.Play(UISound.Confirm);
            Owner.Close(); // 이동 연출(페이드)은 구현 쪽 GameUI.Flow.Transition
        }
    }
}
