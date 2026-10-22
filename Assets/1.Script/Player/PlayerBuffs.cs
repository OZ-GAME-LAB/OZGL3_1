using OZ.UI.Contracts;
using UnityEngine;

// 아이템 버프(공격 강화·방어 강화·신속) 보관.
// 최종 값은 쓰는 쪽에서 곱한다: 최종 공격력 = 스탯 공격력 x AttackMultiplier
public class PlayerBuffs : MonoBehaviour
{
    private IItemSource _items;

    private float _attackEndTime;
    private float _defenseEndTime;
    private float _speedEndTime;

    // 아이템 데이터의 수치(%)를 비율로 저장. 20% → 0.2
    private float _attackBonus;
    private float _defenseReduce;
    private float _speedBonus;

    // 버프가 없으면 1 -> 곱해도 값이 그대로라서, 쓰는 쪽은 버프 유무를 신경 쓸 필요 없음
    public float AttackMultiplier { get { return IsActive(_attackEndTime) ? 1f + _attackBonus : 1f; } }         // 공격 강화: 1.2
    public float DamageTakenMultiplier { get { return IsActive(_defenseEndTime) ? 1f - _defenseReduce : 1f; } } // 방어 강화: 0.75
    public float MoveSpeedMultiplier { get { return IsActive(_speedEndTime) ? 1f + _speedBonus : 1f; } }        // 신속: 1.2

    private void OnEnable()
    {
        UISources.Changed += Connect;
        Connect();
    }

    private void OnDisable()
    {
        UISources.Changed -= Connect;
        SetSource(null);

        // 재도전, 타이틀 이동 때 플레이어를 껐다 켜므로, 그때 버프도 같이 사라지게
        ClearAll();
    }

    private void Connect()
    {
        SetSource(UISources.Items);
    }

    private void SetSource(IItemSource source)
    {
        if (ReferenceEquals(_items, source))
        {
            return; // 같은 소스에 이중 구독 방지
        }

        if (_items != null)
        {
            _items.BuffApplied -= OnBuffApplied;
        }

        _items = source;

        if (_items != null)
        {
            _items.BuffApplied += OnBuffApplied;
        }
    }

    // 아이템 쪽이 "버프 시작(또는 시간 갱신)"을 알려줄 때
    private void OnBuffApplied(ItemData item, float duration)
    {
        if (item == null)
        {
            return;
        }

        Apply(item.effectType, item.effectPercent, duration);
    }

    // 같은 버프는 중첩하지 않고 지속시간만 갱신 -> 끝나는 시각을 덮어쓰기만 하면 됨
    private void Apply(ItemEffectType type, float percent, float duration)
    {
        if (duration <= 0f)
        {
            return; // 회복처럼 즉시 효과는 버프가 아님
        }

        float endTime = Time.time + duration;
        float ratio = Mathf.Max(0f, percent) / 100f;

        switch (type)
        {
            case ItemEffectType.AttackBuff:
                _attackEndTime = endTime;
                _attackBonus = ratio;
                break;

            case ItemEffectType.DefenseBuff:
                _defenseEndTime = endTime;
                _defenseReduce = Mathf.Clamp01(ratio); // 100% 넘게 줄면 피해가 음수(회복)가 되므로 막음
                break;

            case ItemEffectType.SpeedBuff:
                _speedEndTime = endTime;
                _speedBonus = ratio;
                break;
        }
    }

    public void ClearAll()
    {
        _attackEndTime = 0f;
        _defenseEndTime = 0f;
        _speedEndTime = 0f;
    }

    private static bool IsActive(float endTime)
    {
        return Time.time < endTime;
    }

    // 아이템이 없어도 확인할 수 있게
    [ContextMenu("Test/공격 강화 (+20%, 10초)")] private void TestAttack() { Apply(ItemEffectType.AttackBuff, 20f, 10f); LogTest(); }
    [ContextMenu("Test/방어 강화 (-25%, 10초)")] private void TestDefense() { Apply(ItemEffectType.DefenseBuff, 25f, 10f); LogTest(); }
    [ContextMenu("Test/신속 (+20%, 10초)")] private void TestSpeed() { Apply(ItemEffectType.SpeedBuff, 20f, 10f); LogTest(); }
    [ContextMenu("Test/전부 해제")] private void TestClear() { ClearAll(); LogTest(); }

    private void LogTest()
    {
        Debug.Log($"[PlayerBuffs] 공격 x{AttackMultiplier} / 받는 피해 x{DamageTakenMultiplier} / 이동 x{MoveSpeedMultiplier}");
    }
}
