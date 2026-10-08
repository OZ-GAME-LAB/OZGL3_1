using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OZ.UI
{
    /// <summary>
    /// 버튼·토글·슬라이더 소리. UIRoot 빌더가 모든 Selectable에 붙인다 (템플릿에 붙어 있으니 복제된 칸도 자동).
    ///   마우스 올림 / 키보드·패드로 선택 → hover,  클릭·Submit → click,  슬라이더 값 변경 → slider(간격 제한)
    /// 직접 소리를 내는 곳(스킬 노드 해금/거절, 인벤토리 집기/놓기)은 click = None.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("OZ/UI/Audio/UI Selectable Sound")]
    public class UISelectableSound : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IPointerClickHandler, ISubmitHandler
    {
        [SerializeField] internal UISound hover = UISound.Hover;
        [SerializeField] internal UISound click = UISound.Click;
        [SerializeField] internal UISound valueChange = UISound.None;

        Selectable _sel;
        float _lastHover;

        void Awake()
        {
            _sel = GetComponent<Selectable>();
            if (_sel is Slider s && valueChange != UISound.None) s.onValueChanged.AddListener(_ => GameUI.Sound.Play(valueChange));
            if (_sel is Toggle t && valueChange != UISound.None) t.onValueChanged.AddListener(_ => GameUI.Sound.Play(valueChange));
        }

        bool Usable => _sel == null || _sel.IsInteractable();

        void HoverSound()
        {
            if (!Usable || Time.unscaledTime - _lastHover < 0.08f) return;
            _lastHover = Time.unscaledTime;
            GameUI.Sound.Play(hover);
        }

        public void OnPointerEnter(PointerEventData e) => HoverSound();
        public void OnSelect(BaseEventData e) { if (!(e is PointerEventData)) HoverSound(); }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left && Usable) GameUI.Sound.Play(click);
        }

        public void OnSubmit(BaseEventData e) { if (Usable) GameUI.Sound.Play(click); }
    }
}
