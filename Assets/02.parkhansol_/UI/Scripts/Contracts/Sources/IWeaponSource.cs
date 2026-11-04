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
}
