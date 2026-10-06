using System;

namespace OZ.UI.Contracts
{
    public readonly struct HealthChange
    {
        public readonly float Previous;
        public readonly float Current;
        public readonly float Max;

        public HealthChange(float previous, float current, float max)
        {
            Previous = previous;
            Current = current;
            Max = max;
        }

        public float Delta => Current - Previous;
        public bool IsDamage => Current < Previous;
        public bool IsHeal => Current > Previous;
        public float Normalized => Max > 0f ? Current / Max : 0f;
    }

    /// <summary>
    /// [플레이어 담당 구현] 체력. 값이 바뀔 때마다 HealthChanged를 호출해 주세요.
    /// MaxHP가 바뀌는 경우도 같은 이벤트로 보내면 됩니다.
    /// </summary>
    public interface IHealthSource
    {
        float HP { get; }
        float MaxHP { get; }
        event Action<HealthChange> HealthChanged;
    }
}
