using System;

namespace OZ.UI.Contracts
{
    /// <summary>HUD 평타 칸에 보일 무기 종류 (팀원 쪽 WeaponType과 순서 같음: 맨손·검·스태프)</summary>
    public enum UIWeapon { None = 0, Sword = 1, Staff = 2 }

    /// <summary>
    /// [v1.4] 평타(마우스 왼쪽) 무기 표시용. 플레이어 담당이 직접 구현하거나,
    /// GameBridge/PlayerWeaponBridge가 PlayerWeapon.WeaponChanged를 구독해 대신 구현한다 (팀원 코드 수정 없음).
    /// </summary>
    public interface IWeaponSource
    {
        UIWeapon CurrentWeapon { get; }
        /// <summary>평타 피해 유형이 마법인지 (물리 = 힘, 마법 = 마력)</summary>
        bool IsMagicAttack { get; }
        /// <summary>콤보 타 수 (검 3타 = 3). 콤보가 없으면 0 → 점 표시 없음</summary>
        int ComboLength { get; }
        /// <summary>지금 몇 번째 타인지 (0부터). 공격 중이 아니면 -1</summary>
        int ComboStep { get; }
        event Action WeaponChanged;
    }

    /// <summary>
    /// [v1.5] 무기 획득 (상점 구매·보상 공용). "무기는 하나만" 규칙은 플레이어 쪽(PlayerWeapon)이 판단한다.
    /// GameBridge/PlayerWeaponBridge가 PlayerWeapon.CanAcquire / TryAcquire로 대신 구현한다 (팀원 코드 수정 없음).
    /// IWeaponSource와 같은 오브젝트가 구현하면 UISources.Weapon으로 함께 찾는다.
    /// </summary>
    public interface IWeaponAcquireSource
    {
        /// <summary>지금 이 무기를 얻을 수 있는지 (이미 무기가 있으면 false) — 상점 잠김 표시용</summary>
        bool CanAcquire(UIWeapon weapon);
        /// <summary>획득 시도. 실패하면 false → 상점은 포인트를 깎지 않는다</summary>
        bool TryAcquire(UIWeapon weapon);
    }

    /// <summary>[v1.5] 상점 구현·UI가 같은 규칙을 쓰도록 모아 둔 무기 아이템 검사</summary>
    public static class WeaponShopRules
    {
        /// <summary>등록된 무기 획득 소스 (없으면 null — UI 샌드박스 등)</summary>
        public static IWeaponAcquireSource Acquirer => UISources.Weapon as IWeaponAcquireSource;

        /// <summary>무기를 주는 아이템인데 다른 무기를 이미 골라서 못 사는지 (소스가 없으면 false)</summary>
        public static bool IsLocked(ItemData item)
        {
            if (item == null || !item.GrantsWeapon || IsHolding(item)) return false;
            var a = Acquirer;
            return a != null && !a.CanAcquire(item.grantsWeapon);
        }

        /// <summary>플레이어가 지금 이 아이템의 무기를 들고 있는지</summary>
        public static bool IsHolding(ItemData item) =>
            item != null && item.GrantsWeapon && UISources.Weapon != null && UISources.Weapon.CurrentWeapon == item.grantsWeapon;
    }
}
