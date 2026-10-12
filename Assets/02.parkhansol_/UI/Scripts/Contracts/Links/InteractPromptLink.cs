using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [v0.7 · 문·레버·NPC·세이브 지점용] 플레이어가 가까이 오면 머리 위에 "[F] 조사"를 띄운다. 표시만 담당.
    ///   실제 상호작용(키 입력 → 문 열기)은 게임 쪽 코드. 상호작용이 끝나 더 못 쓰게 되면 SetAvailable(false).
    /// 거리 방식(기본): Radius 안에 UIPlayerTag가 들어오면 표시 — 콜라이더 필요 없음.
    /// 트리거 방식: Radius를 0으로 두고 Is Trigger 콜라이더를 붙이면 들어올 때/나갈 때 표시.
    /// </summary>
    [AddComponentMenu("OZ/UI/Links/Interact Prompt Link")]
    public class InteractPromptLink : MonoBehaviour
    {
        [SerializeField] internal string action = "조사";
        [SerializeField] internal string key = "F";
        [Tooltip("대상 기준 표시 위치 (월드 단위)")]
        [SerializeField] internal Vector3 offset = new Vector3(0f, 2.2f, 0f);
        [Tooltip("0보다 크면 거리 방식, 0이면 트리거 방식")]
        [SerializeField, Min(0f)] internal float radius = 1.8f;
        [SerializeField] internal string playerTag = "Player";
        [SerializeField] internal bool available = true;

        bool _inside;

        public bool IsShowing => GameUI.Prompt.Current == transform;
        public bool PlayerInside => _inside;

        public void SetAvailable(bool value)
        {
            available = value;
            Refresh();
        }

        public void SetAction(string newAction)
        {
            action = newAction;
            if (IsShowing) GameUI.Prompt.Show(transform, action, key, offset);
        }

        void OnDisable()
        {
            _inside = false;
            GameUI.Prompt.Hide(transform);
        }

        void Update()
        {
            if (radius <= 0f) return;
            var p = UIPlayerTag.Current;
            bool inside = p != null && (p.position - transform.position).sqrMagnitude <= radius * radius;
            if (inside != _inside) { _inside = inside; Refresh(); }
        }

        void OnTriggerEnter(Collider other) { if (radius <= 0f && UIPlayerTag.IsPlayer(other, playerTag)) { _inside = true; Refresh(); } }
        void OnTriggerExit(Collider other) { if (radius <= 0f && UIPlayerTag.IsPlayer(other, playerTag)) { _inside = false; Refresh(); } }
        void OnTriggerEnter2D(Collider2D other) { if (radius <= 0f && UIPlayerTag.IsPlayer(other, playerTag)) { _inside = true; Refresh(); } }
        void OnTriggerExit2D(Collider2D other) { if (radius <= 0f && UIPlayerTag.IsPlayer(other, playerTag)) { _inside = false; Refresh(); } }

        void Refresh()
        {
            if (_inside && available && isActiveAndEnabled) GameUI.Prompt.Show(transform, action, key, offset);
            else GameUI.Prompt.Hide(transform);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.85f, 1f, 0.8f);
            if (radius > 0f) Gizmos.DrawWireSphere(transform.position, radius);
            Gizmos.DrawWireCube(transform.position + offset, new Vector3(0.6f, 0.25f, 0.05f));
        }
#endif
    }
}
