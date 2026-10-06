using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OZ.UI
{
    /// <summary>
    /// 스킬 창 (K). 보유 포인트 · Q/E/R 습득/강화 단계 · 다음 효과 · 비용.
    /// 투자 버튼 → ISkillSource.TryInvest (실제 규칙은 플레이어 담당 구현이 판단).
    /// 기획서 기본안: 열면 전투 시간 정지 (pausesGame).
    /// </summary>
    [AddComponentMenu("OZ/UI/Screens/Skill Window")]
    public class SkillWindow : UIWindow
    {
        [SerializeField] internal SkillCardView[] cards = new SkillCardView[3];
        [SerializeField] internal TMP_Text pointsText;
        [SerializeField] internal TMP_Text classText;
        [SerializeField] internal RectTransform pointsBadge;

        ISkillSource _skills;
        IProgressionSource _progress;

        protected override void Awake()
        {
            base.Awake();
            foreach (var c in cards) if (c != null) c.InvestClicked += OnInvest;
        }

        protected override void OnOpened()
        {
            _skills = UISources.Skills;
            _progress = UISources.Progression;
            if (_skills != null) _skills.SkillChanged += OnSkillChanged;
            if (_progress != null) _progress.ProgressionChanged += Refresh;
            Refresh();
            if (cards.Length > 0 && cards[0] != null && cards[0].investButton != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(cards[0].investButton.gameObject);
        }

        protected override void OnClosed()
        {
            if (_skills != null) _skills.SkillChanged -= OnSkillChanged;
            if (_progress != null) _progress.ProgressionChanged -= Refresh;
            _skills = null; _progress = null;
        }

        void OnSkillChanged(SkillSlot _) => Refresh();

        void Refresh()
        {
            foreach (var c in cards) if (c != null) c.Refresh(_skills);
            if (pointsText != null) pointsText.text = $"스킬 포인트  {(_progress != null ? _progress.SkillPoints : 0)}";
            if (classText != null) classText.text = _progress != null ? $"{UIText.ClassName(_progress.Class)} 계열  ·  Lv.{_progress.Level}" : "";
        }

        void OnInvest(SkillCardView card)
        {
            if (_skills == null) return;
            if (_skills.TryInvest(card.Slot))
            {
                card.PlayInvested();
                if (pointsBadge != null) pointsBadge.Punch(0.2f);
            }
            else card.PlayDenied();
            Refresh();
        }
    }
}
