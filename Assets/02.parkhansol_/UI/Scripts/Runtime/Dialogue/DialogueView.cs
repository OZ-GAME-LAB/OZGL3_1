using System;
using System.Collections.Generic;
using DG.Tweening;
using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// 대화창 + 좌/우 초상화 (GameUI.Dialogue 구현).
    ///   말하는 쪽: 밝게·원래 크기 / 듣는 쪽: 어둡게·0.92배
    ///   타이핑: maxVisibleCharacters 증가 (DOTween 무료판), 진행 키 → 즉시 완성 → 다음 줄
    ///   진행 키: Space / Enter / 마우스 좌클릭 / 게임패드 A
    /// </summary>
    [AddComponentMenu("OZ/UI/Dialogue/Dialogue View")]
    public class DialogueView : UIWindow, IDialogueApi
    {
        [SerializeField] internal RectTransform box;
        [SerializeField] internal TMP_Text nameText;
        [SerializeField] internal TMP_Text bodyText;
        [SerializeField] internal Graphic nextIndicator;

        [Header("Portraits")]
        [SerializeField] internal Image leftPortrait;
        [SerializeField] internal Image rightPortrait;
        [SerializeField] internal Color listenerTint = new Color(0.45f, 0.45f, 0.5f, 1f);
        [SerializeField] internal float listenerScale = 0.92f;

        [SerializeField] internal float defaultCharsPerSecond = 30f;

        readonly List<DialogueLine> _lines = new List<DialogueLine>();
        int _index = -1;
        Tween _typing;
        Action _onFinished;
        InputAction _advance;
        bool _pauseGameForThis;
        SpeakerData _leftSpeaker, _rightSpeaker;

        public bool IsPlaying => IsOpen;

        protected override void Awake()
        {
            base.Awake();
            _advance = new InputAction("UI_DialogueNext", InputActionType.Button);
            _advance.AddBinding("<Keyboard>/space");
            _advance.AddBinding("<Keyboard>/enter");
            _advance.AddBinding("<Mouse>/leftButton");
            _advance.AddBinding("<Gamepad>/buttonSouth");
            _advance.performed += _ => Advance();
        }

        void OnEnable() => GameUI.Register((IDialogueApi)this);
        void OnDisable() { GameUI.Unregister(this); _advance?.Disable(); }
        void OnDestroy() => _advance?.Dispose();

        // ── IDialogueApi ──
        public void Play(DialogueData dialogue, Action onFinished = null)
        {
            if (dialogue == null || dialogue.lines == null || dialogue.lines.Count == 0) { onFinished?.Invoke(); return; }
            _pauseGameForThis = dialogue.pauseGame;
            Begin(dialogue.lines, onFinished);
        }

        public void Say(SpeakerData speaker, string text, DialogueSide side = DialogueSide.Right, Action onFinished = null)
        {
            _pauseGameForThis = false;
            Begin(new List<DialogueLine> { new DialogueLine { speaker = speaker, side = side, text = text } }, onFinished);
        }

        public void Stop()
        {
            _typing?.Kill();
            _lines.Clear();
            _index = -1;
            var ui = UIManager.Instance;
            if (ui != null) ui.Close(this);
        }

        void Begin(List<DialogueLine> lines, Action onFinished)
        {
            _lines.Clear();
            _lines.AddRange(lines);
            _onFinished = onFinished;
            _index = -1;
            _leftSpeaker = _rightSpeaker = null;
            SetPortrait(leftPortrait, null);
            SetPortrait(rightPortrait, null);

            pausesGame = _pauseGameForThis;
            var ui = UIManager.Instance;
            if (ui != null) ui.Open(this);
            else gameObject.SetActive(true);

            if (box != null) box.SlideIn(Vector2.down, 24f);
            Next();
        }

        protected override void OnOpened() => _advance.Enable();
        protected override void OnClosed() => _advance.Disable();

        void Advance()
        {
            if (!IsOpen) return;
            if (_typing != null && _typing.IsActive() && _typing.IsPlaying())
            {
                _typing.Complete(); // 즉시 완성
                return;
            }
            Next();
        }

        void Next()
        {
            _index++;
            if (_index >= _lines.Count)
            {
                var done = _onFinished;
                _onFinished = null;
                Stop();
                done?.Invoke();
                return;
            }
            ShowLine(_lines[_index]);
        }

        void ShowLine(DialogueLine line)
        {
            var speaker = line.speaker;
            bool left = line.side == DialogueSide.Left;

            if (left) _leftSpeaker = speaker; else _rightSpeaker = speaker;
            var portrait = speaker != null ? speaker.GetPortrait(line.expression) : null;
            Image active = left ? leftPortrait : rightPortrait;
            Image other = left ? rightPortrait : leftPortrait;

            bool wasHidden = active != null && !active.enabled;
            SetPortrait(active, portrait);
            if (wasHidden && active != null && active.enabled)
                active.rectTransform.SlideIn(left ? Vector2.left : Vector2.right, 30f);

            Highlight(active, true);
            Highlight(other, false);

            if (nameText != null)
            {
                nameText.text = speaker != null ? speaker.displayName : "";
                nameText.color = speaker != null ? speaker.nameColor : Color.white;
                nameText.alignment = left ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
            }

            if (nextIndicator != null) { nextIndicator.DOKill(); nextIndicator.enabled = false; }

            if (bodyText != null)
            {
                bodyText.text = line.text;
                float cps = line.charsPerSecond > 0f ? line.charsPerSecond : defaultCharsPerSecond;
                _typing = bodyText.Typewrite(cps).OnComplete(ShowIndicator);
            }
            else ShowIndicator();
        }

        void ShowIndicator()
        {
            if (nextIndicator == null) return;
            nextIndicator.enabled = true;
            var c = nextIndicator.color; c.a = 1f; nextIndicator.color = c;
            nextIndicator.DOFade(0.2f, 0.4f).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(nextIndicator.gameObject);
        }

        void Highlight(Image portrait, bool speaking)
        {
            if (portrait == null || !portrait.enabled) return;
            portrait.DOKill();
            portrait.rectTransform.DOKill();
            portrait.DOColor(speaking ? Color.white : listenerTint, UITweenStyle.Fast).SetUpdate(true).SetLink(portrait.gameObject);
            portrait.rectTransform.DOScale(speaking ? 1f : listenerScale, UITweenStyle.Fast).SetUpdate(true).SetLink(portrait.gameObject);
        }

        static void SetPortrait(Image img, Sprite sprite)
        {
            if (img == null) return;
            img.sprite = sprite;
            img.enabled = sprite != null;
            img.preserveAspect = true;
        }
    }
}
