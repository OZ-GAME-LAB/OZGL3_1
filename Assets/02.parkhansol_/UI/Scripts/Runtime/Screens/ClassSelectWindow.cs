using System.Collections.Generic;
using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;

namespace OZ.UI
{
    /// <summary>계열 선택 (검술 / 마법) → UIRequests.ClassSelected</summary>
    [AddComponentMenu("OZ/UI/Screens/Class Select Window")]
    public class ClassSelectWindow : UIWindow
    {
        [SerializeField] internal ClassCardView[] cards;
        [Tooltip("ShowClassSelect(null)일 때 쓸 기본 목록")]
        [SerializeField] internal ClassData[] defaultClasses;

        protected override void Awake()
        {
            base.Awake();
            if (cards == null) return;
            foreach (var c in cards) if (c != null) c.Chosen += OnChosen;
        }

        protected override void OnSetup(object args)
        {
            IReadOnlyList<ClassData> list = (args as ClassSelectArgs)?.Classes;
            if (list == null || list.Count == 0) list = defaultClasses;
            if (cards == null) return;
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null) continue;
                bool has = list != null && i < list.Count && list[i] != null;
                cards[i].gameObject.SetActive(has);
                if (has) cards[i].Show(list[i]);
            }
        }

        protected override void OnOpened()
        {
            if (cards != null && cards.Length > 0 && cards[0] != null && cards[0].button != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(cards[0].button.gameObject);
        }

        void OnChosen(ClassCardView card)
        {
            if (card.Data == null) return;
            ((RectTransform)card.transform).Punch(0.15f);
            Close();
            UIRequests.RaiseClassSelected(card.Data.playerClass);
        }
    }
}
