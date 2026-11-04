// 힘·마력(공격력)·민첩(공격 속도)을 받는 평타 공통 인터페이스

public interface IAttackStatReceiver
{
    // 어느 공격력을 받을지. Physical = 힘, Magic = 마력
    // 스킬과 같은 기준(물리=힘, 마법=마력)이라 enum을 하나 더 만들 이유가 없음
    SkillDamageType DamageType { get; }

    void SetAttackPower(float attackPower); // DamageType에 맞는 공격력 (20 + 3 x 힘 또는 마력)
    void SetSpeedBonus(float bonus);        // 민첩 구매 시 (0.09 = +9%). 평타면 종류 상관없이 적용
}
