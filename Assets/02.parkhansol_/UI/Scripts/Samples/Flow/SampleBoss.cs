using System;
using OZ.UI.Contracts;
using UnityEngine;

namespace OZ.UI.Samples
{
    /// <summary>
    /// 보스 몸체(ShowcaseEnemy)의 체력을 IBossSource로 바꿔 주는 어댑터. 보스 담당 참고용.
    ///   GameUI.Boss.Show(this, onIntroFinished) 한 줄이면 등장 연출 + 상단 체력바 + 페이즈 눈금 + 격파 연출.
    /// </summary>
    [AddComponentMenu("OZ/UI/Samples/Flow/Sample Boss")]
    public class SampleBoss : MonoBehaviour, IBossSource
    {
        [SerializeField] internal BossData data;
        [SerializeField] internal ShowcaseEnemy body;

        int _phase;
        bool _defeated;

        public BossData Data => data;
        public float HP => body != null ? body.HP : 0f;
        public float MaxHP => body != null ? body.MaxHP : 1f;
        public int Phase => _phase;
        public bool IsActive => body != null && body.gameObject.activeSelf;

        public event Action<float, float> HPChanged;
        public event Action<int> PhaseChanged;
        public event Action Defeated;

        void Awake()
        {
            if (body != null)
            {
                body.HealthChanged += OnBodyHealth;
                body.Died += OnBodyDied;
                body.gameObject.SetActive(false);
            }
        }

        void OnDestroy()
        {
            if (body != null) { body.HealthChanged -= OnBodyHealth; body.Died -= OnBodyDied; }
        }

        /// <summary>보스 등장 (몸체 활성 + 체력 초기화). 실제 공격은 onReady 이후 시작</summary>
        public void Spawn(Vector3 position, Action onReady)
        {
            if (body == null) { onReady?.Invoke(); return; }
            _phase = 0;
            _defeated = false;
            body.gameObject.SetActive(true);
            body.ResetForSpawn(position);
            body.enabled = false; // 등장 연출 동안 가만히
            GameUI.Boss.Show(this, () => { body.enabled = true; onReady?.Invoke(); }); // [UI]
        }

        public void Despawn()
        {
            if (body != null) body.gameObject.SetActive(false);
            GameUI.Boss.Hide(); // [UI]
        }

        void OnBodyHealth(HealthChange c)
        {
            if (_defeated || !IsActive) return;
            HPChanged?.Invoke(c.Current, c.Max);
            if (data != null && data.phaseThresholds != null && c.Max > 0f)
            {
                int phase = 0;
                foreach (var t in data.phaseThresholds) if (c.Current / c.Max <= t) phase++;
                if (phase != _phase) { _phase = phase; PhaseChanged?.Invoke(_phase); }
            }
        }

        void OnBodyDied(ShowcaseEnemy e)
        {
            if (_defeated) return;
            _defeated = true;
            Defeated?.Invoke(); // [UI] 격파 연출은 BossHUD가 처리
            body.gameObject.SetActive(false);
        }
    }
}
