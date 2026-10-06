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
        // 위치 연출(SlideIn/SlideOut/Shake)이 중간에 끊기면(다른 코드가 DOKill, 시퀀스 Kill 등)
        // 끊긴 자리가 다음 연출의 "제자리"가 되어 점점 밀리는 문제가 있었다 (대화 초상화 좌우 밀림).
        // → 연출 시작 전 제자리를 기억해 두고, 끝나거나 끊기면 제자리로 되돌린다.
        //    콜백 없이 끊겨도 다음 연출 시작 때 기억해 둔 제자리로 먼저 복구한다.
        sealed class PosState { public bool active; public Vector2 rest; }
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<RectTransform, PosState> _pos =
            new System.Runtime.CompilerServices.ConditionalWeakTable<RectTransform, PosState>();

        /// <summary>위치 연출 시작: 이전 연출이 끊겨 있었다면 그 제자리로 복구 후, 현재 위치를 제자리로 기록</summary>
        static PosState BeginPos(RectTransform rt)
        {
            rt.DOKill(true);
            var st = _pos.GetValue(rt, _ => new PosState());
            if (st.active) rt.anchoredPosition = st.rest; // 콜백 없이 끊긴 이전 연출 정리
            st.rest = rt.anchoredPosition;
            st.active = true;
            return st;
        }

        static T EndPos<T>(this T t, RectTransform rt, PosState st, Vector2 finalPos) where T : Tween
        {
            System.Action end = () =>
            {
                if (!st.active) return;
                st.active = false;
                if (rt != null) rt.anchoredPosition = finalPos;
            };
            t.OnComplete(() => end()).OnKill(() => end());
            return t;
        }

        public static Tween SlideIn(this RectTransform rt, Vector2 from, float distance = 40f, float duration = UITweenStyle.Normal)
        {
            var st = BeginPos(rt);
            Vector2 target = st.rest;
            rt.anchoredPosition = target + from.normalized * distance;
            return rt.DOAnchorPos(target, duration).SetEase(Ease.OutCubic).Ui(rt).EndPos(rt, st, target);
        }

        public static Tween SlideOut(this RectTransform rt, Vector2 to, float distance = 40f, float duration = UITweenStyle.Fast)
        {
            var st = BeginPos(rt);
            Vector2 target = st.rest + to.normalized * distance;
            // active를 켜 둔 채로 둠 → 다음 SlideIn/Shake 때 원래 제자리(rest)로 먼저 복구됨
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
            var st = BeginPos(rt);
            return rt.DOShakeAnchorPos(duration, new Vector2(strength, 0f), 20, 0f, true, true).Ui(rt).EndPos(rt, st, st.rest);
        }

        sealed class FlashState { public Color baseColor; public Sequence seq; }
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Graphic, FlashState> _flash =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Graphic, FlashState>();

        /// <summary>
        /// 색 플래시 후 원래 색으로 복귀. 연속으로 불러도 "원래 색"은 첫 플래시 전 색으로 고정
        /// (빠른 연타 피격 시 빨간색이 남는 문제 방지).
        /// </summary>
        public static Sequence FlashColor(this Graphic g, Color color, float duration = UITweenStyle.Fast)
        {
            var st = _flash.GetValue(g, _ => new FlashState());
            if (st.seq != null && st.seq.IsActive())
            {
                st.seq.Kill();
                g.color = st.baseColor;
            }
            else st.baseColor = g.color;

            Color original = st.baseColor;
            var seq = DOTween.Sequence();
            seq.Append(g.DOColor(color, duration * 0.3f));
            seq.Append(g.DOColor(original, duration * 0.7f));
            seq.OnKill(() => { if (g != null && st.seq == seq) { g.color = original; st.seq = null; } });
            st.seq = seq;
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
