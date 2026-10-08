using System;
using System.Collections.Generic;
using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// 스테이지 지도 (방 단위). 레벨 디자인 담당이 방 좌표만 채우면 미니맵/전체 지도가 그려진다.
    /// 좌표 1칸 = 지도 셀 1칸. 큰 방은 size로 (예: 3×1 복도).
    /// 플레이 중에는 방 트리거에서 GameUI.Map.SetPlayerRoom("A_03") 한 줄.
    /// </summary>
    [CreateAssetMenu(menuName = "OZ/UI/Map Data", fileName = "Map_")]
    public class MapData : ScriptableObject
    {
        public string id;
        public string displayName;
        public List<MapRoom> rooms = new List<MapRoom>();
        [Tooltip("문 연결 (선택) — 방 사이에 통로 선을 그림")]
        public List<MapLink> links = new List<MapLink>();

        public MapRoom Find(string roomId)
        {
            foreach (var r in rooms) if (r.id == roomId) return r;
            return null;
        }

        /// <summary>모든 방을 감싸는 셀 범위</summary>
        public RectInt GetBounds()
        {
            if (rooms == null || rooms.Count == 0) return new RectInt(0, 0, 1, 1);
            int xMin = int.MaxValue, yMin = int.MaxValue, xMax = int.MinValue, yMax = int.MinValue;
            foreach (var r in rooms)
            {
                xMin = Mathf.Min(xMin, r.position.x);
                yMin = Mathf.Min(yMin, r.position.y);
                xMax = Mathf.Max(xMax, r.position.x + Mathf.Max(1, r.size.x));
                yMax = Mathf.Max(yMax, r.position.y + Mathf.Max(1, r.size.y));
            }
            return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }
    }

    [Serializable]
    public class MapRoom
    {
        public string id;
        public string displayName;
        [Tooltip("왼쪽 아래 셀 좌표 (y는 위로 증가)")]
        public Vector2Int position;
        public Vector2Int size = Vector2Int.one;
        public MapRoomIcon icon;
        [Tooltip("처음부터 지도에 보임")]
        public bool revealedAtStart;
    }

    [Serializable]
    public struct MapLink
    {
        public string fromRoom;
        public string toRoom;
    }
}
