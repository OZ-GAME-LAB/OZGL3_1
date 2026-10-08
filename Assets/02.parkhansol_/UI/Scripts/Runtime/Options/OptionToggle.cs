using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>옵션 한 줄: 이름 + 켜기/끄기 스위치 + "켜짐/꺼짐" 글자</summary>
    [AddComponentMenu("OZ/UI/Options/Option Toggle")]
    public class OptionToggle : MonoBehaviour
    {
        [SerializeField] internal Toggle toggle;
        [SerializeField] internal TMP_Text stateText;

        public event Action<bool> ValueChanged;
        public bool Value => toggle != null && toggle.isOn;
        public Selectable Selectable => toggle;

        void Awake()
        {
            if (toggle != null) toggle.onValueChanged.AddListener(v => { UpdateLabel(v); ValueChanged?.Invoke(v); });
        }

        public void SetWithoutNotify(bool v)
        {
            if (toggle != null) toggle.SetIsOnWithoutNotify(v);
            UpdateLabel(v);
        }

        void UpdateLabel(bool v)
        {
            if (stateText != null) stateText.text = v ? "켜짐" : "꺼짐";
        }
    }
}
