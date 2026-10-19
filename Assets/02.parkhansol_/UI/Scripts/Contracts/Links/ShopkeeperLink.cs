using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [v1.0 · 상점 NPC] InteractPromptLink 옆에 붙이면 "[F] 상점" → 상점 창이 열린다.
    ///   1) NPC 오브젝트에 Interact Prompt Link + Shopkeeper Link
    ///   2) Shop 칸에 ShopData (예: UI/Data/Samples/Shop_Potion)
    ///   3) (선택) Greeting에 대화 데이터 → 대화가 끝나면 상점이 열림
    /// 임시 NPC는 메뉴 OZ > UI > Place Temp Shop NPC 로 바로 만들 수 있다.
    /// </summary>
    [RequireComponent(typeof(InteractPromptLink))]
    [AddComponentMenu("OZ/UI/Links/Shopkeeper Link")]
    public class ShopkeeperLink : MonoBehaviour
    {
        [SerializeField] internal ShopData shop;
        [Tooltip("상점 열기 전에 재생할 대화 (선택)")]
        [SerializeField] internal DialogueData greeting;
        [Tooltip("머리 위 안내 문구")]
        [SerializeField] internal string promptAction = "상점";

        InteractPromptLink _prompt;

        public ShopData Shop => shop;

        void Awake()
        {
            _prompt = GetComponent<InteractPromptLink>();
            if (_prompt != null && !string.IsNullOrEmpty(promptAction)) _prompt.action = promptAction;
        }

        void OnEnable()
        {
            if (_prompt == null) _prompt = GetComponent<InteractPromptLink>();
            if (_prompt != null) _prompt.onInteract.AddListener(OpenShop);
        }

        void OnDisable()
        {
            if (_prompt != null) _prompt.onInteract.RemoveListener(OpenShop);
        }

        /// <summary>코드·UnityEvent에서 직접 불러도 된다</summary>
        public void OpenShop()
        {
            if (shop == null) { Debug.LogWarning("[ShopkeeperLink] Shop 데이터가 비어 있습니다.", this); return; }
            if (GameUI.Shop.IsOpen || GameUI.Dialogue.IsPlaying) return;
            if (greeting != null) GameUI.Dialogue.Play(greeting, () => GameUI.Shop.Open(shop));
            else GameUI.Shop.Open(shop);
        }
    }
}
