using DG.Tweening;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>스테이지 클리어: 승급 연출 + 다음 스테이지</summary>
    [AddComponentMenu("OZ/UI/Screens/Stage Clear Screen")]
    public class StageClearScreen : UIWindow
    {
        [SerializeField] internal TMP_Text stageText;
        [SerializeField] internal TMP_Text rankText;
        [SerializeField] internal RectTransform rankBadge;
        [SerializeField] internal Button nextButton;

        protected override void Awake()
        {
            base.Awake();
            if (nextButton != null) nextButton.onClick.AddListener(() => { Close(); UIRequests.RaiseNextStage(); });
        }

        protected override void OnSetup(object args)
        {
            if (!(args is StageClearArgs a)) return;
            if (stageText != null) stageText.text = $"STAGE {a.StageNumber} CLEAR";
            if (rankText != null) rankText.text = $"헌터 랭크  {a.NewRank.ToDisplay()}";
        }

        protected override void OnOpened()
        {
            if (rankBadge != null) DOVirtual.DelayedCall(0.4f, () => rankBadge.Punch(0.5f, 0.5f), true).SetLink(gameObject);
        }
    }
}
