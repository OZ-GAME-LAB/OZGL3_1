using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>UI 연출 공통 수치. 연출 톤을 한 곳에서 맞춘다.</summary>
    public static class UITweenStyle
    {
        public const float Fast = 0.12f;
        public const float Normal = 0.25f;
        public const float Slow = 0.45f;

        public const Ease InEase = Ease.OutBack;
        public const Ease OutEase = Ease.InQuad;
    }

    /// <summary>
    /// DOTween 프리셋 확장 메서드. 모든 트윈은
    ///  - SetUpdate(true): Time.timeScale=0(스킬 창, 히트스톱) 중에도 동작
    ///  - SetLink(gameObject): 오브젝트 파괴 시 자동 Kill
    /// </summary>
    public static class UITween
    {
        static T Ui<T>(this T t, Component owner) where T : Tween
        {
            t.SetUpdate(true);
            if (owner != null) t.SetLink(owner.gameObject);
            return t;
        }

        // ── 창 열기/닫기 ──
        public static Sequence PopIn(this RectTransform rt, CanvasGroup cg = null, float duration = UITweenStyle.Normal)
        {
            rt.DOKill(true);
            rt.localScale = Vector3.one * 0.85f;
            var seq = DOTween.Sequence();
            seq.Append(rt.DOScale(1f, duration).SetEase(UITweenStyle.InEase));
            if (cg != null)
            {
                cg.alpha = 0f;
                seq.Join(cg.DOFade(1f, duration * 0.6f));
            }
            return seq.Ui(rt);
        }

        public static Sequence PopOut(this RectTransform rt, CanvasGroup cg = null, float duration = UITweenStyle.Fast)
        {
            rt.DOKill(true);
            var seq = DOTween.Sequence();
            seq.Append(rt.DOScale(0.9f, duration).SetEase(UITweenStyle.OutEase));
            if (cg != null) seq.Join(cg.DOFade(0f, duration));
            return seq.Ui(rt);
        }

        public static Tween FadeIn(this CanvasGroup cg, float duration = UITweenStyle.Normal)
        {
            cg.DOKill();
            return cg.DOFade(1f, duration).Ui(cg);
        }

        public static Tween FadeOut(this CanvasGroup cg, float duration = UITweenStyle.Fast)
        {
            cg.DOKill();
            return cg.DOFade(0f, duration).Ui(cg);
        }

        /// <summary>from 방향에서 원래 위치로 미끄러져 들어온다. from 예: Vector2.down</summary>
        public static Tween SlideIn(this RectTransform rt, Vector2 from, float distance = 40f, float duration = UITweenStyle.Normal)
        {
            rt.DOKill(true);
            Vector2 target = rt.anchoredPosition;
            rt.anchoredPosition = target + from.normalized * distance;
            return rt.DOAnchorPos(target, duration).SetEase(Ease.OutCubic).Ui(rt);
        }

        public static Tween SlideOut(this RectTransform rt, Vector2 to, float distance = 40f, float duration = UITweenStyle.Fast)
        {
            rt.DOKill(true);
            Vector2 target = rt.anchoredPosition + to.normalized * distance;
            return rt.DOAnchorPos(target, duration).SetEase(Ease.InCubic).Ui(rt);
        }

        // ── 피드백 ──
        public static Tween Punch(this RectTransform rt, float strength = 0.2f, float duration = UITweenStyle.Normal)
        {
            rt.DOKill(true);
            rt.localScale = Vector3.one;
            return rt.DOPunchScale(Vector3.one * strength, duration, 6, 0.5f).Ui(rt);
        }

        /// <summary>좌우 흔들림 (사용 불가, 피격). strength는 UI 좌표(px/배율) 기준</summary>
        public static Tween Shake(this RectTransform rt, float strength = 3f, float duration = 0.2f)
        {
            rt.DOKill(true);
            return rt.DOShakeAnchorPos(duration, new Vector2(strength, 0f), 20, 0f, true, true).Ui(rt);
        }

        /// <summary>그래픽을 color로 번쩍였다가 원래 색으로</summary>
        public static Sequence FlashColor(this Graphic g, Color color, float duration = UITweenStyle.Fast)
        {
            g.DOKill(true);
            Color original = g.color;
            var seq = DOTween.Sequence();
            seq.Append(g.DOColor(color, duration * 0.3f));
            seq.Append(g.DOColor(original, duration * 0.7f));
            return seq.Ui(g);
        }

        /// <summary>저체력 등 맥박 루프. 끄려면 rt.DOKill() 후 localScale = one</summary>
        public static Tween Pulse(this RectTransform rt, float scale = 1.08f, float halfPeriod = 0.35f)
        {
            rt.DOKill(true);
            rt.localScale = Vector3.one;
            return rt.DOScale(scale, halfPeriod).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).Ui(rt);
        }

        /// <summary>Image.fillAmount 트윈 (DOTween 무료판 모듈 사용)</summary>
        public static Tween Fill(this Image img, float to, float duration, Ease ease = Ease.OutQuad)
        {
            img.DOKill();
            return img.DOFillAmount(to, duration).SetEase(ease).Ui(img);
        }

        /// <summary>
        /// TMP 숫자 카운트 (DOTween Pro의 DOCounter 대체).
        /// </summary>
        public static Tween Count(this TMPro.TMP_Text text, int from, int to, float duration = UITweenStyle.Slow, string format = "{0}")
        {
            text.DOKill();
            int value = from;
            return DOTween.To(() => value, v => { value = v; text.text = string.Format(format, v); }, to, duration).SetTarget(text).Ui(text);
        }

        /// <summary>
        /// 대사/안내문 타이핑 (DOTween Pro의 DOText(TMP) 대체). maxVisibleCharacters를 늘린다.
        /// </summary>
        public static Tween Typewrite(this TMPro.TMP_Text text, float charsPerSecond = 30f)
        {
            text.DOKill();
            text.ForceMeshUpdate();
            int total = text.textInfo.characterCount;
            text.maxVisibleCharacters = 0;
            float duration = charsPerSecond > 0f ? total / charsPerSecond : 0f;
            return DOTween.To(() => text.maxVisibleCharacters, v => text.maxVisibleCharacters = v, total, duration)
                .SetEase(Ease.Linear).SetTarget(text).Ui(text);
        }
    }
}
