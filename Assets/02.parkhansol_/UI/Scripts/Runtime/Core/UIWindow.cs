using System;
using DG.Tweening;
using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OZ.UI
{
    public enum WindowTransition
    {
        PopIn = 0,
        Fade = 1,
        SlideUp = 2,
        None = 3,
    }

    /// <summary>
    /// 모든 창/화면의 베이스. 상속해서 OnSetup/OnOpened/OnClosed만 구현하면 된다.
    /// UIManager가 시작 시 자식에서 찾아 ScreenId로 등록한다.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIWindow : MonoBehaviour
    {
        [Header("Window")]
        [SerializeField] internal ScreenId screenId = ScreenId.None;
        [SerializeField] internal UILayer layer = UILayer.Screen;
        [Tooltip("열려 있는 동안 게임플레이 입력 차단 (UIState.IsGameplayInputBlocked)")]
        [SerializeField] internal bool blocksGameplayInput = true;
        [Tooltip("열려 있는 동안 Time.timeScale = 0 (기획서: 스킬 창 기본안)")]
        [SerializeField] internal bool pausesGame;
        [Tooltip("ESC/B 버튼으로 닫기 가능")]
        [SerializeField] internal bool closeOnBack = true;
        [SerializeField] internal WindowTransition transition = WindowTransition.PopIn;
        [Tooltip("열릴 때 포커스 (게임패드/키보드 내비게이션)")]
        [SerializeField] internal Selectable firstSelected;
        [Tooltip("연출이 적용될 본체. 비우면 자기 자신")]
        [SerializeField] internal RectTransform content;

        CanvasGroup _group;

        public ScreenId Id => screenId;
        public UILayer Layer => layer;
        public bool BlocksGameplayInput => blocksGameplayInput;
        public bool PausesGame => pausesGame;
        public bool CloseOnBack => closeOnBack;
        public bool IsOpen { get; private set; }

        public event Action<UIWindow> Opened;
        public event Action<UIWindow> Closed;

        protected CanvasGroup Group => _group != null ? _group : (_group = GetComponent<CanvasGroup>());
        protected RectTransform Content => content != null ? content : (RectTransform)transform;

        protected virtual void Awake()
        {
            // 시작은 항상 닫힌 상태
            SetVisible(false);
        }

        /// <summary>UIManager.Open 경유로 호출. args는 창별 데이터 (StageClearArgs 등)</summary>
        internal void OpenInternal(object args)
        {
            OnSetup(args);
            if (IsOpen) return;
            IsOpen = true;
            gameObject.SetActive(true);
            SetVisible(true);
            transform.SetAsLastSibling();
            PlayOpen();
            if (firstSelected != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);
            OnOpened();
            Opened?.Invoke(this);
        }

        internal void CloseInternal()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Group.interactable = false;
            Group.blocksRaycasts = false;
            PlayClose(() =>
            {
                if (!IsOpen) SetVisible(false);
            });
            OnClosed();
            Closed?.Invoke(this);
        }

        /// <summary>자기 자신 닫기 (버튼 OnClick 연결용)</summary>
        public void Close() => UIManager.Instance?.Close(this);

        protected virtual void OnSetup(object args) { }
        protected virtual void OnOpened() { }
        protected virtual void OnClosed() { }

        void PlayOpen()
        {
            switch (transition)
            {
                case WindowTransition.PopIn: Content.PopIn(Group); break;
                case WindowTransition.Fade: Group.alpha = 0f; Group.FadeIn(); break;
                case WindowTransition.SlideUp: Group.alpha = 1f; Content.SlideIn(Vector2.down); break;
                default: Group.alpha = 1f; break;
            }
        }

        void PlayClose(Action done)
        {
            switch (transition)
            {
                case WindowTransition.PopIn: Content.PopOut(Group).OnComplete(() => done()); break;
                case WindowTransition.Fade:
                case WindowTransition.SlideUp: Group.FadeOut().OnComplete(() => done()); break;
                default: done(); break;
            }
        }

        void SetVisible(bool visible)
        {
            Group.alpha = visible ? 1f : 0f;
            Group.interactable = visible;
            Group.blocksRaycasts = visible;
            Content.localScale = Vector3.one;
        }
    }
}
