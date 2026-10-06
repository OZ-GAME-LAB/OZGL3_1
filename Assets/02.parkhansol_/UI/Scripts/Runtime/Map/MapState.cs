using System.Collections.Generic;
using OZ.UI.Contracts;

namespace OZ.UI
{
    /// <summary>지도 발견/방문/현재 방 상태 (UI 내부용). 세이브가 필요하면 코어가 roomId 목록만 저장하면 됨.</summary>
    public class MapState
    {
        public MapData Map { get; private set; }
        public string CurrentRoom { get; internal set; }

        readonly HashSet<string> _revealed = new HashSet<string>();
        readonly HashSet<string> _visited = new HashSet<string>();
        readonly Dictionary<string, MapRoomIcon> _icons = new Dictionary<string, MapRoomIcon>();

        public void Reset(MapData map)
        {
            Map = map;
            CurrentRoom = null;
            _revealed.Clear();
            _visited.Clear();
            _icons.Clear();
            if (map == null) return;
            foreach (var r in map.rooms)
                if (r.revealedAtStart) _revealed.Add(r.id);
        }

        public bool IsRevealed(string id) => _revealed.Contains(id);
        public bool IsVisited(string id) => _visited.Contains(id);

        /// <summary>새로 발견됐으면 true</summary>
        public bool Reveal(string id) => !string.IsNullOrEmpty(id) && _revealed.Add(id);
        public void Visit(string id) { if (!string.IsNullOrEmpty(id)) _visited.Add(id); }

        public void SetIcon(string id, MapRoomIcon icon) => _icons[id] = icon;

        public MapRoomIcon GetIcon(MapRoom room)
        {
            if (room == null) return MapRoomIcon.None;
            return _icons.TryGetValue(room.id, out var icon) ? icon : room.icon;
        }

        public IEnumerable<string> Revealed => _revealed;
    }
}
