using OZ.UI.Contracts;
using UnityEngine;

namespace OZ.UI
{
    /// <summary>
    /// GameUI.Map 구현. 지도 상태를 들고 미니맵/전체 지도 렌더러에 뿌린다.
    /// 레벨 담당: 방 트리거에서 GameUI.Map.SetPlayerRoom("A_03")
    /// </summary>
    [AddComponentMenu("OZ/UI/Map/Map Controller")]
    public class MapController : MonoBehaviour, IMapApi
    {
        [SerializeField] internal MapRenderer[] renderers;
        [Tooltip("테스트용 시작 지도 (선택). 실제 게임에서는 코어가 SetMap 호출")]
        [SerializeField] internal MapData startMap;
        [SerializeField] internal string startRoom;

        readonly MapState _state = new MapState();

        public MapData Current => _state.Map;
        public MapState State => _state;

        void OnEnable() => GameUI.Register((IMapApi)this);
        void OnDisable() => GameUI.Unregister(this);

        void Start()
        {
            if (startMap != null && _state.Map == null)
            {
                SetMap(startMap);
                if (!string.IsNullOrEmpty(startRoom)) SetPlayerRoom(startRoom);
            }
        }

        public void SetMap(MapData map)
        {
            _state.Reset(map);
            RebuildAll();
        }

        public void SetPlayerRoom(string roomId)
        {
            if (_state.Map == null || _state.Map.Find(roomId) == null)
            {
                Debug.LogWarning($"[Map] 방 '{roomId}'을(를) 현재 지도에서 찾을 수 없습니다.");
                return;
            }
            bool newly = _state.Reveal(roomId);
            _state.Visit(roomId);
            _state.CurrentRoom = roomId;
            if (newly) RebuildAll(); else RefreshAll();
        }

        public void RevealRoom(string roomId)
        {
            if (_state.Reveal(roomId)) RebuildAll();
        }

        public void SetRoomIcon(string roomId, MapRoomIcon icon)
        {
            _state.SetIcon(roomId, icon);
            RebuildAll();
        }

        /// <summary>창이 열릴 때 등 강제 갱신</summary>
        public void RebuildAll()
        {
            if (renderers == null) return;
            foreach (var r in renderers) if (r != null) r.Rebuild(_state);
        }

        void RefreshAll()
        {
            if (renderers == null) return;
            foreach (var r in renderers) if (r != null) r.Refresh(_state);
        }
    }
}
