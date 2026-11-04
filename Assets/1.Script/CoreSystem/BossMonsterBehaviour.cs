using System.Collections;

public class BossMonsterBehaviour : MonsterBehaviour
{
    protected override float AttackDelay => 1.5f;

    protected override IEnumerator ExecuteAttack()
    {
        // 보스 공격 패턴을 구현합니다.
        yield break;
    }
}
