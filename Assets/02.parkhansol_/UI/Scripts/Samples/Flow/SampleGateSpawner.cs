using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OZ.UI.Samples
{
    /// <summary>
    /// [팀 설계 GateSpawner 모양 샘플] StartSpawn() / StopSpawn() → SpawnManager.Spawn().
    /// 정해진 수만큼 적을 내보내고, 모두 쓰러지면 WaveCleared.
    /// UI 연결: 없음 (게이트 단위로 GateManager가 처리)
    /// </summary>
    [AddComponentMenu("OZ/UI/Samples/Flow/Sample Gate Spawner")]
    public class SampleGateSpawner : MonoBehaviour
    {
        [SerializeField] internal SampleSpawnManager spawnManager;
        [SerializeField] internal ShowcaseEnemy enemyPrefab;
        [SerializeField] internal float interval = 0.7f;
        [SerializeField] internal float spread = 1.6f;

        readonly List<ShowcaseEnemy> _alive = new List<ShowcaseEnemy>();
        Coroutine _routine;
        int _toSpawn;

        public bool IsSpawning => _routine != null;
        public int AliveCount => _alive.Count;
        public event Action WaveCleared;

        public void StartSpawn(int count)
        {
            StopSpawn();
            _toSpawn = Mathf.Max(1, count);
            if (spawnManager != null) spawnManager.EnemyKilled += OnKilled;
            _routine = StartCoroutine(SpawnRoutine());
        }

        /// <summary>남은 적은 풀로 돌려보낸다 (재시작·스테이지 교체 시)</summary>
        public void StopSpawn()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            _toSpawn = 0;
            if (spawnManager != null)
            {
                spawnManager.EnemyKilled -= OnKilled;
                foreach (var e in _alive) spawnManager.Despawn(e);
            }
            _alive.Clear();
        }

        IEnumerator SpawnRoutine()
        {
            int i = 0;
            while (_toSpawn > 0)
            {
                float side = (i % 2 == 0 ? 1f : -1f) * (0.4f + spread * ((i / 2) % 3) / 3f);
                var e = spawnManager.Spawn(enemyPrefab, transform.position + new Vector3(side, 0f, 0f));
                if (e != null) _alive.Add(e);
                _toSpawn--;
                i++;
                yield return new WaitForSeconds(interval);
            }
            _routine = null;
            CheckCleared();
        }

        void OnKilled(ShowcaseEnemy e)
        {
            if (!_alive.Remove(e)) return;
            CheckCleared();
        }

        void CheckCleared()
        {
            if (_routine != null || _toSpawn > 0 || _alive.Count > 0) return;
            if (spawnManager != null) spawnManager.EnemyKilled -= OnKilled;
            WaveCleared?.Invoke();
        }

        void OnDisable() => StopSpawn();
    }
}
