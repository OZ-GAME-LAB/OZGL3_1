using System;
using OZ.UI.Contracts;
using UnityEngine;

/// <summary>
/// [박한솔 UI · v1.2 선택 규칙] 물리/마법 계열 고정. 기획데이터 v0.5는 '자유'라 기본으로는 붙지 않는다.
///   GrowthShopBridge.lockCombatPath 를 켜거나 이 컴포넌트를 아무 오브젝트에 붙이면 동작:
///   처음 힘·물리 기술(또는 마력·마법 기술)을 사면 그 계열로 고정 → 반대 계열 스탯·기술 잠금, 장착돼 있던 반대 계열 기술은 뺀다.
///   상태창 캐릭터 탭은 이 소스(ICombatPathSource)가 있을 때만 [물리][마법] 버튼과 잠금 표시를 보여 준다.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("OZ/UI/Links/Combat Path Lock")]
public class CombatPathLock : MonoBehaviour, ICombatPathSource
{
    [Tooltip("켜면 정한 뒤에도 바꿀 수 있다")]
    [SerializeField] bool allowPathChange = false;

    CombatPath _path = CombatPath.None;

    public event Action PathChanged;

    void OnEnable() => GameUI.Bind(this);
    void OnDisable() => GameUI.Unbind(this);

    public CombatPath Path => _path;
    public bool CanChangePath => allowPathChange || _path == CombatPath.None;

    public ShopBuyResult TrySetPath(CombatPath path)
    {
        if (path == _path) return ShopBuyResult.Success;
        if (!CanChangePath) return ShopBuyResult.Unavailable;
        _path = path;
        // 반대 계열 기술은 장착에서 뺀다 (선택한 계열만 작동)
        var lo = UISources.Loadout;
        if (lo != null)
            for (int i = 0; i < 3; i++)
            {
                var o = GrowthShopBridge.Offer(lo.GetEquipped((SkillSlot)i));
                if (o.IsValid && CombatPathRules.IsLocked(_path, o.damageKind)) lo.Unequip((SkillSlot)i);
            }
        PathChanged?.Invoke();
        return ShopBuyResult.Success;
    }

    /// <summary>계열 규칙이 켜져 있고 아직 미선택이면, 처음 산 쪽으로 정한다 (규칙이 없으면 아무 일도 안 함)</summary>
    public static void AutoPick(CombatPath p)
    {
        var src = UISources.Path;
        if (src != null && src.Path == CombatPath.None && p != CombatPath.None) src.TrySetPath(p);
    }
}
