using System;
using UnityEngine;

namespace OZ.UI.Samples
{
    /// <summary>
    /// [팀 설계 SpawnManager 모양 샘플] Spawn() → PoolManager.Get&lt;T&gt;().
    /// 적이 쓰러지면 경험치를 주고 잠시 뒤 풀에 돌려보낸다.
    /// UI 연결: 없음 (체력바는 적 프리팹의 EnemyHealthBarTracker가 처리, 경험치 바는 플레이어 소스가 처리)
    /// </summary>
    [AddComponentMenu("OZ/UI/Samples/Flow/Sample Spawn Manager")]
    public class SampleSpawnManager : MonoBehaviour
    {
        [SerializeField] internal SamplePoolManager pool;
        [SerializeField] internal DummyPlayer player;
        [SerializeField] internal float expPerKill = 12f;

        public event Action<ShowcaseEnemy> EnemyKilled;

        public ShowcaseEnemy Spawn(ShowcaseEnemy prefab, Vector3 position)
        {
            if (pool == null || prefab == null) return null;
            var e = pool.Get(prefab, position);
            e.ResetForSpawn(position);
            e.Died -= OnDied; // 재사용 시 중복 구독 방지
            e.Died += OnDied;
            return e;
        }

        public void Despawn(ShowcaseEnemy e)
        {
            if (e == null) return;
            e.Died -= OnDied;
            pool.Release(e);
        }

        void OnDied(ShowcaseEnemy e)
        {
            e.Died -= OnDied;
            if (player != null) player.AddExp(expPerKill);
            EnemyKilled?.Invoke(e);
            pool.Release(e);
        }
    }
}
