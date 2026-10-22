using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// [v1.3 · 기획서 v0.5 3장] GameUI.Tutorial 구현. UIRoot(UIManager)에 붙는다 — 없으면 UIManager가 알아서 붙인다.
    ///   · 강조 설명 (Show / Play): TutorialWindow로 화면 정지 + 한 곳 밝히기 + 설명
    ///   · 상태창 설명 (PlayStatusTour): 상태창 열기 → 탭을 순서대로 → 포인트 → 상태창은 열어 둔 채 끝 (첫 구매)
    ///   · 각성 카드 (ShowAwakening): 번쩍 + "각성 · F급 · 상태창 · +1,000P" → HUD 등장
    /// 화면은 코드로 만든다 (UIRoot 프리팹을 다시 만들지 않아도 동작). 글꼴·패널 그림은 비우면 HUD 것을 빌린다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("OZ/UI/Tutorial/Tutorial Director")]
    public class TutorialDirector : MonoBehaviour, ITutorialApi
    {
        [Header("글꼴·그림 (비우면 HUD에서 빌림)")]
        [SerializeField] internal TMP_FontAsset titleFont;
        [SerializeField] internal TMP_FontAsset bodyFont;
        [SerializeField] internal TMP_FontAsset smallFont;
        [SerializeField] internal Sprite panelSprite;

        [Header("색 (기획서: 상태창 = 청록)")]
        [SerializeField] internal Color accent = new Color(0.37f, 0.88f, 0.85f);
        [SerializeField] internal Color pointsColor = new Color(1f, 0.85f, 0.4f);
        [SerializeField, Range(0f, 1f)] internal float dimAlpha = 0.62f;

        [Header("각성 카드")]
        [SerializeField] internal float awakeningHold = 2.2f;
        [SerializeField] internal float pointsCountUp = 0.7f;

        TutorialWindow _window;
        List<TutorialStep> _steps;
        int _index = -1;
        Action _onFinished;
        CanvasGroup _lockedStatus;

        RectTransform _card;
        CanvasGroup _cardGroup;
        TMP_Text _cardKind, _cardTitle, _cardRank, _cardPoints;
        Sequence _awakeSeq;
        bool _awakening;

        internal TutorialWindow Window => _window;
        internal RectTransform Card => _card;
        internal CanvasGroup CardGroup => _cardGroup;
        internal TMP_Text CardPoints => _cardPoints;

        // ───────────── 등록 ─────────────

        // 중복 UIRoot(UIManager가 지우는 쪽)가 등록을 덮어쓰지 않게
        bool IsLiveRoot => UIManager.Instance == null || GetComponentInParent<UIManager>() == UIManager.Instance;
        void OnEnable() { if (IsLiveRoot) GameUI.Register((ITutorialApi)this); }
        void OnDisable() => GameUI.Unregister(this);

        // ───────────── ITutorialApi ─────────────

        public bool IsShowing => _window != null && _window.IsOpen && _steps != null && _index >= 0;
        public bool IsAwakening => _awakening;
        public int StepIndex => IsShowing ? _index : -1;
        public int StepCount => _steps != null ? _steps.Count : 0;
        public event Action<int> StepChanged;

        public void Show(TutorialStep step, Action onNext = null) => Play(step != null ? new[] { step } : null, onNext);

        public void Play(IReadOnlyList<TutorialStep> steps, Action onFinished = null)
        {
            var ui = UIManager.Instance;
            if (steps == null || steps.Count == 0 || ui == null || !EnsureWindow()) { onFinished?.Invoke(); return; }
            StopSteps();
            _steps = new List<TutorialStep>();
            foreach (var s in steps) if (s != null) _steps.Add(s);
            _onFinished = onFinished;
            _index = -1;
            if (!_window.IsOpen) ui.Open(_window);
            Advance();
        }

        public void PlayStatusTour(Action onFinished = null)
        {
            var sw = Status;
            if (sw == null) { onFinished?.Invoke(); return; }
            var steps = new List<TutorialStep>
            {
                new TutorialStep
                {
                    target = TutorialTarget.StatusWindow, title = "상태창",
                    body = "각성 능력입니다. 성장 확인 · 구매 · 장착 · 구역 이동을 모두 여기서 합니다.\n[Tab]으로 열고 닫으며, 열려 있는 동안 전투가 멈춥니다.",
                },
            };
            for (int i = 0; i < sw.pages.Length; i++)
            {
                if (sw.pages[i] == null) continue;
                var tab = (StatusTab)i;
                string label = i < sw.tabLabels.Length && sw.tabLabels[i] != null ? sw.tabLabels[i].text : null;
                steps.Add(TutorialStep.Tab(tab, string.IsNullOrEmpty(label) ? TabTitle(tab) : label, TabBody(tab)));
            }
            if (sw.pointsText != null)
                steps.Add(new TutorialStep
                {
                    target = TutorialTarget.StatusPoints, title = "포인트",
                    body = "포인트는 퀘스트 보상으로만 얻습니다. 원하는 기술을 사서 장착하거나, 스탯에 투자하거나, 남겨 둘 수도 있습니다.",
                });
            Play(steps, () =>
            {
                // 끝: 상태창은 열어 둔 채 상점 탭 → 첫 구매 (기획서 3장 6단계)
                if (sw.IsOpen && (int)StatusTab.Shop < sw.pages.Length && sw.pages[(int)StatusTab.Shop] != null) sw.Open(StatusTab.Shop);
                onFinished?.Invoke();
            });
        }

        public void Next()
        {
            if (!IsShowing) return;
            Advance();
        }

        public void Stop()
        {
            StopSteps();
            if (_window != null && _window.IsOpen) UIManager.Instance?.Close(_window);
        }

        // ───────────── 단계 진행 ─────────────

        void StopSteps()
        {
            _steps = null;
            _index = -1;
            _onFinished = null;
            UnlockStatus();
        }

        void Advance()
        {
            if (_steps == null) return;
            if (_index >= 0) UISfx.Play(UISound.DialogueNext);
            _index++;
            if (_index >= _steps.Count) { Finish(); return; }
            ShowStep(_steps[_index]);
            StepChanged?.Invoke(_index);
        }

        void Finish()
        {
            var cb = _onFinished;
            StopSteps();
            // 같은 프레임에 창을 닫으면 [Space]가 플레이어 점프로도 읽힌다 → 한 프레임 뒤 닫기
            if (isActiveAndEnabled) StartCoroutine(CloseNextFrame(cb));
            else { if (_window != null && _window.IsOpen) UIManager.Instance?.Close(_window); cb?.Invoke(); }
        }

        IEnumerator CloseNextFrame(Action cb)
        {
            yield return null;
            if (_steps == null && _window != null && _window.IsOpen) UIManager.Instance?.Close(_window);
            cb?.Invoke();
        }

        void ShowStep(TutorialStep step)
        {
            bool last = _index == _steps.Count - 1;
            string footer = $"[Space] {(last ? "확인" : "다음")}" + (_steps.Count > 1 ? $"   {_index + 1}/{_steps.Count}" : "");
            _window.SetStep(step.title, step.body, footer, Resolve(step));
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        /// <summary>단계 → 밝힐 칸 계산 함수 (매 프레임 부름)</summary>
        Func<Rect?> Resolve(TutorialStep step)
        {
            var w = _window;
            float pad = step.padding;
            switch (step.target)
            {
                case TutorialTarget.Hud:
                {
                    var hud = Hud;
                    var list = new List<RectTransform>();
                    if (hud != null)
                        foreach (HudPart p in Enum.GetValues(typeof(HudPart)))
                            if (IsSingle(p) && (step.hudPart & p) != 0 && hud.PartRoot(p) != null) list.Add(hud.PartRoot(p));
                    return () => w.FromRects(list, pad);
                }
                case TutorialTarget.StatusTab:
                {
                    var sw = OpenStatus(step.tab, true);
                    int i = (int)step.tab;
                    var b = sw != null && i >= 0 && i < sw.tabButtons.Length && sw.tabButtons[i] != null ? (RectTransform)sw.tabButtons[i].transform : null;
                    return () => w.FromRect(b, pad);
                }
                case TutorialTarget.StatusWindow:
                {
                    var sw = OpenStatus(null, false);
                    var rt = sw != null ? (sw.content != null ? sw.content : (RectTransform)sw.transform) : null;
                    return () => w.FromRect(rt, pad);
                }
                case TutorialTarget.StatusPoints:
                {
                    var sw = OpenStatus(null, false);
                    var rt = sw != null && sw.pointsText != null ? sw.pointsText.rectTransform : null;
                    return () => w.FromRect(rt, pad);
                }
                case TutorialTarget.World:
                    return () => step.world != null ? w.FromWorld(step.world.position + step.worldOffset, step.worldSize, pad) : null;
                case TutorialTarget.Rect:
                    return () => w.FromRect(step.rect, pad);
                default:
                    return null;
            }
        }

        static bool IsSingle(HudPart p) => p != HudPart.None && ((int)p & ((int)p - 1)) == 0;

        // ───────────── 상태창 ─────────────

        static StatusWindow Status => UIManager.Instance != null ? UIManager.Instance.GetWindow(ScreenId.Status) as StatusWindow : null;
        HudView Hud => GetComponentInChildren<HudView>(true) ?? FindAnyObjectByType<HudView>(FindObjectsInactive.Include);

        /// <summary>상태창 열기 (tab null = 지금 탭). 설명 중엔 상태창 버튼을 잠근다 (Space/Enter가 구매로 새지 않게)</summary>
        StatusWindow OpenStatus(StatusTab? tab, bool switchTab)
        {
            var sw = Status;
            if (sw == null) return null;
            if (!sw.IsOpen) sw.Open(tab ?? sw.CurrentTab);
            else if (switchTab && tab.HasValue && sw.CurrentTab != tab.Value) sw.Open(tab.Value);
            var cg = sw.GetComponent<CanvasGroup>();
            if (cg != null) { cg.interactable = false; _lockedStatus = cg; }
            return sw;
        }

        void UnlockStatus()
        {
            if (_lockedStatus == null) return;
            var sw = _lockedStatus.GetComponent<StatusWindow>();
            if (sw != null && sw.IsOpen)
            {
                _lockedStatus.interactable = true;
                sw.Page(sw.CurrentTab)?.Focus();
            }
            _lockedStatus = null;
        }

        static string TabTitle(StatusTab t)
        {
            switch (t)
            {
                case StatusTab.Character: return "캐릭터";
                case StatusTab.Quests: return "퀘스트";
                case StatusTab.Shop: return "상점";
                case StatusTab.Map: return "지도";
                default: return t.ToString();
            }
        }

        /// <summary>기획서 v0.5 4장 탭 설명 (지금 빌드: 캐릭터 = 스탯 + 스킬)</summary>
        static string TabBody(StatusTab t)
        {
            switch (t)
            {
                case StatusTab.Character:
                    return "힘은 주먹·검술의 물리 피해, 마력은 마법 피해, 체력은 최대 HP, 민첩은 이동과 기본 공격 속도에 영향을 줍니다.\n구매한 기술은 여기서 Q/E/R에 장착합니다.";
                case StatusTab.Quests:
                    return "메인 목표는 다음 구역을 열고, 서브 목표는 특정 몬스터를 처치해 반복해서 포인트를 모읍니다.";
                case StatusTab.Shop:
                    return "포인트로 기술과 스탯을 삽니다. 기술은 한 번 사면 영구 보유하고, 같은 스탯은 살수록 가격이 오릅니다.";
                case StatusTab.Map:
                    return "구역 사이 이동은 여기서 합니다. 잠긴 노드는 해금 조건을 보여 주고, 열린 노드를 골라 이동합니다.";
                default:
                    return "";
            }
        }

        // ───────────── 각성 카드 ─────────────

        public void ShowAwakening(int points = 1000, HunterRank rank = HunterRank.F, Action onFinished = null, bool revealHud = true)
        {
            if (!EnsureCard())
            {
                if (revealHud) GameUI.HUD.SetParts(HudPart.All, true);
                onFinished?.Invoke();
                return;
            }
            _awakeSeq?.Kill();
            _awakening = true;
            _card.gameObject.SetActive(true);
            _cardGroup.alpha = 0f;
            _card.localScale = Vector3.one * 0.92f;
            _cardKind.text = "SYSTEM  ·  상태창";
            _cardTitle.text = "각성을 확인했습니다";
            _cardRank.text = $"헌터 랭크 <color=#{ColorUtility.ToHtmlStringRGB(pointsColor)}>{rank.ToDisplay()}</color>    능력 <color=#{ColorUtility.ToHtmlStringRGB(accent)}>상태창</color>";
            int shown = 0;
            _cardPoints.text = points > 0 ? "+0P" : "";

            GameUI.HUD.Flash(new Color(0.8f, 1f, 0.98f, 0.8f), 0.6f);
            UISfx.Play(UISound.RankUp);

            var seq = DOTween.Sequence();
            seq.AppendInterval(0.15f);
            seq.Append(_cardGroup.DOFade(1f, UITweenStyle.Normal));
            seq.Join(_card.DOScale(1f, UITweenStyle.Normal).SetEase(Ease.OutBack));
            if (points > 0)
            {
                seq.Append(DOTween.To(() => shown, v => { shown = v; _cardPoints.text = $"+{v:N0}P"; }, points, pointsCountUp).SetEase(Ease.OutCubic));
                seq.AppendCallback(() => { _cardPoints.text = $"+{points:N0}P"; _cardPoints.rectTransform.Punch(0.3f); UISfx.Play(UISound.Confirm); });
            }
            seq.AppendInterval(awakeningHold);
            seq.Append(_cardGroup.DOFade(0f, UITweenStyle.Slow));
            seq.AppendCallback(() => { if (revealHud) GameUI.HUD.SetParts(HudPart.All, true); });
            if (revealHud) seq.AppendInterval(1.1f); // HUD가 하나씩 다 뜰 때까지 (6부위 × 0.12초 + 페이드)
            seq.OnComplete(() =>
            {
                _awakening = false;
                _card.gameObject.SetActive(false);
                onFinished?.Invoke();
            });
            seq.OnKill(() => _awakening = false);
            _awakeSeq = seq.SetUpdate(true).SetLink(gameObject);
        }

        // ───────────── 만들기 ─────────────

        void ResolveArt()
        {
            var hud = Hud;
            if (hud != null)
            {
                if (titleFont == null && hud.guideText != null) titleFont = hud.guideText.font;
                if (bodyFont == null && hud.progression != null && hud.progression.rankText != null) bodyFont = hud.progression.rankText.font;
                if (smallFont == null && hud.health != null && hud.health.valueText != null) smallFont = hud.health.valueText.font;
                if (panelSprite == null)
                {
                    var mm = hud.PartRoot(HudPart.Minimap);
                    var img = mm != null ? mm.GetComponent<Image>() : null;
                    if (img != null) panelSprite = img.sprite;
                }
            }
            if (bodyFont == null) bodyFont = TMP_Settings.defaultFontAsset;
            if (titleFont == null) titleFont = bodyFont;
            if (smallFont == null) smallFont = bodyFont;
        }

        bool EnsureWindow()
        {
            if (_window != null) return true;
            var ui = UIManager.Instance;
            if (ui == null) return false;
            ResolveArt();
            _window = TutorialWindow.Create(ui.GetLayer(UILayer.Popup).transform, titleFont, bodyFont, smallFont, panelSprite, accent, dimAlpha);
            _window.NextPressed += Next;
            ui.RegisterWindow(_window);
            return true;
        }

        bool EnsureCard()
        {
            if (_card != null) return true;
            var ui = UIManager.Instance;
            if (ui == null) return false;
            ResolveArt();
            var go = new GameObject("AwakeningCard", typeof(RectTransform));
            go.layer = 5;
            _card = (RectTransform)go.transform;
            _card.SetParent(ui.GetLayer(UILayer.Cinematic).transform, false);
            _card.anchorMin = _card.anchorMax = _card.pivot = new Vector2(0.5f, 0.5f);
            _card.anchoredPosition = new Vector2(0f, 30f);
            _card.sizeDelta = new Vector2(232f, 82f);
            _cardGroup = go.AddComponent<CanvasGroup>();
            _cardGroup.blocksRaycasts = false;
            _cardGroup.interactable = false;
            var bg = go.AddComponent<Image>();
            bg.sprite = panelSprite;
            bg.color = panelSprite != null ? Color.white : new Color(0.05f, 0.07f, 0.13f, 0.96f);
            if (panelSprite != null && panelSprite.border != Vector4.zero) bg.type = Image.Type.Sliced;
            bg.raycastTarget = false;

            var bar = TutorialWindow.Solid("AccentBar", _card, accent, false).rectTransform;
            bar.anchorMin = new Vector2(0f, 1f); bar.anchorMax = Vector2.one; bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = Vector2.zero; bar.sizeDelta = new Vector2(0f, 2f);

            _cardKind = Line("Kind", smallFont, 10f, accent, 6f, 11f);
            _cardTitle = Line("Title", titleFont, 15f, Color.white, 20f, 16f);
            _cardRank = Line("Rank", bodyFont, 12f, new Color(0.93f, 0.95f, 1f), 40f, 13f);
            _cardPoints = Line("Points", titleFont, 15f, pointsColor, 58f, 16f);
            go.SetActive(false);
            return true;
        }

        TMP_Text Line(string name, TMP_FontAsset font, float size, Color color, float top, float h)
        {
            var t = TutorialWindow.Text(name, _card, font, size, color, TextAlignmentOptions.Center);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -top);
            rt.sizeDelta = new Vector2(-16f, h);
            return t;
        }
    }
}
