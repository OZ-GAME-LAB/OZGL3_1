using System.Collections;

public class SmashMonsterBehaviour : MonsterBehaviour
{
    protected override float AttackDelay => 1.0f;

    protected override IEnumerator ExecuteAttack()
    {
        // 강타 공격 패턴을 구현합니다. 데이터의 스킬 데미지는 SkillDamage로 읽습니다.
        yield break;
    }
}
