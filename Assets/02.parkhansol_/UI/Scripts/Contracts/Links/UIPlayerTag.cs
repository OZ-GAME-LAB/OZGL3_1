using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [v0.7] "이 오브젝트가 플레이어"라는 표시. AreaNameTrigger · InteractPromptLink · 화면 밖 화살표 거리 계산이 이걸로 플레이어를 찾는다.
    /// MainScene에서는 GameBridge(PlayerHealthBridge·PlayerDashBridge)가 Play 시 PlayerController에 자동으로 붙인다 (프리팹 수정 없음).
    /// 다른 씬의 플레이어에는 직접 Add Component 하거나, 트리거의 Player Tag를 맞추면 된다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("OZ/UI/Links/UI Player Tag")]
    public class UIPlayerTag : MonoBehaviour
    {
        /// <summary>지금 활성인 플레이어 (없으면 null)</summary>
        public static Transform Current { get; private set; }

        void OnEnable() => Current = transform;
        void OnDisable() { if (Current == transform) Current = null; }

        /// <summary>c가 플레이어(또는 플레이어의 자식)인지. UIPlayerTag가 없으면 태그로 판단</summary>
        public static bool IsPlayer(Component c, string fallbackTag = "Player")
        {
            if (c == null) return false;
            if (c.GetComponentInParent<UIPlayerTag>() != null) return true;
            if (string.IsNullOrEmpty(fallbackTag) || fallbackTag == "Untagged") return false;
            var root = c.transform.root;
            return c.gameObject.tag == fallbackTag || (root != null && root.gameObject.tag == fallbackTag);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;
    }
}
