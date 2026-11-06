using UnityEngine;

public enum EnemyAnimState
{
    Idle,
    Move,
    Attack,
    Damaged
}

[DisallowMultipleComponent]
public class EnemyAnimController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    public event System.Action AttackHit;
    private bool canPauseAnimation;

    private static readonly int IsIdle = Animator.StringToHash("IsIdle");
    private static readonly int IsMove = Animator.StringToHash("IsMove");
    private static readonly int IsAttack = Animator.StringToHash("IsAttack");
    private static readonly int IsDamaged = Animator.StringToHash("IsDamaged");

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (animator == null)
            Debug.LogWarning("EnemyAnimController에 Animator를 연결하세요.", this);
    }

    private void OnEnable()
    {
        ResetAnimState();
    }

    private void OnDisable()
    {
        ResumeAnimation();
    }

    public void ChangeAnimState(EnemyAnimState state)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;

        switch (state)
        {
            case EnemyAnimState.Idle:
                SetMovementState(true, false);
                break;
            case EnemyAnimState.Move:
                SetMovementState(false, true);
                break;
            case EnemyAnimState.Attack:
                animator.SetTrigger(IsAttack);
                break;
            case EnemyAnimState.Damaged:
                ResumeAnimation();
                animator.SetTrigger(IsDamaged);
                break;
        }
    }

    // 공격 클립의 타격 프레임에 이 함수를 Animation Event로 지정합니다.
    public void OnAttackHit()
    {
        if (isActiveAndEnabled) AttackHit?.Invoke();
    }

    public void ResetAnimState()
    {
        ResumeAnimation();
        if (animator == null || animator.runtimeAnimatorController == null) return;

        animator.ResetTrigger(IsAttack);
        animator.ResetTrigger(IsDamaged);
        SetMovementState(true, false);
    }

    public void BeginAttackPause()
    {
        ResumeAnimation();
        canPauseAnimation = true;
    }

    // 칼을 들어 올린 프레임에 지정하는 Animation Event입니다.
    public void PauseAnimation()
    {
        if (canPauseAnimation && isActiveAndEnabled && animator != null)
            animator.speed = 0f;
    }

    public void ResumeAnimation()
    {
        canPauseAnimation = false;
        if (animator != null) animator.speed = 1f;
    }

    private void SetMovementState(bool idle, bool move)
    {
        animator.SetBool(IsIdle, idle);
        animator.SetBool(IsMove, move);
    }
}
