using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [v0.7 · 레벨 배치용] 플레이어가 들어오면 화면 위쪽에 지역 이름이 부드럽게 떴다가 사라진다.
    ///   1) 빈 오브젝트 + BoxCollider (Is Trigger 자동 체크) → 지역 입구를 덮게 크기 조절
    ///   2) Area Name 입력 ("서울 거리"), 필요하면 Subtitle ("1스테이지")
    ///   3) Map Room Id를 넣으면 지도의 현재 방도 같이 바뀜 (GameUI.Map.SetPlayerRoom)
    /// 플레이어 판별: UIPlayerTag (MainScene은 자동) 또는 Player Tag.
    /// 코드로 부르려면 GameUI.Area.Show("이름").
    /// </summary>
    [AddComponentMenu("OZ/UI/Links/Area Name Trigger")]
    public class AreaNameTrigger : MonoBehaviour
    {
        [SerializeField] internal string areaName = "서울 거리";
        [Tooltip("이름 아래 작은 글씨 (비우면 없음)")]
        [SerializeField] internal string subtitle = "";
        [Tooltip("지도 방 ID (비우면 지도는 그대로)")]
        [SerializeField] internal string mapRoomId = "";
        [SerializeField, Min(0.5f)] internal float holdSeconds = 2.2f;
        [Tooltip("켜면 처음 들어올 때 한 번만 표시")]
        [SerializeField] internal bool onlyFirstTime;
        [Tooltip("UIPlayerTag가 없을 때 플레이어로 볼 태그")]
        [SerializeField] internal string playerTag = "Player";

        bool _shown;

        public string AreaName => areaName;

        void Reset()
        {
            var c = GetComponent<Collider>();
            if (c == null) c = gameObject.AddComponent<BoxCollider>();
            c.isTrigger = true;
        }

        void OnTriggerEnter(Collider other) { if (UIPlayerTag.IsPlayer(other, playerTag)) Enter(); }
        void OnTriggerEnter2D(Collider2D other) { if (UIPlayerTag.IsPlayer(other, playerTag)) Enter(); }

        /// <summary>코드·UnityEvent에서 직접 부를 때</summary>
        public void Enter()
        {
            if (!string.IsNullOrEmpty(mapRoomId) && GameUI.Map.Current != null) GameUI.Map.SetPlayerRoom(mapRoomId);
            if (onlyFirstTime && _shown) return;
            if (string.IsNullOrEmpty(areaName)) return;
            _shown = true;
            GameUI.Area.Show(areaName, subtitle, holdSeconds);
        }

        /// <summary>재도전 등으로 "처음" 상태로 되돌릴 때</summary>
        public void ResetShown() => _shown = false;

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            var c = GetComponent<Collider>();
            if (c == null) return;
            Gizmos.color = new Color(1f, 0.85f, 0.4f, 0.25f);
            Gizmos.DrawCube(c.bounds.center, c.bounds.size);
            Gizmos.color = new Color(1f, 0.85f, 0.4f, 0.9f);
            Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
        }
#endif
    }
}
