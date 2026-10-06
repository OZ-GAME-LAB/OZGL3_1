using OZ.UI.Contracts;
using TMPro;
using UnityEngine;

namespace OZ.UI
{
    /// <summary>레벨 · 경험치 바 · 헌터 랭크 · 남은 스킬 포인트 알림</summary>
    [AddComponentMenu("OZ/UI/HUD/Progression View")]
    public class ProgressionView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text levelText;
        [SerializeField] internal TweenFillBar expBar;
        [SerializeField] internal TMP_Text rankText;
        [SerializeField] internal RectTransform rankBadge;
        [Tooltip("미사용 스킬 포인트가 있으면 표시 (\"SP 1 [K]\")")]
        [SerializeField] internal GameObject pointsHint;
        [SerializeField] internal TMP_Text pointsText;

        IProgressionSource _source;

        internal void Bind(IProgressionSource source)
        {
            Unsubscribe();
            _source = source;
            if (_source != null)
            {
                _source.ProgressionChanged += OnChanged;
                _source.LevelUp += OnLevelUp;
                _source.RankUp += OnRankUp;
            }
            Refresh(false);
        }

        void Unsubscribe()
        {
            if (_source != null)
            {
                _source.ProgressionChanged -= OnChanged;
                _source.LevelUp -= OnLevelUp;
                _source.RankUp -= OnRankUp;
            }
        }

        void OnDestroy() => Unsubscribe();

        void OnChanged() => Refresh(true);

        void OnLevelUp(int level)
        {
            if (levelText != null) levelText.rectTransform.Punch(0.4f, 0.4f);
            GameUI.Notify.Toast($"레벨 업! Lv.{level}  스킬 포인트 +1", ToastType.Success);
        }

        void OnRankUp(HunterRank rank)
        {
            if (rankBadge != null) rankBadge.Punch(0.5f, 0.5f);
            GameUI.Notify.Toast($"헌터 랭크 승급: {rank.ToDisplay()}", ToastType.Success);
        }

        void Refresh(bool animate)
        {
            if (_source == null)
            {
                if (levelText != null) levelText.text = "Lv.-";
                if (expBar != null) expBar.SetValue(0f, false);
                if (rankText != null) rankText.text = "-";
                if (pointsHint != null) pointsHint.SetActive(false);
                return;
            }

            if (levelText != null) levelText.text = $"Lv.{_source.Level}";
            float need = _source.ExpToNextLevel;
            if (expBar != null) expBar.SetValue(need > 0f ? _source.Exp / need : 1f, animate);
            if (rankText != null) rankText.text = _source.Rank.ToDisplay();

            int sp = _source.SkillPoints;
            if (pointsHint != null) pointsHint.SetActive(sp > 0);
            if (pointsText != null) pointsText.text = $"SP {sp}  [K]";
        }
    }
}
