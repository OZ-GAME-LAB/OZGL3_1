using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>보스 체력바/등장 연출용 데이터.</summary>
    [CreateAssetMenu(menuName = "OZ/UI/Boss Data", fileName = "Boss_")]
    public class BossData : ScriptableObject
    {
        public string id;
        public string displayName;
        [Tooltip("이름 위/아래 칭호 (예: 게이트의 파수꾼)")]
        public string title;

        [Tooltip("페이즈가 바뀌는 HP 비율 (내림차순, 예: 0.5). 바에 눈금으로 표시")]
        public float[] phaseThresholds = { 0.5f };

        [Header("등장 연출")]
        public bool playIntro = true;
        [Tooltip("상하 레터박스 사용")]
        public bool useLetterbox = true;
        [Tooltip("이름 배너 유지 시간(초)")]
        [Min(0f)] public float bannerHold = 1.0f;
        [Tooltip("체력바 0→100% 채우는 시간(초)")]
        [Min(0f)] public float barFillDuration = 0.8f;

        [Tooltip("에셋 BossBarA~D 중 사용할 테마 번호 0~3")]
        [Range(0, 3)] public int barTheme;
    }
}
