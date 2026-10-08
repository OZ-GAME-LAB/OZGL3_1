using DG.Tweening;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>퀘스트 목록 한 줄: [□] 제목 ········ 1 / 2  + 진행 막대</summary>
    [AddComponentMenu("OZ/UI/HUD/Quest Row View")]
    public class QuestRowView : MonoBehaviour
    {
        [SerializeField] internal CanvasGroup group;
        [SerializeField] internal Image box;
        [SerializeField] internal Image check;
        [SerializeField] internal TMP_Text title;
        [SerializeField] internal TMP_Text right;
        [SerializeField] internal Image barBack;
        [SerializeField] internal Image barFill;

        [SerializeField] internal Color activeText = new Color(0.93f, 0.95f, 1f);
        [SerializeField] internal Color lockedText = new Color(0.45f, 0.5f, 0.6f);
        [SerializeField] internal Color doneText = new Color(0.45f, 0.75f, 0.55f);
        [SerializeField] internal Color progressColor = new Color(0.32f, 0.86f, 1f);
        [SerializeField] internal Color doneColor = new Color(0.3f, 0.82f, 0.48f);

        public string Id { get; internal set; }
        public QuestInfo Info { get; private set; }
        public RectTransform Rect => (RectTransform)transform;
        public float doneAt = -1f;
        internal bool fresh;
        internal float slotY = float.NaN;

        internal void Apply(QuestInfo q, bool animate)
        {
            var prev = Info;
            Info = q;
            bool hasTarget = q.Target > 0;
            title.text = q.State == QuestState.Done ? $"<s>{q.Title}</s>" : q.Title;
            title.color = q.State == QuestState.Active ? activeText : q.State == QuestState.Locked ? lockedText : doneText;

            if (q.State == QuestState.Locked) { right.text = q.Hint ?? ""; right.color = lockedText; }
            else if (hasTarget) { right.text = $"{Mathf.Min(q.Progress, q.Target)} / {q.Target}"; right.color = q.State == QuestState.Done ? doneText : progressColor; }
            else { right.text = q.State == QuestState.Done ? "완료" : ""; right.color = doneText; }

            bool showBar = hasTarget && q.State == QuestState.Active;
            barBack.gameObject.SetActive(showBar);
            float ratio = hasTarget ? Mathf.Clamp01((float)q.Progress / q.Target) : 0f;
            if (showBar)
            {
                barFill.DOKill();
                if (animate) barFill.DOFillAmount(ratio, 0.25f).SetUpdate(true).SetLink(barFill.gameObject);
                else barFill.fillAmount = ratio;
            }

            box.color = q.State == QuestState.Locked ? lockedText : q.State == QuestState.Done ? doneColor : progressColor;
            check.gameObject.SetActive(q.State == QuestState.Done);

            if (!animate) return;
            if (q.State == QuestState.Done && prev.State != QuestState.Done)
            {
                Rect.Punch(0.12f, 0.3f);
                check.rectTransform.Punch(0.6f, 0.35f);
            }
            else if (q.Progress != prev.Progress || q.State != prev.State)
            {
                right.rectTransform.Punch(0.35f, 0.3f);
            }
        }
    }
}
