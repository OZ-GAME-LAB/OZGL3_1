using System.Collections.Generic;
using DG.Tweening;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// [v0.7] 헌터 랭크 승급 연출 — 팡파레 + 화면 가운데 화려한 승급 표시.
    ///   금빛 번쩍 → 뒤에서 빛줄기가 돌며 펼쳐지고 충격파 고리 2번 → 랭크 배지에 이전 랭크가 보이다가
    ///   아래로 빠지고 새 랭크가 아래에서 위로 솟아오르며(▲) 크게 튐 → 금·흰·하늘색 반짝이 조각이 터져 흩날림
    ///   → "RANK UP!" · "헌터 랭크 승급  F급 → E급" → 약 2.8초 뒤 위로 살짝 떠오르며 사라짐.
    ///   게임은 멈추지 않고 입력도 막지 않는다. 연속 승급이면 마지막 랭크로 다시 재생.
    /// IProgressionSource.RankUp(= PlayerUILink.SetRank로 랭크가 오를 때)이 오면 자동 재생 → 팀원 작업 없음.
    /// </summary>
    [AddComponentMenu("OZ/UI/HUD/Rank Up View")]
    public class RankUpView : MonoBehaviour
    {
        public static RankUpView Current { get; private set; }

        [SerializeField] internal CanvasGroup group;
        [SerializeField] internal RectTransform content;
        [SerializeField] internal Image flash;
        [SerializeField] internal RectTransform rays;
        [SerializeField] internal Image raysImage;
        [SerializeField] internal Image[] rings = new Image[0];
        [SerializeField] internal RectTransform badge;
        [SerializeField] internal RectTransform rankMask;
        [SerializeField] internal TMP_Text oldRankText;
        [SerializeField] internal TMP_Text newRankText;
        [SerializeField] internal RectTransform upArrow;
        [SerializeField] internal TMP_Text titleText;
        [SerializeField] internal TMP_Text subText;
        [SerializeField] internal Image sparkTemplate;
        [SerializeField] internal Color gold = new Color(1f, 0.85f, 0.3f);
        [SerializeField, Min(0.5f)] internal float hold = 1.8f;
        [SerializeField, Min(4)] internal int sparkCount = 28;
        [SerializeField] internal UISound sound = UISound.RankUp;

        class Spark { public RectTransform rt; public Image img; public Vector2 vel; public float life, max; }

        readonly List<Spark> _sparks = new List<Spark>();
        readonly Stack<Image> _pool = new Stack<Image>();
        Sequence _seq;
        Vector2 _contentPos;
        bool _cached;

        public bool IsPlaying => _seq != null && _seq.IsActive();
        public HunterRank ShownRank { get; private set; }

        void Awake() => Cache();
        void OnEnable() => Current = this;
        void OnDisable() { if (Current == this) Current = null; }
        void OnDestroy() => _seq?.Kill();

        void Cache()
        {
            if (_cached) return;
            _cached = true;
            if (content != null) _contentPos = content.anchoredPosition;
            if (group != null) group.alpha = 0f;
            if (sparkTemplate != null) sparkTemplate.gameObject.SetActive(false);
        }

        public void Play(HunterRank from, HunterRank to)
        {
            Cache();
            if (group == null || content == null) return;
            ShownRank = to;
            UISfx.Play(sound);
            _seq?.Kill();
            content.DOKill();

            group.alpha = 1f;
            content.anchoredPosition = _contentPos;
            content.localScale = Vector3.one;
            if (titleText != null) { titleText.text = "RANK UP!"; titleText.alpha = 0f; titleText.characterSpacing = 18f; }
            if (subText != null) { subText.text = $"헌터 랭크 승급   {from.ToDisplay()} → {to.ToDisplay()}"; subText.alpha = 0f; }
            if (oldRankText != null) { oldRankText.text = from.ToString(); oldRankText.rectTransform.anchoredPosition = Vector2.zero; oldRankText.alpha = 1f; }
            if (newRankText != null) { newRankText.text = to.ToString(); newRankText.rectTransform.anchoredPosition = new Vector2(0f, -40f); newRankText.alpha = 1f; newRankText.color = gold; }
            if (badge != null) badge.localScale = Vector3.zero;
            if (rays != null) { rays.localScale = Vector3.one * 0.2f; rays.localEulerAngles = Vector3.zero; }
            if (raysImage != null) { var c = gold; c.a = 0f; raysImage.color = c; }
            if (upArrow != null) { upArrow.anchoredPosition = new Vector2(upArrow.anchoredPosition.x, -6f); upArrow.localScale = Vector3.zero; }
            foreach (var r in rings) if (r != null) { r.rectTransform.localScale = Vector3.one * 0.2f; var c = gold; c.a = 0f; r.color = c; }
            if (flash != null) { var c = gold; c.a = 0f; flash.color = c; flash.enabled = true; }

            var s = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            // 0.00 금빛 번쩍 + 배지 등장
            if (flash != null) s.Insert(0f, flash.DOFade(0.55f, 0.08f)).Insert(0.08f, flash.DOFade(0f, 0.5f));
            if (badge != null) s.Insert(0.05f, badge.DOScale(1f, 0.35f).SetEase(Ease.OutBack, 2.2f));
            if (rays != null) s.Insert(0.05f, rays.DOScale(1.6f, 0.6f).SetEase(Ease.OutCubic));
            if (raysImage != null) s.Insert(0.05f, raysImage.DOFade(0.55f, 0.3f));
            // 0.45 이전 랭크 아래로 빠지고 새 랭크 솟아오름
            if (oldRankText != null)
            {
                s.Insert(0.45f, oldRankText.rectTransform.DOAnchorPosY(30f, 0.22f).SetEase(Ease.InBack));
                s.Insert(0.45f, oldRankText.DOFade(0f, 0.2f));
            }
            if (upArrow != null)
            {
                s.Insert(0.4f, upArrow.DOScale(1f, 0.15f).SetEase(Ease.OutBack));
                s.Insert(0.45f, upArrow.DOAnchorPosY(30f, 0.45f).SetEase(Ease.OutCubic));
                s.Insert(0.75f, upArrow.DOScale(0f, 0.2f));
            }
            if (newRankText != null) s.Insert(0.55f, newRankText.rectTransform.DOAnchorPosY(0f, 0.3f).SetEase(Ease.OutBack, 2.5f));
            s.InsertCallback(0.75f, () =>
            {
                if (badge != null) badge.Punch(0.35f, 0.4f);
                Burst();
                if (flash != null) flash.DOFade(0.3f, 0.05f).SetUpdate(true).OnComplete(() => flash.DOFade(0f, 0.35f).SetUpdate(true));
            });
            for (int i = 0; i < rings.Length; i++)
            {
                var r = rings[i];
                if (r == null) continue;
                float at = 0.75f + i * 0.18f;
                s.Insert(at, r.DOFade(0.9f, 0.05f));
                s.Insert(at, r.rectTransform.DOScale(3.2f, 0.6f).SetEase(Ease.OutCubic));
                s.Insert(at + 0.2f, r.DOFade(0f, 0.4f));
            }
            // 0.85 글자
            if (titleText != null)
            {
                s.Insert(0.85f, titleText.DOFade(1f, 0.25f));
                s.Insert(0.85f, DOTween.To(() => titleText.characterSpacing, v => titleText.characterSpacing = v, 4f, 0.45f).SetEase(Ease.OutCubic));
            }
            if (subText != null) s.Insert(1.05f, subText.DOFade(1f, 0.3f));
            // 유지 후 퇴장
            float end = 1.1f + hold;
            s.Insert(end, content.DOAnchorPosY(_contentPos.y + 14f, 0.5f).SetEase(Ease.InQuad));
            s.Insert(end, group.DOFade(0f, 0.5f));
            s.OnComplete(() => { group.alpha = 0f; ClearSparks(); });
            _seq = s;
        }

        void Burst()
        {
            if (sparkTemplate == null) return;
            Color[] cols = { gold, Color.white, new Color(0.45f, 0.9f, 1f), new Color(1f, 0.6f, 0.25f) };
            for (int i = 0; i < sparkCount; i++)
            {
                var img = _pool.Count > 0 ? _pool.Pop() : Instantiate(sparkTemplate, sparkTemplate.transform.parent);
                img.gameObject.SetActive(true);
                img.color = cols[i % cols.Length];
                float size = (i % 3) + 2f;
                img.rectTransform.sizeDelta = new Vector2(size, size);
                img.rectTransform.anchoredPosition = Vector2.zero;
                float ang = (i / (float)sparkCount) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
                float spd = Random.Range(90f, 190f);
                float life = Random.Range(0.8f, 1.4f);
                _sparks.Add(new Spark { rt = img.rectTransform, img = img, vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.8f + 0.35f) * spd, life = life, max = life });
            }
        }

        void Update()
        {
            if (rays != null && group != null && group.alpha > 0f) rays.Rotate(0f, 0f, -25f * Time.unscaledDeltaTime);
            if (_sparks.Count == 0) return;
            float dt = Time.unscaledDeltaTime;
            for (int i = _sparks.Count - 1; i >= 0; i--)
            {
                var p = _sparks[i];
                p.life -= dt;
                if (p.life <= 0f) { p.img.gameObject.SetActive(false); _pool.Push(p.img); _sparks.RemoveAt(i); continue; }
                p.vel += new Vector2(0f, -220f) * dt; // 떨어짐
                p.vel *= 1f - 1.2f * dt;
                Vector2 pos = p.rt.anchoredPosition + p.vel * dt;
                p.rt.anchoredPosition = pos;
                var c = p.img.color;
                c.a = Mathf.Clamp01(p.life / p.max * 1.5f) * ((Mathf.FloorToInt(p.life * 20f) % 2 == 0) ? 1f : 0.6f); // 반짝
                p.img.color = c;
            }
        }

        void ClearSparks()
        {
            foreach (var p in _sparks) { p.img.gameObject.SetActive(false); _pool.Push(p.img); }
            _sparks.Clear();
        }
    }
}
