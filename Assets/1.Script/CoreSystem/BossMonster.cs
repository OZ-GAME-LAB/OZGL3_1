public class BossMonster : Monster
{
    [UnityEngine.SerializeField] private OZ.UI.Contracts.BossUILink bossUI;
    private bool isUIShowing;
    public float PatternTriggerHpRatio { get; private set; }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (bossUI == null) bossUI = GetComponentInChildren<OZ.UI.Contracts.BossUILink>(true);
        if (bossUI == null) bossUI = gameObject.AddComponent<OZ.UI.Contracts.BossUILink>();
        Died += OnDied;
        Damaged += OnDamaged;
        isUIShowing = true;
        bossUI.Begin(MaxHP);
    }

    private void OnDamaged(Monster monster)
    {
        if (isUIShowing) bossUI.SetHP(CurrentHP);
    }

    private void OnDied(Monster monster)
    {
        if (!isUIShowing) return;
        isUIShowing = false;
        bossUI.Defeat();
    }

    protected override void OnDisable()
    {
        Died -= OnDied;
        Damaged -= OnDamaged;
        // 스포너가 사망 이벤트를 먼저 받아 풀로 반환하는 경우도 처리합니다.
        if (IsDead) OnDied(this);
        else if (isUIShowing)
        {
            isUIShowing = false;
            bossUI.Hide();
        }
        base.OnDisable();
    }

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
