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
    /// <summary>퀘스트 한 줄: [메인]/[서브] · 제목 · 진행 · 막대 · 보상</summary>
    [AddComponentMenu("OZ/UI/Status/Quest Entry View")]
    public class QuestEntryView : MonoBehaviour
    {
        [SerializeField] internal TMP_Text tagText;
        [SerializeField] internal TMP_Text titleText;
        [SerializeField] internal TMP_Text progressText;
        [SerializeField] internal Image barFill;
        [SerializeField] internal TMP_Text rewardText;
        [SerializeField] internal Color mainColor = new Color(1f, 0.85f, 0.4f);
        [SerializeField] internal Color subColor = new Color(0.55f, 0.85f, 1f);
        [SerializeField] internal Color doneColor = new Color(0.55f, 1f, 0.65f);

        internal void Show(QuestInfo q, bool hasReward, int reward, bool isMain)
        {
            Color c = q.State == QuestState.Done ? doneColor : isMain ? mainColor : subColor;
            if (tagText != null) { tagText.text = isMain ? "메인" : "서브"; tagText.color = c; }
            if (titleText != null)
            {
                titleText.text = q.State == QuestState.Done ? $"<s>{q.Title}</s>" : q.Title;
                titleText.color = q.State == QuestState.Locked ? StatusColors.Dim : Color.white;
            }
            if (progressText != null)
                progressText.text = q.State == QuestState.Locked ? (q.Hint ?? "잠김")
                    : q.State == QuestState.Done ? "완료"
                    : q.Target > 0 ? $"{q.Progress} / {q.Target}" : "";
            if (barFill != null)
            {
                barFill.transform.parent.gameObject.SetActive(q.Target > 0 && q.State != QuestState.Locked);
                barFill.fillAmount = q.Target > 0 ? Mathf.Clamp01((float)q.Progress / q.Target) : 0f;
                barFill.color = c;
            }
            if (rewardText != null) rewardText.text = hasReward && reward > 0 ? $"+{reward:N0}P" : "";
        }
    }
}
