using System;
using OZ.UI.Contracts;
using UnityEngine;

namespace OZ.UI.Samples
{
    /// <summary>
    /// [팀 설계 Gate 모양 샘플] Open() / Close().
    /// 열리면 포탈이 커지며 GateSpawner가 적을 내보내고, 적을 다 쓰러뜨리면 스스로 닫힌다(봉쇄).
    /// UI 연결 (선택): 지도 방 아이콘 — GameUI.Map.SetRoomIcon(roomId, Gate / None)
    /// </summary>
    [AddComponentMenu("OZ/UI/Samples/Flow/Sample Gate")]
    public class SampleGate : MonoBehaviour
    {
        [SerializeField] internal SampleGateSpawner spawner;
        [SerializeField] internal Transform portal;
        [SerializeField] internal int enemyCount = 3;
        [Tooltip("지도 방 id (비우면 지도 연결 안 함)")]
        [SerializeField] internal string roomId;

        public bool IsOpen { get; private set; }
        /// <summary>적을 모두 쓰러뜨려 봉쇄됨</summary>
        public event Action<SampleGate> Sealed;

        Vector3 _portalScale = Vector3.one;
        float _grow; // 0 닫힘 ~ 1 열림 (포탈 크기)

        void Awake()
        {
            if (portal != null) { _portalScale = portal.localScale; portal.localScale = Vector3.zero; portal.gameObject.SetActive(false); }
        }

        void Update()
        {
            if (portal == null) return;
            float target = IsOpen ? 1f : 0f;
            _grow = Mathf.MoveTowards(_grow, target, Time.deltaTime * (IsOpen ? 2.2f : 3f));
            float k = IsOpen ? EaseOutBack(_grow) : _grow * _grow;
            portal.localScale = _portalScale * k;
            if (_grow > 0f) portal.Rotate(0f, 0f, 90f * Time.deltaTime, Space.Self);
            if (!IsOpen && _grow <= 0f && portal.gameObject.activeSelf) portal.gameObject.SetActive(false);
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        public void Open(int count = -1)
        {
            if (IsOpen) return;
            IsOpen = true;
            if (portal != null) portal.gameObject.SetActive(true);
            if (!string.IsNullOrEmpty(roomId)) GameUI.Map.SetRoomIcon(roomId, MapRoomIcon.Gate); // [UI] 지도 아이콘
            if (spawner != null)
            {
                spawner.WaveCleared -= OnWaveCleared;
                spawner.WaveCleared += OnWaveCleared;
                spawner.StartSpawn(count > 0 ? count : enemyCount);
            }
        }

        /// <summary>강제로 닫기 (재시작 등). 봉쇄로 치지 않음</summary>
        public void Close()
        {
            if (spawner != null) { spawner.WaveCleared -= OnWaveCleared; spawner.StopSpawn(); }
            CloseVisual();
        }

        void OnWaveCleared()
        {
            spawner.WaveCleared -= OnWaveCleared;
            CloseVisual();
            Sealed?.Invoke(this);
        }

        void CloseVisual()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (!string.IsNullOrEmpty(roomId)) GameUI.Map.SetRoomIcon(roomId, MapRoomIcon.None); // [UI] 지도 아이콘 제거
        }
    }
}
