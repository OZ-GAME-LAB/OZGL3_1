using System;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;

namespace OZ.UI
{
    public enum DamageSpark { None = 0, Normal = 1, Critical = 2 }

    /// <summary>피해 숫자 종류별 모양·움직임 (DamageFxController 인스펙터에서 조절)</summary>
    [Serializable]
    public class DamageNumberStyle
    {
        public DamageKind kind;
        public Color color = Color.white;
        [Tooltip("비우면 원본 템플릿 폰트. 크기 15는 Galmuri14, 12는 Galmuri11 권장")]
        public TMP_FontAsset font;
        [Tooltip("글자 크기 (Galmuri 픽셀폰트는 12/15의 정수배가 선명)")]
        public float fontSize = 12f;
        [Tooltip("시작 크기 배율 → 1로 튕기며 줄어듦")]
        public float popScale = 1.4f;
        [Tooltip("위로 떠오르는 거리 (UI px)")]
        public float rise = 20f;
        [Tooltip("전체 표시 시간")]
        public float duration = 0.7f;
        [Tooltip("좌우 흩어짐 (UI px)")]
        public float jitterX = 6f;
        [Tooltip("등장 직후 흔들림 세기 (0이면 없음)")]
        public float shake = 0f;
        [Tooltip("그림자 거리 (px). 2면 글자가 더 묵직해 보임")]
        public int shadowOffset = 1;
        [Tooltip("숫자 위 작은 글자 (예: 치명타). 비우면 없음")]
        public string label = "";
        public Color labelColor = new Color(1f, 0.55f, 0.2f);
        [Tooltip("숫자 앞 기호 (회복은 +)")]
        public string prefix = "";
        [Tooltip("기본 높이에 더하는 값. 치명타 계열은 위쪽 별도 줄, 지속 피해는 음수(아래)")]
        public float lane = 0f;
        [Tooltip("0보다 크면 이 시간 안에 같은 자리에 들어온 피해를 하나로 합침 (지속 피해용)")]
        public float mergeWindow = 0f;
        public DamageSpark spark = DamageSpark.None;
        [Tooltip("타격 이펙트 색 (흰색 = 원본 색)")]
        public Color sparkTint = Color.white;

        /// <summary>치명타 계열(위쪽 줄에 따로 쌓임)</summary>
        public bool IsCritLane => lane > 0f;

        public static DamageNumberStyle[] Defaults() => new[]
        {
            new DamageNumberStyle { kind = DamageKind.Normal, color = new Color(1f, 1f, 1f), fontSize = 12, popScale = 1.5f, rise = 18, duration = 0.65f,
                                    spark = DamageSpark.Normal },
            new DamageNumberStyle { kind = DamageKind.Critical, color = new Color(1f, 0.84f, 0.25f), fontSize = 15, popScale = 2.2f, rise = 26, duration = 0.95f, jitterX = 4, shake = 2f,
                                    label = "치명타", labelColor = new Color(1f, 0.62f, 0.2f), lane = 10f, spark = DamageSpark.Critical },
            // 약점·처치는 글자 없이 색·크기·두께로 구분: 약점 = 주황 굵은 글꼴 24px(2배), 처치 = 빨강 30px(2배) + 굵은 그림자
            new DamageNumberStyle { kind = DamageKind.Weakness, color = new Color(1f, 0.55f, 0.15f), fontSize = 24, popScale = 1.8f, rise = 26, duration = 1.0f, jitterX = 4, shake = 2.5f,
                                    shadowOffset = 2, lane = 10f, spark = DamageSpark.Critical, sparkTint = new Color(1f, 0.6f, 0.35f) },
            new DamageNumberStyle { kind = DamageKind.Finisher, color = new Color(1f, 0.27f, 0.27f), fontSize = 30, popScale = 2.0f, rise = 30, duration = 1.15f, jitterX = 2, shake = 3f,
                                    shadowOffset = 2, lane = 14f, spark = DamageSpark.Critical, sparkTint = new Color(1f, 0.45f, 0.45f) },
            new DamageNumberStyle { kind = DamageKind.DamageOverTime, color = new Color(0.78f, 0.55f, 1f), fontSize = 10, popScale = 1.2f, rise = 8, duration = 0.6f, jitterX = 3,
                                    lane = -14f, mergeWindow = 0.4f },
            new DamageNumberStyle { kind = DamageKind.PlayerHurt, color = new Color(1f, 0.33f, 0.35f), fontSize = 12, popScale = 1.3f, rise = 14, duration = 0.7f },
            new DamageNumberStyle { kind = DamageKind.Heal, color = new Color(0.45f, 1f, 0.55f), fontSize = 12, popScale = 1.2f, rise = 16, duration = 0.8f, prefix = "+" },
            new DamageNumberStyle { kind = DamageKind.Miss, color = new Color(0.65f, 0.7f, 0.8f), fontSize = 10, popScale = 1.1f, rise = 12, duration = 0.6f },
        };
    }
}
