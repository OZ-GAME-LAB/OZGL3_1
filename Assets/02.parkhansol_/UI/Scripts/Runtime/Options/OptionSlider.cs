using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>옵션 한 줄: 이름 + 슬라이더(0~1) + 퍼센트. 키보드·패드 좌우로 10%씩</summary>
    [AddComponentMenu("OZ/UI/Options/Option Slider")]
    public class OptionSlider : MonoBehaviour
    {
        [SerializeField] internal Slider slider;
        [SerializeField] internal TMP_Text valueText;

        public event Action<float> ValueChanged;
        public float Value => slider != null ? slider.value : 0f;
        public Selectable Selectable => slider;

        void Awake()
        {
            if (slider == null) return;
            slider.minValue = 0f; slider.maxValue = 1f; slider.wholeNumbers = false;
            slider.onValueChanged.AddListener(v =>
            {
                float snapped = Mathf.Round(v * 20f) / 20f; // 5% 단위
                if (!Mathf.Approximately(snapped, v)) { slider.SetValueWithoutNotify(snapped); v = snapped; }
                UpdateLabel(v);
                ValueChanged?.Invoke(v);
            });
        }

        public void SetWithoutNotify(float v)
        {
            if (slider != null) slider.SetValueWithoutNotify(v);
            UpdateLabel(v);
        }

        void UpdateLabel(float v)
        {
            if (valueText != null) valueText.text = Mathf.RoundToInt(v * 100f).ToString();
        }
    }
}
