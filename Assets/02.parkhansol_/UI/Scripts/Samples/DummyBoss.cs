using System;
using OZ.UI.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace OZ.UI.Samples
{
    /// <summary>
    /// UI_Sandbox 테스트용 가짜 보스. IBossSource 참고 구현.
    /// 디버그 키: B 보스전 시작 / N 보스 피격 -8% / V 큰 피격 -25%
    /// </summary>
    [AddComponentMenu("OZ/UI/Samples/Dummy Boss")]
    public class DummyBoss : MonoBehaviour, IBossSource
    {
        [SerializeField] internal BossData data;
        [SerializeField] internal float maxHP = 500f;
        [SerializeField] internal bool enableDebugKeys = true;

        float _hp;
        int _phase;

        public BossData Data => data;
        public float HP => _hp;
        public float MaxHP => maxHP;
        public int Phase => _phase;

        public event Action<float, float> HPChanged;
        public event Action<int> PhaseChanged;
        public event Action Defeated;

        void Awake() => _hp = maxHP;

        public void StartFight()
        {
            _hp = maxHP;
            _phase = 0;
            // 보스 담당 실제 사용 예: 등장 연출이 끝나면 AI 시작
            GameUI.Boss.Show(this, () => Debug.Log("[DummyBoss] 연출 종료 → 전투 시작"));
        }

        public void Damage(float amount)
        {
            if (_hp <= 0f) return;
            _hp = Mathf.Max(0f, _hp - amount);
            HPChanged?.Invoke(_hp, maxHP);

            if (data != null && data.phaseThresholds != null)
            {
                int phase = 0;
                foreach (var t in data.phaseThresholds)
                    if (_hp / maxHP <= t) phase++;
                if (phase != _phase)
                {
                    _phase = phase;
                    PhaseChanged?.Invoke(_phase);
                }
            }

            if (_hp <= 0f) Defeated?.Invoke();
        }

        void Update()
        {
            if (!enableDebugKeys || UIState.IsGameplayInputBlocked) return;
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.bKey.wasPressedThisFrame) StartFight();
            if (kb.nKey.wasPressedThisFrame) Damage(maxHP * 0.08f);
            if (kb.vKey.wasPressedThisFrame) Damage(maxHP * 0.25f);
        }
    }
}
