public class BossMonster : Monster
{
    public float PatternTriggerHpRatio { get; private set; }

    public override void InitializeStats(BaseMonsterData data)
    {
        if (!(data is BossMonsterData bossData))
            throw new System.ArgumentException("보스 초기화에는 BossMonsterData가 필요합니다.", nameof(data));
        if (!(bossData.PatternTriggerHpRatio >= 0f && bossData.PatternTriggerHpRatio <= 1f))
            throw new System.ArgumentException($"보스 '{bossData.Id}'의 PatternTriggerHpRatio는 0~1이어야 합니다.");
        base.InitializeStats(data);
        PatternTriggerHpRatio = bossData.PatternTriggerHpRatio;
        // 보스 패턴이 구현되면 페이즈와 패턴 진행 상태도 여기서 초기화합니다.
    }
}
