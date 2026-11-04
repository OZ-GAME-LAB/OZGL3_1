using DG.Tweening;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// [v1.4] Q/E/R 오른쪽 "평타" 칸: 지금 무기(맨손·검·스태프) 아이콘 + 피해 유형 색 + 콤보 점.
    /// 소스(IWeaponSource)가 없으면 칸을 숨긴다.
    /// UIRoot를 다시 만들지 않아도 HudView가 R 칸을 복사해 실행 중에 만든다 (그때는 아이콘 대신 글자).
    /// </summary>
    [AddComponentMenu("OZ/UI/HUD/Weapon Slot View")]
    public class WeaponSlotView : MonoBehaviour
    {
        [SerializeField] internal Image icon;
        [SerializeField] internal TMP_Text letter;
        [SerializeField] internal TMP_Text keyLabel;
        [SerializeField] internal Graphic frame;
        [SerializeField] internal Image[] comboPips = new Image[0];

        [Header("아이콘 (비우면 글자)")]
        [SerializeField] internal Sprite fistIcon;
        [SerializeField] internal Sprite swordIcon;
        [SerializeField] internal Sprite staffIcon;

        [Header("색")]
        [SerializeField] internal Color physicalColor = new Color(1f, 0.62f, 0.3f);
        [SerializeField] internal Color magicColor = new Color(0.55f, 0.72f, 1f);
        [SerializeField] internal Color pipOff = new Color(1f, 1f, 1f, 0.18f);

        IWeaponSource _source;
        UIWeapon _shown = (UIWeapon)(-1);
        int _lastStep = -2;

        internal IWeaponSource Source => _source;
        internal UIWeapon Shown => _shown;
        internal int LitPips { get; private set; }

        internal void Bind(IWeaponSource source)
        {
            if (_source != null) _source.WeaponChanged -= OnWeaponChanged;
            _source = source;
            if (_source != null) _source.WeaponChanged += OnWeaponChanged;
            gameObject.SetActive(_source != null);
            _shown = (UIWeapon)(-1);
            Refresh(false);
        }

        void OnDestroy() { if (_source != null) _source.WeaponChanged -= OnWeaponChanged; }

        void OnWeaponChanged() => Refresh(true);

        internal void Refresh(bool animate)
        {
            if (_source == null) return;
            var w = _source.CurrentWeapon;
            bool changed = w != _shown;
            _shown = w;

            Sprite sp = w == UIWeapon.Sword ? swordIcon : w == UIWeapon.Staff ? staffIcon : fistIcon;
            if (icon != null) { icon.sprite = sp; icon.enabled = sp != null; icon.color = Color.white; }
            if (letter != null)
            {
                letter.text = sp != null ? "" : Letter(w);
                letter.color = _source.IsMagicAttack ? magicColor : physicalColor;
            }
            if (keyLabel != null)
            {
                keyLabel.text = "평타";
                keyLabel.color = _source.IsMagicAttack ? magicColor : physicalColor;
            }

            int n = Mathf.Max(0, _source.ComboLength);
            for (int i = 0; i < comboPips.Length; i++)
                if (comboPips[i] != null) comboPips[i].gameObject.SetActive(i < n);
            _lastStep = -2;
            UpdatePips();

            if (animate && changed)
            {
                var rt = (RectTransform)transform;
                rt.DOKill(true);
                rt.Punch(0.35f, 0.3f);
                if (icon != null) { icon.rectTransform.DOKill(true); icon.rectTransform.localScale = new Vector3(0f, 1f, 1f); icon.rectTransform.DOScaleX(1f, 0.15f).SetUpdate(true); }
                UISfx.Play(UISound.ItemPlace, 0.8f);
            }
        }

        internal static string Letter(UIWeapon w) => w == UIWeapon.Sword ? "검" : w == UIWeapon.Staff ? "봉" : "손";

        void Update() { if (_source != null) UpdatePips(); }

        void UpdatePips()
        {
            int n = Mathf.Min(Mathf.Max(0, _source.ComboLength), comboPips.Length);
            int step = n > 0 ? _source.ComboStep : -1;
            if (step == _lastStep) return;
            bool advanced = step > _lastStep && step >= 0;
            _lastStep = step;
            LitPips = step < 0 ? 0 : Mathf.Min(step + 1, n);
            Color on = _source.IsMagicAttack ? magicColor : physicalColor;
            for (int i = 0; i < n; i++)
            {
                var p = comboPips[i];
                if (p == null) continue;
                p.color = i < LitPips ? on : pipOff;
                if (advanced && i == LitPips - 1) { p.rectTransform.DOKill(true); p.rectTransform.Punch(0.6f, 0.15f); }
            }
        }

        // ── 실행 중 생성 (UIRoot 재생성 없이) ──
        /// <summary>R 칸(template)을 복사해 오른쪽에 평타 칸을 만든다. 스킬 기능 컴포넌트는 지운다.</summary>
        internal static WeaponSlotView CreateFrom(SkillSlotView template, float gapX = 6f)
        {
            if (template == null) return null;
            var src = (RectTransform)template.transform;
            var go = Instantiate(template.gameObject, src.parent);
            go.name = "Weapon_Basic";
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = src.anchoredPosition + new Vector2(src.rect.width + gapX, 0f);

            var oldView = go.GetComponent<SkillSlotView>();
            var view = go.AddComponent<WeaponSlotView>();
            view.icon = oldView.icon;
            view.letter = oldView.iconLetter;
            view.keyLabel = oldView.keyLabel;
            view.frame = oldView.tintTarget;
            if (oldView.lockOverlay != null) oldView.lockOverlay.SetActive(false);
            if (oldView.rankText != null) oldView.rankText.text = "";
            foreach (var r in go.GetComponentsInChildren<CooldownRadial>(true))
            {
                r.Clear();
                if (r.sweepHand != null) r.sweepHand.gameObject.SetActive(false);
                if (r.readyFlash != null) r.readyFlash.gameObject.SetActive(false);
                if (r.label != null) r.label.gameObject.SetActive(false);
                bool keep = r.gameObject == go || (view.icon != null && r.gameObject == view.icon.gameObject);
                if (!keep) r.gameObject.SetActive(false);
                Destroy(r);
            }
            Destroy(oldView);

            // 콤보 점 3개 (칸 위쪽, 키 글자 아래)
            view.comboPips = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var pgo = new GameObject("ComboPip" + i, typeof(RectTransform), typeof(Image));
                var prt = (RectTransform)pgo.transform;
                prt.SetParent(rt, false);
                prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0f);
                prt.pivot = new Vector2(0.5f, 0.5f);
                prt.sizeDelta = new Vector2(6f, 3f);
                prt.anchoredPosition = new Vector2((i - 1) * 9f, -5f);
                var img = pgo.GetComponent<Image>();
                img.raycastTarget = false;
                img.color = view.pipOff;
                view.comboPips[i] = img;
            }
            return view;
        }
    }
}
