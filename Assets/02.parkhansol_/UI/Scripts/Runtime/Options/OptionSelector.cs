using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>옵션 한 줄: ◀ 값 ▶ (해상도 등). 선택된 상태에서 좌우 키/패드로 바꿈, 화살표 클릭도 가능</summary>
    [AddComponentMenu("OZ/UI/Options/Option Selector")]
    public class OptionSelector : Selectable
    {
        [SerializeField] internal TMP_Text valueText;
        [SerializeField] internal Button prevButton;
        [SerializeField] internal Button nextButton;
        [SerializeField] internal bool wrap = true;

        readonly List<string> _options = new List<string>();
        int _index;

        public event Action<int> IndexChanged;
        public int Index => _index;
        public int Count => _options.Count;

        protected override void Awake()
        {
            base.Awake();
            if (prevButton != null) prevButton.onClick.AddListener(() => Step(-1));
            if (nextButton != null) nextButton.onClick.AddListener(() => Step(1));
        }

        public void SetOptions(IList<string> options, int index)
        {
            _options.Clear();
            if (options != null) _options.AddRange(options);
            SetIndexWithoutNotify(index);
        }

        public void SetIndexWithoutNotify(int index)
        {
            _index = _options.Count == 0 ? 0 : Mathf.Clamp(index, 0, _options.Count - 1);
            if (valueText != null) valueText.text = _options.Count == 0 ? "-" : _options[_index];
        }

        public void Step(int dir)
        {
            if (_options.Count == 0) return;
            int next = _index + dir;
            if (wrap) next = (next % _options.Count + _options.Count) % _options.Count;
            else next = Mathf.Clamp(next, 0, _options.Count - 1);
            if (next == _index) return;
            SetIndexWithoutNotify(next);
            if (valueText != null) ((RectTransform)valueText.transform).Punch(0.12f, 0.15f);
            IndexChanged?.Invoke(_index);
        }

        public override void OnMove(AxisEventData e)
        {
            if (e.moveDir == MoveDirection.Left) { Step(-1); e.Use(); return; }
            if (e.moveDir == MoveDirection.Right) { Step(1); e.Use(); return; }
            base.OnMove(e);
        }
    }
}
