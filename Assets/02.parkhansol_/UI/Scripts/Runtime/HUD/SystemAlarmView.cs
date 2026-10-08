using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace OZ.UI
{
    /// <summary>
    /// 지도 바로 아래에서 내려오는 "SYSTEM" 알림 (GameUI.Notify.System).
    ///   - 지도와 같은 폭. 루트에 RectMask2D → 지도 아래 가장자리에서 미끄러져 나오는 것처럼 보임
    ///   - 최대 maxCards장 아래로 쌓임, holdSeconds 뒤 위로 올라가며 사라짐 → 남은 카드가 위로 당겨짐
    ///   - mergeKey가 같은 카드가 떠 있으면 내용만 바꾸고 시간 연장 (연속 레벨업 → 한 장)
    /// </summary>
    [AddComponentMenu("OZ/UI/HUD/System Alarm View")]
    public class SystemAlarmView : MonoBehaviour
    {
        [SerializeField] internal RectTransform cardTemplate;
        [SerializeField] internal float cardHeight = 40f;
        [SerializeField] internal float gap = 4f;
        [SerializeField] internal int maxCards = 3;
        [SerializeField] internal float holdSeconds = 2.5f;
        [SerializeField] internal float slideIn = 0.18f;
        [SerializeField] internal float slideOut = 0.22f;

        internal class Card
        {
            public RectTransform rt;
            public CanvasGroup cg;
            public TMP_Text kind, main, sub;
            public string mergeKey;
            public float hideAt;
            public bool leaving;
        }

        readonly List<Card> _cards = new List<Card>();
        readonly Stack<Card> _free = new Stack<Card>();

        internal IReadOnlyList<Card> Cards => _cards;
        public int VisibleCount { get { int n = 0; foreach (var c in _cards) if (!c.leaving) n++; return n; } }

        void Awake() { if (cardTemplate != null) cardTemplate.gameObject.SetActive(false); }

        float SlotY(int i) => -i * (cardHeight + gap);
        float HiddenY => cardHeight + 2f; // 마스크 위쪽 = 지도 뒤

        public void Show(string kind, string main, string sub, string mergeKey)
        {
            if (cardTemplate == null) { Debug.Log($"[SYSTEM] {kind} {main} {sub}"); return; }

            if (!string.IsNullOrEmpty(mergeKey))
            {
                foreach (var c in _cards)
                {
                    if (c.leaving || c.mergeKey != mergeKey) continue;
                    Fill(c, kind, main, sub);
                    c.hideAt = Time.unscaledTime + holdSeconds;
                    c.main.rectTransform.Punch(0.3f, 0.3f);
                    return;
                }
            }

            // 꽉 차면 가장 오래된 카드를 먼저 내보냄
            int live = VisibleCount;
            for (int i = 0; i < _cards.Count && live >= maxCards; i++)
                if (!_cards[i].leaving) { Leave(_cards[i]); live--; }

            var card = _free.Count > 0 ? _free.Pop() : Create();
            card.mergeKey = mergeKey;
            card.leaving = false;
            card.hideAt = Time.unscaledTime + holdSeconds;
            Fill(card, kind, main, sub);
            card.rt.gameObject.SetActive(true);
            card.rt.SetAsFirstSibling(); // 먼저 온 카드가 위에 그려지도록 새 카드는 뒤로
            _cards.Add(card);

            int slot = SlotIndex(card);
            card.rt.DOKill();
            card.cg.DOKill();
            // 새 카드는 자기 자리 바로 위(앞 카드 뒤 또는 지도 뒤)에서 내려온다
            card.rt.anchoredPosition = new Vector2(0f, slot == 0 ? HiddenY : SlotY(slot) + cardHeight * 0.6f);
            card.cg.alpha = slot == 0 ? 1f : 0f;
            card.rt.DOAnchorPosY(SlotY(slot), slideIn).SetEase(Ease.OutBack, 1.2f).SetUpdate(true).SetLink(card.rt.gameObject);
            if (slot != 0) card.cg.DOFade(1f, slideIn).SetUpdate(true).SetLink(card.rt.gameObject);
        }

        int SlotIndex(Card card)
        {
            int i = 0;
            foreach (var c in _cards) { if (c == card) return i; if (!c.leaving) i++; }
            return i;
        }

        void Fill(Card c, string kind, string main, string sub)
        {
            c.kind.text = kind ?? "";
            c.main.text = main ?? "";
            c.sub.text = sub ?? "";
            c.sub.gameObject.SetActive(!string.IsNullOrEmpty(sub));
        }

        Card Create()
        {
            var rt = Instantiate(cardTemplate, cardTemplate.parent);
            rt.name = "SystemCard";
            var cg = rt.GetComponent<CanvasGroup>() ?? rt.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            return new Card
            {
                rt = rt,
                cg = cg,
                kind = rt.Find("Header/Kind").GetComponent<TMP_Text>(),
                main = rt.Find("Main").GetComponent<TMP_Text>(),
                sub = rt.Find("Sub").GetComponent<TMP_Text>(),
            };
        }

        void Leave(Card c)
        {
            c.leaving = true;
            c.rt.DOKill();
            c.cg.DOKill();
            bool first = SlotIndexIgnoringLeaving(c) == 0;
            var seq = DOTween.Sequence();
            // 맨 윗장은 지도 뒤로 올라가고, 중간 카드는 제자리에서 흐려짐
            if (first) seq.Append(c.rt.DOAnchorPosY(HiddenY, slideOut).SetEase(Ease.InCubic));
            else seq.Append(c.cg.DOFade(0f, slideOut));
            seq.OnComplete(() => Recycle(c)).SetUpdate(true).SetLink(c.rt.gameObject);
        }

        int SlotIndexIgnoringLeaving(Card card)
        {
            int i = 0;
            foreach (var c in _cards) { if (c == card) return i; if (!c.leaving) i++; }
            return i;
        }

        void Recycle(Card c)
        {
            _cards.Remove(c);
            c.rt.gameObject.SetActive(false);
            _free.Push(c);
            Restack();
        }

        void Restack()
        {
            int i = 0;
            foreach (var c in _cards)
            {
                if (c.leaving) continue;
                float y = SlotY(i++);
                if (Mathf.Abs(c.rt.anchoredPosition.y - y) < 0.5f) continue;
                c.rt.DOKill();
                c.rt.DOAnchorPosY(y, 0.15f).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(c.rt.gameObject);
            }
        }

        void Update()
        {
            float now = Time.unscaledTime;
            // 위에서부터 차례로 (맨 윗장이 지도 뒤로 들어가면 다음 장이 올라옴)
            for (int i = 0; i < _cards.Count; i++)
            {
                var c = _cards[i];
                if (c.leaving || now < c.hideAt) continue;
                Leave(c);
                break;
            }
        }

        /// <summary>테스트·씬 리셋용: 모두 즉시 치움</summary>
        public void ClearAll()
        {
            foreach (var c in _cards) { c.rt.DOKill(); c.cg.DOKill(); c.rt.gameObject.SetActive(false); _free.Push(c); }
            _cards.Clear();
        }
    }
}
