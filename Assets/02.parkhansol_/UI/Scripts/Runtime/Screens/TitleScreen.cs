using OZ.UI.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>시작 화면: 새 게임 / 이어하기 / 설정 / 종료 → UIRequests로 코어에 전달</summary>
    [AddComponentMenu("OZ/UI/Screens/Title Screen")]
    public class TitleScreen : UIWindow
    {
        [SerializeField] internal Button newGameButton;
        [SerializeField] internal Button continueButton;
        [SerializeField] internal Button settingsButton;
        [SerializeField] internal Button quitButton;

        [Header("v1.5")]
        [Tooltip("로고 아래 부제 (비우면 프리팹 글자 그대로)")]
        [SerializeField] internal string subtitle = "게이트 헌터";
        [SerializeField] internal TMP_Text subtitleText;
        [Tooltip("꺼진 버튼 글자·테두리 색 (켜진 버튼과 구분)")]
        [SerializeField] internal Color disabledTint = new Color(0.45f, 0.48f, 0.58f, 0.6f);
        [Tooltip("열린 뒤 이 시간 동안 저장 파일을 다시 확인 (타이틀이 저장보다 먼저 뜨는 경우)")]
        [SerializeField] internal float recheckSeconds = 1.5f;

        float _recheckUntil, _nextCheck;
        bool _forcedContinue;
        TMP_Text _continueLabel;
        Color _continueLabelColor = Color.white;

        protected override void Awake()
        {
            base.Awake();
            if (newGameButton != null) newGameButton.onClick.AddListener(() => { Close(); UIRequests.RaiseNewGame(); });
            if (continueButton != null) continueButton.onClick.AddListener(() => { Close(); UIRequests.RaiseContinue(); });
            if (settingsButton != null) settingsButton.onClick.AddListener(() => UIManager.Instance?.Open(ScreenId.Options));
            if (quitButton != null) quitButton.onClick.AddListener(UIRequests.RaiseQuitGame);
            if (continueButton != null) _continueLabel = continueButton.GetComponentInChildren<TMP_Text>(true);
            if (_continueLabel != null) _continueLabelColor = _continueLabel.color;
            if (subtitleText == null) subtitleText = transform.Find("Sub")?.GetComponent<TMP_Text>();
            if (subtitleText != null && !string.IsNullOrEmpty(subtitle)) subtitleText.text = subtitle;
        }

        protected override void OnSetup(object args)
        {
            _forcedContinue = args is TitleArgs t && t.CanContinue;
            SetContinue(_forcedContinue || UIContinue.Available); // 저장 파일 유무는 UIContinue.HasSave
            // v1.5: '타이틀로' 버튼은 화면 전환과 저장(SaveManager)이 같은 신호에서 일어나서,
            //       첫 저장 때는 파일이 생기기 전에 타이틀이 뜰 수 있음 → 잠깐 동안 다시 확인
            _recheckUntil = Time.unscaledTime + recheckSeconds;
            _nextCheck = 0f;
        }

        void Update()
        {
            if (continueButton == null || continueButton.interactable || Time.unscaledTime > _recheckUntil) return;
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + 0.2f;
            if (UIContinue.Available) SetContinue(true);
        }

        void SetContinue(bool on)
        {
            if (continueButton == null) return;
            continueButton.interactable = on;
            // 테두리 색 전환만으로는 꺼진 게 잘 안 보임(검은 바탕 + 흰 글자 그대로) → 글자를 직접 흐리게
            if (_continueLabel != null) _continueLabel.color = on ? _continueLabelColor : disabledTint;
            if (continueButton.targetGraphic != null && continueButton.transition != Selectable.Transition.ColorTint)
                continueButton.targetGraphic.color = on ? Color.white : disabledTint;
        }
    }
}
