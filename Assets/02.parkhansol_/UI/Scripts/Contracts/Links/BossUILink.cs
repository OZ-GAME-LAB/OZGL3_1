using System;
using UnityEngine;

namespace OZ.UI.Contracts
{
    /// <summary>
    /// [보스 / StageManager.StartBossphase용] 보스 프리팹(또는 StageManager)에 붙이고 메서드만 부르면 된다.
    ///
    ///   bossUI.Begin(maxHP, () => StartBossAI());   // 등장 연출 → 끝나면 콜백 (전투 시작 타이밍)
    ///   bossUI.SetHP(hp);                            // 맞을 때마다 (페이즈 눈금은 BossData.phaseThresholds로 자동)
    ///   bossUI.Defeat();                             // 처치 연출
    ///   bossUI.Hide();                               // 재시작 등으로 그냥 치울 때
    /// </summary>
    [AddComponentMenu("OZ/UI/Links/Boss UI Link")]
    public class BossUILink : MonoBehaviour, IBossSource
    {
        [Tooltip("이름·칭호·페이즈 구간 (UI/Data의 Boss_ 에셋)")]
        [SerializeField] internal BossData data;

        float _hp, _max = 1f;
        int _phase;
        bool _alive;

        public BossData Data => data;
        public float HP => _hp;
        public float MaxHP => _max;
        public int Phase => _phase;

        Action<float, float> _hpChanged;
        Action<int> _phaseChanged;
        Action _defeated;
        event Action<float, float> IBossSource.HPChanged { add => _hpChanged += value; remove => _hpChanged -= value; }
        event Action<int> IBossSource.PhaseChanged { add => _phaseChanged += value; remove => _phaseChanged -= value; }
        event Action IBossSource.Defeated { add => _defeated += value; remove => _defeated -= value; }

        /// <summary>코드에서 보스 데이터를 바꿀 때 (스테이지별 보스)</summary>
        public void SetData(BossData bossData) => data = bossData;

        /// <summary>보스전 시작: 체력 가득 + 등장 연출 + 상단 체력바</summary>
        public void Begin(float maxHP, Action onIntroFinished = null)
        {
            _max = Mathf.Max(1f, maxHP);
            _hp = _max;
            _phase = 0;
            _alive = true;
            GameUI.Boss.Show(this, onIntroFinished);
        }

        public void SetHP(float hp)
        {
            if (!_alive) return;
            _hp = Mathf.Clamp(hp, 0f, _max);
            _hpChanged?.Invoke(_hp, _max);
            if (data != null && data.phaseThresholds != null)
            {
                int phase = 0;
                foreach (var t in data.phaseThresholds) if (_hp / _max <= t) phase++;
                if (phase != _phase) { _phase = phase; _phaseChanged?.Invoke(_phase); }
            }
            if (_hp <= 0f) Defeat();
        }

        public void Damage(float amount) => SetHP(_hp - amount);

        /// <summary>처치 연출 ("격파" 띠 + 체력바 퇴장)</summary>
        public void Defeat()
        {
            if (!_alive) return;
            _alive = false;
            _hp = 0f;
            _defeated?.Invoke();
        }

        public void Hide()
        {
            _alive = false;
            GameUI.Boss.Hide();
        }
    }
}
