using System;
using System.Collections.Generic;
using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// UI 루트. 레이어 캔버스 생성, 창 등록/스택, 입력 차단·일시정지 상태 관리,
    /// GameUI.Screens 구현.
    /// 씬에 UIRoot 프리팹(이 컴포넌트 + 창들) 하나만 두면 된다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [AddComponentMenu("OZ/UI/UI Manager")]
    public class UIManager : MonoBehaviour, IScreenApi
    {
        public static UIManager Instance { get; private set; }

        [SerializeField] internal bool dontDestroyOnLoad = true;
        [Tooltip("PausesGame 창이 열리면 Time.timeScale = 0 (끄면 UIState.IsPausedByUI만 알림)")]
        [SerializeField] internal bool controlTimeScale = true;
        [SerializeField] internal Vector2Int logicalResolution = new Vector2Int(640, 360);

        readonly Dictionary<UILayer, Canvas> _layers = new Dictionary<UILayer, Canvas>();
        readonly Dictionary<ScreenId, UIWindow> _windows = new Dictionary<ScreenId, UIWindow>();
        readonly List<UIWindow> _allWindows = new List<UIWindow>();
        readonly List<UIWindow> _stack = new List<UIWindow>(); // 마지막이 맨 위

        bool _pausedTimeScale;
        float _timeScaleBeforePause = 1f;

        public event Action<UIWindow> WindowOpened;
        public event Action<UIWindow> WindowClosed;

        public IReadOnlyList<UIWindow> OpenStack => _stack;
        public UIWindow Top => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[UIManager] 이미 UIManager가 있어 중복을 제거합니다.", this);
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DG.Tweening.DOTween.SetTweensCapacity(500, 125); // UI 연출이 몰려도 용량 자동 확장 경고가 안 뜨게
            if (dontDestroyOnLoad && transform.parent == null) DontDestroyOnLoad(gameObject);

            foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
                GetOrCreateLayer(layer);

            foreach (var w in GetComponentsInChildren<UIWindow>(true))
                RegisterWindow(w);

            EnsureEventSystem();
            UISettingsApplier.Hook(); // 저장된 볼륨·화면 설정 적용
            GameUI.Register((IScreenApi)this);
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            GameUI.Unregister(this);
            ApplyPause(false);
            UIState.SetInputBlocked(false);
            Instance = null;
        }

        // ───────────── 레이어 ─────────────

        public Canvas GetLayer(UILayer layer) => GetOrCreateLayer(layer);

        Canvas GetOrCreateLayer(UILayer layer)
        {
            if (_layers.TryGetValue(layer, out var existing) && existing != null) return existing;

            string name = "Layer_" + layer;
            Transform t = transform.Find(name);
            GameObject go = t != null ? t.gameObject : new GameObject(name, typeof(RectTransform));
            if (t == null) go.transform.SetParent(transform, false);
            go.layer = LayerMask.NameToLayer("UI") >= 0 ? LayerMask.NameToLayer("UI") : go.layer;

            var canvas = go.GetComponent<Canvas>();
            if (canvas == null) canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = (int)layer;
            canvas.pixelPerfect = true;

            var scaler = go.GetComponent<PixelCanvasScaler>();
            if (scaler == null && go.GetComponent<CanvasScaler>() == null) scaler = go.AddComponent<PixelCanvasScaler>();
            if (scaler != null) scaler.SetLogicalResolution(logicalResolution);
            if (go.GetComponent<GraphicRaycaster>() == null) go.AddComponent<GraphicRaycaster>();

            _layers[layer] = canvas;
            return canvas;
        }

        // ───────────── 창 등록 ─────────────

        /// <summary>런타임에 생성한 창도 이걸로 등록 (프리팹 Instantiate 후)</summary>
        public void RegisterWindow(UIWindow w)
        {
            if (w == null || _allWindows.Contains(w)) return;
            _allWindows.Add(w);

            var layerCanvas = GetOrCreateLayer(w.Layer);
            if (w.transform.parent != layerCanvas.transform)
                w.transform.SetParent(layerCanvas.transform, false);

            if (w.Id != ScreenId.None)
            {
                if (_windows.ContainsKey(w.Id))
                    Debug.LogWarning($"[UIManager] ScreenId {w.Id} 창이 두 개입니다: {w.name}", w);
                else
                    _windows[w.Id] = w;
            }
        }

        public T GetWindow<T>() where T : UIWindow
        {
            foreach (var w in _allWindows)
                if (w is T t) return t;
            return null;
        }

        public UIWindow GetWindow(ScreenId id) => _windows.TryGetValue(id, out var w) ? w : null;

        // ───────────── 열기/닫기 ─────────────

        public void Open(ScreenId id) => Open(id, null);

        public void Open(ScreenId id, object args)
        {
            var w = GetWindow(id);
            if (w == null)
            {
                Debug.LogWarning($"[UIManager] {id} 창이 아직 없습니다 (프리팹 미구현).");
                return;
            }
            Open(w, args);
        }

        public void Open(UIWindow w, object args = null)
        {
            if (w == null) return;
            w.OpenInternal(args);
            _stack.Remove(w);
            _stack.Add(w);
            RefreshState();
            WindowOpened?.Invoke(w);
        }

        public void Close(ScreenId id)
        {
            var w = GetWindow(id);
            if (w != null) Close(w);
        }

        public void Close(UIWindow w)
        {
            if (w == null || !w.IsOpen) return;
            w.CloseInternal();
            _stack.Remove(w);
            RefreshState();
            WindowClosed?.Invoke(w);
        }

        public void Toggle(ScreenId id)
        {
            if (IsOpen(id)) Close(id); else Open(id);
        }

        public void CloseAll()
        {
            for (int i = _stack.Count - 1; i >= 0; i--) Close(_stack[i]);
        }

        public bool IsOpen(ScreenId id)
        {
            var w = GetWindow(id);
            return w != null && w.IsOpen;
        }

        /// <summary>ESC/B: 맨 위 닫을 수 있는 창을 닫는다. 닫은 게 없으면 false</summary>
        public bool Back()
        {
            var top = Top;
            if (top == null || !top.CloseOnBack) return false;
            Close(top);
            return true;
        }

        // ───────────── IScreenApi 단축 ─────────────

        public void ShowTitle(bool canContinue) => Open(ScreenId.Title, new TitleArgs(canContinue));
        public void ShowClassSelect(IReadOnlyList<ClassData> classes) => Open(ScreenId.ClassSelect, new ClassSelectArgs(classes));
        public void ShowDeath() => Open(ScreenId.Death);
        public void ShowStageClear(int stageNumber, HunterRank newRank) =>
            GameUI.HUD.ShowBanner("게이트 파괴", $"STAGE {stageNumber} 클리어  ·  헌터 랭크 {newRank.ToDisplay()}", 2.5f, UIRequests.RaiseNextStage);
        public void ShowDemoEnd() => Open(ScreenId.DemoEnd);

        // ───────────── 상태 ─────────────

        void RefreshState()
        {
            bool block = false, pause = false;
            foreach (var w in _stack)
            {
                block |= w.BlocksGameplayInput;
                pause |= w.PausesGame;
            }
            UIState.SetInputBlocked(block);
            ApplyPause(pause);
        }

        void ApplyPause(bool pause)
        {
            UIState.SetPaused(pause);
            if (!controlTimeScale) return;

            if (pause && !_pausedTimeScale)
            {
                // 히트스톱(0) 도중에 열려도 닫을 때 게임이 멈춘 채로 남지 않게
                _timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
                _pausedTimeScale = true;
            }
            else if (!pause && _pausedTimeScale)
            {
                Time.timeScale = _timeScaleBeforePause;
                _pausedTimeScale = false;
            }
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(es);
        }
    }
}
